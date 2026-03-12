using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbaserApi.Data;
using UrbaserApi.DTOs;
using UrbaserApi.Models;
using UrbaserApi.Telemetry;

namespace UrbaserApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly UrbaserDbContext _db;
    private readonly ILogger<AlertsController> _logger;
    private readonly UrbaserMetrics _metrics;

    public AlertsController(UrbaserDbContext db, ILogger<AlertsController> logger, UrbaserMetrics metrics)
    {
        _db = db;
        _logger = logger;
        _metrics = metrics;
    }

    private static AlertSummary ToSummary(Alert a) =>
        new(a.Id, a.Type.ToString(), a.Severity.ToString(), a.Message,
            a.BinId, a.Bin?.Name, a.TruckId, a.Truck?.Name,
            a.IsAcknowledged, a.AcknowledgedBy, a.CreatedAt, a.AcknowledgedAt);

    [HttpGet]
    public async Task<ActionResult<IList<AlertSummary>>> GetAlerts(
        [FromQuery] bool? acknowledged = null,
        [FromQuery] string? severity = null)
    {
        var query = _db.Alerts.Include(a => a.Bin).Include(a => a.Truck).AsQueryable();

        if (acknowledged.HasValue)
            query = query.Where(a => a.IsAcknowledged == acknowledged.Value);

        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<AlertSeverity>(severity, true, out var sev))
            query = query.Where(a => a.Severity == sev);

        var alerts = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        _logger.LogInformation("GetAlerts: Acknowledged={Acknowledged}, Severity={Severity}, Count={Count}",
            acknowledged, severity, alerts.Count);

        return Ok(alerts.Select(ToSummary).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AlertSummary>> GetAlert(int id)
    {
        var alert = await _db.Alerts
            .Include(a => a.Bin)
            .Include(a => a.Truck)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alert is null)
        {
            _logger.LogWarning("GetAlert: Alert {AlertId} not found", id);
            return NotFound();
        }

        return Ok(ToSummary(alert));
    }

    [HttpPut("{id:int}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(int id, [FromBody] AcknowledgeAlertRequest request)
    {
        var alert = await _db.Alerts.FindAsync(id);
        if (alert is null) return NotFound();
        if (alert.IsAcknowledged) return BadRequest("Alert already acknowledged");

        alert.IsAcknowledged = true;
        alert.AcknowledgedBy = request.AcknowledgedBy;
        alert.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _metrics.RecordAlertAcknowledged();
        _logger.LogInformation("Alert acknowledged: AlertId={AlertId}, Type={Type}, AcknowledgedBy={AcknowledgedBy}",
            id, alert.Type, request.AcknowledgedBy);
        return NoContent();
    }

    [HttpPut("acknowledge-all")]
    public async Task<IActionResult> AcknowledgeAllAlerts([FromBody] AcknowledgeAlertRequest request)
    {
        var openAlerts = await _db.Alerts.Where(a => !a.IsAcknowledged).ToListAsync();
        var now = DateTime.UtcNow;

        foreach (var alert in openAlerts)
        {
            alert.IsAcknowledged = true;
            alert.AcknowledgedBy = request.AcknowledgedBy;
            alert.AcknowledgedAt = now;
        }

        await _db.SaveChangesAsync();

        foreach (var _ in openAlerts) _metrics.RecordAlertAcknowledged();
        _logger.LogInformation("All alerts acknowledged: Count={Count}, AcknowledgedBy={AcknowledgedBy}",
            openAlerts.Count, request.AcknowledgedBy);
        return NoContent();
    }
}
