// src/quantlib/tools/system_info.cpp
// System information utility for Alaris

#include <iostream>
#include <string>
#include <vector>
#include <fstream>
#include <sstream>
#include <thread>
#include <chrono>
#include <iomanip>

#ifdef ALARIS_BUILD_INFO_AVAILABLE
#include "alaris_build_info.h"
#endif

// Platform-specific includes
#ifdef __linux__
#include <sys/utsname.h>
#include <sys/sysinfo.h>
#include <unistd.h>
#include <sys/resource.h>
#include <sched.h>
#endif

#ifdef __APPLE__
#include <sys/utsname.h>
#include <sys/sysctl.h>
#include <unistd.h>
#include <mach/mach.h>
#endif

#ifdef _WIN32
#include <windows.h>
#include <sysinfoapi.h>
#endif

class SystemInfo {
public:
    struct CPUInfo {
        std::string model;
        int cores;
        int logical_processors;
        std::vector<std::string> features;
        double frequency_mhz;
    };
    
    struct MemoryInfo {
        size_t total_bytes;
        size_t available_bytes;
        size_t free_bytes;
        bool has_huge_pages;
        size_t huge_page_size;
        size_t huge_pages_total;
        size_t huge_pages_free;
    };
    
    struct SystemCapabilities {
        bool has_real_time;
        bool has_shared_memory;
        bool has_cpu_affinity;
        bool has_memory_locking;
        bool has_high_resolution_timer;
        int max_rt_priority;
    };
    
    void printSystemInfo(bool verbose = false) {
        std::cout << "🖥️  Alaris System Information" << std::endl;
        std::cout << "================================" << std::endl;
        std::cout << std::endl;
        
        printBuildInfo();
        printOSInfo();
        printCPUInfo(verbose);
        printMemoryInfo(verbose);
        printCapabilities(verbose);
        printPerformanceInfo();
        
        if (verbose) {
            printDetailedInfo();
        }
    }
    
    bool runHealthCheck() {
        std::cout << "🔍 Running Alaris Health Check..." << std::endl;
        std::cout << std::endl;
        
        bool healthy = true;
        std::vector<std::string> issues;
        std::vector<std::string> warnings;
        
        // Check CPU cores
        auto cpu = getCPUInfo();
        if (cpu.cores < 2) {
            issues.push_back("Insufficient CPU cores (minimum 2 required)");
            healthy = false;
        } else if (cpu.cores < 4) {
            warnings.push_back("Low CPU core count (4+ recommended for optimal performance)");
        }
        
        // Check memory
        auto memory = getMemoryInfo();
        const size_t min_memory = 2ULL * 1024 * 1024 * 1024; // 2GB
        const size_t recommended_memory = 8ULL * 1024 * 1024 * 1024; // 8GB
        
        if (memory.total_bytes < min_memory) {
            issues.push_back("Insufficient memory (minimum 2GB required)");
            healthy = false;
        } else if (memory.total_bytes < recommended_memory) {
            warnings.push_back("Low memory (8GB+ recommended for optimal performance)");
        }
        
        // Check system capabilities
        auto caps = getSystemCapabilities();
        if (!caps.has_shared_memory) {
            issues.push_back("Shared memory not available");
            healthy = false;
        }
        
        if (!caps.has_real_time) {
            warnings.push_back("Real-time capabilities not available (may impact latency)");
        }
        
        // Check huge pages (Linux)
        if (!memory.has_huge_pages) {
            warnings.push_back("Huge pages not configured (may impact memory performance)");
        }
        
        // Print results
        if (!issues.empty()) {
            std::cout << "❌ Critical Issues:" << std::endl;
            for (const auto& issue : issues) {
                std::cout << "   • " << issue << std::endl;
            }
            std::cout << std::endl;
        }
        
        if (!warnings.empty()) {
            std::cout << "⚠️  Warnings:" << std::endl;
            for (const auto& warning : warnings) {
                std::cout << "   • " << warning << std::endl;
            }
            std::cout << std::endl;
        }
        
        if (healthy && warnings.empty()) {
            std::cout << "✅ System health check passed!" << std::endl;
        } else if (healthy) {
            std::cout << "✅ System is functional with warnings" << std::endl;
        } else {
            std::cout << "❌ System health check failed!" << std::endl;
        }
        
        return healthy;
    }
    
private:
    void printBuildInfo() {
#ifdef ALARIS_BUILD_INFO_AVAILABLE
        std::cout << "📦 Build Information:" << std::endl;
        std::cout << "   Version: " << Alaris::BuildInfo::getShortVersionString() << std::endl;
        std::cout << "   Build Type: " << Alaris::BuildInfo::BUILD_TYPE << std::endl;
        std::cout << "   Build Date: " << Alaris::BuildInfo::BUILD_DATE << std::endl;
        std::cout << "   Compiler: " << Alaris::BuildInfo::COMPILER_ID 
                  << " " << Alaris::BuildInfo::COMPILER_VERSION << std::endl;
        std::cout << std::endl;
#endif
    }
    
    void printOSInfo() {
        std::cout << "🐧 Operating System:" << std::endl;
        
#ifdef __linux__
        struct utsname info;
        if (uname(&info) == 0) {
            std::cout << "   System: " << info.sysname << std::endl;
            std::cout << "   Release: " << info.release << std::endl;
            std::cout << "   Version: " << info.version << std::endl;
            std::cout << "   Architecture: " << info.machine << std::endl;
        }
        
        // Try to get distribution info
        std::ifstream release("/etc/os-release");
        if (release.is_open()) {
            std::string line;
            while (std::getline(release, line)) {
                if (line.starts_with("PRETTY_NAME=")) {
                    std::string distro = line.substr(12);
                    if (distro.front() == '"' && distro.back() == '"') {
                        distro = distro.substr(1, distro.length() - 2);
                    }
                    std::cout << "   Distribution: " << distro << std::endl;
                    break;
                }
            }
        }
#elif __APPLE__
        struct utsname info;
        if (uname(&info) == 0) {
            std::cout << "   System: " << info.sysname << std::endl;
            std::cout << "   Release: " << info.release << std::endl;
            std::cout << "   Architecture: " << info.machine << std::endl;
        }
#elif _WIN32
        std::cout << "   System: Windows" << std::endl;
        
        OSVERSIONINFOEX osvi;
        ZeroMemory(&osvi, sizeof(OSVERSIONINFOEX));
        osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFOEX);
        
        if (GetVersionEx((OSVERSIONINFO*)&osvi)) {
            std::cout << "   Version: " << osvi.dwMajorVersion << "." << osvi.dwMinorVersion << std::endl;
        }
#endif
        std::cout << std::endl;
    }
    
    void printCPUInfo(bool verbose) {
        auto cpu = getCPUInfo();
        
        std::cout << "🔧 CPU Information:" << std::endl;
        std::cout << "   Model: " << cpu.model << std::endl;
        std::cout << "   Physical Cores: " << cpu.cores << std::endl;
        std::cout << "   Logical Processors: " << cpu.logical_processors << std::endl;
        
        if (cpu.frequency_mhz > 0) {
            std::cout << "   Base Frequency: " << std::fixed << std::setprecision(0) 
                      << cpu.frequency_mhz << " MHz" << std::endl;
        }
        
        if (verbose && !cpu.features.empty()) {
            std::cout << "   Features: ";
            for (size_t i = 0; i < cpu.features.size(); ++i) {
                if (i > 0) std::cout << ", ";
                std::cout << cpu.features[i];
            }
            std::cout << std::endl;
        }
        std::cout << std::endl;
    }
    
    void printMemoryInfo(bool verbose) {
        auto memory = getMemoryInfo();
        
        std::cout << "💾 Memory Information:" << std::endl;
        std::cout << "   Total: " << formatBytes(memory.total_bytes) << std::endl;
        std::cout << "   Available: " << formatBytes(memory.available_bytes) << std::endl;
        std::cout << "   Free: " << formatBytes(memory.free_bytes) << std::endl;
        
        if (memory.has_huge_pages) {
            std::cout << "   Huge Pages: " << memory.huge_pages_total 
                      << " total, " << memory.huge_pages_free << " free" << std::endl;
            std::cout << "   Huge Page Size: " << formatBytes(memory.huge_page_size) << std::endl;
        } else {
            std::cout << "   Huge Pages: Not configured" << std::endl;
        }
        std::cout << std::endl;
    }
    
    void printCapabilities(bool verbose) {
        auto caps = getSystemCapabilities();
        
        std::cout << "⚙️  System Capabilities:" << std::endl;
        std::cout << "   Real-time: " << (caps.has_real_time ? "✅ Available" : "❌ Not available") << std::endl;
        std::cout << "   Shared Memory: " << (caps.has_shared_memory ? "✅ Available" : "❌ Not available") << std::endl;
        std::cout << "   CPU Affinity: " << (caps.has_cpu_affinity ? "✅ Available" : "❌ Not available") << std::endl;
        std::cout << "   Memory Locking: " << (caps.has_memory_locking ? "✅ Available" : "❌ Not available") << std::endl;
        std::cout << "   High-Res Timer: " << (caps.has_high_resolution_timer ? "✅ Available" : "❌ Not available") << std::endl;
        
        if (caps.has_real_time && caps.max_rt_priority > 0) {
            std::cout << "   Max RT Priority: " << caps.max_rt_priority << std::endl;
        }
        std::cout << std::endl;
    }
    
    void printPerformanceInfo() {
        std::cout << "📊 Performance Information:" << std::endl;
        
        // Memory bandwidth test (simplified)
        auto start = std::chrono::high_resolution_clock::now();
        const size_t test_size = 1024 * 1024; // 1MB
        std::vector<char> buffer(test_size);
        
        for (int i = 0; i < 100; ++i) {
            std::fill(buffer.begin(), buffer.end(), i % 256);
        }
        
        auto end = std::chrono::high_resolution_clock::now();
        auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
        
        double bandwidth_mbps = (100.0 * test_size) / (duration.count() / 1000000.0) / (1024 * 1024);
        std::cout << "   Memory Bandwidth: " << std::fixed << std::setprecision(1) 
                  << bandwidth_mbps << " MB/s (estimated)" << std::endl;
        
        // Timer resolution test
        auto timer_start = std::chrono::high_resolution_clock::now();
        std::this_thread::sleep_for(std::chrono::microseconds(1));
        auto timer_end = std::chrono::high_resolution_clock::now();
        auto timer_res = std::chrono::duration_cast<std::chrono::nanoseconds>(timer_end - timer_start).count();
        
        std::cout << "   Timer Resolution: ~" << timer_res << " ns" << std::endl;
        std::cout << std::endl;
    }
    
    void printDetailedInfo() {
        std::cout << "🔍 Detailed System Information:" << std::endl;
        
        // Process limits
#ifdef __linux__
        struct rlimit limit;
        if (getrlimit(RLIMIT_RTPRIO, &limit) == 0) {
            std::cout << "   RT Priority Limit: " << limit.rlim_cur << "/" << limit.rlim_max << std::endl;
        }
        if (getrlimit(RLIMIT_MEMLOCK, &limit) == 0) {
            std::cout << "   Memory Lock Limit: " << formatBytes(limit.rlim_cur) << std::endl;
        }
#endif
        
        // CPU governor (Linux)
#ifdef __linux__
        std::ifstream governor("/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor");
        if (governor.is_open()) {
            std::string gov;
            std::getline(governor, gov);
            std::cout << "   CPU Governor: " << gov << std::endl;
        }
#endif
        
        std::cout << std::endl;
    }
    
    CPUInfo getCPUInfo() {
        CPUInfo cpu;
        cpu.cores = std::thread::hardware_concurrency();
        cpu.logical_processors = cpu.cores;
        cpu.frequency_mhz = 0.0;
        
#ifdef __linux__
        // Get CPU model from /proc/cpuinfo
        std::ifstream cpuinfo("/proc/cpuinfo");
        std::string line;
        while (std::getline(cpuinfo, line)) {
            if (line.starts_with("model name")) {
                size_t pos = line.find(':');
                if (pos != std::string::npos) {
                    cpu.model = line.substr(pos + 2);
                    break;
                }
            }
        }
        
        // Get CPU features
        cpuinfo.clear();
        cpuinfo.seekg(0);
        while (std::getline(cpuinfo, line)) {
            if (line.starts_with("flags")) {
                size_t pos = line.find(':');
                if (pos != std::string::npos) {
                    std::istringstream iss(line.substr(pos + 2));
                    std::string feature;
                    while (iss >> feature) {
                        if (feature == "avx2" || feature == "fma" || feature == "sse4_2") {
                            cpu.features.push_back(feature);
                        }
                    }
                    break;
                }
            }
        }
        
        // Get actual core count
        cpu.cores = sysconf(_SC_NPROCESSORS_ONLN);
#elif __APPLE__
        size_t size = sizeof(cpu.cores);
        sysctlbyname("hw.physicalcpu", &cpu.cores, &size, nullptr, 0);
        sysctlbyname("hw.logicalcpu", &cpu.logical_processors, &size, nullptr, 0);
        
        char brand[256];
        size = sizeof(brand);
        if (sysctlbyname("machdep.cpu.brand_string", brand, &size, nullptr, 0) == 0) {
            cpu.model = brand;
        }
#elif _WIN32
        SYSTEM_INFO sysinfo;
        GetSystemInfo(&sysinfo);
        cpu.logical_processors = sysinfo.dwNumberOfProcessors;
        cpu.cores = cpu.logical_processors; // Simplified
        cpu.model = "Windows CPU";
#endif
        
        if (cpu.model.empty()) {
            cpu.model = "Unknown";
        }
        
        return cpu;
    }
    
    MemoryInfo getMemoryInfo() {
        MemoryInfo memory = {};
        
#ifdef __linux__
        struct sysinfo info;
        if (sysinfo(&info) == 0) {
            memory.total_bytes = info.totalram * info.mem_unit;
            memory.free_bytes = info.freeram * info.mem_unit;
            memory.available_bytes = memory.free_bytes; // Simplified
        }
        
        // Check huge pages
        std::ifstream meminfo("/proc/meminfo");
        std::string line;
        while (std::getline(meminfo, line)) {
            if (line.starts_with("HugePages_Total:")) {
                std::istringstream iss(line);
                std::string label;
                iss >> label >> memory.huge_pages_total;
                memory.has_huge_pages = memory.huge_pages_total > 0;
            } else if (line.starts_with("HugePages_Free:")) {
                std::istringstream iss(line);
                std::string label;
                iss >> label >> memory.huge_pages_free;
            } else if (line.starts_with("Hugepagesize:")) {
                std::istringstream iss(line);
                std::string label;
                size_t size_kb;
                iss >> label >> size_kb;
                memory.huge_page_size = size_kb * 1024;
            }
        }
#elif __APPLE__
        int64_t memsize;
        size_t size = sizeof(memsize);
        if (sysctlbyname("hw.memsize", &memsize, &size, nullptr, 0) == 0) {
            memory.total_bytes = memsize;
        }
        
        mach_port_t host = mach_host_self();
        vm_size_t page_size;
        vm_statistics64_data_t vm_stat;
        mach_msg_type_number_t count = HOST_VM_INFO64_COUNT;
        
        if (host_page_size(host, &page_size) == KERN_SUCCESS &&
            host_statistics64(host, HOST_VM_INFO64, (host_info64_t)&vm_stat, &count) == KERN_SUCCESS) {
            memory.free_bytes = vm_stat.free_count * page_size;
            memory.available_bytes = memory.free_bytes;
        }
#elif _WIN32
        MEMORYSTATUSEX status;
        status.dwLength = sizeof(status);
        if (GlobalMemoryStatusEx(&status)) {
            memory.total_bytes = status.ullTotalPhys;
            memory.available_bytes = status.ullAvailPhys;
            memory.free_bytes = status.ullAvailPhys;
        }
#endif
        
        return memory;
    }
    
    SystemCapabilities getSystemCapabilities() {
        SystemCapabilities caps = {};
        
#ifdef __linux__
        caps.has_shared_memory = true; // Linux always has shared memory
        caps.has_cpu_affinity = true;
        caps.has_memory_locking = true;
        caps.has_high_resolution_timer = true;
        
        // Check real-time capabilities
        caps.max_rt_priority = sched_get_priority_max(SCHED_FIFO);
        caps.has_real_time = caps.max_rt_priority > 0;
#elif __APPLE__
        caps.has_shared_memory = true;
        caps.has_cpu_affinity = true;
        caps.has_memory_locking = true;
        caps.has_high_resolution_timer = true;
        caps.has_real_time = true;
        caps.max_rt_priority = 31;
#elif _WIN32
        caps.has_shared_memory = true;
        caps.has_cpu_affinity = true;
        caps.has_memory_locking = false; // Simplified
        caps.has_high_resolution_timer = true;
        caps.has_real_time = false; // Windows doesn't have POSIX RT
        caps.max_rt_priority = 0;
#endif
        
        return caps;
    }
    
    std::string formatBytes(size_t bytes) {
        const char* units[] = {"B", "KB", "MB", "GB", "TB"};
        int unit = 0;
        double size = static_cast<double>(bytes);
        
        while (size >= 1024.0 && unit < 4) {
            size /= 1024.0;
            unit++;
        }
        
        std::ostringstream oss;
        oss << std::fixed << std::setprecision(1) << size << " " << units[unit];
        return oss.str();
    }
};

void printUsage(const char* program_name) {
    std::cout << "Alaris System Information Utility" << std::endl;
    std::cout << "Usage: " << program_name << " [OPTIONS]" << std::endl;
    std::cout << std::endl;
    std::cout << "Options:" << std::endl;
    std::cout << "  -h, --help              Show this help message" << std::endl;
    std::cout << "  -v, --verbose           Show detailed information" << std::endl;
    std::cout << "  --health-check          Run system health check" << std::endl;
    std::cout << "  --version               Show version information" << std::endl;
    std::cout << std::endl;
}

int main(int argc, char* argv[]) {
    bool verbose = false;
    bool health_check = false;
    bool show_version = false;
    
    // Parse command line arguments
    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        
        if (arg == "-h" || arg == "--help") {
            printUsage(argv[0]);
            return 0;
        } else if (arg == "-v" || arg == "--verbose") {
            verbose = true;
        } else if (arg == "--health-check") {
            health_check = true;
        } else if (arg == "--version") {
            show_version = true;
        } else {
            std::cerr << "Unknown option: " << arg << std::endl;
            printUsage(argv[0]);
            return 1;
        }
    }
    
    SystemInfo sysinfo;
    
    if (show_version) {
#ifdef ALARIS_BUILD_INFO_AVAILABLE
        std::cout << Alaris::BuildInfo::getBuildInfoString() << std::endl;
#else
        std::cout << "Alaris System Information Utility" << std::endl;
        std::cout << "Version: Unknown" << std::endl;
#endif
        return 0;
    }
    
    if (health_check) {
        return sysinfo.runHealthCheck() ? 0 : 1;
    } else {
        sysinfo.printSystemInfo(verbose);
        return 0;
    }
}