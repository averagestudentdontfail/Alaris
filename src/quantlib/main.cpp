// src/quantlib/main.cpp
#include "pricing/alo_engine.h"
#include "volatility/gjrgarch_wrapper.h"
#include "strategy/vol_arb.h"
#include "core/memory_pool.h"
#include "core/time_trigger.h"
#include "core/event_log.h"
#include "ipc/shared_memory.h"
#include <iostream>
#include <signal.h>
#include <unistd.h>
#include <sys/mman.h>
#include <sched.h>
#include <yaml-cpp/yaml.h>

using namespace Alaris;

class AlarisQuantLibProcess {
private:
    // Core components
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Core::PerCycleAllocator> allocator_;
    std::unique_ptr<Core::EventLogger> event_logger_;
    std::unique_ptr<IPC::SharedMemoryManager> shared_memory_;
    std::unique_ptr<Core::TimeTriggeredExecutor> executor_;
    
    // Trading components
    std::unique_ptr<Pricing::QuantLibALOEngine> pricer_;
    std::unique_ptr<Strategy::VolatilityArbitrageStrategy> strategy_;
    
    // Configuration
    YAML::Node config_;
    
    // Runtime state
    std::atomic<bool> shutdown_requested_{false};
    std::atomic<bool> trading_enabled_{false};
    
    // Performance metrics
    uint64_t cycles_executed_ = 0;
    uint64_t signals_generated_ = 0;
    
public:
    AlarisQuantLibProcess(const std::string& config_file) {
        try {
            config_ = YAML::LoadFile(config_file);
            initialize_system();
            initialize_components();
            setup_signal_handlers();
            
            std::cout << "Alaris QuantLib Process initialized successfully" << std::endl;
            event_logger_->log_system_status("QuantLib process started");
        }
        catch (const std::exception& e) {
            std::cerr << "Failed to initialize QuantLib process: " << e.what() << std::endl;
            throw;
        }
    }
    
    ~AlarisQuantLibProcess() {
        shutdown();
    }
    
    void run() {
        try {
            std::cout << "Starting Alaris QuantLib Process..." << std::endl;
            
            // Send heartbeat every 1 second
            executor_->register_task([this]() { send_heartbeat(); }, 
                                   std::chrono::seconds(1));
            
            // Process market data every 1ms
            executor_->register_task([this]() { process_market_data(); }, 
                                   std::chrono::milliseconds(1));
            
            // Generate signals every 10ms
            executor_->register_task([this]() { generate_trading_signals(); }, 
                                   std::chrono::milliseconds(10));
            
            // Process control messages every 5ms
            executor_->register_task([this]() { process_control_messages(); }, 
                                   std::chrono::milliseconds(5));
            
            // Performance reporting every 10 seconds
            executor_->register_task([this]() { report_performance(); }, 
                                   std::chrono::seconds(10));
            
            trading_enabled_ = true;
            
            // Run main execution loop
            executor_->run_continuous();
            
        }
        catch (const std::exception& e) {
            std::cerr << "Error in main execution loop: " << e.what() << std::endl;
            event_logger_->log_error("Main loop error: " + std::string(e.what()));
        }
    }
    
    void shutdown() {
        if (shutdown_requested_.exchange(true)) {
            return; // Already shutting down
        }
        
        std::cout << "Shutting down QuantLib process..." << std::endl;
        event_logger_->log_system_status("QuantLib process shutting down");
        
        trading_enabled_ = false;
        
        if (executor_) {
            executor_->stop();
        }
        
        // Final performance report
        report_performance();
        
        std::cout << "QuantLib process shutdown complete" << std::endl;
    }

private:
    void initialize_system() {
        // Set process priority
        int priority = config_["process"]["priority"].as<int>(80);
        struct sched_param param;
        param.sched_priority = priority;
        if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
            std::cerr << "Warning: Failed to set real-time priority" << std::endl;
        }
        
        // Set CPU affinity
        if (config_["process"]["cpu_affinity"]) {
            cpu_set_t set;
            CPU_ZERO(&set);
            
            for (const auto& cpu : config_["process"]["cpu_affinity"]) {
                CPU_SET(cpu.as<int>(), &set);
            }
            
            if (sched_setaffinity(0, sizeof(set), &set) != 0) {
                std::cerr << "Warning: Failed to set CPU affinity" << std::endl;
            }
        }
        
        // Lock memory
        if (config_["process"]["memory_lock"].as<bool>(true)) {
            if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
                std::cerr << "Warning: Failed to lock memory" << std::endl;
            }
        }
    }
    
    void initialize_components() {
        // Initialize memory pool
        size_t pool_size = 64 * 1024 * 1024; // 64MB default
        mem_pool_ = std::make_unique<Core::MemoryPool>(pool_size);
        allocator_ = std::make_unique<Core::PerCycleAllocator>(*mem_pool_);
        
        // Initialize event logger
        std::string log_file = config_["logging"]["file"].as<std::string>("/var/log/alaris/quantlib.log");
        event_logger_ = std::make_unique<Core::EventLogger>(log_file);
        
        // Initialize shared memory
        shared_memory_ = std::make_unique<IPC::SharedMemoryManager>(true); // Producer mode
        
        // Initialize time-triggered executor
        auto major_frame = std::chrono::milliseconds(10);
        executor_ = std::make_unique<Core::TimeTriggeredExecutor>(major_frame);
        
        // Initialize pricing engine
        pricer_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
        
        // Configure pricing engine
        auto scheme_str = config_["pricing"]["alo_engine"]["scheme"].as<std::string>("ModifiedCraigSneyd");
        if (scheme_str == "ModifiedCraigSneyd") {
            pricer_->set_scheme(QuantLib::QdFpAmericanEngine::ModifiedCraigSneyd);
        }
        
        pricer_->set_grid_parameters(
            config_["pricing"]["alo_engine"]["time_steps"].as<QuantLib::Size>(800),
            config_["pricing"]["alo_engine"]["asset_steps"].as<QuantLib::Size>(800)
        );
        
        // Initialize strategy
        strategy_ = std::make_unique<Strategy::VolatilityArbitrageStrategy>(
            *pricer_, *allocator_, *event_logger_, *mem_pool_);
        
        // Configure strategy parameters
        Strategy::StrategyParameters params;
        params.entry_threshold = config_["strategy"]["vol_arbitrage"]["entry_threshold"].as<double>(0.05);
        params.exit_threshold = config_["strategy"]["vol_arbitrage"]["exit_threshold"].as<double>(0.02);
        params.risk_limit = config_["strategy"]["vol_arbitrage"]["risk_limit"].as<double>(0.10);
        strategy_->set_parameters(params);
    }
    
    void setup_signal_handlers() {
        signal(SIGINT, [](int) {
            std::cout << "\nReceived SIGINT, shutting down..." << std::endl;
            // Note: In production, would use a more sophisticated signal handling mechanism
            exit(0);
        });
        
        signal(SIGTERM, [](int) {
            std::cout << "\nReceived SIGTERM, shutting down..." << std::endl;
            exit(0);
        });
    }
    
    void send_heartbeat() {
        IPC::ControlMessage heartbeat((uint32_t)IPC::ControlMessageType::HEARTBEAT);
        heartbeat.value1 = static_cast<double>(cycles_executed_);
        heartbeat.value2 = static_cast<double>(signals_generated_);
        
        shared_memory_->publish_control(heartbeat);
    }
    
    void process_market_data() {
        if (!trading_enabled_) return;
        
        // Process incoming market data from Lean
        IPC::MarketDataMessage market_data;
        while (shared_memory_->consume_market_data(market_data)) {
            strategy_->on_market_data(market_data);
            event_logger_->log_market_data(market_data);
        }
        
        cycles_executed_++;
        
        // Reset per-cycle allocator
        allocator_->reset();
    }
    
    void generate_trading_signals() {
        if (!trading_enabled_) return;
        
        std::vector<IPC::TradingSignalMessage> signals;
        strategy_->generate_signals(signals);
        
        for (const auto& signal : signals) {
            if (shared_memory_->publish_signal(signal)) {
                signals_generated_++;
                event_logger_->log_trading_signal(signal);
            }
        }
    }
    
    void process_control_messages() {
        IPC::ControlMessage control;
        while (shared_memory_->consume_control(control)) {
            auto msg_type = static_cast<IPC::ControlMessageType>(control.message_type);
            
            switch (msg_type) {
                case IPC::ControlMessageType::START_TRADING:
                    trading_enabled_ = true;
                    event_logger_->log_system_status("Trading enabled");
                    break;
                    
                case IPC::ControlMessageType::STOP_TRADING:
                    trading_enabled_ = false;
                    event_logger_->log_system_status("Trading disabled");
                    break;
                    
                case IPC::ControlMessageType::RESET_MODELS:
                    // Reset volatility models
                    event_logger_->log_system_status("Models reset");
                    break;
                    
                default:
                    break;
            }
            
            event_logger_->log_control_message(control);
        }
    }
    
    void report_performance() {
        auto metrics = executor_->get_performance_metrics();
        
        std::cout << "Performance Report:" << std::endl;
        std::cout << "  Cycles: " << cycles_executed_ << std::endl;
        std::cout << "  Signals: " << signals_generated_ << std::endl;
        std::cout << "  Avg Cycle Time: " << metrics.average_cycle_time_us << " μs" << std::endl;
        std::cout << "  Max Cycle Time: " << metrics.max_cycle_time_us << " μs" << std::endl;
        std::cout << "  Avg Jitter: " << metrics.average_jitter_us << " μs" << std::endl;
        std::cout << "  Deadlines Missed: " << metrics.total_deadlines_missed << std::endl;
        
        // Log metrics
        event_logger_->log_performance_metric("cycles_executed", static_cast<double>(cycles_executed_));
        event_logger_->log_performance_metric("signals_generated", static_cast<double>(signals_generated_));
        event_logger_->log_performance_metric("avg_cycle_time_us", metrics.average_cycle_time_us);
        event_logger_->log_performance_metric("max_cycle_time_us", metrics.max_cycle_time_us);
        
        // Check shared memory health
        auto sm_status = shared_memory_->get_status();
        if (!shared_memory_->is_healthy()) {
            std::cerr << "Warning: Shared memory buffers approaching capacity" << std::endl;
            event_logger_->log_error("Shared memory buffer overflow warning");
        }
    }
};

int main(int argc, char* argv[]) {
    try {
        std::string config_file = "config/quantlib_process.yaml";
        
        if (argc > 1) {
            config_file = argv[1];
        }
        
        std::cout << "Starting Alaris QuantLib Process with config: " << config_file << std::endl;
        
        AlarisQuantLibProcess process(config_file);
        process.run();
        
        return 0;
    }
    catch (const std::exception& e) {
        std::cerr << "Fatal error: " << e.what() << std::endl;
        return 1;
    }
}