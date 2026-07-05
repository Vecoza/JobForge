using JobForge.Api.Dtos;
using JobForge.Core;
using JobForge.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobForge.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NotificationResponse>> Create(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await db.Jobs.FirstOrDefaultAsync(j => j.RequestId == request.RequestId, cancellationToken);
        if (existing is not null)
        {
            return Ok(ToResponse(existing));
        }

        var job = new Job
        {
            RequestId = request.RequestId,
            RecipientEmail = request.RecipientEmail,
            Subject = request.Subject,
            Body = request.Body,
            Status = JobStatus.Pending,
            NextRunAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        };

        db.Jobs.Add(job);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            existing = await db.Jobs.FirstOrDefaultAsync(j => j.RequestId == request.RequestId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return Ok(ToResponse(existing));
        }

        return Accepted(ToResponse(job));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static NotificationResponse ToResponse(Job job) =>
        new(job.Id, job.RequestId, job.Status.ToString(), job.CreatedAt);
}
