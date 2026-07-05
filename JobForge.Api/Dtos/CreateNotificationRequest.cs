using System.ComponentModel.DataAnnotations;

namespace JobForge.Api.Dtos;

public record CreateNotificationRequest
{
    [Required]
    public Guid RequestId { get; init; }

    [Required]
    [EmailAddress]
    public string RecipientEmail { get; init; } = string.Empty;

    [Required]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string Body { get; init; } = string.Empty;
}
