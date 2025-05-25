// src/quantlib/main.cpp
#include "pricing/alo_engine.h"
#include "volatility/gjrgarch_wrapper.h" // Though not directly used here, strategy uses it
#include "strategy/vol_arb.h"
#include "core/memory_pool.h"
#include "core/time_trigger.h"
#include "core/event_log.h"
#include "ipc/shared_memory.h" // For Alaris::IPC::SharedMemoryManager and message types
#include <iostream>
#include <string>
#include <vector>
#include <memory> // For std::unique_ptr
#include <csignal> // For signal handling
#include <unistd.h> // For POSIX functions like sleep, not used in main loop directly
#include <sys/mman.h> // For mlockall
#include <sched.h>    // For sched_setscheduler, sched_setaffinity
#include <yaml-cpp/yaml.h>

// Using namespace Alaris for brevity within this file if preferred,
// or qualify with Alaris::Core::, Alaris::IPC::, etc.
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
    std::unique_ptr<Core::PerCycleAllocator> allocator_; // Allocator per processing cycle
    std::unique_ptr<Core::EventLogger> event_logger_;
    std::unique_ptr<IPC::SharedMemoryManager> shared_memory_manager_; // Corrected name
    std::unique_ptr<Core::TimeTriggeredExecutor> executor_;
    
    // Trading components
    std::unique_ptr<Pricing::QuantLibALOEngine> pricer_;
    std::unique_ptr<Strategy::VolatilityArbitrageStrategy> strategy_;
    
    // Configuration
    YAML::Node config_;
    
    // Runtime state
    // g_shutdown_requested_ is now global for signal handler access
    std::atomic<bool> trading_enabled_{false};
    
    // Performance metrics
    uint64_t cycles_executed_ = 0;
    // signals_generated_ is now part of strategy_, can be fetched from there if needed for heartbeat
    
public:
    AlarisQuantLibProcess(const std::string& config_file_path) { // Renamed for clarity
        try {
            config_ = YAML::LoadFile(config_file_path);
            initialize_system_settings(); // Renamed for clarity
            initialize_components();
            // Signal handlers are set up in main() to allow for a clean shutdown sequence
            
            std::cout << "Alaris QuantLib Process initialized successfully from config: " << config_file_path << std::endl;
            if(event_logger_) event_logger_->log_system_status("QuantLib process started and initialized.");
        }
        catch (const YAML::Exception& e) {
            std::cerr << "Failed to load or parse configuration file '" << config_file_path << "': " << e.what() << std::endl;
            throw; // Re-throw critical error
        }
        catch (const std::exception& e) {
            std::cerr << "Failed to initialize QuantLib process: " << e.what() << std::endl;
            if(event_logger_) event_logger_->log_error("Process initialization failed: " + std::string(e.what()));
            throw; // Re-throw critical error
        }
    }
    
    ~AlarisQuantLibProcess() {
        // Shutdown logic should ideally be called explicitly before destruction,
        // but a failsafe call here can be useful.
        // Ensure executor is stopped first if it runs in a separate thread.
        if (executor_ && executor_->is_running()) {
             executor_->stop();
        }
        if(event_logger_) event_logger_->log_system_status("QuantLib process destructor called.");
        // Other components will be cleaned up by unique_ptr
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
            // Example frequencies, adjust based on requirements and config
            executor_->register_task([this]() { this->process_market_data(); }, 
                                   std::chrono::milliseconds(config_["executor"]["market_data_interval_ms"].as<long>(1)));
            
            executor_->register_task([this]() { this->generate_trading_signals(); }, 
                                   std::chrono::milliseconds(config_["executor"]["signal_interval_ms"].as<long>(10)));
            
            executor_->register_task([this]() { this->process_control_messages(); }, 
                                   std::chrono::milliseconds(config_["executor"]["control_interval_ms"].as<long>(5)));

            executor_->register_task([this]() { this->send_heartbeat(); }, 
                                   std::chrono::seconds(config_["executor"]["heartbeat_interval_s"].as<long>(1)));
            
            executor_->register_task([this]() { this->report_performance_metrics(); }, // Renamed for clarity
                                   std::chrono::seconds(config_["executor"]["perf_report_interval_s"].as<long>(10)));
            
            // Initial state from config or default to false
            trading_enabled_ = config_["process"]["start_trading_enabled"].as<bool>(false);
            if (trading_enabled_) {
                event_logger_->log_system_status("Trading enabled on startup as per configuration.");
            } else {
                event_logger_->log_system_status("Trading disabled on startup as per configuration.");
            }
            
            // Run main execution loop (blocks until stop() or g_shutdown_requested makes it stop)
            executor_->run_continuous(g_shutdown_requested); // Pass the global shutdown flag
            
            std::cout << "Alaris QuantLib Process main loop finished." << std::endl;
            event_logger_->log_system_status("QuantLib process main loop finished.");

        }
        catch (const std::exception& e) {
            std::cerr << "Runtime error in Alaris QuantLib Process: " << e.what() << std::endl;
            if(event_logger_) event_logger_->log_error("Runtime error in main execution loop: " + std::string(e.what()));
        }
    }
    
    void perform_shutdown() { // Renamed for clarity
        if (g_shutdown_requested.exchange(true)) { // Ensure this is only run once
            return; 
        }
        
        std::cout << "Shutting down QuantLib process explicitly..." << std::endl;
        if(event_logger_) event_logger_->log_system_status("QuantLib process initiating shutdown sequence.");
        
        trading_enabled_ = false; // Stop trading activities
        
        if (executor_) {
            executor_->stop(); // Request executor to stop its loop
        }
        
        // Perform a final performance report
        report_performance_metrics();
        
        if(strategy_) {
             // strategy_->close_all_positions(); // This should generate signals, not directly close.
             // Actual closing is an interaction with Lean via signals.
        }

        std::cout << "QuantLib process shutdown sequence complete." << std::endl;
        if(event_logger_) event_logger_->log_system_status("QuantLib process shutdown sequence complete.");
    }

private:
    void initialize_system_settings() {
        // Process Priority & Affinity (from config)
        if (config_["process"]["priority"]) {
            int priority = config_["process"]["priority"].as<int>(80); // Default priority 80 for SCHED_FIFO
            struct sched_param param;
            param.sched_priority = priority;
            if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
                perror("Warning: Failed to set real-time priority (sched_setscheduler)");
                // Non-fatal, but log it
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
        // Huge pages are typically configured at the OS level. Application can advise (MADV_HUGEPAGE).
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
        shared_memory_manager_ = std::make_unique<IPC::SharedMemoryManager>(true); // This is the producer process
        
        // Time-Triggered Executor
        long major_frame_ms = config_["executor"]["major_frame_ms"].as<long>(10);
        executor_ = std::make_unique<Core::TimeTriggeredExecutor>(std::chrono::milliseconds(major_frame_ms));
        
        // Pricing Engine (QuantLibALOEngine)
        pricer_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
        if (config_["pricing"] && config_["pricing"]["alo_engine"]) {
            auto alo_config = config_["pricing"]["alo_engine"];
            // Example: Configure pricer from YAML if needed
            // pricer_->set_time_steps(alo_config["time_steps"].as<size_t>(800));
            // pricer_->set_asset_steps(alo_config["asset_steps"].as<size_t>(800));
            // Assuming QuantLibALOEngine has such setters or is configured via its constructor.
            // The original code directly sets iteration scheme.
            std::string scheme_str = alo_config["scheme"].as<std::string>("accurate");
             if (scheme_str == "accurate") {
                pricer_->set_iteration_scheme(QuantLib::QdFpAmericanEngine::accurateScheme());
            } else if (scheme_str == "high_precision") {
                pricer_->set_iteration_scheme(QuantLib::QdFpAmericanEngine::highPrecisionScheme());
            } else if (scheme_str == "fast") { // Ensure "fast" is a valid scheme name if used
                pricer_->set_iteration_scheme(QuantLib::QdFpAmericanEngine::fastScheme());
            }
            // Fixed point equation can also be set if configurable.
            // pricer_->set_fixed_point_equation(QuantLib::QdFpAmericanEngine::Auto); // Default
        }
        
        // Volatility Arbitrage Strategy
        strategy_ = std::make_unique<Strategy::VolatilityArbitrageStrategy>(*pricer_, *allocator_, *event_logger_, *mem_pool_);
        if (config_["strategy"] && config_["strategy"]["vol_arbitrage"]) {
            Strategy::StrategyParameters params; // Matches struct in vol_arb.h
            params.entry_threshold = config_["strategy"]["vol_arbitrage"]["entry_threshold"].as<double>(0.05);
            params.exit_threshold = config_["strategy"]["vol_arbitrage"]["exit_threshold"].as<double>(0.02);
            params.risk_limit = config_["strategy"]["vol_arbitrage"]["risk_limit"].as<double>(0.10);
            params.confidence_threshold = config_["strategy"]["vol_arbitrage"]["confidence_threshold"].as<double>(0.7);
            params.max_position_size = config_["strategy"]["vol_arbitrage"]["max_position_size"].as<double>(0.05);
            strategy_->set_parameters(params);

            std::string model_selection_str = config_["strategy"]["vol_arbitrage"]["model_selection"].as<std::string>("ensemble");
            if (model_selection_str == "gjr_garch_direct") {
                strategy_->set_active_volatility_model_type(Strategy::VolatilityModelType::GJR_GARCH_DIRECT);
            } else { // Default to ensemble
                strategy_->set_active_volatility_model_type(Strategy::VolatilityModelType::ENSEMBLE_GJR_HISTORICAL);
            }
        }
    }
        
    void send_heartbeat() {
        if (!shared_memory_manager_ || !strategy_) return;
        IPC::ControlMessage heartbeat_msg(static_cast<uint32_t>(IPC::ControlMessageType::HEARTBEAT));
        heartbeat_msg.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                                  std::chrono::system_clock::now().time_since_epoch()).count();
        heartbeat_msg.value1 = static_cast<double>(cycles_executed_);
        
        // Get signals generated from strategy (assuming a method like get_total_signals_generated())
        // For now, placeholder, as signals_generated_ was removed from this class.
        // heartbeat_msg.value2 = static_cast<double>(strategy_->get_performance_metrics().total_signals_generated); 
        
        shared_memory_manager_->publish_control(heartbeat_msg);
    }
    
    void process_market_data() {
        if (!trading_enabled_ || !shared_memory_manager_ || !strategy_ || !event_logger_ || !allocator_) return;
        
        allocator_->reset(); // Reset per-cycle allocator at the beginning of the cycle

        IPC::MarketDataMessage market_data_msg;
        // Process a batch of market data to keep up if there's a burst
        // The number 10 is arbitrary, could be tuned.
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
        // The strategy needs to know which option chains to scan.
        // This might come from a configuration, discovery, or control message.
        // For this example, let's assume scan_option_chain is called for relevant underlyings.
        // This part needs to be fleshed out: how does the strategy know which options to scan?
        // E.g., it might maintain a list of active underlyings from config or discovery.
        
        // --- Placeholder for option chain scanning logic ---
        // Example: Iterate through configured underlyings
        // for (uint32_t underlying_sym_id : configured_underlyings_) {
        //    std::vector<Pricing::OptionData> chain = get_option_chain_for(underlying_sym_id); // Needs implementation
        //    std::vector<IPC::MarketDataMessage> option_md = get_md_for_chain(chain); // Needs implementation
        //    strategy_->scan_option_chain(underlying_sym_id, chain, option_md, signals);
        // }
        // --- End Placeholder ---

        // If signals are generated by a different mechanism within strategy (e.g., periodic internal scan)
        // strategy_->generate_signals(signals); // If generate_signals is a top-level call in strategy

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
            event_logger_->log_control_message(control_msg); // Log received control message
            auto msg_type = static_cast<IPC::ControlMessageType>(control_msg.message_type);
            
            switch (msg_type) {
                case IPC::ControlMessageType::START_TRADING:
                    if (!trading_enabled_.exchange(true)) { // Atomically set and check previous value
                        event_logger_->log_system_status("Trading enabled by control message.");
                    }
                    break;
                case IPC::ControlMessageType::STOP_TRADING:
                    if (trading_enabled_.exchange(false)) {
                        event_logger_->log_system_status("Trading disabled by control message.");
                        // Consider actions like cancelling open orders via signals to Lean
                    }
                    break;
                case IPC::ControlMessageType::UPDATE_PARAMETERS:
                    // Example: Deserialize parameters from control_msg.data or use value1/value2
                    // Strategy::StrategyParameters new_params;
                    // ... populate new_params ...
                    // strategy_->set_parameters(new_params);
                    event_logger_->log_system_status("Received UPDATE_PARAMETERS control message (logic to apply params needed).");
                    break;
                case IPC::ControlMessageType::RESET_MODELS:
                    // Example: strategy_->calibrate_gjr_model(fetch_historical_data());
                    // This needs a data source for calibration.
                    event_logger_->log_system_status("Received RESET_MODELS control message (calibration logic needed).");
                    break;
                case IPC::ControlMessageType::HEARTBEAT: // Usually QuantLib sends, Lean receives. Could be bi-directional.
                     // If QuantLib receives a heartbeat, it might be a ping from Lean.
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
        // auto strat_metrics = strategy_->get_performance_metrics(); // If you want strategy specific metrics

        // Log to console (optional)
        std::cout << "--- Performance Report ---" << std::endl;
        std::cout << "Executor Cycles: " << exec_metrics.total_cycles
                  << ", Avg Cycle (us): " << exec_metrics.average_cycle_time_us
                  << ", Max Cycle (us): " << exec_metrics.max_cycle_time_us
                  << ", Deadlines Missed: " << exec_metrics.total_deadlines_missed << std::endl;
        std::cout << "MarketData Buffer: " << sm_status.market_data_size << "/" << 4096 // Hardcoded size, get from SM
                  << " (Util: " << sm_status.market_data_utilization * 100 << "%)" << std::endl;
        // ... more stats ...
        
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
}; // End of AlarisQuantLibProcess class

int main(int argc, char* argv[]) {
    // Setup signal handling for graceful shutdown
    signal(SIGINT, signal_handler);
    signal(SIGTERM, signal_handler);
    // SIGPIPE should be ignored if writing to a pipe whose reader has terminated
    signal(SIGPIPE, SIG_IGN);


    std::string config_file = "config/quantlib_process.yaml"; // Default config path
    if (argc > 1) {
        config_file = argv[1]; // Allow overriding config file via command line
    }
    std::cout << "Alaris QuantLib Process starting..." << std::endl;
    std::cout << "Using configuration file: " << config_file << std::endl;

    try {
        AlarisQuantLibProcess process(config_file);
        process.run(); // Main blocking call
        process.perform_shutdown(); // Explicit shutdown after run() finishes (due to g_shutdown_requested)
    }
    catch (const std::exception& e) {
        std::cerr << "Fatal error encountered: " << e.what() << std::endl;
        // Consider specific exit codes for different error types
        return 1; 
    }
    catch (...) {
        std::cerr << "Unknown fatal error encountered." << std::endl;
        return 2;
    }

    std::cout << "Alaris QuantLib Process has shut down gracefully." << std::endl;
    return 0;
}