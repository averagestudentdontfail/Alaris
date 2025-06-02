#pragma once

#include <functional>
#include <vector>
#include <chrono>
#include <string>
#include <unordered_map>
#include <atomic>

namespace Alaris::Core {

/**
 * @brief True Time-Triggered Architecture (TTA) Task Scheduler
 * 
 * Implements strict TTA principles:
 * - Fixed, predetermined schedule computed offline
 * - Deterministic execution timing
 * - No dynamic period adjustment
 * - Strict offline scheduling
 * - Guaranteed worst-case execution time (WCET)
 */
class TaskScheduler {
public:
    using TaskFunction = std::function<void()>;
    using Duration = std::chrono::nanoseconds;
    using TimePoint = std::chrono::high_resolution_clock::time_point;

    /**
     * @brief Task definition for TTA scheduling
     */
    struct TaskDefinition {
        std::string name;
        TaskFunction function;
        Duration period;                    // Task period (must be multiple of basic time unit)
        Duration worst_case_execution_time; // WCET for schedulability analysis
        Duration deadline;                  // Relative deadline (defaults to period)
        int priority;                       // Higher number = higher priority
        bool is_critical;                   // Critical tasks get priority in conflicts

        TaskDefinition(std::string task_name, TaskFunction func, Duration per, 
                      Duration wcet, Duration dead = Duration::zero(), 
                      int prio = 0, bool critical = false)
            : name(std::move(task_name)), function(std::move(func)), period(per),
              worst_case_execution_time(wcet), deadline(dead == Duration::zero() ? per : dead),
              priority(prio), is_critical(critical) {}
    };

    /**
     * @brief Represents a scheduled task execution in the timeline
     */
    struct ScheduledExecution {
        size_t task_id;                    // Index into task definitions
        Duration start_time;               // Start time within hyperperiod
        Duration end_time;                 // End time within hyperperiod
        uint64_t instance_number;          // Which instance of the task this is
    };

    /**
     * @brief Statistics for schedulability analysis
     */
    struct SchedulabilityReport {
        bool is_schedulable;
        Duration hyperperiod;
        Duration basic_time_unit;
        double cpu_utilization;
        size_t total_executions_per_hyperperiod;
        std::vector<std::string> conflicts;
        std::vector<std::string> warnings;
    };

private:
    // Configuration
    Duration basic_time_unit_;             // Smallest time unit (GCD of all periods)
    Duration hyperperiod_;                 // LCM of all task periods
    
    // Task definitions and schedule
    std::vector<TaskDefinition> task_definitions_;
    std::vector<ScheduledExecution> schedule_table_;
    std::unordered_map<std::string, size_t> task_name_to_id_;
    
    // Runtime state
    std::atomic<bool> is_running_{false};
    TimePoint schedule_start_time_;
    Duration current_hyperperiod_offset_;
    
    // Performance tracking
    struct TaskMetrics {
        uint64_t executions_completed;
        uint64_t deadline_misses;
        Duration total_execution_time;
        Duration max_execution_time;
        Duration last_execution_time;
    };
    std::vector<TaskMetrics> task_metrics_;
    
    // Schedule computation helpers
    Duration compute_gcd(Duration a, Duration b) const;
    Duration compute_lcm(Duration a, Duration b) const;
    Duration compute_hyperperiod() const;
    bool validate_task_definition(const TaskDefinition& task) const;
    bool check_schedulability() const;
    void generate_schedule_table();
    bool has_timing_conflict(const ScheduledExecution& exec1, const ScheduledExecution& exec2) const;
    
public:
    /**
     * @brief Construct TaskScheduler with basic time unit
     * @param basic_time_unit Fundamental time unit (typically 1ms or less)
     */
    explicit TaskScheduler(Duration basic_time_unit = std::chrono::microseconds(100));
    
    ~TaskScheduler() = default;
    
    // Non-copyable, non-movable for deterministic behavior
    TaskScheduler(const TaskScheduler&) = delete;
    TaskScheduler& operator=(const TaskScheduler&) = delete;
    TaskScheduler(TaskScheduler&&) = delete;
    TaskScheduler& operator=(TaskScheduler&&) = delete;
    
    /**
     * @brief Add a task to the schedule (must be called before finalize_schedule)
     */
    bool add_task(const TaskDefinition& task);
    
    /**
     * @brief Add a task with simplified parameters
     */
    bool add_task(const std::string& name, TaskFunction function, Duration period, 
                  Duration wcet, int priority = 0);
    
    /**
     * @brief Finalize the schedule - computes static schedule table
     * Must be called after all tasks are added and before start_execution
     */
    SchedulabilityReport finalize_schedule();
    
    /**
     * @brief Start executing the predetermined schedule
     */
    bool start_execution();
    
    /**
     * @brief Stop execution
     */
    void stop_execution();
    
    /**
     * @brief Execute one complete hyperperiod (for testing/validation)
     */
    void execute_one_hyperperiod();
    
    /**
     * @brief Check if scheduler is currently running
     */
    bool is_running() const { return is_running_.load(); }
    
    /**
     * @brief Get current schedule information
     */
    Duration get_hyperperiod() const { return hyperperiod_; }
    Duration get_basic_time_unit() const { return basic_time_unit_; }
    size_t get_task_count() const { return task_definitions_.size(); }
    
    /**
     * @brief Get task metrics for performance monitoring
     */
    const TaskMetrics& get_task_metrics(size_t task_id) const;
    const TaskMetrics& get_task_metrics(const std::string& task_name) const;
    
    /**
     * @brief Get the complete schedule table (for debugging/visualization)
     */
    const std::vector<ScheduledExecution>& get_schedule_table() const { return schedule_table_; }
    
    /**
     * @brief Print schedule table for debugging
     */
    void print_schedule_table() const;
    
    /**
     * @brief Validate that a set of tasks can be scheduled
     * Static method for pre-validation without creating scheduler
     */
    static SchedulabilityReport validate_task_set(const std::vector<TaskDefinition>& tasks, 
                                                   Duration basic_time_unit);
};

/**
 * @brief Helper class for building task sets with validation
 */
class TaskSetBuilder {
private:
    std::vector<TaskScheduler::TaskDefinition> tasks_;
    TaskScheduler::Duration basic_time_unit_;
    
public:
    explicit TaskSetBuilder(TaskScheduler::Duration basic_time_unit = std::chrono::microseconds(100))
        : basic_time_unit_(basic_time_unit) {}
    
    TaskSetBuilder& add_periodic_task(const std::string& name, TaskScheduler::TaskFunction function,
                                     TaskScheduler::Duration period, TaskScheduler::Duration wcet,
                                     int priority = 0);
    
    TaskSetBuilder& add_critical_task(const std::string& name, TaskScheduler::TaskFunction function,
                                     TaskScheduler::Duration period, TaskScheduler::Duration wcet,
                                     int priority = 100);
    
    TaskScheduler::SchedulabilityReport validate() const;
    
    bool build_scheduler(TaskScheduler& scheduler) const;
    
    const std::vector<TaskScheduler::TaskDefinition>& get_tasks() const { return tasks_; }
};

} // namespace Alaris::Core