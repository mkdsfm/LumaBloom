using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
using BrightnessSensor.DeviceReading.Models;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class MessageProcessor(RuntimeStateStore stateStore)
{
    private readonly RuntimeStateStore _stateStore = stateStore;

    public void ProcessMessage(
        SensorMessage sensorMessage,
        IReadOnlyList<MonitorSession> monitorSessions,
        MeasurementKind measurementKind,
        CancellationToken cancellationToken)
    {
        _stateStore.SetLatestSensor(sensorMessage);

        if (monitorSessions.Count == 0)
        {
            return;
        }

        if (_stateStore.IsPaused)
        {
            return;
        }

        var monitorTasks = new List<Task>();
        foreach (var session in monitorSessions)
        {
            if (!session.IsEnabled)
            {
                continue;
            }

            var task = Task.Run(() =>
            {
                var evaluationResult = session.Processor.Evaluate(sensorMessage.Value);
                if (!evaluationResult.ShouldApply)
                {
                    _stateStore.RecordMonitorStatus(
                        session.Monitor.Source,
                        session.Monitor.Name,
                        "Waiting for a brightness delta outside hysteresis.");
                    return;
                }

                if (!session.Monitor.TrySetBrightness(evaluationResult.TargetBrightness, out var error))
                {
                    session.Disable();
                    _stateStore.RecordMonitorDisabled(session.Monitor.Source, session.Monitor.Name, error ?? "Unknown error");
                    _stateStore.AddEvent(
                        $"Brightness update failed ({session.Monitor.Source}:{session.Monitor.Name}): {error}",
                        RuntimeEventSeverity.Error);
                    return;
                }

                _stateStore.RecordBrightnessApplied(
                    session.Monitor.Source,
                    session.Monitor.Name,
                    evaluationResult.RequestedBrightness,
                    evaluationResult.TargetBrightness,
                    evaluationResult.Normalized,
                    evaluationResult.Filtered);
                var sourceLabel = measurementKind == MeasurementKind.Normalized1000 ? "norm1000" : "raw";
                var requestedLabel = evaluationResult.RequestedBrightness == evaluationResult.TargetBrightness
                    ? string.Empty
                    : $" requested={evaluationResult.RequestedBrightness}%";
                _stateStore.AddEvent(
                    $"[{sourceLabel}] {session.Monitor.Source}:{session.Monitor.Name} value={sensorMessage.Value} norm={evaluationResult.Normalized:F3} filt={evaluationResult.Filtered:F3}{requestedLabel} -> brightness={evaluationResult.TargetBrightness}%",
                    RuntimeEventSeverity.Info);
            }, cancellationToken);

            monitorTasks.Add(task);
        }

        Task.WaitAll([.. monitorTasks], cancellationToken);
    }
}
