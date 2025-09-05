namespace Contracts.DTOs
{
    /// <summary>
    /// Dashboard overview with key metrics and recent activities
    /// </summary>
    public class DashboardOverviewDto
    {
        /// <summary>
        /// Current pipeline execution statistics
        /// </summary>
        public PipelineMetricsDto Metrics { get; set; } = new();

        /// <summary>
        /// Currently running pipeline executions
        /// </summary>
        public List<PipelineExecutionDto> ActiveExecutions { get; set; } = new();

        /// <summary>
        /// Recent pipeline execution activities
        /// </summary>
        public List<PipelineActivityDto> RecentActivities { get; set; } = new();

        /// <summary>
        /// System health indicators
        /// </summary>
        public SystemHealthDto SystemHealth { get; set; } = new();
    }

    /// <summary>
    /// Pipeline execution metrics
    /// </summary>
    public class PipelineMetricsDto
    {
        /// <summary>
        /// Total number of executions today
        /// </summary>
        public int ExecutionsToday { get; set; }

        /// <summary>
        /// Number of successful executions today
        /// </summary>
        public int SuccessfulToday { get; set; }

        /// <summary>
        /// Number of failed executions today
        /// </summary>
        public int FailedToday { get; set; }

        /// <summary>
        /// Number of currently running executions
        /// </summary>
        public int CurrentlyRunning { get; set; }

        /// <summary>
        /// Average execution time in minutes
        /// </summary>
        public double AverageExecutionTimeMinutes { get; set; }

        /// <summary>
        /// Success rate percentage for today
        /// </summary>
        public double TodaySuccessRate => ExecutionsToday > 0 ? (double)SuccessfulToday / ExecutionsToday * 100 : 0;
    }

    /// <summary>
    /// Pipeline activity record
    /// </summary>
    public class PipelineActivityDto
    {
        /// <summary>
        /// Activity timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Product name
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Pipeline stage name
        /// </summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// Activity type (Started, Completed, Failed, etc.)
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Activity description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Severity level (Info, Warning, Error)
        /// </summary>
        public string Severity { get; set; } = "Info";
    }

    /// <summary>
    /// System health indicators
    /// </summary>
    public class SystemHealthDto
    {
        /// <summary>
        /// Database connection status
        /// </summary>
        public bool DatabaseConnected { get; set; }

        /// <summary>
        /// Hangfire job server status
        /// </summary>
        public bool JobServerHealthy { get; set; }

        /// <summary>
        /// Number of pending jobs in queue
        /// </summary>
        public int PendingJobs { get; set; }

        /// <summary>
        /// Number of failed jobs requiring attention
        /// </summary>
        public int FailedJobs { get; set; }

        /// <summary>
        /// Overall system status
        /// </summary>
        public string OverallStatus => DatabaseConnected && JobServerHealthy ? "Healthy" : "Warning";
    }
}