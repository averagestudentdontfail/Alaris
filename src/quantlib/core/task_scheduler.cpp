#include "task_scheduler.h"
#include "time_type.h"
#include <algorithm>
#include <numeric>
#include <iostream>
#include <iomanip>
#include <thread>
#include <cassert>
#include <cmath>

namespace Alaris::Core {

TaskScheduler::TaskScheduler(Duration basic_time_unit)
    : basic_time_unit_(basic_time_unit),
      hyperperiod_(Duration::zero()),
      schedule_start_time_(TimePoint::min()),
      current_hyperperiod_offset_(Duration::zero()) {
    
    if (basic_time_unit <= Duration::zero()) {
        basic_time_unit_ = std::chrono::microseconds(100); // Default 100μs
    }
}

TaskScheduler::Duration TaskScheduler::compute_gcd(Duration a, Duration b) const {
    auto gcd_value = std::gcd(a.count(), b.count());
    return Duration(gcd_value);
}

TaskScheduler::Duration TaskScheduler::compute_lcm(Duration a, Duration b) const {
    auto lcm_value = std::lcm(a.count(), b.count());
    return Duration(lcm_value);
}

TaskScheduler::Duration TaskScheduler::compute_hyperperiod() const {
    if (task_definitions_.empty()) {
        return basic_time_unit_;
    }
    
    Duration hp = task_definitions_[0].period;
    for (size_t i = 1; i < task_definitions_.size(); ++i) {
        hp = compute_lcm(hp, task_definitions_[i].period);
        
        // Prevent excessive hyperperiods
        if (hp > std::chrono::seconds(60)) {
            std::cerr << "TaskScheduler: Warning - Hyperperiod exceeds 60 seconds: " 
                      << std::chrono::duration_cast<std::chrono::milliseconds>(hp).count() 
                      << "ms" << std::endl;
        }
    }
    
    return hp;
}

bool TaskScheduler::validate_task_definition(const TaskDefinition& task) const {
    // Check that period is multiple of basic time unit
    if (task.period.count() % basic_time_unit_.count() != 0) {
        std::cerr << "TaskScheduler: Task '" << task.name 
                  << "' period must be multiple of basic time unit" << std::endl;
        return false;
    }
    
    // Check that WCET is reasonable compared to period
    if (task.worst_case_execution_time > task.period) {
        std::cerr << "TaskScheduler: Task '" << task.name 
                  << "' WCET exceeds period" << std::endl;
        return false;
    }
    
    // Check that deadline is reasonable
    if (task.deadline > task.period) {
        std::cerr << "TaskScheduler: Task '" << task.name 
                  << "' deadline exceeds period" << std::endl;
        return false;
    }
    
    // Check for zero period
    if (task.period <= Duration::zero()) {
        std::cerr << "TaskScheduler: Task '" << task.name 
                  << "' must have positive period" << std::endl;
        return false;
    }
    
    return true;
}

bool TaskScheduler::check_schedulability() const {
    // Liu & Layland utilization bound test for Rate Monotonic
    double total_utilization = 0.0;
    
    for (const auto& task : task_definitions_) {
        double utilization = static_cast<double>(task.worst_case_execution_time.count()) / 
                           static_cast<double>(task.period.count());
        total_utilization += utilization;
    }
    
    // Liu & Layland bound: U ≤ n(2^(1/n) - 1)
    size_t n = task_definitions_.size();
    double liu_layland_bound = static_cast<double>(n) * (std::pow(2.0, 1.0 / static_cast<double>(n)) - 1.0);
    
    if (total_utilization > 1.0) {
        std::cerr << "TaskScheduler: Total utilization " << total_utilization 
                  << " exceeds 100%" << std::endl;
        return false;
    }
    
    if (total_utilization > liu_layland_bound) {
        std::cerr << "TaskScheduler: Warning - Utilization " << total_utilization 
                  << " exceeds Liu & Layland bound " << liu_layland_bound << std::endl;
        // Continue anyway - exact schedulability analysis would be needed
    }
    
    return true;
}

void TaskScheduler::generate_schedule_table() {
    schedule_table_.clear();
    
    if (task_definitions_.empty()) {
        return;
    }
    
    // Generate all task instances within the hyperperiod
    std::vector<ScheduledExecution> all_executions;
    
    for (size_t task_id = 0; task_id < task_definitions_.size(); ++task_id) {
        const auto& task = task_definitions_[task_id];
        
        // Calculate how many times this task executes in the hyperperiod
        uint64_t executions_per_hyperperiod = hyperperiod_.count() / task.period.count();
        
        for (uint64_t instance = 0; instance < executions_per_hyperperiod; ++instance) {
            ScheduledExecution exec;
            exec.task_id = task_id;
            exec.start_time = Duration(instance * task.period.count());
            exec.end_time = exec.start_time + task.worst_case_execution_time;
            exec.instance_number = instance;
            
            all_executions.push_back(exec);
        }
    }
    
    // Sort by start time, then by priority (higher priority first)
    std::sort(all_executions.begin(), all_executions.end(), 
              [this](const ScheduledExecution& a, const ScheduledExecution& b) {
                  if (a.start_time == b.start_time) {
                      return task_definitions_[a.task_id].priority > task_definitions_[b.task_id].priority;
                  }
                  return a.start_time < b.start_time;
              });
    
    // Check for conflicts and resolve using priority
    for (size_t i = 0; i < all_executions.size(); ++i) {
        bool has_conflict = false;
        
        // Check against already scheduled executions
        for (const auto& scheduled : schedule_table_) {
            if (has_timing_conflict(all_executions[i], scheduled)) {
                has_conflict = true;
                
                // If current task has higher priority, it can preempt
                if (task_definitions_[all_executions[i].task_id].priority > 
                    task_definitions_[scheduled.task_id].priority) {
                    std::cerr << "TaskScheduler: Task '" << task_definitions_[all_executions[i].task_id].name
                              << "' would preempt '" << task_definitions_[scheduled.task_id].name
                              << "' - preemption not supported in TTA" << std::endl;
                }
                break;
            }
        }
        
        if (!has_conflict) {
            schedule_table_.push_back(all_executions[i]);
        } else {
            // Try to reschedule at next available time slot
            Duration next_slot = all_executions[i].start_time;
            bool found_slot = false;
            
            while (next_slot < hyperperiod_ && !found_slot) {
                next_slot += basic_time_unit_;
                
                ScheduledExecution test_exec = all_executions[i];
                test_exec.start_time = next_slot;
                test_exec.end_time = next_slot + task_definitions_[test_exec.task_id].worst_case_execution_time;
                
                // Check if this violates the deadline
                Duration release_time = Duration(all_executions[i].instance_number * 
                                               task_definitions_[test_exec.task_id].period.count());
                if (test_exec.end_time > release_time + task_definitions_[test_exec.task_id].deadline) {
                    std::cerr << "TaskScheduler: Cannot reschedule task '" 
                              << task_definitions_[test_exec.task_id].name 
                              << "' without missing deadline" << std::endl;
                    break;
                }
                
                // Check for conflicts at new time
                bool new_conflict = false;
                for (const auto& scheduled : schedule_table_) {
                    if (has_timing_conflict(test_exec, scheduled)) {
                        new_conflict = true;
                        break;
                    }
                }
                
                if (!new_conflict) {
                    schedule_table_.push_back(test_exec);
                    found_slot = true;
                }
            }
            
            if (!found_slot) {
                std::cerr << "TaskScheduler: Failed to schedule task '" 
                          << task_definitions_[all_executions[i].task_id].name 
                          << "' instance " << all_executions[i].instance_number << std::endl;
            }
        }
    }
    
    // Sort final schedule by start time
    std::sort(schedule_table_.begin(), schedule_table_.end(),
              [](const ScheduledExecution& a, const ScheduledExecution& b) {
                  return a.start_time < b.start_time;
              });
}

bool TaskScheduler::has_timing_conflict(const ScheduledExecution& exec1, 
                                       const ScheduledExecution& exec2) const {
    return !(exec1.end_time <= exec2.start_time || exec2.end_time <= exec1.start_time);
}

bool TaskScheduler::add_task(const TaskDefinition& task) {
    if (is_running_.load()) {
        std::cerr << "TaskScheduler: Cannot add tasks while running" << std::endl;
        return false;
    }
    
    if (task_name_to_id_.find(task.name) != task_name_to_id_.end()) {
        std::cerr << "TaskScheduler: Task name '" << task.name << "' already exists" << std::endl;
        return false;
    }
    
    if (!validate_task_definition(task)) {
        return false;
    }
    
    size_t task_id = task_definitions_.size();
    task_definitions_.push_back(task);
    task_name_to_id_[task.name] = task_id;
    
    // Initialize metrics
    TaskMetrics metrics = {};
    task_metrics_.push_back(metrics);
    
    return true;
}

bool TaskScheduler::add_task(const std::string& name, TaskFunction function, Duration period, 
                            Duration wcet, int priority) {
    TaskDefinition task(name, std::move(function), period, wcet, Duration::zero(), priority, false);
    return add_task(task);
}

TaskScheduler::SchedulabilityReport TaskScheduler::finalize_schedule() {
    SchedulabilityReport report;
    report.is_schedulable = false;
    
    if (task_definitions_.empty()) {
        report.conflicts.push_back("No tasks defined");
        return report;
    }
    
    // Compute hyperperiod
    hyperperiod_ = compute_hyperperiod();
    report.hyperperiod = hyperperiod_;
    report.basic_time_unit = basic_time_unit_;
    
    // Check schedulability
    if (!check_schedulability()) {
        report.conflicts.push_back("Failed Liu & Layland schedulability test");
        return report;
    }
    
    // Generate static schedule
    generate_schedule_table();
    
    // Calculate utilization
    double total_utilization = 0.0;
    for (const auto& task : task_definitions_) {
        total_utilization += static_cast<double>(task.worst_case_execution_time.count()) / 
                           static_cast<double>(task.period.count());
    }
    report.cpu_utilization = total_utilization;
    report.total_executions_per_hyperperiod = schedule_table_.size();
    
    // Check for any unscheduled tasks
    size_t expected_executions = 0;
    for (const auto& task : task_definitions_) {
        expected_executions += hyperperiod_.count() / task.period.count();
    }
    
    if (schedule_table_.size() < expected_executions) {
        report.conflicts.push_back("Some task instances could not be scheduled");
        return report;
    }
    
    report.is_schedulable = true;
    
    if (total_utilization > 0.8) {
        report.warnings.push_back("High CPU utilization: " + std::to_string(total_utilization * 100) + "%");
    }
    
    if (hyperperiod_ > std::chrono::seconds(10)) {
        report.warnings.push_back("Long hyperperiod: " + 
            std::to_string(std::chrono::duration_cast<std::chrono::milliseconds>(hyperperiod_).count()) + "ms");
    }
    
    return report;
}

bool TaskScheduler::start_execution() {
    if (schedule_table_.empty()) {
        std::cerr << "TaskScheduler: No schedule available. Call finalize_schedule() first." << std::endl;
        return false;
    }
    
    if (is_running_.exchange(true)) {
        std::cerr << "TaskScheduler: Already running" << std::endl;
        return false;
    }
    
    schedule_start_time_ = std::chrono::high_resolution_clock::now();
    
    // Start execution thread
    std::thread execution_thread([this]() {
        while (is_running_.load()) {
            execute_one_hyperperiod();
        }
    });
    
    execution_thread.detach();
    return true;
}

void TaskScheduler::stop_execution() {
    is_running_.store(false);
}

void TaskScheduler::execute_one_hyperperiod() {
    TimePoint hyperperiod_start = std::chrono::high_resolution_clock::now();
    
    for (const auto& execution : schedule_table_) {
        if (!is_running_.load()) {
            break;
        }
        
        // Wait until it's time to execute this task
        TimePoint target_time = hyperperiod_start + execution.start_time;
        std::this_thread::sleep_until(target_time);
        
        // Execute the task
        TimePoint actual_start = std::chrono::high_resolution_clock::now();
        
        try {
            task_definitions_[execution.task_id].function();
        } catch (const std::exception& e) {
            std::cerr << "TaskScheduler: Exception in task '" 
                      << task_definitions_[execution.task_id].name 
                      << "': " << e.what() << std::endl;
        }
        
        TimePoint actual_end = std::chrono::high_resolution_clock::now();
        Duration actual_execution_time = actual_end - actual_start;
        
        // Update metrics
        auto& metrics = task_metrics_[execution.task_id];
        metrics.executions_completed++;
        metrics.total_execution_time += actual_execution_time;
        metrics.max_execution_time = std::max(metrics.max_execution_time, actual_execution_time);
        metrics.last_execution_time = actual_execution_time;
        
        // Check for deadline miss
        Duration elapsed_since_release = actual_end - (hyperperiod_start + 
            Duration(execution.instance_number * task_definitions_[execution.task_id].period.count()));
        
        if (elapsed_since_release > task_definitions_[execution.task_id].deadline) {
            metrics.deadline_misses++;
            std::cerr << "TaskScheduler: Deadline miss for task '" 
                      << task_definitions_[execution.task_id].name << "'" << std::endl;
        }
        
        // Check for WCET violation
        if (actual_execution_time > task_definitions_[execution.task_id].worst_case_execution_time) {
            std::cerr << "TaskScheduler: WCET violation for task '" 
                      << task_definitions_[execution.task_id].name 
                      << "' - actual: " << std::chrono::duration_cast<std::chrono::microseconds>(actual_execution_time).count()
                      << "μs, WCET: " << std::chrono::duration_cast<std::chrono::microseconds>(
                          task_definitions_[execution.task_id].worst_case_execution_time).count() << "μs" << std::endl;
        }
    }
}

const TaskScheduler::TaskMetrics& TaskScheduler::get_task_metrics(size_t task_id) const {
    assert(task_id < task_metrics_.size());
    return task_metrics_[task_id];
}

const TaskScheduler::TaskMetrics& TaskScheduler::get_task_metrics(const std::string& task_name) const {
    auto it = task_name_to_id_.find(task_name);
    assert(it != task_name_to_id_.end());
    return task_metrics_[it->second];
}

void TaskScheduler::print_schedule_table() const {
    std::cout << "\n=== TTA Schedule Table ===" << std::endl;
    std::cout << "Hyperperiod: " << std::chrono::duration_cast<std::chrono::milliseconds>(hyperperiod_).count() 
              << "ms" << std::endl;
    std::cout << "Basic Time Unit: " << std::chrono::duration_cast<std::chrono::microseconds>(basic_time_unit_).count() 
              << "μs" << std::endl;
    std::cout << std::endl;
    
    std::cout << std::setw(20) << "Task Name" 
              << std::setw(10) << "Start(ms)" 
              << std::setw(10) << "End(ms)" 
              << std::setw(10) << "Duration" 
              << std::setw(10) << "Instance" << std::endl;
    std::cout << std::string(70, '-') << std::endl;
    
    for (const auto& execution : schedule_table_) {
        const auto& task = task_definitions_[execution.task_id];
        
        auto start_ms = std::chrono::duration_cast<std::chrono::milliseconds>(execution.start_time).count();
        auto end_ms = std::chrono::duration_cast<std::chrono::milliseconds>(execution.end_time).count();
        auto duration_us = std::chrono::duration_cast<std::chrono::microseconds>(
            execution.end_time - execution.start_time).count();
        
        std::cout << std::setw(20) << task.name
                  << std::setw(10) << start_ms
                  << std::setw(10) << end_ms  
                  << std::setw(9) << duration_us << "μs"
                  << std::setw(10) << execution.instance_number << std::endl;
    }
    std::cout << std::endl;
}

TaskScheduler::SchedulabilityReport TaskScheduler::validate_task_set(
    const std::vector<TaskDefinition>& tasks, Duration basic_time_unit) {
    
    TaskScheduler temp_scheduler(basic_time_unit);
    
    for (const auto& task : tasks) {
        if (!temp_scheduler.add_task(task)) {
            SchedulabilityReport report;
            report.is_schedulable = false;
            report.conflicts.push_back("Invalid task: " + task.name);
            return report;
        }
    }
    
    return temp_scheduler.finalize_schedule();
}

// TaskSetBuilder implementation

TaskSetBuilder& TaskSetBuilder::add_periodic_task(const std::string& name, 
                                                  TaskScheduler::TaskFunction function,
                                                  TaskScheduler::Duration period, 
                                                  TaskScheduler::Duration wcet,
                                                  int priority) {
    tasks_.emplace_back(name, std::move(function), period, wcet, TaskScheduler::Duration::zero(), priority, false);
    return *this;
}

TaskSetBuilder& TaskSetBuilder::add_critical_task(const std::string& name, 
                                                  TaskScheduler::TaskFunction function,
                                                  TaskScheduler::Duration period, 
                                                  TaskScheduler::Duration wcet,
                                                  int priority) {
    tasks_.emplace_back(name, std::move(function), period, wcet, TaskScheduler::Duration::zero(), priority, true);
    return *this;
}

TaskScheduler::SchedulabilityReport TaskSetBuilder::validate() const {
    return TaskScheduler::validate_task_set(tasks_, basic_time_unit_);
}

bool TaskSetBuilder::build_scheduler(TaskScheduler& scheduler) const {
    for (const auto& task : tasks_) {
        if (!scheduler.add_task(task)) {
            return false;
        }
    }
    
    auto report = scheduler.finalize_schedule();
    return report.is_schedulable;
}

} // namespace Alaris::Core