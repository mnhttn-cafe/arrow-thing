using ArrowThing.Server.Data;
using ArrowThing.Server.Models;

namespace ArrowThing.Server.Auth;

public class AuditLogService
{
    readonly AppDbContext _db;
    readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        string eventType,
        Guid? userId,
        string? email,
        string? ipAddress,
        string? detail = null
    )
    {
        _db.AuditLogs.Add(
            new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Event = eventType,
                UserId = userId,
                Email = email,
                IpAddress = ipAddress,
                Detail = detail,
            }
        );
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Audit: {AuditEvent} UserId={UserId} Email={Email} IP={IpAddress} Detail={Detail}",
            eventType,
            userId,
            email,
            ipAddress,
            detail
        );
    }
}
