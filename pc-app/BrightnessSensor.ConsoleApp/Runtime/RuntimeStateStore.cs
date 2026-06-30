using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.DeviceReading.Models;
using BrightnessSensor.WindowsBrightness;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class RuntimeStateStore
{
    private const int MaxEvents = 40;

    private readonly object _gate = new();
    private readonly Dictionary<string, MonitorRuntimeSnapshot> _monitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _monitorOrder = [];
    private readonly List<RuntimeEventEntry> _events = [];

    private AppLifecycleState _lifecycleState = AppLifecycleState.Starting;
    private string _statusMessage = "Starting...";
    private string _calibrationStatus = "Not started";
    private bool _isPaused;
    private bool _showEventLog;
    private int _recalibrationPending;
    private int? _pendingCalibrationBrightnessPercent;
    private bool _isCalibrationInputActive;
    private string _calibrationInputBuffer = string.Empty;
    private string? _portName;
    private int? _baudRate;
    private string? _connectionSummary;
    private string? _profileId;
    private string? _profileSummary;
    private string? _measurementKind;
    private bool? _isGenericProfile;
    private SensorRuntimeSnapshot? _latestSensor;
    private long _version;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _isPaused;
            }
        }
    }

    public long GetVersion()
    {
        lock (_gate)
        {
            return _version;
        }
    }

    public bool IsCalibrationInputActive
    {
        get
        {
            lock (_gate)
            {
                return _isCalibrationInputActive;
            }
        }
    }

    public void SetLifecycle(AppLifecycleState state, string statusMessage)
    {
        lock (_gate)
        {
            _lifecycleState = state;
            _statusMessage = statusMessage;
            IncrementVersion();
        }
    }

    public void SetCalibrationStatus(string calibrationStatus)
    {
        lock (_gate)
        {
            _calibrationStatus = calibrationStatus;
            IncrementVersion();
        }
    }

    public bool TogglePause()
    {
        lock (_gate)
        {
            _isPaused = !_isPaused;
            _statusMessage = _isPaused
                ? "Paused: telemetry continues, brightness writes are suspended."
                : "Running.";
            IncrementVersion();
            return _isPaused;
        }
    }

    public bool ToggleLogVisibility()
    {
        lock (_gate)
        {
            _showEventLog = !_showEventLog;
            IncrementVersion();
            return _showEventLog;
        }
    }

    public bool TryRequestRecalibration()
    {
        return TryRequestRecalibration(null);
    }

    public bool TryRequestRecalibration(int? targetBrightnessPercent)
    {
        if (Interlocked.CompareExchange(ref _recalibrationPending, 1, 0) == 0)
        {
            lock (_gate)
            {
                _pendingCalibrationBrightnessPercent = targetBrightnessPercent;
                IncrementVersion();
            }

            return true;
        }

        return false;
    }

    public bool TryConsumeRecalibrationRequest(out int? targetBrightnessPercent)
    {
        if (Interlocked.Exchange(ref _recalibrationPending, 0) == 1)
        {
            lock (_gate)
            {
                targetBrightnessPercent = _pendingCalibrationBrightnessPercent;
                _pendingCalibrationBrightnessPercent = null;
                IncrementVersion();
            }

            return true;
        }

        targetBrightnessPercent = null;
        return false;
    }

    public void BeginCalibrationInput()
    {
        lock (_gate)
        {
            _isCalibrationInputActive = true;
            _calibrationInputBuffer = string.Empty;
            IncrementVersion();
        }
    }

    public void CancelCalibrationInput()
    {
        lock (_gate)
        {
            _isCalibrationInputActive = false;
            _calibrationInputBuffer = string.Empty;
            IncrementVersion();
        }
    }

    public bool TryAppendCalibrationInputDigit(char digit)
    {
        lock (_gate)
        {
            if (!_isCalibrationInputActive || !char.IsDigit(digit))
            {
                return false;
            }

            var candidate = _calibrationInputBuffer + digit;
            if (candidate.Length > 3)
            {
                return false;
            }

            if (int.TryParse(candidate, out var parsed) && parsed <= 100)
            {
                _calibrationInputBuffer = candidate;
                IncrementVersion();
                return true;
            }

            return false;
        }
    }

    public bool TryBackspaceCalibrationInput()
    {
        lock (_gate)
        {
            if (!_isCalibrationInputActive || _calibrationInputBuffer.Length == 0)
            {
                return false;
            }

            _calibrationInputBuffer = _calibrationInputBuffer[..^1];
            IncrementVersion();
            return true;
        }
    }

    public bool TryCommitCalibrationInput(out int? targetBrightnessPercent)
    {
        lock (_gate)
        {
            if (!_isCalibrationInputActive)
            {
                targetBrightnessPercent = null;
                return false;
            }

            targetBrightnessPercent = string.IsNullOrWhiteSpace(_calibrationInputBuffer)
                ? null
                : int.Parse(_calibrationInputBuffer);
            _isCalibrationInputActive = false;
            _calibrationInputBuffer = string.Empty;
            IncrementVersion();
            return true;
        }
    }

    public void SetConnection(string portName, int baudRate, string connectionSummary)
    {
        lock (_gate)
        {
            _portName = portName;
            _baudRate = baudRate;
            _connectionSummary = connectionSummary;
            IncrementVersion();
        }
    }

    public void SetProfile(ResolvedAppSettings settings, string profileSummary)
    {
        lock (_gate)
        {
            _profileId = settings.ProfileId;
            _profileSummary = profileSummary;
            _measurementKind = settings.MeasurementKind.ToString();
            _isGenericProfile = settings.IsGenericProfile;
            IncrementVersion();
        }
    }

    public void SetLatestSensor(SensorMessage sensorMessage)
    {
        lock (_gate)
        {
            _latestSensor = new SensorRuntimeSnapshot(
                sensorMessage.DeviceId,
                sensorMessage.SensorId,
                sensorMessage.Timestamp,
                sensorMessage.Value,
                sensorMessage.Raw,
                sensorMessage.Calibrated,
                DateTimeOffset.Now);
            IncrementVersion();
        }
    }

    public void SetMonitors(IReadOnlyList<IMonitorBrightness> monitors)
    {
        lock (_gate)
        {
            _monitors.Clear();
            _monitorOrder.Clear();

            foreach (var monitor in monitors)
            {
                var key = CreateMonitorKey(monitor.Source, monitor.Name);
                _monitorOrder.Add(key);
                _monitors[key] = new MonitorRuntimeSnapshot(
                    monitor.Source,
                    monitor.Name,
                    IsEnabled: true,
                    LastAppliedBrightness: null,
                    LastRequestedBrightness: null,
                    LastNormalized: null,
                    LastFiltered: null,
                    LastUpdatedAt: null,
                    LastError: null,
                    LastStatus: "Ready");
            }

            IncrementVersion();
        }
    }

    public void MarkMonitorCalibration(string source, string name, int screenBrightness, int sensorAverage, int sampleCount)
    {
        UpdateMonitor(source, name, snapshot => snapshot with
        {
            LastAppliedBrightness = screenBrightness,
            LastUpdatedAt = DateTimeOffset.Now,
            LastStatus = $"Calibrated with screen={screenBrightness}% sensorAvg={sensorAverage} ({sampleCount} samples)",
            LastError = null
        });
    }

    public void RecordBrightnessApplied(
        string source,
        string name,
        int requestedBrightness,
        int targetBrightness,
        double normalized,
        double filtered)
    {
        UpdateMonitor(source, name, snapshot => snapshot with
        {
            LastRequestedBrightness = requestedBrightness,
            LastAppliedBrightness = targetBrightness,
            LastNormalized = normalized,
            LastFiltered = filtered,
            LastUpdatedAt = DateTimeOffset.Now,
            LastStatus = requestedBrightness == targetBrightness
                ? $"Applied {targetBrightness}%"
                : $"Applied {targetBrightness}% toward {requestedBrightness}%",
            LastError = null
        });
    }

    public void RecordMonitorDisabled(string source, string name, string error)
    {
        UpdateMonitor(source, name, snapshot => snapshot with
        {
            IsEnabled = false,
            LastUpdatedAt = DateTimeOffset.Now,
            LastStatus = "Disabled after brightness control failure",
            LastError = error
        });
    }

    public void RecordMonitorStatus(string source, string name, string status)
    {
        UpdateMonitor(source, name, snapshot => snapshot with
        {
            LastUpdatedAt = DateTimeOffset.Now,
            LastStatus = status
        });
    }

    public void AddEvent(string message, RuntimeEventSeverity severity = RuntimeEventSeverity.Info)
    {
        lock (_gate)
        {
            _events.Add(new RuntimeEventEntry(DateTimeOffset.Now, severity, message));
            if (_events.Count > MaxEvents)
            {
                _events.RemoveRange(0, _events.Count - MaxEvents);
            }

            IncrementVersion();
        }
    }

    public DashboardSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var monitors = _monitorOrder
                .Where(key => _monitors.ContainsKey(key))
                .Select(key => _monitors[key])
                .ToList();

            return new DashboardSnapshot(
                _lifecycleState,
                _statusMessage,
                _calibrationStatus,
                _isPaused,
                _showEventLog,
                _recalibrationPending == 1,
                _pendingCalibrationBrightnessPercent,
                _isCalibrationInputActive,
                _calibrationInputBuffer,
                _portName,
                _baudRate,
                _connectionSummary,
                _profileId,
                _profileSummary,
                _measurementKind,
                _isGenericProfile,
                _latestSensor,
                monitors,
                [.. _events]);
        }
    }

    private void UpdateMonitor(string source, string name, Func<MonitorRuntimeSnapshot, MonitorRuntimeSnapshot> update)
    {
        lock (_gate)
        {
            var key = CreateMonitorKey(source, name);
            if (!_monitors.TryGetValue(key, out var monitor))
            {
                monitor = new MonitorRuntimeSnapshot(
                    source,
                    name,
                    IsEnabled: true,
                    LastAppliedBrightness: null,
                    LastRequestedBrightness: null,
                    LastNormalized: null,
                    LastFiltered: null,
                    LastUpdatedAt: null,
                    LastError: null,
                    LastStatus: "Discovered late");
                _monitors[key] = monitor;
                _monitorOrder.Add(key);
            }

            _monitors[key] = update(monitor);
            IncrementVersion();
        }
    }

    private void IncrementVersion()
    {
        _version++;
    }

    private static string CreateMonitorKey(string source, string name)
    {
        return $"{source}:{name}";
    }
}
