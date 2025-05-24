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
        validateOptional(process, "priority", "int", 1, 99);
        validateOptional(process, "cpu_affinity", "sequence");
        validateOptional(process, "memory_lock", "bool");
        validateOptional(process, "huge_pages", "bool");
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
    
    void validatePricingSection(const YAML::Node& pricing) {
        if (!pricing) {
            warnings_.push_back("Missing 'pricing' section - using defaults");
            return;
        }
        
        auto alo_engine = pricing["alo_engine"];
        if (alo_engine) {
            validateOptional(alo_engine, "scheme", "string", 
                           {"ModifiedCraigSneyd", "TrBDF2", "CrankNicolson"});
            validateOptional(alo_engine, "time_steps", "int", 100, 2000);
            validateOptional(alo_engine, "asset_steps", "int", 100, 2000);
        }
    }
    
    void validateVolatilitySection(const YAML::Node& volatility) {
        if (!volatility) {
            warnings_.push_back("Missing 'volatility' section - using defaults");
            return;
        }
        
        auto gjr_garch = volatility["gjr_garch"];
        if (gjr_garch) {
            validateOptional(gjr_garch, "max_iterations", "int", 100, 10000);
            validateOptional(gjr_garch, "tolerance", "double", 1e-8, 1e-3);
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
            validateOptional(vol_arbitrage, "entry_threshold", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "exit_threshold", "double", 0.001, 1.0);
            validateOptional(vol_arbitrage, "risk_limit", "double", 0.01, 1.0);
            validateOptional(vol_arbitrage, "model_selection", "string", 
                           {"gjr_garch", "standard_garch", "ensemble"});
        }
    }
    
    void validateLoggingSection(const YAML::Node& logging) {
        if (!logging) {
            warnings_.push_back("Missing 'logging' section - using defaults");
            return;
        }
        
        validateOptional(logging, "level", "string", {"DEBUG", "INFO", "WARN", "ERROR"});
        validateRequired(logging, "file", "string");
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
                    errors_.push_back("Invalid value for " + key + ": " + value);
                }
            }
        }
    }
    
    template<typename T>
    void validateOptional(const YAML::Node& node, const std::string& key, const std::string& type,
                         T min_val, T max_val) {
        if (node[key]) {
            validateType(node[key], key, type);
            
            T value = node[key].as<T>();
            if (value < min_val || value > max_val) {
                errors_.push_back("Value for " + key + " out of range: " + std::to_string(value));
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
        } catch (const std::exception& e) {
            errors_.push_back("Type validation error for " + key + ": " + e.what());
        }
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
        success = validator.validateLeanConfig(config_file);
    } else {
        std::cerr << "Error: Unknown config type: " << config_type << std::endl;
        std::cerr << "Supported types: quantlib, lean" << std::endl;
        return 1;
    }
    
    validator.printResults();
    
    return success ? 0 : 1;
}