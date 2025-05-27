// src/quantlib/main.cpp
#include "pricing/alo_engine.h"
#include "volatility/garch_wrapper.h"  // Changed from gjrgarch_wrapper.h
#include "strategy/vol_arb.h"
#include "core/memory_pool.h"
#include "core/time_trigger.h"
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
    std::unique_ptr<Core::TimeTriggeredExecutor> executor_;
    
    // Trading components
    std::unique_ptr<Pricing::QuantLibALOEngine> pricer_;
    std::unique_ptr<Strategy::VolatilityArbitrageStrategy> strategy_;
    
    // Configuration
    YAML::Node config_;
    
    // Runtime state
    std::atomic<bool> trading_enabled_{false};
    
    // Performance metrics
    uint64_t cycles_executed_{0};
    
public:
    AlarisQuantLibProcess(const std::string& config_file_path) {
        try {
            config_ = YAML::LoadFile(config_file_path);
            initialize_system_settings();
            initialize_components();
            
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
        if (executor_ && executor_->is_running()) {
             executor_->stop();
        }
        if(event_logger_) event_logger_->log_system_status("QuantLib process destructor called.");
    }
    
    void run() {
        if (!executor_ || !strategy_ || !shared_memory_manager_ || !event_logger_) {
            std::cerr << "Cannot run: Essential components not initialized." << std::endl;
            return;
        }

        try {
            std::cout << "Starting Alaris QuantLib Process main loop..." << std::endl;
            event_logger_->log_system_status("QuantLib process main loop starting.");
            
            // Register tasks with the time-triggered executor
            executor_->register_task([this]() { this->process_market_data(); }, 
                                   std::chrono::milliseconds(config_["executor"]["market_data_interval_ms"].as<long>(1)));
            
            executor_->register_task([this]() { this->generate_trading_signals(); }, 
                                   std::chrono::milliseconds(config_["executor"]["signal_interval_ms"].as<long>(10)));
            
            executor_->register_task([this]() { this->process_control_messages(); }, 
                                   std::chrono::milliseconds(config_["executor"]["control_interval_ms"].as<long>(5)));

            executor_->register_task([this]() { this->send_heartbeat(); }, 
                                   std::chrono::seconds(config_["executor"]["heartbeat_interval_s"].as<long>(1)));
            
            executor_->register_task([this]() { this->report_performance_metrics(); }, 
                                   std::chrono::seconds(config_["executor"]["perf_report_interval_s"].as<long>(10)));
            
            // Initial state from config or default to false
            trading_enabled_ = config_["process"]["start_trading_enabled"].as<bool>(false);
            if (trading_enabled_) {
                event_logger_->log_system_status("Trading enabled on startup as per configuration.");
            } else {
                event_logger_->log_system_status("Trading disabled on startup as per configuration.");
            }
            
            // Run main execution loop
            executor_->run_continuous(g_shutdown_requested);
            
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
        
        if (executor_) {
            executor_->stop();
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
        
        // Memory Locking (from config)
        if (config_["process"]["memory_lock"] && config_["process"]["memory_lock"].as<bool>(true)) {
            if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
                perror("Warning: Failed to lock memory (mlockall)");
            } else {
                std::cout << "Memory locking (mlockall) enabled." << std::endl;
            }
        }
    }
    
    void initialize_components() {
        // Memory Pool
        size_t pool_size_mb = config_["memory"]["pool_size_mb"].as<size_t>(64);
        mem_pool_ = std::make_unique<Core::MemoryPool>(pool_size_mb * 1024 * 1024);
        allocator_ = std::make_unique<Core::PerCycleAllocator>(*mem_pool_);
        
        // Event Logger
        std::string log_file = config_["logging"]["file"].as<std::string>("/var/log/alaris/quantlib.log");
        bool log_binary = config_["logging"]["binary_mode"].as<bool>(true);
        event_logger_ = std::make_unique<Core::EventLogger>(log_file, log_binary);
        
        // Shared Memory Manager
        shared_memory_manager_ = std::make_unique<IPC::SharedMemoryManager>(true);
        
        // Time-Triggered Executor
        long major_frame_ms = config_["executor"]["major_frame_ms"].as<long>(10);
        executor_ = std::make_unique<Core::TimeTriggeredExecutor>(std::chrono::milliseconds(major_frame_ms));
        
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
        
    void send_heartbeat() {
        if (!shared_memory_manager_ || !strategy_) return;
        IPC::ControlMessage heartbeat_msg(static_cast<uint32_t>(IPC::ControlMessageType::HEARTBEAT));
        heartbeat_msg.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                                  std::chrono::system_clock::now().time_since_epoch()).count();
        heartbeat_msg.value1 = static_cast<double>(cycles_executed_);
        
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
        if (!executor_ || !event_logger_ || !shared_memory_manager_) return;

        auto exec_metrics = executor_->get_performance_metrics();
        auto sm_status = shared_memory_manager_->get_status();

        // Log to console
        std::cout << "--- Performance Report ---" << std::endl;
        std::cout << "Executor Cycles: " << exec_metrics.total_cycles
                  << ", Avg Cycle (us): " << exec_metrics.average_cycle_time_us
                  << ", Max Cycle (us): " << exec_metrics.max_cycle_time_us
                  << ", Deadlines Missed: " << exec_metrics.total_deadlines_missed << std::endl;
        std::cout << "MarketData Buffer: " << sm_status.market_data_size << "/" << 4096
                  << " (Util: " << sm_status.market_data_utilization * 100 << "%)" << std::endl;
        
        // Log to EventLogger
        event_logger_->log_performance_metric("executor_total_cycles", static_cast<double>(exec_metrics.total_cycles));
        event_logger_->log_performance_metric("executor_avg_cycle_us", exec_metrics.average_cycle_time_us);
        event_logger_->log_performance_metric("executor_max_cycle_us", exec_metrics.max_cycle_time_us);
        event_logger_->log_performance_metric("executor_missed_deadlines", static_cast<double>(exec_metrics.total_deadlines_missed));
        event_logger_->log_performance_metric("sm_market_data_util", sm_status.market_data_utilization);
        event_logger_->log_performance_metric("sm_signal_util", sm_status.signal_utilization);
        event_logger_->log_performance_metric("sm_control_util", sm_status.control_utilization);

        if (!shared_memory_manager_->is_healthy()) {
            event_logger_->log_error("Shared memory buffers approaching capacity or unhealthy!");
        }
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
    std::cout << "Alaris QuantLib Process starting..." << std::endl;
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