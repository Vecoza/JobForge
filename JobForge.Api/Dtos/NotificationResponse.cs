namespace JobForge.Api.Dtos;

public record NotificationResponse(Guid JobId, Guid RequestId, string Status, DateTimeOffset CreatedAt);
