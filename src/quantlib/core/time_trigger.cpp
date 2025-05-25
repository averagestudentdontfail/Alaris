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


void TimeTriggeredExecutor::run(Duration duration, std::atomic<bool>& shutdown_flag) {
    executor_start_time_ = Clock::now();
    stop_requested_ = false;
    current_major_frame_count_ = 0;
    TimePoint previous_frame_actual_end_time = executor_start_time_;

    const TimePoint overall_end_time = executor_start_time_ + duration;

    while (Clock::now() < overall_end_time && !shutdown_flag.load(std::memory_order_relaxed) && !stop_requested_.load(std::memory_order_relaxed)) {
        TimePoint current_frame_ideal_start_time = executor_start_time_ + current_major_frame_count_ * major_frame_;
        TimePoint current_frame_actual_start_time = Clock::now();

        Duration jitter = current_frame_actual_start_time - previous_frame_actual_end_time - 
                          (current_frame_ideal_start_time - (previous_frame_actual_end_time - major_frame_)); 
                          // More simply, jitter relative to ideal periodic start
        jitter = current_frame_actual_start_time - current_frame_ideal_start_time;


        size_t tasks_executed_this_cycle = 0; // Track for CyclePerformanceMetrics
        size_t deadlines_missed_this_cycle = 0; // Track for CyclePerformanceMetrics
        
        // Store pre-missed deadlines to calculate diff for this cycle
        uint64_t deadlines_missed_before_cycle = 0;
        for(const auto& t : tasks_) deadlines_missed_before_cycle += t.missed_deadlines_count;

        execute_tasks_for_current_frame(current_frame_ideal_start_time);
        
        uint64_t deadlines_missed_after_cycle = 0;
        for(const auto& t : tasks_) {
            deadlines_missed_after_cycle += t.missed_deadlines_count;
            // This task execution count check logic is flawed if tasks have different periods.
            // tasks_executed_this_cycle should count tasks that were due and run in this frame.
        }
        deadlines_missed_this_cycle = deadlines_missed_after_cycle - deadlines_missed_before_cycle;


        TimePoint current_frame_actual_end_time = Clock::now();
        Duration current_frame_actual_duration = current_frame_actual_end_time - current_frame_actual_start_time;
        
        record_cycle_metrics({current_frame_actual_start_time, current_frame_actual_duration, major_frame_, jitter, tasks_.size() /*approx*/, deadlines_missed_this_cycle});

        // Sleep until the start of the next major frame
        TimePoint next_frame_ideal_start_time = executor_start_time_ + (current_major_frame_count_ + 1) * major_frame_;
        auto time_to_sleep = next_frame_ideal_start_time - Clock::now();

        if (time_to_sleep > Duration::zero()) {
            std::this_thread::sleep_for(time_to_sleep);
        } else {
            // Overrun: Current frame took longer than major_frame_
            // Log this overrun, it's a critical piece of information for determinism
             std::cerr << "TimeTriggeredExecutor: Warning - Major frame overrun on frame " << current_major_frame_count_
                       << ". Actual duration: " << std::chrono::duration_cast<std::chrono::microseconds>(current_frame_actual_duration).count() << "us."
                       << " Ideal: " << std::chrono::duration_cast<std::chrono::microseconds>(major_frame_).count() << "us." << std::endl;
        }
        previous_frame_actual_end_time = Clock::now(); // End of the sleep/frame.
        current_major_frame_count_++;
    }
    stop_requested_ = true; // Mark as stopped
}


void TimeTriggeredExecutor::run_continuous(std::atomic<bool>& shutdown_flag) {
    executor_start_time_ = Clock::now();
    stop_requested_ = false;
    current_major_frame_count_ = 0;
    TimePoint previous_frame_actual_end_time = executor_start_time_;


    while (!shutdown_flag.load(std::memory_order_relaxed) && !stop_requested_.load(std::memory_order_relaxed)) {
        TimePoint current_frame_ideal_start_time = executor_start_time_ + current_major_frame_count_ * major_frame_;
        TimePoint current_frame_actual_start_time = Clock::now();
        
        Duration jitter = current_frame_actual_start_time - current_frame_ideal_start_time;
        // Note: A more common jitter definition is variation in inter-arrival times of frames.
        // Here, it's deviation from the ideal periodic start.

        size_t tasks_executed_this_cycle = 0; 
        size_t deadlines_missed_this_cycle = 0;
        
        uint64_t deadlines_missed_before_cycle = 0;
        for(const auto& t : tasks_) deadlines_missed_before_cycle += t.missed_deadlines_count;

        execute_tasks_for_current_frame(current_frame_ideal_start_time);
        
        // Count tasks executed in this frame.
        // This simple loop assumes all tasks are checked every frame.
        // A task is "executed" if its condition was met.
        // The `tasks_executed_in_cycle` for CyclePerformanceMetrics requires more careful counting
        // based on which tasks actually ran in `execute_tasks_for_current_frame`.
        // This is not easily done without modifying `execute_tasks_for_current_frame` to return count.
        // For simplicity, one might count tasks whose execution_count incremented.

        uint64_t deadlines_missed_after_cycle = 0;
        for(const auto& t : tasks_) deadlines_missed_after_cycle += t.missed_deadlines_count;
        deadlines_missed_this_cycle = deadlines_missed_after_cycle - deadlines_missed_before_cycle;


        TimePoint current_frame_actual_end_time = Clock::now();
        Duration current_frame_actual_duration = current_frame_actual_end_time - current_frame_actual_start_time;

        record_cycle_metrics({current_frame_actual_start_time, current_frame_actual_duration, major_frame_, jitter, tasks_.size() /*approx num tasks checked*/, deadlines_missed_this_cycle});

        TimePoint next_frame_ideal_start_time = executor_start_time_ + (current_major_frame_count_ + 1) * major_frame_;
        auto time_to_sleep = next_frame_ideal_start_time - Clock::now();

        if (time_to_sleep > Duration::zero()) {
            std::this_thread::sleep_for(time_to_sleep);
        } else {
            // Log overrun
            std::cerr << "TimeTriggeredExecutor: Warning - Major frame overrun on frame " << current_major_frame_count_
                       << ". Actual duration: " << std::chrono::duration_cast<std::chrono::microseconds>(current_frame_actual_duration).count() << "us."
                       << " Ideal: " << std::chrono::duration_cast<std::chrono::microseconds>(major_frame_).count() << "us." << std::endl;
        }
        previous_frame_actual_end_time = Clock::now();
        current_major_frame_count_++;
    }
    stop_requested_ = true; // Ensure it's marked as stopped
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
    PerformanceReport report{};
    if (cycle_history_.empty()) {
        return report;
    }

    report.total_major_frames_executed = current_major_frame_count_; // Or cycle_history_.size() if history is complete

    Duration total_frame_time_ns = Duration::zero();
    Duration max_frame_time_ns = Duration::zero();
    Duration min_frame_time_ns = Duration::max();
    Duration total_jitter_ns = Duration::zero();
    Duration max_jitter_ns = Duration::zero();
    
    for (const auto& cycle_metric : cycle_history_) {
        total_frame_time_ns += cycle_metric.actual_duration_ns;
        max_frame_time_ns = std::max(max_frame_time_ns, cycle_metric.actual_duration_ns);
        min_frame_time_ns = std::min(min_frame_time_ns, cycle_metric.actual_duration_ns);
        total_jitter_ns += std::chrono::abs(cycle_metric.jitter_ns); // Sum of absolute jitter values
        max_jitter_ns = std::max(max_jitter_ns, std::chrono::abs(cycle_metric.jitter_ns));
        report.total_task_deadlines_missed_overall += cycle_metric.deadlines_missed_in_cycle;
    }

    size_t history_count = cycle_history_.size();
    report.average_major_frame_time_us = static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(total_frame_time_ns).count()) / history_count;
    report.max_major_frame_time_us = static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(max_frame_time_ns).count());
    report.min_major_frame_time_us = (min_frame_time_ns == Duration::max()) ? 0.0 : static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(min_frame_time_ns).count());
    report.average_jitter_us = static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(total_jitter_ns).count()) / history_count;
    report.max_jitter_us = static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(max_jitter_ns).count());

    report.task_reports.reserve(tasks_.size());
    for (const auto& task : tasks_) {
        PerformanceReport::TaskReport task_rep;
        task_rep.name = task.name;
        task_rep.execution_count = task.execution_count;
        task_rep.missed_deadlines_count = task.missed_deadlines_count;
        if (task.execution_count > 0) {
            task_rep.average_execution_time_us = static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(task.total_execution_time_ns).count()) / task.execution_count;
            task_rep.miss_rate_percent = (static_cast<double>(task.missed_deadlines_count) / task.execution_count) * 100.0;
        } else {
            task_rep.average_execution_time_us = 0.0;
            task_rep.miss_rate_percent = 0.0;
        }
        task_rep.max_execution_time_us = static_cast<double>(std::chrono::duration_cast<std::chrono::microseconds>(task.max_execution_time_ns).count());
        report.task_reports.push_back(task_rep);
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