using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.DeviceReading.Models;
using BrightnessSensor.WindowsBrightness;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class RuntimeStateStore
{
    private const int MaxEvents = 40;
    private static readonly OverviewAction[] OverviewActions =
    [
        OverviewAction.AutoMode,
        OverviewAction.ManualMode,
        OverviewAction.ManualDecreaseFast,
        OverviewAction.ManualDecrease,
        OverviewAction.ManualIncrease,
        OverviewAction.ManualIncreaseFast
    ];

    private static readonly RuntimeScreen[] Screens =
    [
        RuntimeScreen.Overview,
        RuntimeScreen.Calibration,
        RuntimeScreen.Events,
        RuntimeScreen.Diagnostics
    ];

    private readonly object _gate = new();
    private readonly Dictionary<string, MonitorRuntimeSnapshot> _monitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _monitorOrder = [];
    private readonly List<RuntimeEventEntry> _events = [];

    private RuntimeScreen _activeScreen = RuntimeScreen.Overview;
    private UiLanguage _language = UiLanguage.English;
    private bool _isCompact;
    private OverviewAction _focusedOverviewAction = OverviewAction.AutoMode;
    private BrightnessControlMode _brightnessControlMode = BrightnessControlMode.Auto;
    private int _manualBrightnessPercent = 50;
    private int? _lastManualAppliedBrightnessPercent;
    private bool _forceNextAutoBrightnessApply;
    private SettingsSection _activeSettingsSection = SettingsSection.General;
    private CalibrationWizardStep _calibrationWizardStep = CalibrationWizardStep.ChooseTarget;
    private CalibrationAction _focusedCalibrationAction = CalibrationAction.UseCurrentBrightness;
    private CalibrationTargetMode? _calibrationTargetMode;
    private string _calibrationManualInputBuffer = string.Empty;
    private string? _calibrationInputError;
    private AppLifecycleState _lifecycleState = AppLifecycleState.Starting;
    private string _statusMessage = "Starting...";
    private string _calibrationStatus = "Not started";
    private bool _isPaused;
    private bool _showEventLog;
    private bool _autostartEnabled;
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
    private ProcessingSettings? _processingSettings;
    private IReadOnlyList<BrightnessCurvePoint> _brightnessCurve = [];
    private readonly Queue<LanguageUpdateRequest> _languageUpdateRequests = [];
    private readonly Queue<AutostartUpdateRequest> _autostartUpdateRequests = [];
    private readonly Queue<ProcessingUpdateRequest> _processingUpdateRequests = [];
    private readonly Queue<BrightnessCurveUpdateRequest> _brightnessCurveUpdateRequests = [];
    private readonly Queue<TestBrightnessRequest> _testBrightnessRequests = [];
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

    public void SetLanguage(UiLanguage language)
    {
        lock (_gate)
        {
            _language = language;
            IncrementVersion();
        }
    }

    public void RequestLanguageChange(UiLanguage language, string code)
    {
        lock (_gate)
        {
            _language = language;
            _languageUpdateRequests.Enqueue(new LanguageUpdateRequest(language, code));
            IncrementVersion();
        }
    }

    public bool TryConsumeLanguageUpdateRequest(out LanguageUpdateRequest request)
    {
        lock (_gate)
        {
            if (_languageUpdateRequests.Count == 0)
            {
                request = new LanguageUpdateRequest(_language, string.Empty);
                return false;
            }

            request = _languageUpdateRequests.Dequeue();
            return true;
        }
    }

    public void SetAutostartEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (_autostartEnabled == enabled)
            {
                return;
            }

            _autostartEnabled = enabled;
            IncrementVersion();
        }
    }

    public void RequestAutostartChange(bool enabled)
    {
        lock (_gate)
        {
            _autostartUpdateRequests.Enqueue(new AutostartUpdateRequest(enabled));
            IncrementVersion();
        }
    }

    public bool TryConsumeAutostartUpdateRequest(out AutostartUpdateRequest request)
    {
        lock (_gate)
        {
            if (_autostartUpdateRequests.Count == 0)
            {
                request = new AutostartUpdateRequest(_autostartEnabled);
                return false;
            }

            request = _autostartUpdateRequests.Dequeue();
            return true;
        }
    }

    public void SetCompactMode(bool isCompact)
    {
        lock (_gate)
        {
            if (_isCompact == isCompact)
            {
                return;
            }

            _isCompact = isCompact;
            IncrementVersion();
        }
    }

    public void SwitchScreen(RuntimeScreen screen)
    {
        lock (_gate)
        {
            _activeScreen = screen;
            if (screen == RuntimeScreen.Calibration && _calibrationWizardStep == CalibrationWizardStep.Queued)
            {
                ResetCalibrationWizard();
            }

            IncrementVersion();
        }
    }

    public void SetActiveSettingsSection(SettingsSection section)
    {
        lock (_gate)
        {
            _activeSettingsSection = section;
            if (_activeScreen != RuntimeScreen.Calibration)
            {
                _activeScreen = RuntimeScreen.Calibration;
            }

            IncrementVersion();
        }
    }

    public SettingsSection GetActiveSettingsSection()
    {
        lock (_gate)
        {
            return _activeSettingsSection;
        }
    }

    public void RequestProcessingUpdate(ProcessingParameter parameter, string value)
    {
        lock (_gate)
        {
            _processingUpdateRequests.Enqueue(new ProcessingUpdateRequest(parameter, value));
            IncrementVersion();
        }
    }

    public bool TryConsumeProcessingUpdateRequest(out ProcessingUpdateRequest request)
    {
        lock (_gate)
        {
            if (_processingUpdateRequests.Count == 0)
            {
                request = new ProcessingUpdateRequest(ProcessingParameter.AdcMin, string.Empty);
                return false;
            }

            request = _processingUpdateRequests.Dequeue();
            return true;
        }
    }

    public void RequestBrightnessCurveUpdate(int lightPercent, int brightnessPercent)
    {
        lock (_gate)
        {
            _brightnessCurveUpdateRequests.Enqueue(new BrightnessCurveUpdateRequest(
                Math.Clamp(lightPercent, 0, 100),
                Math.Clamp(brightnessPercent, 0, 100)));
            IncrementVersion();
        }
    }

    public bool TryConsumeBrightnessCurveUpdateRequest(out BrightnessCurveUpdateRequest request)
    {
        lock (_gate)
        {
            if (_brightnessCurveUpdateRequests.Count == 0)
            {
                request = new BrightnessCurveUpdateRequest(0, 0);
                return false;
            }

            request = _brightnessCurveUpdateRequests.Dequeue();
            return true;
        }
    }

    public void RequestTestBrightness(int brightnessPercent)
    {
        lock (_gate)
        {
            _testBrightnessRequests.Enqueue(new TestBrightnessRequest(Math.Clamp(brightnessPercent, 0, 100)));
            IncrementVersion();
        }
    }

    public bool TryConsumeTestBrightnessRequest(out TestBrightnessRequest request)
    {
        lock (_gate)
        {
            if (_testBrightnessRequests.Count == 0)
            {
                request = new TestBrightnessRequest(0);
                return false;
            }

            request = _testBrightnessRequests.Dequeue();
            return true;
        }
    }

    public void MoveScreen(int delta)
    {
        lock (_gate)
        {
            var index = Array.IndexOf(Screens, _activeScreen);
            var nextIndex = Wrap(index + delta, Screens.Length);
            _activeScreen = Screens[nextIndex];
            IncrementVersion();
        }
    }

    public void MoveFocus(int delta)
    {
        lock (_gate)
        {
            switch (_activeScreen)
            {
                case RuntimeScreen.Overview:
                    MoveOverviewFocus(delta);
                    break;
                case RuntimeScreen.Calibration:
                    MoveCalibrationFocus(delta);
                    break;
            }
        }
    }

    public BrightnessControlMode BrightnessControlMode
    {
        get
        {
            lock (_gate)
            {
                return _brightnessControlMode;
            }
        }
    }

    public int ManualBrightnessPercent
    {
        get
        {
            lock (_gate)
            {
                return _manualBrightnessPercent;
            }
        }
    }

    public OverviewAction GetFocusedOverviewAction()
    {
        lock (_gate)
        {
            return _focusedOverviewAction;
        }
    }

    public RuntimeScreen GetActiveScreen()
    {
        lock (_gate)
        {
            return _activeScreen;
        }
    }

    public CalibrationAction GetFocusedCalibrationAction()
    {
        lock (_gate)
        {
            return _focusedCalibrationAction;
        }
    }

    public CalibrationWizardStep GetCalibrationWizardStep()
    {
        lock (_gate)
        {
            return _calibrationWizardStep;
        }
    }

    public void BeginCalibrationWizard()
    {
        lock (_gate)
        {
            _activeScreen = RuntimeScreen.Calibration;
            ResetCalibrationWizard();
            IncrementVersion();
        }
    }

    public void SelectCalibrationCurrentBrightness()
    {
        lock (_gate)
        {
            _calibrationTargetMode = CalibrationTargetMode.CurrentBrightness;
            _calibrationWizardStep = CalibrationWizardStep.Review;
            _focusedCalibrationAction = CalibrationAction.Confirm;
            _calibrationInputError = null;
            IncrementVersion();
        }
    }

    public void SelectCalibrationManualTarget()
    {
        lock (_gate)
        {
            _calibrationTargetMode = CalibrationTargetMode.ManualTarget;
            _calibrationWizardStep = CalibrationWizardStep.ManualTarget;
            _focusedCalibrationAction = CalibrationAction.Confirm;
            _calibrationInputError = null;
            IncrementVersion();
        }
    }

    public void SelectCalibrationManualTarget(int targetBrightnessPercent)
    {
        lock (_gate)
        {
            var clamped = Math.Clamp(targetBrightnessPercent, 0, 100);
            _calibrationTargetMode = CalibrationTargetMode.ManualTarget;
            _calibrationManualInputBuffer = clamped.ToString();
            _calibrationWizardStep = CalibrationWizardStep.Review;
            _focusedCalibrationAction = CalibrationAction.Confirm;
            _calibrationInputError = null;
            IncrementVersion();
        }
    }

    public bool TryAppendCalibrationManualDigit(char digit)
    {
        lock (_gate)
        {
            if (_calibrationWizardStep != CalibrationWizardStep.ManualTarget || !char.IsDigit(digit))
            {
                return false;
            }

            var candidate = _calibrationManualInputBuffer + digit;
            if (candidate.Length > 3 || !int.TryParse(candidate, out var parsed) || parsed > 100)
            {
                _calibrationInputError = "calibration.invalid";
                IncrementVersion();
                return false;
            }

            _calibrationManualInputBuffer = candidate;
            _calibrationInputError = null;
            IncrementVersion();
            return true;
        }
    }

    public bool TryBackspaceCalibrationManualInput()
    {
        lock (_gate)
        {
            if (_calibrationWizardStep != CalibrationWizardStep.ManualTarget ||
                _calibrationManualInputBuffer.Length == 0)
            {
                return false;
            }

            _calibrationManualInputBuffer = _calibrationManualInputBuffer[..^1];
            _calibrationInputError = null;
            IncrementVersion();
            return true;
        }
    }

    public bool TryReviewManualCalibrationTarget()
    {
        lock (_gate)
        {
            if (_calibrationWizardStep != CalibrationWizardStep.ManualTarget)
            {
                return false;
            }

            if (!int.TryParse(_calibrationManualInputBuffer, out var parsed) || parsed is < 0 or > 100)
            {
                _calibrationInputError = "calibration.invalid";
                IncrementVersion();
                return false;
            }

            _calibrationTargetMode = CalibrationTargetMode.ManualTarget;
            _calibrationWizardStep = CalibrationWizardStep.Review;
            _focusedCalibrationAction = CalibrationAction.Confirm;
            _calibrationInputError = null;
            IncrementVersion();
            return true;
        }
    }

    public bool TryGetReviewedCalibrationTarget(out int? targetBrightnessPercent)
    {
        lock (_gate)
        {
            if (_calibrationWizardStep != CalibrationWizardStep.Review)
            {
                targetBrightnessPercent = null;
                return false;
            }

            if (_calibrationTargetMode == CalibrationTargetMode.ManualTarget)
            {
                targetBrightnessPercent = int.Parse(_calibrationManualInputBuffer);
            }
            else
            {
                targetBrightnessPercent = null;
            }

            return true;
        }
    }

    public void MarkCalibrationQueued()
    {
        lock (_gate)
        {
            _calibrationWizardStep = CalibrationWizardStep.Queued;
            _focusedCalibrationAction = CalibrationAction.Cancel;
            IncrementVersion();
        }
    }

    public void BackCalibrationWizard()
    {
        lock (_gate)
        {
            switch (_calibrationWizardStep)
            {
                case CalibrationWizardStep.ChooseTarget:
                    _activeScreen = RuntimeScreen.Overview;
                    break;
                case CalibrationWizardStep.ManualTarget:
                    _calibrationWizardStep = CalibrationWizardStep.ChooseTarget;
                    _focusedCalibrationAction = CalibrationAction.SetManualTarget;
                    _calibrationInputError = null;
                    break;
                case CalibrationWizardStep.Review:
                    if (_calibrationTargetMode == CalibrationTargetMode.ManualTarget)
                    {
                        _calibrationWizardStep = CalibrationWizardStep.ManualTarget;
                        _focusedCalibrationAction = CalibrationAction.Confirm;
                    }
                    else
                    {
                        _calibrationWizardStep = CalibrationWizardStep.ChooseTarget;
                        _focusedCalibrationAction = CalibrationAction.UseCurrentBrightness;
                    }

                    break;
                case CalibrationWizardStep.Queued:
                    _activeScreen = RuntimeScreen.Overview;
                    ResetCalibrationWizard();
                    break;
            }

            IncrementVersion();
        }
    }

    public void CancelCalibrationWizard()
    {
        lock (_gate)
        {
            _activeScreen = RuntimeScreen.Overview;
            ResetCalibrationWizard();
            IncrementVersion();
        }
    }

    public void SetLifecycle(AppLifecycleState state, string statusMessage)
    {
        lock (_gate)
        {
            if (_lifecycleState == state && string.Equals(_statusMessage, statusMessage, StringComparison.Ordinal))
            {
                return;
            }

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

    public void SetBrightnessControlMode(BrightnessControlMode mode)
    {
        lock (_gate)
        {
            if (_brightnessControlMode == mode)
            {
                return;
            }

            var previousMode = _brightnessControlMode;
            _brightnessControlMode = mode;
            if (previousMode == BrightnessControlMode.Manual && mode == BrightnessControlMode.Auto)
            {
                _forceNextAutoBrightnessApply = true;
            }

            _statusMessage = mode == BrightnessControlMode.Auto
                ? "Running."
                : $"Manual brightness mode: target {_manualBrightnessPercent}%.";
            IncrementVersion();
        }
    }

    public bool ConsumeForceNextAutoBrightnessApply()
    {
        lock (_gate)
        {
            if (!_forceNextAutoBrightnessApply)
            {
                return false;
            }

            _forceNextAutoBrightnessApply = false;
            return true;
        }
    }

    public void SetManualBrightnessPercent(int brightnessPercent)
    {
        lock (_gate)
        {
            var clamped = Math.Clamp(brightnessPercent, 0, 100);
            if (_manualBrightnessPercent == clamped)
            {
                return;
            }

            _manualBrightnessPercent = clamped;
            if (_brightnessControlMode == BrightnessControlMode.Manual)
            {
                _statusMessage = $"Manual brightness mode: target {_manualBrightnessPercent}%.";
            }

            IncrementVersion();
        }
    }

    public void AdjustManualBrightnessPercent(int delta)
    {
        lock (_gate)
        {
            SetManualBrightnessPercent(_manualBrightnessPercent + delta);
        }
    }

    public void MarkManualBrightnessApplied(int brightnessPercent)
    {
        lock (_gate)
        {
            _lastManualAppliedBrightnessPercent = Math.Clamp(brightnessPercent, 0, 100);
            IncrementVersion();
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
            _processingSettings = settings.Processing;
            _brightnessCurve = settings.Brightness.Curve;
            IncrementVersion();
        }
    }

    public void SetEffectiveSettings(ResolvedAppSettings settings, string profileSummary)
    {
        lock (_gate)
        {
            _processingSettings = settings.Processing;
            _brightnessCurve = settings.Brightness.Curve;
            _profileSummary = profileSummary;
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

    public void ClearLatestSensor()
    {
        lock (_gate)
        {
            if (_latestSensor is null)
            {
                return;
            }

            _latestSensor = null;
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
                _activeScreen,
                _language,
                _isCompact,
                _focusedOverviewAction,
                _calibrationWizardStep,
                _focusedCalibrationAction,
                _calibrationTargetMode,
                _calibrationManualInputBuffer,
                _calibrationInputError,
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
                [.. _events],
                _brightnessControlMode,
                _manualBrightnessPercent,
                _lastManualAppliedBrightnessPercent,
                _activeSettingsSection,
                _brightnessCurve,
                _processingSettings?.AdcMin,
                _processingSettings?.AdcMax,
                _processingSettings?.Invert,
                _processingSettings?.EmaAlpha,
                _processingSettings?.HysteresisPercent,
                _processingSettings?.MaxBrightnessStepPercent,
                _processingSettings?.Gamma,
                _autostartEnabled);
        }
    }

    private void MoveOverviewFocus(int delta)
    {
        var index = Array.IndexOf(OverviewActions, _focusedOverviewAction);
        var nextIndex = Wrap(index + delta, OverviewActions.Length);
        _focusedOverviewAction = OverviewActions[nextIndex];
        IncrementVersion();
    }

    private void MoveCalibrationFocus(int delta)
    {
        var actions = GetCurrentCalibrationActions();
        var index = Array.IndexOf(actions, _focusedCalibrationAction);
        if (index < 0)
        {
            _focusedCalibrationAction = actions[0];
        }
        else
        {
            _focusedCalibrationAction = actions[Wrap(index + delta, actions.Length)];
        }

        IncrementVersion();
    }

    private CalibrationAction[] GetCurrentCalibrationActions()
    {
        return _calibrationWizardStep switch
        {
            CalibrationWizardStep.ChooseTarget =>
            [
                CalibrationAction.UseCurrentBrightness,
                CalibrationAction.SetManualTarget,
                CalibrationAction.Cancel
            ],
            CalibrationWizardStep.ManualTarget =>
            [
                CalibrationAction.Confirm,
                CalibrationAction.Cancel
            ],
            CalibrationWizardStep.Review =>
            [
                CalibrationAction.Confirm,
                CalibrationAction.Cancel
            ],
            _ =>
            [
                CalibrationAction.Cancel
            ]
        };
    }

    private void ResetCalibrationWizard()
    {
        _calibrationWizardStep = CalibrationWizardStep.ChooseTarget;
        _focusedCalibrationAction = CalibrationAction.UseCurrentBrightness;
        _calibrationTargetMode = null;
        _calibrationManualInputBuffer = string.Empty;
        _calibrationInputError = null;
    }

    private static int Wrap(int index, int length)
    {
        return ((index % length) + length) % length;
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
