// src/quantlib/core/time_trigger.cpp
#include "time_trigger.h"
#include <sched.h>
#include <sys/mman.h>
#include <algorithm>
#include <iostream>

namespace Alaris::Core {

TimeTriggeredExecutor::TimeTriggeredExecutor(Duration major_frame)
    : major_frame_(major_frame), running_(false), stop_requested_(false) {
    
    tasks_.reserve(32); // Reserve space for typical number of tasks
    cycle_history_.reserve(MAX_HISTORY);
    
    // Set real-time scheduling
    struct sched_param param;
    param.sched_priority = 80;
    if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
        std::cerr << "Warning: Failed to set real-time priority\n";
    }
    
    // Lock memory to prevent paging
    if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
        std::cerr << "Warning: Failed to lock memory\n";
    }
}

TimeTriggeredExecutor::~TimeTriggeredExecutor() {
    stop();
}

void TimeTriggeredExecutor::register_task(TaskFunction task, Duration period,
                                         Duration phase_offset, Duration deadline) {
    if (deadline == Duration::zero()) {
        deadline = period; // Default deadline equals period
    }
    
    tasks_.emplace_back(std::move(task), period, phase_offset, deadline);
}

void TimeTriggeredExecutor::execute_single_cycle() {
    const auto cycle_start = Clock::now();
    
    CycleMetrics metrics{};
    metrics.tasks_executed = 0;
    metrics.deadlines_missed = 0;
    
    for (auto& task : tasks_) {
        const auto current_time = Clock::now();
        const auto time_since_start = current_time - start_time_;
        
        // Check if task should execute
        const auto next_execution = task.phase_offset + 
                                   task.execution_count * task.period;
        
        if (time_since_start >= next_execution) {
            const auto task_start = Clock::now();
            
            // Execute task
            task.function();
            
            const auto task_end = Clock::now();
            const auto execution_time = task_end - task_start;
            
            // Update task metrics
            task.last_execution = current_time;
            task.execution_count++;
            task.total_execution_time += execution_time;
            task.max_execution_time = std::max(task.max_execution_time, execution_time);
            
            metrics.tasks_executed++;
            
            // Check deadline
            if (execution_time > task.deadline) {
                task.missed_deadlines++;
                metrics.deadlines_missed++;
            }
        }
    }
    
    const auto cycle_end = Clock::now();
    metrics.cycle_time = cycle_end - cycle_start;
    
    // Calculate jitter (deviation from expected cycle time)
    const auto expected_cycle_time = major_frame_;
    metrics.jitter = metrics.cycle_time > expected_cycle_time ?
                    metrics.cycle_time - expected_cycle_time :
                    expected_cycle_time - metrics.cycle_time;
    
    collect_metrics(metrics);
}

void TimeTriggeredExecutor::run(Duration duration) {
    start_time_ = Clock::now();
    running_ = true;
    stop_requested_ = false;
    
    const auto end_time = start_time_ + duration;
    
    while (Clock::now() < end_time && !stop_requested_) {
        const auto cycle_start = Clock::now();
        
        execute_single_cycle();
        
        // Sleep until next major frame
        const auto cycle_end = Clock::now();
        const auto elapsed = cycle_end - cycle_start;
        
        if (elapsed < major_frame_) {
            std::this_thread::sleep_for(major_frame_ - elapsed);
        }
    }
    
    running_ = false;
}

void TimeTriggeredExecutor::run_continuous() {
    start_time_ = Clock::now();
    running_ = true;
    stop_requested_ = false;
    
    while (!stop_requested_) {
        const auto cycle_start = Clock::now();
        
        execute_single_cycle();
        
        // Sleep until next major frame
        const auto cycle_end = Clock::now();
        const auto elapsed = cycle_end - cycle_start;
        
        if (elapsed < major_frame_) {
            std::this_thread::sleep_for(major_frame_ - elapsed);
        }
    }
    
    running_ = false;
}

void TimeTriggeredExecutor::stop() {
    stop_requested_ = true;
    
    // Wait for current cycle to complete
    while (running_) {
        std::this_thread::sleep_for(std::chrono::microseconds(100));
    }
}

void TimeTriggeredExecutor::collect_metrics(const CycleMetrics& metrics) {
    if (cycle_history_.size() >= MAX_HISTORY) {
        // Remove oldest entry
        cycle_history_.erase(cycle_history_.begin());
    }
    
    cycle_history_.push_back(metrics);
}

TimeTriggeredExecutor::PerformanceMetrics 
TimeTriggeredExecutor::get_performance_metrics() const {
    PerformanceMetrics metrics{};
    
    if (cycle_history_.empty()) {
        return metrics;
    }
    
    // Calculate cycle metrics
    Duration total_cycle_time{0};
    Duration max_cycle_time{0};
    Duration total_jitter{0};
    Duration max_jitter{0};
    size_t total_deadlines_missed = 0;
    
    for (const auto& cycle : cycle_history_) {
        total_cycle_time += cycle.cycle_time;
        max_cycle_time = std::max(max_cycle_time, cycle.cycle_time);
        total_jitter += cycle.jitter;
        max_jitter = std::max(max_jitter, cycle.jitter);
        total_deadlines_missed += cycle.deadlines_missed;
    }
    
    const auto cycle_count = cycle_history_.size();
    
    metrics.average_cycle_time_us = 
        std::chrono::duration_cast<std::chrono::microseconds>(total_cycle_time).count() / 
        static_cast<double>(cycle_count);
    
    metrics.max_cycle_time_us = 
        std::chrono::duration_cast<std::chrono::microseconds>(max_cycle_time).count();
    
    metrics.average_jitter_us = 
        std::chrono::duration_cast<std::chrono::microseconds>(total_jitter).count() / 
        static_cast<double>(cycle_count);
    
    metrics.max_jitter_us = 
        std::chrono::duration_cast<std::chrono::microseconds>(max_jitter).count();
    
    metrics.total_cycles = cycle_count;
    metrics.total_deadlines_missed = total_deadlines_missed;
    metrics.deadline_miss_rate = static_cast<double>(total_deadlines_missed) / 
                                static_cast<double>(cycle_count);
    
    // Calculate task metrics
    metrics.task_metrics.reserve(tasks_.size());
    
    for (size_t i = 0; i < tasks_.size(); ++i) {
        const auto& task = tasks_[i];
        PerformanceMetrics::TaskMetrics task_metrics{};
        
        task_metrics.task_id = i;
        task_metrics.execution_count = task.execution_count;
        task_metrics.missed_deadlines = task.missed_deadlines;
        
        if (task.execution_count > 0) {
            task_metrics.average_execution_time_us = 
                std::chrono::duration_cast<std::chrono::microseconds>(task.total_execution_time).count() /
                static_cast<double>(task.execution_count);
            
            task_metrics.miss_rate = static_cast<double>(task.missed_deadlines) /
                                   static_cast<double>(task.execution_count);
        }
        
        task_metrics.max_execution_time_us = 
            std::chrono::duration_cast<std::chrono::microseconds>(task.max_execution_time).count();
        
        metrics.task_metrics.push_back(task_metrics);
    }
    
    return metrics;
}

void TimeTriggeredExecutor::reset_metrics() {
    cycle_history_.clear();
    
    for (auto& task : tasks_) {
        task.execution_count = 0;
        task.missed_deadlines = 0;
        task.total_execution_time = Duration::zero();
        task.max_execution_time = Duration::zero();
    }
}

} // namespace Alaris::Core