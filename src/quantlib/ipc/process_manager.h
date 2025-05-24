#pragma once

#include <string>
#include <vector>
#include <memory>
#include <functional>
#include "shared_memory.h"

namespace alaris {
namespace ipc {

/**
 * @brief Process management and IPC coordination
 * 
 * This class handles process lifecycle, IPC setup, and coordination
 * between different components of the trading system.
 */
class ProcessManager {
public:
    /**
     * @brief Construct a new Process Manager
     * 
     * @param config_path Path to the configuration file
     */
    explicit ProcessManager(const std::string& config_path);

    /**
     * @brief Initialize the process manager
     * 
     * @return true if initialization was successful
     */
    bool initialize();

    /**
     * @brief Start the managed process
     * 
     * @return true if process started successfully
     */
    bool start();

    /**
     * @brief Stop the managed process
     * 
     * @param force Whether to force stop the process
     * @return true if process stopped successfully
     */
    bool stop(bool force = false);

    /**
     * @brief Check if the process is running
     * 
     * @return true if process is running
     */
    bool isRunning() const;

    /**
     * @brief Get the process ID
     * 
     * @return int Process ID, or -1 if not running
     */
    int getPid() const;

    /**
     * @brief Set a callback for process state changes
     * 
     * @param callback Function to call on state change
     */
    void setStateChangeCallback(std::function<void(bool)> callback);

    /**
     * @brief Get the shared memory segment
     * 
     * @return SharedMemory& Reference to the shared memory
     */
    SharedMemory& getSharedMemory();

private:
    class Impl;
    std::unique_ptr<Impl> pimpl_;  // PIMPL idiom for ABI stability
};

} // namespace ipc
} // namespace alaris 