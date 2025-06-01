// src/quantlib/tools/config_validator.cpp
// Configuration validation utility for Alaris

#include <iostream>
#include <string>
#include <vector>
#include <filesystem>
#include <yaml-cpp/yaml.h>

#ifdef ALARIS_BUILD_INFO_AVAILABLE
#include "alaris_build_info.h"
#endif

namespace fs = std::filesystem;

class ConfigValidator {
private:
    std::vector<std::string> errors_;
    std::vector<std::string> warnings_;
    
public:
    bool validateQuantLibConfig(const std::string& filepath) {
        if (!fs::exists(filepath)) {
            errors_.push_back("Configuration file does not exist: " + filepath);
            return false;
        }
        
        try {
            YAML::Node config = YAML::LoadFile(filepath);
            
            // Validate required sections
            validateProcessSection(config["process"]);
            validateQuantLibSection(config["quantlib"]);
            validateSharedMemorySection(config["shared_memory"]);
            validateMemorySection(config["memory"]);
            validateExecutorSection(config["executor"]);
            validatePricingSection(config["pricing"]);
            validateVolatilitySection(config["volatility"]);
            validateStrategySection(config["strategy"]);
            validateLoggingSection(config["logging"]);
            
        } catch (const YAML::Exception& e) {
            errors_.push_back("YAML parsing error: " + std::string(e.what()));
            return false;
        } catch (const std::exception& e) {
            errors_.push_back("Validation error: " + std::string(e.what()));
            return false;
        }
        
        return errors_.empty();
    }
    
    bool validateLeanConfig(const std::string& filepath) {
        if (!fs::exists(filepath)) {
            errors_.push_back("Configuration file does not exist: " + filepath);
            return false;
        }
        
        try {
            YAML::Node config = YAML::LoadFile(filepath);
            
            // Validate Lean-specific sections
            validateAlgorithmSection(config["algorithm"]);
            validateBrokerageSection(config["brokerage"]);
            validateDataSection(config["data"]);
            validateRiskManagementSection(config["risk_management"]);
            validateUniverseSection(config["universe"]);
            validateIBSettingsSection(config["ib_settings"]);
            
        } catch (const YAML::Exception& e) {
            errors_.push_back("YAML parsing error: " + std::string(e.what()));
            return false;
        } catch (const std::exception& e) {
            errors_.push_back("Validation error: " + std::string(e.what()));
            return false;
        }
        
        return errors_.empty();
    }
    
    void printResults() const {
        if (!errors_.empty()) {
            std::cout << "❌ Validation Errors:" << std::endl;
            for (const auto& error : errors_) {
                std::cout << "   • " << error << std::endl;
            }
            std::cout << std::endl;
        }
        
        if (!warnings_.empty()) {
            std::cout << "⚠️  Warnings:" << std::endl;
            for (const auto& warning : warnings_) {
                std::cout << "   • " << warning << std::endl;
            }
            std::cout << std::endl;
        }
        
        if (errors_.empty()) {
            std::cout << "✅ Configuration validation passed!" << std::endl;
        }
    }
    
    bool hasErrors() const { return !errors_.empty(); }
    
private:
    void validateProcessSection(const YAML::Node& process) {
        if (!process) {
            errors_.push_back("Missing 'process' section");
            return;
        }
        
        validateRequired(process, "name", "string");
        validateOptional(process, "priority", "int", 0, 99);
        validateOptional(process, "cpu_affinity", "sequence");
        validateOptional(process, "memory_lock", "bool");
        validateOptional(process, "huge_pages", "bool");
        validateOptional(process, "start_trading_enabled", "bool");
    }
    
    void validateQuantLibSection(const YAML::Node& quantlib) {
        if (!quantlib) {
            errors_.push_back("Missing 'quantlib' section");
            return;
        }
        
        validateOptional(quantlib, "threading", "string", {"single", "multi"});
        validateOptional(quantlib, "date_format", "string", {"ISO", "US", "European"});
        validateOptional(quantlib, "calendar", "string");
        validateOptional(quantlib, "enable_debug", "bool");
    }
    
    void validateSharedMemorySection(const YAML::Node& shared_memory) {
        if (!shared_memory) {
            errors_.push_back("Missing 'shared_memory' section");
            return;
        }
        
        validateRequired(shared_memory, "market_data_buffer", "string");
        validateRequired(shared_memory, "signal_buffer", "string");
        validateRequired(shared_memory, "control_buffer", "string");
        
        auto buffer_sizes = shared_memory["buffer_sizes"];
        if (buffer_sizes) {
            validateOptional(buffer_sizes, "market_data", "int", 1024, 65536);
            validateOptional(buffer_sizes, "signals", "int", 256, 16384);
            validateOptional(buffer_sizes, "control", "int", 64, 4096);
        }
    }
    
    void validateMemorySection(const YAML::Node& memory) {
        if (!memory) {
            warnings_.push_back("Missing 'memory' section - using defaults");
            return;
        }
        
        validateOptional(memory, "pool_size_mb", "int", 16, 1024);
    }
    
    void validateExecutorSection(const YAML::Node& executor) {
        if (!executor) {
            warnings_.push_back("Missing 'executor' section - using defaults");
            return;
        }
        
        validateOptional(executor, "major_frame_ms", "int", 1, 1000);
        validateOptional(executor, "market_data_interval_ms", "int", 1, 1000);
        validateOptional(executor, "signal_interval_ms", "int", 1, 1000);
        validateOptional(executor, "control_interval_ms", "int", 1, 1000);
        validateOptional(executor, "heartbeat_interval_s", "int", 1, 3600);
        validateOptional(executor, "perf_report_interval_s", "int", 1, 3600);
    }
    
    void validatePricingSection(const YAML::Node& pricing) {
        if (!pricing) {
            warnings_.push_back("Missing 'pricing' section - using defaults");
            return;
        }
        
        auto alo_engine = pricing["alo_engine"];
        if (alo_engine) {
            // ALO engine uses iteration schemes, not time-stepping schemes
            validateOptional(alo_engine, "scheme", "string", 
                           {"fast", "accurate", "high_precision"});
            validateOptional(alo_engine, "fixed_point_equation", "string",
                           {"Auto", "FP_A", "FP_B"});
            
            // Warn if old time-stepping parameters are found
            if (alo_engine["time_steps"]) {
                warnings_.push_back("'time_steps' parameter is deprecated for ALO engine - use 'scheme' instead");
            }
            if (alo_engine["asset_steps"]) {
                warnings_.push_back("'asset_steps' parameter is deprecated for ALO engine - use 'scheme' instead");
            }
        }
    }
    
    void validateVolatilitySection(const YAML::Node& volatility) {
        if (!volatility) {
            warnings_.push_back("Missing 'volatility' section - using defaults");
            return;
        }
        
        // Check for deprecated GJR-GARCH configuration
        if (volatility["gjr_garch"]) {
            errors_.push_back("'gjr_garch' section is deprecated - use 'garch' for standard GARCH model");
            return;
        }
        
        // Validate standard GARCH configuration
        auto garch = volatility["garch"];
        if (garch) {
            validateOptional(garch, "max_iterations", "int", 100, 10000);
            validateOptional(garch, "tolerance", "double", 1e-8, 1e-3);
            validateOptional(garch, "mode", "string", 
                           {"MomentMatchingGuess", "GammaGuess", "BestOfTwo", "DoubleOptimization"});
            validateOptional(garch, "max_history_length", "int", 100, 10000);
        }
        
        validateOptional(volatility, "update_frequency_ms", "int", 10, 10000);
    }
    
    void validateStrategySection(const YAML::Node& strategy) {
        if (!strategy) {
            warnings_.push_back("Missing 'strategy' section - using defaults");
            return;
        }
        
        auto vol_arbitrage = strategy["vol_arbitrage"];
        if (vol_arbitrage) {
            // Core thresholds
            validateOptional(vol_arbitrage, "entry_threshold", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "exit_threshold", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "confidence_threshold", "double", 0.1, 1.0);
            
            // Risk management
            validateOptional(vol_arbitrage, "max_portfolio_delta", "double", 0.01, 1.0);
            validateOptional(vol_arbitrage, "max_portfolio_gamma", "double", 0.01, 1.0);
            validateOptional(vol_arbitrage, "max_portfolio_vega", "double", 0.1, 10.0);
            validateOptional(vol_arbitrage, "max_position_size", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "max_correlation_exposure", "double", 0.1, 1.0);
            
            // Position sizing
            validateOptional(vol_arbitrage, "kelly_fraction", "double", 0.001, 0.25);
            validateOptional(vol_arbitrage, "max_kelly_position", "double", 0.001, 0.5);
            validateOptional(vol_arbitrage, "min_edge_ratio", "double", 1.0, 10.0);
            
            // Stop loss and profit taking
            validateOptional(vol_arbitrage, "stop_loss_percent", "double", 0.01, 1.0);
            validateOptional(vol_arbitrage, "profit_target_percent", "double", 0.01, 2.0);
            validateOptional(vol_arbitrage, "trailing_stop_percent", "double", 0.01, 1.0);
            
            // Strategy mode
            validateOptional(vol_arbitrage, "strategy_mode", "string", 
                           {"DELTA_NEUTRAL", "GAMMA_SCALPING", "VOLATILITY_TIMING", "RELATIVE_VALUE"});
            
            // Model selection - updated for standard GARCH
            validateOptional(vol_arbitrage, "model_selection", "string", 
                           {"GARCH_DIRECT", "ENSEMBLE_GARCH_HISTORICAL"});
            
            // Check for deprecated GJR-GARCH model selection
            if (vol_arbitrage["model_selection"]) {
                std::string model = vol_arbitrage["model_selection"].as<std::string>();
                if (model.find("GJR") != std::string::npos) {
                    errors_.push_back("GJR-GARCH model selection is deprecated - use GARCH_DIRECT or ENSEMBLE_GARCH_HISTORICAL");
                }
            }
            
            // Hedging
            validateOptional(vol_arbitrage, "hedge_threshold_delta", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "hedge_threshold_gamma", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "auto_hedge_enabled", "bool");
            validateOptional(vol_arbitrage, "hedge_frequency_minutes", "double", 1.0, 1440.0);
            
            // Market regime detection
            validateOptional(vol_arbitrage, "low_vol_threshold", "double", 0.01, 1.0);
            validateOptional(vol_arbitrage, "high_vol_threshold", "double", 0.01, 2.0);
            validateOptional(vol_arbitrage, "regime_lookback_days", "int", 5, 252);
        }
    }
    
    void validateLoggingSection(const YAML::Node& logging) {
        if (!logging) {
            warnings_.push_back("Missing 'logging' section - using defaults");
            return;
        }
        
        validateOptional(logging, "level", "string", {"DEBUG", "INFO", "WARN", "ERROR"});
        validateRequired(logging, "file", "string");
        validateOptional(logging, "binary_mode", "bool");
        validateOptional(logging, "enable_performance_log", "bool");
    }
    
    void validateAlgorithmSection(const YAML::Node& algorithm) {
        if (!algorithm) {
            errors_.push_back("Missing 'algorithm' section");
            return;
        }
        
        validateRequired(algorithm, "name", "string");
        validateRequired(algorithm, "start_date", "string");
        validateRequired(algorithm, "end_date", "string");
        validateRequired(algorithm, "cash", "int");
    }
    
    void validateBrokerageSection(const YAML::Node& brokerage) {
        if (!brokerage) {
            errors_.push_back("Missing 'brokerage' section");
            return;
        }
        
        validateRequired(brokerage, "type", "string");
        validateRequired(brokerage, "gateway_host", "string");
        validateRequired(brokerage, "gateway_port", "int");
        validateRequired(brokerage, "account", "string");
        
        // Validate IB-specific port numbers and provide guidance
        if (brokerage["gateway_port"]) {
            int port = brokerage["gateway_port"].as<int>();
            if (port != 4001 && port != 4002) {
                errors_.push_back("IB Gateway port must be 4001 (live trading) or 4002 (paper trading)");
            } else if (port == 4001) {
                warnings_.push_back("⚠️  LIVE TRADING PORT (4001) DETECTED - Ensure this is intended for production use!");
            } else if (port == 4002) {
                warnings_.push_back("Paper trading port (4002) configured - Safe for development and testing");
            }
        }
        
        // Validate account format
        if (brokerage["account"]) {
            std::string account = brokerage["account"].as<std::string>();
            if (account.starts_with("DU") && brokerage["gateway_port"] && brokerage["gateway_port"].as<int>() == 4001) {
                warnings_.push_back("Paper trading account (DU prefix) with live trading port (4001) - Check configuration");
            }
            if (account.starts_with("U") && brokerage["gateway_port"] && brokerage["gateway_port"].as<int>() == 4002) {
                warnings_.push_back("Live trading account (U prefix) with paper trading port (4002) - Check configuration");
            }
        }
    }
    
    void validateDataSection(const YAML::Node& data) {
        if (!data) {
            errors_.push_back("Missing 'data' section");
            return;
        }
        
        validateRequired(data, "provider", "string");
        validateOptional(data, "resolution", "string", {"Tick", "Second", "Minute", "Hour", "Daily"});
    }
    
    void validateRiskManagementSection(const YAML::Node& risk_management) {
        if (!risk_management) {
            warnings_.push_back("Missing 'risk_management' section - using defaults");
            return;
        }
        
        validateOptional(risk_management, "max_position_size", "double", 0.001, 1.0);
        validateOptional(risk_management, "max_daily_loss", "double", 0.001, 1.0);
    }
    
    void validateUniverseSection(const YAML::Node& universe) {
        if (!universe) {
            errors_.push_back("Missing 'universe' section");
            return;
        }
        
        validateRequired(universe, "symbols", "sequence");
        validateOptional(universe, "option_chains", "bool");
    }
    
    void validateIBSettingsSection(const YAML::Node& ib_settings) {
        if (!ib_settings) {
            return; // Optional section
        }
        
        validateOptional(ib_settings, "connection_timeout", "int", 5, 300);
        validateOptional(ib_settings, "enable_market_data", "bool");
        validateOptional(ib_settings, "order_timeout_seconds", "int", 10, 3600);
        
        auto paper_trading = ib_settings["paper_trading"];
        if (paper_trading) {
            validateOptional(paper_trading, "enabled", "bool");
            validateOptional(paper_trading, "starting_cash", "int", 10000, 10000000);
        }
        
        auto live_trading = ib_settings["live_trading"];
        if (live_trading) {
            validateOptional(live_trading, "enabled", "bool");
            if (live_trading["enabled"] && live_trading["enabled"].as<bool>()) {
                warnings_.push_back("⚠️  Live trading is ENABLED in configuration - Ensure this is intended!");
            }
        }
    }
    
    void validateRequired(const YAML::Node& node, const std::string& key, const std::string& type) {
        if (!node[key]) {
            errors_.push_back("Missing required field: " + key);
            return;
        }
        
        validateType(node[key], key, type);
    }
    
    void validateOptional(const YAML::Node& node, const std::string& key, const std::string& type) {
        if (node[key]) {
            validateType(node[key], key, type);
        }
    }
    
    void validateOptional(const YAML::Node& node, const std::string& key, const std::string& type,
                         const std::vector<std::string>& allowed_values) {
        if (node[key]) {
            validateType(node[key], key, type);
            
            if (type == "string") {
                std::string value = node[key].as<std::string>();
                bool found = false;
                for (const auto& allowed : allowed_values) {
                    if (value == allowed) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    errors_.push_back("Invalid value for " + key + ": " + value + " (allowed: " + 
                                    joinVector(allowed_values, ", ") + ")");
                }
            }
        }
    }
    
    template<typename T>
    void validateOptional(const YAML::Node& node, const std::string& key, const std::string& type,
                         T min_val, T max_val) {
        if (node[key]) {
            validateType(node[key], key, type);
            
            try {
                T value = node[key].as<T>();
                if (value < min_val || value > max_val) {
                    errors_.push_back("Value for " + key + " out of range: " + std::to_string(value) + 
                                    " (allowed: " + std::to_string(min_val) + "-" + std::to_string(max_val) + ")");
                }
            } catch (const std::exception& e) {
                errors_.push_back("Invalid value type for " + key + ": " + e.what());
            }
        }
    }
    
    void validateType(const YAML::Node& node, const std::string& key, const std::string& expected_type) {
        try {
            if (expected_type == "string" && !node.IsScalar()) {
                errors_.push_back("Field " + key + " must be a string");
            } else if (expected_type == "int" && !node.IsScalar()) {
                errors_.push_back("Field " + key + " must be an integer");
            } else if (expected_type == "double" && !node.IsScalar()) {
                errors_.push_back("Field " + key + " must be a number");
            } else if (expected_type == "bool" && !node.IsScalar()) {
                errors_.push_back("Field " + key + " must be a boolean");
            } else if (expected_type == "sequence" && !node.IsSequence()) {
                errors_.push_back("Field " + key + " must be a list");
            }
            
            // Additional type checking for scalars
            if (node.IsScalar()) {
                if (expected_type == "int") {
                    node.as<int>(); // Will throw if not convertible
                } else if (expected_type == "double") {
                    node.as<double>(); // Will throw if not convertible
                } else if (expected_type == "bool") {
                    node.as<bool>(); // Will throw if not convertible
                }
            }
        } catch (const std::exception& e) {
            errors_.push_back("Type validation error for " + key + ": " + e.what());
        }
    }
    
    std::string joinVector(const std::vector<std::string>& vec, const std::string& delimiter) {
        std::string result;
        for (size_t i = 0; i < vec.size(); ++i) {
            if (i > 0) result += delimiter;
            result += vec[i];
        }
        return result;
    }
};

void printUsage(const char* program_name) {
    std::cout << "Alaris Configuration Validator" << std::endl;
    std::cout << "Usage: " << program_name << " [OPTIONS] <config_file>" << std::endl;
    std::cout << std::endl;
    std::cout << "Options:" << std::endl;
    std::cout << "  -h, --help              Show this help message" << std::endl;
    std::cout << "  -v, --version           Show version information" << std::endl;
    std::cout << "  -t, --type <type>       Specify config type (quantlib|lean)" << std::endl;
    std::cout << "  --verbose               Enable verbose output" << std::endl;
    std::cout << std::endl;
    std::cout << "Examples:" << std::endl;
    std::cout << "  " << program_name << " config/quantlib_process.yaml" << std::endl;
    std::cout << "  " << program_name << " -t lean config/lean_process.yaml" << std::endl;
    std::cout << std::endl;
    std::cout << "Notes:" << std::endl;
    std::cout << "  • Port 4001 = Live Trading (⚠️  Use with caution!)" << std::endl;
    std::cout << "  • Port 4002 = Paper Trading (Safe for development)" << std::endl;
    std::cout << "  • Standard GARCH model is now used (GJR-GARCH deprecated)" << std::endl;
    std::cout << "  • ALO engine uses iteration schemes (fast/accurate/high_precision)" << std::endl;
}

void printVersion() {
#ifdef ALARIS_BUILD_INFO_AVAILABLE
    std::cout << Alaris::BuildInfo::getBuildInfoString() << std::endl;
#else
    std::cout << "Alaris Configuration Validator" << std::endl;
    std::cout << "Version: Unknown (build info not available)" << std::endl;
#endif
}

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printUsage(argv[0]);
        return 1;
    }
    
    std::string config_file;
    std::string config_type = "auto";
    bool verbose = false;
    
    // Parse command line arguments
    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        
        if (arg == "-h" || arg == "--help") {
            printUsage(argv[0]);
            return 0;
        } else if (arg == "-v" || arg == "--version") {
            printVersion();
            return 0;
        } else if (arg == "-t" || arg == "--type") {
            if (i + 1 < argc) {
                config_type = argv[++i];
            } else {
                std::cerr << "Error: --type requires an argument" << std::endl;
                return 1;
            }
        } else if (arg == "--verbose") {
            verbose = true;
        } else if (arg[0] != '-') {
            if (config_file.empty()) {
                config_file = arg;
            } else {
                std::cerr << "Error: Multiple config files specified" << std::endl;
                return 1;
            }
        } else {
            std::cerr << "Error: Unknown option: " << arg << std::endl;
            return 1;
        }
    }
    
    if (config_file.empty()) {
        std::cerr << "Error: No configuration file specified" << std::endl;
        printUsage(argv[0]);
        return 1;
    }
    
    if (verbose) {
        std::cout << "Validating configuration file: " << config_file << std::endl;
        std::cout << "Configuration type: " << config_type << std::endl;
        std::cout << std::endl;
    }
    
    // Auto-detect config type if not specified
    if (config_type == "auto") {
        if (config_file.find("quantlib") != std::string::npos) {
            config_type = "quantlib";
        } else if (config_file.find("lean") != std::string::npos) {
            config_type = "lean";
        } else {
            config_type = "quantlib";  // Default
        }
        
        if (verbose) {
            std::cout << "Auto-detected config type: " << config_type << std::endl;
        }
    }
    
    ConfigValidator validator;
    bool success = false;
    
    if (config_type == "quantlib") {
        success = validator.validateQuantLibConfig(config_file);
    } else if (config_type == "lean") {
        success = validator.validateLeanCon