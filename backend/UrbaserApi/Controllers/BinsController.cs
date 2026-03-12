using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbaserApi.Data;
using UrbaserApi.DTOs;
using UrbaserApi.Models;
using UrbaserApi.Telemetry;

namespace UrbaserApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BinsController : ControllerBase
{
    private readonly UrbaserDbContext _db;
    private readonly ILogger<BinsController> _logger;

    public BinsController(UrbaserDbContext db, ILogger<BinsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IList<BinSummary>>> GetBins(
        [FromQuery] string? type = null,
        [FromQuery] string? status = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var query = _db.WasteBins.AsQueryable();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<BinType>(type, true, out var binType))
            query = query.Where(b => b.Type == binType);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BinStatus>(status, true, out var binStatus))
            query = query.Where(b => b.Status == binStatus);

        var bins = await query
            .Select(b => new BinSummary(
                b.Id, b.Name, b.Location, b.Type.ToString(), b.Status.ToString(),
                b.CurrentFillLevel, b.CapacityLiters, b.LastCollected, b.LastSensorReading))
            .ToListAsync();

        sw.Stop();
        _logger.LogInformation("GetBins completed: Type={Type}, Status={Status}, Count={Count}, Duration={Duration}ms",
            type, status, bins.Count, sw.ElapsedMilliseconds);

        return Ok(bins);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BinDetail>> GetBin(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var bin = await _db.WasteBins
            .Include(b => b.FillLevelReadings.OrderByDescending(r => r.RecordedAt).Take(24))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bin is null)
        {
            _logger.LogWarning("GetBin: Bin {BinId} not found", id);
            return NotFound();
        }

        var detail = new BinDetail(
            bin.Id, bin.Name, bin.Location, bin.Type.ToString(), bin.Status.ToString(),
            bin.CurrentFillLevel, bin.CapacityLiters, bin.LastCollected, bin.LastSensorReading,
            bin.Latitude, bin.Longitude, bin.InstalledDate,
            bin.FillLevelReadings.OrderByDescending(r => r.RecordedAt)
                .Select(r => new FillLevelReadingDto(r.FillLevel, r.Temperature, r.BatteryLevel, r.RecordedAt))
                .ToList());

        sw.Stop();
        using var activity = UrbaserActivitySource.StartBinGetDetail(bin.Id, bin.CurrentFillLevel);
        _logger.LogInformation("GetBin: BinId={BinId}, Name={Name}, FillLevel={FillLevel}, Duration={Duration}ms",
            id, bin.Name, bin.CurrentFillLevel, sw.ElapsedMilliseconds);

        return Ok(detail);
    }

    [HttpGet("{id:int}/readings")]
    public async Task<ActionResult<IList<FillLevelReadingDto>>> GetBinReadings(int id, [FromQuery] int hours = 24)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var binExists = await _db.WasteBins.AnyAsync(b => b.Id == id);
        if (!binExists)
        {
            _logger.LogWarning("GetBinReadings: Bin {BinId} not found", id);
            return NotFound();
        }

        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var readings = await _db.FillLevelReadings
            .Where(r => r.BinId == id && r.RecordedAt >= cutoff)
            .OrderByDescending(r => r.RecordedAt)
            .Select(r => new FillLevelReadingDto(r.FillLevel, r.Temperature, r.BatteryLevel, r.RecordedAt))
            .ToListAsync();

        sw.Stop();
        _logger.LogInformation("GetBinReadings: BinId={BinId}, Hours={Hours}, Count={Count}, Duration={Duration}ms",
            id, hours, readings.Count, sw.ElapsedMilliseconds);

        return Ok(readings);
    }
}
