namespace JobForge.Api.Dtos;

public record DashboardStatsResponse(int PendingCount, int ProcessingCount, int ProcessedToday, int FailedCount);
