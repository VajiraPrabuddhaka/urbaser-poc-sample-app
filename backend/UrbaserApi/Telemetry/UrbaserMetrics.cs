using System.Diagnostics.Metrics;

namespace UrbaserApi.Telemetry;

public class UrbaserMetrics : IDisposable
{
    public const string MeterName = "UrbaserApi";

    private readonly Meter _meter;
    private readonly Counter<long> _collectionsScheduled;
    private readonly Counter<long> _collectionsCompleted;
    private readonly Counter<long> _alertsGenerated;
    private readonly Counter<long> _alertsAcknowledged;
    private readonly Histogram<double> _collectionDuration;
    private readonly Histogram<double> _fillLevelAtCollection;

    private double _currentAverageFillLevel;
    private int _currentActiveTrucks;

    public UrbaserMetrics()
    {
        _meter = new Meter(MeterName);

        _collectionsScheduled = _meter.CreateCounter<long>(
            "urbaser.collections.scheduled",
            description: "Number of collections scheduled");

        _collectionsCompleted = _meter.CreateCounter<long>(
            "urbaser.collections.completed",
            description: "Number of collections completed");

        _alertsGenerated = _meter.CreateCounter<long>(
            "urbaser.alerts.generated",
            description: "Number of alerts generated");

        _alertsAcknowledged = _meter.CreateCounter<long>(
            "urbaser.alerts.acknowledged",
            description: "Number of alerts acknowledged");

        _collectionDuration = _meter.CreateHistogram<double>(
            "urbaser.collection.duration_minutes",
            unit: "minutes",
            description: "Duration of waste collections in minutes");

        _fillLevelAtCollection = _meter.CreateHistogram<double>(
            "urbaser.bin.fill_level_at_collection",
            unit: "%",
            description: "Bin fill level when collected");

        _meter.CreateObservableGauge<double>(
            "urbaser.bins.average_fill_level",
            () => _currentAverageFillLevel,
            unit: "%",
            description: "Average fill level across all active bins");

        _meter.CreateObservableGauge<int>(
            "urbaser.trucks.active",
            () => _currentActiveTrucks,
            description: "Number of active trucks");
    }

    public void RecordCollectionScheduled() => _collectionsScheduled.Add(1);
    public void RecordCollectionCompleted(double durationMinutes, double fillLevel)
    {
        _collectionsCompleted.Add(1);
        _collectionDuration.Record(durationMinutes);
        _fillLevelAtCollection.Record(fillLevel);
    }
    public void RecordAlertGenerated(string alertType, string severity) =>
        _alertsGenerated.Add(1, new KeyValuePair<string, object?>("alert.type", alertType),
                                new KeyValuePair<string, object?>("alert.severity", severity));
    public void RecordAlertAcknowledged() => _alertsAcknowledged.Add(1);
    public void UpdateAverageFillLevel(double avgFillLevel) => _currentAverageFillLevel = avgFillLevel;
    public void UpdateActiveTrucks(int count) => _currentActiveTrucks = count;

    public void Dispose() => _meter.Dispose();
}
