#include "time_trigger.h"
#include <algorithm> // For std::min, std::max
#include <iostream>  // For warnings/errors if any
#include <numeric>   // For std::accumulate in metrics calculation

// For real-time capabilities (optional, platform-dependent)
#if defined(__linux__)
#include <sched.h>
#include <sys/mman.h>
#endif

namespace Alaris::Core {

TimeTriggeredExecutor::TimeTriggeredExecutor(Duration major_frame)
    : major_frame_(major_frame),
      stop_requested_(false),
      current_major_frame_count_(0) {
    tasks_.reserve(16); // Pre-allocate for a typical number of tasks
    cycle_history_.reserve(MAX_CYCLE_HISTORY);

// Platform-specific real-time setup (example for Linux)
// This might be better handled by the main application (AlarisQuantLibProcess)
// which already sets priority and locks memory.
#if defined(__linux__) && defined(ALARIS_SET_EXECUTOR_RT_PRIORITY) // Control with a macro
    // struct sched_param param;
    // param.sched_priority = sched_get_priority_max(SCHED_FIFO) - 1; // High, but not max
    // if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
    //     perror("TimeTriggeredExecutor: Warning - Failed to set real-time priority");
    // }
    // if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
    //     perror("TimeTriggeredExecutor: Warning - Failed to lock memory");
    // }
#endif
}

TimeTriggeredExecutor::~TimeTriggeredExecutor() {
    if (is_running()) { // Check if it might be considered running
        stop(); // Ensure stop is signaled if it was running in a managed thread (not the case here)
    }
}

void TimeTriggeredExecutor::register_task(
    std::string task_name,
    TaskFunction func,
    Duration period,
    Duration phase_offset,
    Duration deadline) {

    if (period.count() == 0 || period < major_frame_ || period.count() % major_frame_.count() != 0) {
        // Consider throwing an exception or logging an error
        std::cerr << "TimeTriggeredExecutor: Error - Task '" << task_name << "' period must be a non-zero multiple of major_frame." << std::endl;
        return;
    }
    if (phase_offset < Duration::zero() || phase_offset.count() % major_frame_.count() != 0) {
         std::cerr << "TimeTriggeredExecutor: Error - Task '" << task_name << "' phase_offset must be a non-negative multiple of major_frame." << std::endl;
        return;
    }

    if (deadline == Duration::zero()) {
        deadline = period; // Default deadline is the task's period
    }
    if (deadline > period) {
        std::cerr << "TimeTriggeredExecutor: Warning - Task '" << task_name << "' deadline is greater than its period." << std::endl;
    }

    tasks_.emplace_back(std::move(task_name), std::move(func), period, phase_offset, deadline);
}

void TimeTriggeredExecutor::execute_tasks_for_current_frame(TimePoint current_frame_ideal_start_time) {
    Duration time_elapsed_since_executor_start = current_frame_ideal_start_time - executor_start_time_;

    for (auto& task : tasks_) {
        // Check if the task is due in this major frame based on its phase and period
        // A task is due if (time_elapsed_since_executor_start - task.phase_offset) is a non-negative multiple of task.period
        if (time_elapsed_since_executor_start >= task.phase_offset) {
            Duration time_since_phase_offset = time_elapsed_since_executor_start - task.phase_offset;
            if (time_since_phase_offset.count() % task.period.count() == 0) {
                // Task is scheduled to run now
                task.last_execution_scheduled_time = current_frame_ideal_start_time + task.phase_offset; 
                                                    // More accurately, this specific task's ideal start time.
                                                    // For simplicity of TTA, tasks start at frame boundaries.

                task.last_execution_actual_start_time = Clock::now();
                task.function(); // Execute the task
                auto actual_task_end_time = Clock::now();
                
                Duration actual_execution_duration = actual_task_end_time - task.last_execution_actual_start_time;
                task.execution_count++;
                task.total_execution_time_ns += actual_execution_duration;
                task.max_execution_time_ns = std::max(task.max_execution_time_ns, actual_execution_duration);

                // Deadline check: deadline is relative to scheduled start
                // If task execution overran its deadline for this instance
                if (actual_execution_duration > task.deadline) {
                    task.missed_deadlines_count++;
                    // Optionally log deadline misses immediately
                    // std::cerr << "Task '" << task.name << "' missed deadline. Executed in "
                    //           << std::chrono::duration_cast<std::chrono::microseconds>(actual_execution_duration).count()
                    //           << "us, Deadline: " << std::chrono::duration_cast<std::chrono::microseconds>(task.deadline).count() << "us" << std::endl;
                }
            }
        }
    }
}

void TimeTriggeredExecutor::record_cycle_metrics(const CyclePerformanceMetrics& metrics) {
    if (cycle_history_.size() >= MAX_CYCLE_HISTORY) {
        cycle_history_.erase(cycle_history_.begin()); // Keep history bounded
    }
    cycle_history_.push_back(metrics);
}


void TimeTriggeredExecutor::run(Duration duration, std::atomic<bool>& stop_flag) {
    auto start_time = std::chrono::steady_clock::now();
    auto end_time = start_time + duration;
    
    while (!stop_flag && std::chrono::steady_clock::now() < end_time) {
        auto cycle_start = std::chrono::steady_clock::now();
        
        // Execute tasks for this cycle
        for (auto& task : tasks_) {
            if (should_execute_task(task, cycle_start)) {
                execute_task(task, cycle_start);
            }
        }
        
        // Calculate and record cycle metrics
        auto cycle_end = std::chrono::steady_clock::now();
        auto cycle_duration = cycle_end - cycle_start;
        auto jitter = std::abs(std::chrono::duration_cast<std::chrono::nanoseconds>(cycle_duration).count() - 
                             major_frame_ns_);
        
        // Update performance metrics
        total_frame_time_ns_ += cycle_duration;
        total_jitter_ns_ += std::chrono::nanoseconds(jitter);
        history_count_++;
        
        // Sleep until next cycle
        auto next_cycle = cycle_start + std::chrono::nanoseconds(major_frame_ns_);
        std::this_thread::sleep_until(next_cycle);
    }
}

void TimeTriggeredExecutor::run_continuous(std::atomic<bool>& stop_flag) {
    while (!stop_flag) {
        auto cycle_start = std::chrono::steady_clock::now();
        
        // Execute tasks for this cycle
        for (auto& task : tasks_) {
            if (should_execute_task(task, cycle_start)) {
                execute_task(task, cycle_start);
            }
        }
        
        // Calculate and record cycle metrics
        auto cycle_end = std::chrono::steady_clock::now();
        auto cycle_duration = cycle_end - cycle_start;
        auto jitter = std::abs(std::chrono::duration_cast<std::chrono::nanoseconds>(cycle_duration).count() - 
                             major_frame_ns_);
        
        // Update performance metrics
        total_frame_time_ns_ += cycle_duration;
        total_jitter_ns_ += std::chrono::nanoseconds(jitter);
        history_count_++;
        
        // Sleep until next cycle
        auto next_cycle = cycle_start + std::chrono::nanoseconds(major_frame_ns_);
        std::this_thread::sleep_until(next_cycle);
    }
}

void TimeTriggeredExecutor::stop() {
    stop_requested_ = true;
}

bool TimeTriggeredExecutor::is_running() const {
    // If run() or run_continuous() are blocking and called from a thread,
    // this method would need to check the state of that thread or a specific atomic flag set by run methods.
    // If they are blocking in the caller's thread, "running" is true while the method is on the stack.
    // For now, assume it's "running" if not explicitly stopped and start_time_ is not min.
    return executor_start_time_ != TimePoint::min() && !stop_requested_.load(std::memory_order_relaxed);
}

TimeTriggeredExecutor::PerformanceReport TimeTriggeredExecutor::get_performance_metrics() const {
    PerformanceReport report;
    
    if (history_count_ > 0) {
        // Use explicit casts to avoid conversion warnings
        report.average_major_frame_time_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(total_frame_time_ns_).count()) / 
            static_cast<double>(history_count_);
            
        report.average_jitter_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(total_jitter_ns_).count()) / 
            static_cast<double>(history_count_);
    }
    
    // Task-specific metrics
    for (const auto& task : tasks_) {
        TaskPerformanceReport task_rep;
        task_rep.task_name = task.name;
        
        if (task.execution_count > 0) {
            task_rep.average_execution_time_us = static_cast<double>(
                std::chrono::duration_cast<std::chrono::microseconds>(task.total_execution_time_ns).count()) / 
                static_cast<double>(task.execution_count);
                
            task_rep.miss_rate_percent = (static_cast<double>(task.missed_deadlines_count) / 
                                        static_cast<double>(task.execution_count)) * 100.0;
        }
        
        report.task_metrics.push_back(task_rep);
    }
    
    return report;
}

void TimeTriggeredExecutor::reset_metrics() {
    cycle_history_.clear();
    current_major_frame_count_ = 0; // Reset frame count
    for (auto& task : tasks_) {
        task.execution_count = 0;
        task.missed_deadlines_count = 0;
        task.total_execution_time_ns = Duration::zero();
        task.max_execution_time_ns = Duration::zero();
        task.last_execution_scheduled_time = TimePoint::min();
        task.last_execution_actual_start_time = TimePoint::min();
    }
}

} // namespace Alaris::Core