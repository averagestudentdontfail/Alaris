#include "time_trigger.h"
#include <algorithm>
#include <iostream>
#include <numeric>

#if defined(__linux__)
#include <sched.h>
#include <sys/mman.h>
#endif

namespace Alaris::Core {

// Task constructor definition (was causing redefinition error)
TimeTriggeredExecutor::Task::Task(std::string task_name, TaskFunction func, Duration per, Duration offset, Duration dead)
    : name(std::move(task_name)),
      function(std::move(func)),
      period(per),
      phase_offset(offset),
      deadline(dead),
      last_execution_scheduled_time(TimePoint::min()),
      last_execution_actual_start_time(TimePoint::min()),
      execution_count(0),
      missed_deadlines_count(0),
      total_execution_time_ns(Duration::zero()),
      max_execution_time_ns(Duration::zero()) {}

TimeTriggeredExecutor::TimeTriggeredExecutor(Duration major_frame)
    : major_frame_(major_frame),
      stop_requested_(false),
      current_major_frame_count_(0),
      executor_start_time_(TimePoint::min()) {
    tasks_.reserve(16);
    cycle_history_.reserve(MAX_CYCLE_HISTORY);
}

TimeTriggeredExecutor::~TimeTriggeredExecutor() {
    if (is_running()) {
        stop();
    }
}

void TimeTriggeredExecutor::register_task(
    std::string task_name,
    TaskFunction func,
    Duration period,
    Duration phase_offset,
    Duration deadline) {

    if (period.count() == 0 || period < major_frame_ || period.count() % major_frame_.count() != 0) {
        std::cerr << "TimeTriggeredExecutor: Error - Task '" << task_name << "' period must be a non-zero multiple of major_frame." << std::endl;
        return;
    }
    if (phase_offset < Duration::zero() || phase_offset.count() % major_frame_.count() != 0) {
         std::cerr << "TimeTriggeredExecutor: Error - Task '" << task_name << "' phase_offset must be a non-negative multiple of major_frame." << std::endl;
        return;
    }

    if (deadline == Duration::zero()) {
        deadline = period;
    }
    if (deadline > period) {
        std::cerr << "TimeTriggeredExecutor: Warning - Task '" << task_name << "' deadline is greater than its period." << std::endl;
    }

    tasks_.emplace_back(task_name, std::move(func), period, phase_offset, deadline);
}

// Simplified overload for common usage patterns
void TimeTriggeredExecutor::register_task(TaskFunction task, Duration period) {
    static int task_counter = 0;
    std::string task_name = "Task_" + std::to_string(++task_counter);
    register_task(task_name, std::move(task), period, Duration::zero(), period);
}

bool TimeTriggeredExecutor::should_execute_task(const Task& task, TimePoint cycle_start) const {
    if (executor_start_time_ == TimePoint::min()) {
        return false;
    }
    
    Duration time_elapsed = cycle_start - executor_start_time_;
    
    if (time_elapsed >= task.phase_offset) {
        Duration time_since_phase = time_elapsed - task.phase_offset;
        return time_since_phase.count() % task.period.count() == 0;
    }
    
    return false;
}

void TimeTriggeredExecutor::execute_task(Task& task, TimePoint cycle_start) {
    task.last_execution_scheduled_time = cycle_start + task.phase_offset;
    task.last_execution_actual_start_time = Clock::now();
    
    task.function();
    
    auto actual_task_end_time = Clock::now();
    Duration actual_execution_duration = actual_task_end_time - task.last_execution_actual_start_time;
    
    task.execution_count++;
    task.total_execution_time_ns += actual_execution_duration;
    task.max_execution_time_ns = std::max(task.max_execution_time_ns, actual_execution_duration);

    if (actual_execution_duration > task.deadline) {
        task.missed_deadlines_count++;
    }
}

void TimeTriggeredExecutor::execute_tasks_for_current_frame(TimePoint current_frame_ideal_start_time) {
    Duration time_elapsed_since_executor_start = current_frame_ideal_start_time - executor_start_time_;

    for (auto& task : tasks_) {
        if (time_elapsed_since_executor_start >= task.phase_offset) {
            Duration time_since_phase_offset = time_elapsed_since_executor_start - task.phase_offset;
            if (time_since_phase_offset.count() % task.period.count() == 0) {
                execute_task(task, current_frame_ideal_start_time);
            }
        }
    }
}

void TimeTriggeredExecutor::record_cycle_metrics(const CyclePerformanceMetrics& metrics) {
    if (cycle_history_.size() >= MAX_CYCLE_HISTORY) {
        cycle_history_.erase(cycle_history_.begin());
    }
    cycle_history_.push_back(metrics);
}

void TimeTriggeredExecutor::run(Duration duration, std::atomic<bool>& stop_flag) {
    executor_start_time_ = Clock::now();
    auto end_time = executor_start_time_ + duration;
    
    while (!stop_flag.load() && !stop_requested_.load() && Clock::now() < end_time) {
        auto cycle_start = Clock::now();
        
        for (auto& task : tasks_) {
            if (should_execute_task(task, cycle_start)) {
                execute_task(task, cycle_start);
            }
        }
        
        auto cycle_end = Clock::now();
        Duration cycle_duration = cycle_end - cycle_start;
        
        CyclePerformanceMetrics metrics;
        metrics.actual_start_time = cycle_start;
        metrics.actual_duration_ns = cycle_duration;
        metrics.intended_duration_ns = major_frame_;
        metrics.jitter_ns = std::chrono::abs(cycle_duration - major_frame_);
        metrics.tasks_executed_in_cycle = 0;
        metrics.deadlines_missed_in_cycle = 0;
        
        for (const auto& task : tasks_) {
            if (should_execute_task(task, cycle_start)) {
                metrics.tasks_executed_in_cycle++;
                if (cycle_duration > task.deadline) {
                    metrics.deadlines_missed_in_cycle++;
                }
            }
        }
        
        record_cycle_metrics(metrics);
        current_major_frame_count_++;
        
        auto next_cycle = cycle_start + major_frame_;
        std::this_thread::sleep_until(next_cycle);
    }
}

void TimeTriggeredExecutor::run_continuous(std::atomic<bool>& stop_flag) {
    executor_start_time_ = Clock::now();
    
    while (!stop_flag.load() && !stop_requested_.load()) {
        auto cycle_start = Clock::now();
        
        for (auto& task : tasks_) {
            if (should_execute_task(task, cycle_start)) {
                execute_task(task, cycle_start);
            }
        }
        
        auto cycle_end = Clock::now();
        Duration cycle_duration = cycle_end - cycle_start;
        
        CyclePerformanceMetrics metrics;
        metrics.actual_start_time = cycle_start;
        metrics.actual_duration_ns = cycle_duration;
        metrics.intended_duration_ns = major_frame_;
        metrics.jitter_ns = std::chrono::abs(cycle_duration - major_frame_);
        metrics.tasks_executed_in_cycle = 0;
        metrics.deadlines_missed_in_cycle = 0;
        
        for (const auto& task : tasks_) {
            if (should_execute_task(task, cycle_start)) {
                metrics.tasks_executed_in_cycle++;
                if (cycle_duration > task.deadline) {
                    metrics.deadlines_missed_in_cycle++;
                }
            }
        }
        
        record_cycle_metrics(metrics);
        current_major_frame_count_++;
        
        auto next_cycle = cycle_start + major_frame_;
        std::this_thread::sleep_until(next_cycle);
    }
}

void TimeTriggeredExecutor::stop() {
    stop_requested_.store(true);
}

bool TimeTriggeredExecutor::is_running() const {
    return executor_start_time_ != TimePoint::min() && !stop_requested_.load(std::memory_order_relaxed);
}

TimeTriggeredExecutor::PerformanceReport TimeTriggeredExecutor::get_performance_metrics() const {
    PerformanceReport report;
    report.total_major_frames_executed = current_major_frame_count_;
    report.total_task_deadlines_missed_overall = 0;
    
    // Set the backward compatibility fields
    report.total_cycles = current_major_frame_count_;
    report.total_deadlines_missed = 0;
    
    if (!cycle_history_.empty()) {
        Duration total_frame_time = Duration::zero();
        Duration total_jitter = Duration::zero();
        Duration max_frame_time = Duration::zero();
        Duration min_frame_time = Duration::max();
        Duration max_jitter = Duration::zero();
        
        for (const auto& cycle : cycle_history_) {
            total_frame_time += cycle.actual_duration_ns;
            total_jitter += cycle.jitter_ns;
            max_frame_time = std::max(max_frame_time, cycle.actual_duration_ns);
            min_frame_time = std::min(min_frame_time, cycle.actual_duration_ns);
            max_jitter = std::max(max_jitter, cycle.jitter_ns);
            report.total_task_deadlines_missed_overall += cycle.deadlines_missed_in_cycle;
        }
        
        size_t history_count = cycle_history_.size();
        report.average_major_frame_time_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(total_frame_time).count()) / 
            static_cast<double>(history_count);
        report.average_jitter_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(total_jitter).count()) / 
            static_cast<double>(history_count);
        report.max_major_frame_time_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(max_frame_time).count());
        report.min_major_frame_time_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(min_frame_time).count());
        report.max_jitter_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(max_jitter).count());
        
        // Set the backward compatibility fields
        report.average_cycle_time_us = report.average_major_frame_time_us;
        report.max_cycle_time_us = report.max_major_frame_time_us;
        report.total_deadlines_missed = report.total_task_deadlines_missed_overall;
    }
    
    for (const auto& task : tasks_) {
        PerformanceReport::TaskReport task_report;
        task_report.name = task.name;
        task_report.execution_count = task.execution_count;
        task_report.missed_deadlines_count = task.missed_deadlines_count;
        
        if (task.execution_count > 0) {
            task_report.average_execution_time_us = static_cast<double>(
                std::chrono::duration_cast<std::chrono::microseconds>(task.total_execution_time_ns).count()) / 
                static_cast<double>(task.execution_count);
            task_report.miss_rate_percent = (static_cast<double>(task.missed_deadlines_count) / 
                                           static_cast<double>(task.execution_count)) * 100.0;
        } else {
            task_report.average_execution_time_us = 0.0;
            task_report.miss_rate_percent = 0.0;
        }
        
        task_report.max_execution_time_us = static_cast<double>(
            std::chrono::duration_cast<std::chrono::microseconds>(task.max_execution_time_ns).count());
        
        report.task_reports.push_back(task_report);
    }
    
    return report;
}

void TimeTriggeredExecutor::reset_metrics() {
    cycle_history_.clear();
    current_major_frame_count_ = 0;
    
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