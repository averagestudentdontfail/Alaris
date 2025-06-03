// src/quantlib/main.cpp
#include "pricing/alo_engine.h"
#include "strategy/vol_arb.h"
#include "core/memory_pool.h"
#include "core/task_scheduler.h"
#include "core/event_log.h"
#include "ipc/shared_memory.h"
#include <iostream>
#include <string>
#include <vector>
#include <memory>
#include <csignal>
#include <unistd.h>
#include <sys/mman.h>
#include <sched.h>
#include <yaml-cpp/yaml.h>

using namespace Alaris;

// Global shutdown flag to be set by signal handlers
std::atomic<bool> g_shutdown_requested{false};

void signal_handler(int signum) {
    std::cout << "\nSignal (" << signum << ") received, requesting shutdown..." << std::endl;
    g_shutdown_requested = true;
}

class AlarisQuantLibProcess {
private:
    // Core components
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Core::PerCycleAllocator> allocator_;
    std::unique_ptr<Core::EventLogger> event_logger_;
    std::unique_ptr<IPC::SharedMemoryManager> shared_memory_manager_;
    std::unique_ptr<Core::TaskScheduler> scheduler_;  // Changed from TimeTriggeredExecutor
    
    // Trading components
    std::unique_ptr<Pricing::QuantLibALOEngine> pricer_;
    std::unique_ptr<Strategy::VolatilityArbitrageStrategy> strategy_;
    
    // Configuration
    YAML::Node config_;
    
    // Runtime state
    std::atomic<bool> trading_enabled_{false};
    
    // Performance metrics
    uint64_t cycles_executed_{0};
    
    // Task timing configuration (in microseconds for precision)
    static constexpr auto BASIC_TIME_UNIT = std::chrono::milliseconds(1);  // 1ms basic unit for simpler scheduling
    
    // Task periods (all must be multiples of BASIC_TIME_UNIT) - Adjusted for TTA compatibility
    static constexpr auto MARKET_DATA_PERIOD = std::chrono::milliseconds(10);      // 10ms = 10 * 1ms
    static constexpr auto SIGNAL_GENERATION_PERIOD = std::chrono::milliseconds(100);  // 100ms = 100 * 1ms  
    static constexpr auto CONTROL_PROCESSING_PERIOD = std::chrono::milliseconds(50);  // 50ms = 50 * 1ms
    static constexpr auto HEARTBEAT_PERIOD = std::chrono::seconds(1);              // 1s = 1000 * 1ms
    static constexpr auto PERFORMANCE_REPORT_PERIOD = std::chrono::seconds(10);    // 10s = 10000 * 1ms
    
    // Worst-Case Execution Time estimates (conservative but realistic)
    static constexpr auto MARKET_DATA_WCET = std::chrono::milliseconds(1);         // 1ms
    static constexpr auto SIGNAL_GENERATION_WCET = std::chrono::milliseconds(5);   // 5ms
    static constexpr auto CONTROL_PROCESSING_WCET = std::chrono::milliseconds(1);  // 1ms
    static constexpr auto HEARTBEAT_WCET = std::chrono::milliseconds(1);           // 1ms
    static constexpr auto PERFORMANCE_REPORT_WCET = std::chrono::milliseconds(2);  // 2ms
    
public:
    AlarisQuantLibProcess(const std::string& config_file_path) {
        try {
            config_ = YAML::LoadFile(config_file_path);
            initialize_system_settings();
            initialize_components();
            setup_task_schedule();
            
            std::cout << "Alaris QuantLib Process initialized successfully from config: " << config_file_path << std::endl;
            if(event_logger_) event_logger_->log_system_status("QuantLib process started and initialized.");
        }
        catch (const YAML::Exception& e) {
            std::cerr << "Failed to load or parse configuration file '" << config_file_path << "': " << e.what() << std::endl;
            throw;
        }
        catch (const std::exception& e) {
            std::cerr << "Failed to initialize QuantLib process: " << e.what() << std::endl;
            if(event_logger_) event_logger_->log_error("Process initialization failed: " + std::string(e.what()));
            throw;
        }
    }
    
    ~AlarisQuantLibProcess() {
        if (scheduler_ && scheduler_->is_running()) {
             scheduler_->stop_execution();
        }
        if(event_logger_) event_logger_->log_system_status("QuantLib process destructor called.");
    }
    
    void run() {
        if (!scheduler_ || !strategy_ || !shared_memory_manager_ || !event_logger_) {
            std::cerr << "Cannot run: Essential components not initialized." << std::endl;
            return;
        }

        try {
            std::cout << "Starting Alaris QuantLib Process with TTA scheduling..." << std::endl;
            event_logger_->log_system_status("QuantLib process starting TTA execution.");
            
            // Print the computed schedule for verification
            scheduler_->print_schedule_table();
            
            // Initial state from config or default to false
            trading_enabled_ = config_["process"]["start_trading_enabled"].as<bool>(false);
            if (trading_enabled_) {
                event_logger_->log_system_status("Trading enabled on startup as per configuration.");
            } else {
                event_logger_->log_system_status("Trading disabled on startup as per configuration.");
            }
            
            // Start the TTA scheduler
            if (!scheduler_->start_execution()) {
                std::cerr << "Failed to start TaskScheduler" << std::endl;
                return;
            }
            
            // Main loop - just monitor shutdown signal
            while (!g_shutdown_requested.load()) {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
            
            std::cout << "Alaris QuantLib Process main loop finished." << std::endl;
            event_logger_->log_system_status("QuantLib process main loop finished.");

        }
        catch (const std::exception& e) {
            std::cerr << "Runtime error in Alaris QuantLib Process: " << e.what() << std::endl;
            if(event_logger_) event_logger_->log_error("Runtime error in main execution loop: " + std::string(e.what()));
        }
    }
    
    void perform_shutdown() {
        if (g_shutdown_requested.exchange(true)) {
            return; 
        }
        
        std::cout << "Shutting down QuantLib process explicitly..." << std::endl;
        if(event_logger_) event_logger_->log_system_status("QuantLib process initiating shutdown sequence.");
        
        trading_enabled_ = false;
        
        if (scheduler_) {
            scheduler_->stop_execution();
        }
        
        report_performance_metrics();

        std::cout << "QuantLib process shutdown sequence complete." << std::endl;
        if(event_logger_) event_logger_->log_system_status("QuantLib process shutdown sequence complete.");
    }

private:
    void initialize_system_settings() {
        // Process Priority & Affinity (from config)
        if (config_["process"]["priority"]) {
            int priority = config_["process"]["priority"].as<int>(80);
            struct sched_param param;
            param.sched_priority = priority;
            if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
                perror("Warning: Failed to set real-time priority (sched_setscheduler)");
            } else {
                 std::cout << "Process priority set to SCHED_FIFO " << priority << std::endl;
            }
        }
        
        if (config_["process"]["cpu_affinity"] && config_["process"]["cpu_affinity"].IsSequence()) {
            cpu_set_t cpuset;
            CPU_ZERO(&cpuset);
            for (const auto& cpu_node : config_["process"]["cpu_affinity"]) {
                CPU_SET(cpu_node.as<int>(), &cpuset);
            }
            if (sched_setaffinity(0, sizeof(cpu_set_t), &cpuset) != 0) {
                perror("Warning: Failed to set CPU affinity (sched_setaffinity)");
            } else {
                std::cout << "CPU affinity configured." << std::endl;
            }
        }
        
        // Memory Locking (from config) - More conservative approach
        if (config_["process"]["memory_lock"] && config_["process"]["memory_lock"].as<bool>(true)) {
            // Try MCL_CURRENT first (lock current pages)
            if (mlockall(MCL_CURRENT) != 0) {
                perror("Warning: Failed to lock current memory pages (mlockall MCL_CURRENT)");
                // Continue without memory locking
            } else {
                std::cout << "Memory locking (mlockall MCL_CURRENT) enabled." << std::endl;
                
                // Only try MCL_FUTURE if MCL_CURRENT succeeded
                if (mlockall(MCL_FUTURE) != 0) {
                    perror("Warning: Failed to lock future memory pages (mlockall MCL_FUTURE)");
                    // Continue with only current pages locked
                } else {
                    std::cout << "Memory locking (mlockall MCL_FUTURE) enabled." << std::endl;
                }
            }
        }
    }
    
    void initialize_components() {
        // Memory Pool - More conservative size
        size_t pool_size_mb = config_["memory"]["pool_size_mb"].as<size_t>(32);  // Reduced from 64MB to 32MB
        try {
            mem_pool_ = std::make_unique<Core::MemoryPool>(pool_size_mb * 1024 * 1024);
            allocator_ = std::make_unique<Core::PerCycleAllocator>(*mem_pool_);
        } catch (const std::bad_alloc& e) {
            std::cerr << "Failed to initialize memory pool: " << e.what() << std::endl;
            std::cerr << "Try reducing pool_size_mb in config or check system memory limits" << std::endl;
            throw;
        }
        
        // Event Logger
        std::string log_file = config_["logging"]["file"].as<std::string>("/var/log/alaris/quantlib.log");
        bool log_binary = config_["logging"]["binary_mode"].as<bool>(true);
        event_logger_ = std::make_unique<Core::EventLogger>(log_file, log_binary);
        
        // Shared Memory Manager
        shared_memory_manager_ = std::make_unique<IPC::SharedMemoryManager>(true);
        
        // Task Scheduler (TTA)
        scheduler_ = std::make_unique<Core::TaskScheduler>(BASIC_TIME_UNIT);
        
        // Pricing Engine (QuantLibALOEngine)
        pricer_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
        if (config_["pricing"] && config_["pricing"]["alo_engine"]) {
            auto alo_config = config_["pricing"]["alo_engine"];
            std::string scheme_str = alo_config["scheme"].as<std::string>("accurate");
             if (scheme_str == "accurate") {
                pricer_->set_iteration_scheme(QuantLib::QdFpAmericanEngine::accurateScheme());
            } else if (scheme_str == "high_precision") {
                pricer_->set_iteration_scheme(QuantLib::QdFpAmericanEngine::highPrecisionScheme());
            } else if (scheme_str == "fast") {
                pricer_->set_iteration_scheme(QuantLib::QdFpAmericanEngine::fastScheme());
            }
        }
        
        // Volatility Arbitrage Strategy with standard GARCH
        strategy_ = std::make_unique<Strategy::VolatilityArbitrageStrategy>(*pricer_, *allocator_, *event_logger_, *mem_pool_);
        if (config_["strategy"] && config_["strategy"]["vol_arbitrage"]) {
            Strategy::StrategyParameters params;
            // Fixed parameter names to match StrategyParameters struct
            params.vol_difference_threshold = config_["strategy"]["vol_arbitrage"]["entry_threshold"].as<double>(0.05);
            params.vol_exit_threshold = config_["strategy"]["vol_arbitrage"]["exit_threshold"].as<double>(0.02);
            params.confidence_threshold = config_["strategy"]["vol_arbitrage"]["confidence_threshold"].as<double>(0.7);
            params.max_position_size = config_["strategy"]["vol_arbitrage"]["max_position_size"].as<double>(0.05);
            
            // Additional risk parameters
            if (config_["strategy"]["vol_arbitrage"]["risk_limit"]) {
                params.max_portfolio_delta = config_["strategy"]["vol_arbitrage"]["risk_limit"].as<double>(0.10);
            }
            strategy_->set_parameters(params);

            std::string model_selection_str = config_["strategy"]["vol_arbitrage"]["model_selection"].as<std::string>("ensemble");
            if (model_selection_str == "garch_direct") {
                strategy_->set_active_volatility_model_type(Strategy::VolatilityModelType::GARCH_DIRECT);
            } else {
                strategy_->set_active_volatility_model_type(Strategy::VolatilityModelType::ENSEMBLE_GARCH_HISTORICAL);
            }
        }
    }
    
    void setup_task_schedule() {
        if (!scheduler_) {
            throw std::runtime_error("TaskScheduler not initialized");
        }
        
        // Use TaskSetBuilder for clean task definition
        Core::TaskSetBuilder builder(BASIC_TIME_UNIT);
        
        // Add tasks with their periods, WCETs, and priorities
        // Higher priority numbers = higher priority
        
        builder.add_critical_task(
            "MarketDataProcessor",
            [this]() { this->process_market_data(); },
            MARKET_DATA_PERIOD,
            MARKET_DATA_WCET,
            100  // Highest priority - market data is critical
        );
        
        builder.add_periodic_task(
            "ControlMessageProcessor", 
            [this]() { this->process_control_messages(); },
            CONTROL_PROCESSING_PERIOD,
            CONTROL_PROCESSING_WCET,
            90   // High priority - control messages are important
        );
        
        builder.add_periodic_task(
            "SignalGenerator",
            [this]() { this->generate_trading_signals(); },
            SIGNAL_GENERATION_PERIOD,
            SIGNAL_GENERATION_WCET,
            80   // Medium-high priority - trading signals
        );
        
        builder.add_periodic_task(
            "HeartbeatSender",
            [this]() { this->send_heartbeat(); },
            HEARTBEAT_PERIOD,
            HEARTBEAT_WCET,
            20   // Low priority - heartbeat
        );
        
        builder.add_periodic_task(
            "PerformanceReporter",
            [this]() { this->report_performance_metrics(); },
            PERFORMANCE_REPORT_PERIOD,
            PERFORMANCE_REPORT_WCET,
            10   // Lowest priority - performance reporting
        );
        
        // Validate the task set before building
        auto validation_report = builder.validate();
        if (!validation_report.is_schedulable) {
            std::cerr << "Task set is not schedulable!" << std::endl;
            for (const auto& conflict : validation_report.conflicts) {
                std::cerr << "  Conflict: " << conflict << std::endl;
            }
            throw std::runtime_error("Task set validation failed");
        }
        
        // Print validation report
        std::cout << "\n=== TTA Schedulability Analysis ===" << std::endl;
        std::cout << "CPU Utilization: " << (validation_report.cpu_utilization * 100) << "%" << std::endl;
        std::cout << "Hyperperiod: " << std::chrono::duration_cast<std::chrono::milliseconds>(validation_report.hyperperiod).count() << "ms" << std::endl;
        std::cout << "Basic Time Unit: " << std::chrono::duration_cast<std::chrono::microseconds>(validation_report.basic_time_unit).count() << "μs" << std::endl;
        std::cout << "Total Executions per Hyperperiod: " << validation_report.total_executions_per_hyperperiod << std::endl;
        
        for (const auto& warning : validation_report.warnings) {
            std::cout << "Warning: " << warning << std::endl;
        }
        std::cout << "Status: SCHEDULABLE ✓" << std::endl;
        std::cout << std::endl;
        
        // Build the scheduler
        if (!builder.build_scheduler(*scheduler_)) {
            throw std::runtime_error("Failed to build task scheduler");
        }
        
        if(event_logger_) {
            event_logger_->log_system_status("TTA task schedule created successfully");
            event_logger_->log_performance_metric("tta_cpu_utilization", validation_report.cpu_utilization);
            event_logger_->log_performance_metric("tta_hyperperiod_ms", 
                std::chrono::duration_cast<std::chrono::milliseconds>(validation_report.hyperperiod).count());
        }
    }
        
    void send_heartbeat() {
        if (!shared_memory_manager_ || !strategy_) return;
        
        // FIXED: Use default constructor and set fields manually
        IPC::ControlMessage heartbeat_msg;
        heartbeat_msg.message_type = static_cast<uint32_t>(IPC::ControlMessageType::HEARTBEAT);
        
        // FIXED: Use correct field name 'timestamp_ns' instead of 'timestamp'
        heartbeat_msg.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
                                     std::chrono::system_clock::now().time_since_epoch()).count();
        heartbeat_msg.value1 = static_cast<double>(cycles_executed_);
        heartbeat_msg.sequence_number = 0; // Set appropriate sequence number
        heartbeat_msg.source_process_id = 1; // Set appropriate process ID
        heartbeat_msg.target_process_id = 0; // Broadcast
        heartbeat_msg.priority = static_cast<uint32_t>(IPC::TTAPriority::LOW);
        
        shared_memory_manager_->publish_control(heartbeat_msg);
    }
    
    void process_market_data() {
        if (!trading_enabled_ || !shared_memory_manager_ || !strategy_ || !event_logger_ || !allocator_) return;
        
        allocator_->reset();

        IPC::MarketDataMessage market_data_msg;
        int processed_count = 0;
        while (processed_count < 10 && shared_memory_manager_->consume_market_data(market_data_msg)) {
            strategy_->on_market_data(market_data_msg);
            event_logger_->log_market_data(market_data_msg);
            processed_count++;
        }
        
        cycles_executed_++;
    }
    
    void generate_trading_signals() {
        if (!trading_enabled_ || !strategy_ || !shared_memory_manager_ || !event_logger_) return;
        
        std::vector<IPC::TradingSignalMessage> signals;
        
        // Placeholder for option chain scanning logic
        // In a real implementation, this would iterate through configured underlyings
        // and call strategy_->scan_and_generate_signals() for each option chain

        for (const auto& signal : signals) {
            if (shared_memory_manager_->publish_signal(signal)) {
                event_logger_->log_trading_signal(signal);
            } else {
                event_logger_->log_error("Failed to publish trading signal for symbol: " + std::to_string(signal.symbol_id));
            }
        }
    }
    
    void process_control_messages() {
        if (!shared_memory_manager_ || !strategy_ || !event_logger_) return;

        IPC::ControlMessage control_msg;
        while (shared_memory_manager_->consume_control(control_msg)) {
            event_logger_->log_control_message(control_msg);
            auto msg_type = static_cast<IPC::ControlMessageType>(control_msg.message_type);
            
            switch (msg_type) {
                case IPC::ControlMessageType::START_TRADING:
                    if (!trading_enabled_.exchange(true)) {
                        event_logger_->log_system_status("Trading enabled by control message.");
                    }
                    break;
                case IPC::ControlMessageType::STOP_TRADING:
                    if (trading_enabled_.exchange(false)) {
                        event_logger_->log_system_status("Trading disabled by control message.");
                    }
                    break;
                case IPC::ControlMessageType::UPDATE_PARAMETERS:
                    event_logger_->log_system_status("Received UPDATE_PARAMETERS control message (logic to apply params needed).");
                    break;
                case IPC::ControlMessageType::RESET_MODELS:
                    event_logger_->log_system_status("Received RESET_MODELS control message (calibration logic needed).");
                    break;
                case IPC::ControlMessageType::HEARTBEAT:
                    event_logger_->log_system_status("Received HEARTBEAT (typically sent, not received by QL process).");
                    break;
                default:
                    event_logger_->log_system_status("Received unknown control message type: " + std::to_string(control_msg.message_type));
                    break;
            }
        }
    }
    
    void report_performance_metrics() {
        if (!scheduler_ || !event_logger_ || !shared_memory_manager_) return;

        // Get task scheduler metrics
        std::cout << "\n=== TTA Performance Report ===" << std::endl;
        std::cout << "Hyperperiod: " << std::chrono::duration_cast<std::chrono::milliseconds>(scheduler_->get_hyperperiod()).count() << "ms" << std::endl;
        
        // Report metrics for each task
        const std::vector<std::string> task_names = {
            "MarketDataProcessor", "ControlMessageProcessor", "SignalGenerator", 
            "HeartbeatSender", "PerformanceReporter"
        };
        
        for (const auto& task_name : task_names) {
            try {
                const auto& metrics = scheduler_->get_task_metrics(task_name);
                auto avg_exec_time = metrics.executions_completed > 0 ? 
                    metrics.total_execution_time / static_cast<int64_t>(metrics.executions_completed) :
                    Core::TaskScheduler::Duration::zero();
                    
                std::cout << task_name << ": "
                          << "Executions=" << metrics.executions_completed
                          << ", Misses=" << metrics.deadline_misses
                          << ", Avg=" << std::chrono::duration_cast<std::chrono::microseconds>(avg_exec_time).count() << "μs"
                          << ", Max=" << std::chrono::duration_cast<std::chrono::microseconds>(metrics.max_execution_time).count() << "μs"
                          << std::endl;
                
                // Log to EventLogger
                event_logger_->log_performance_metric(task_name + "_executions", static_cast<double>(metrics.executions_completed));
                event_logger_->log_performance_metric(task_name + "_deadline_misses", static_cast<double>(metrics.deadline_misses));
                if (metrics.executions_completed > 0) {
                    auto avg_exec_time_us = std::chrono::duration_cast<std::chrono::microseconds>(avg_exec_time).count();
                    event_logger_->log_performance_metric(task_name + "_avg_execution_us", static_cast<double>(avg_exec_time_us));
                }
                auto max_exec_time_us = std::chrono::duration_cast<std::chrono::microseconds>(metrics.max_execution_time).count();
                event_logger_->log_performance_metric(task_name + "_max_execution_us", static_cast<double>(max_exec_time_us));
                
            } catch (const std::exception& e) {
                std::cerr << "Error getting metrics for " << task_name << ": " << e.what() << std::endl;
            }
        }
        
        // Shared memory status
        auto sm_status = shared_memory_manager_->get_status();
        std::cout << "SharedMemory - MarketData: " << sm_status.market_data_size << "/" << 4096
                  << " (Util: " << sm_status.market_data_utilization * 100 << "%)" << std::endl;
        
        event_logger_->log_performance_metric("sm_market_data_util", sm_status.market_data_utilization);
        event_logger_->log_performance_metric("sm_signal_util", sm_status.signal_utilization);
        event_logger_->log_performance_metric("sm_control_util", sm_status.control_utilization);

        if (!shared_memory_manager_->is_healthy()) {
            event_logger_->log_error("Shared memory buffers approaching capacity or unhealthy!");
        }
        
        std::cout << std::endl;
    }
};

int main(int argc, char* argv[]) {
    // Setup signal handling for graceful shutdown
    signal(SIGINT, signal_handler);
    signal(SIGTERM, signal_handler);
    signal(SIGPIPE, SIG_IGN);

    std::string config_file = "config/quantlib_process.yaml";
    if (argc > 1) {
        config_file = argv[1];
    }
    std::cout << "Alaris QuantLib Process starting with TTA scheduling..." << std::endl;
    std::cout << "Using configuration file: " << config_file << std::endl;

    try {
        AlarisQuantLibProcess process(config_file);
        process.run();
        process.perform_shutdown();
    }
    catch (const std::exception& e) {
        std::cerr << "Fatal error encountered: " << e.what() << std::endl;
        return 1; 
    }
    catch (...) {
        std::cerr << "Unknown fatal error encountered." << std::endl;
        return 2;
    }

    std::cout << "Alaris QuantLib Process has shut down gracefully." << std::endl;
    return 0;
}