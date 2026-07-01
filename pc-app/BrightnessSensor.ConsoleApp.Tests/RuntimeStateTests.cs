using BrightnessSensor.BrightnessMath;
using BrightnessSensor.ConsoleApp.Application;
using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
using BrightnessSensor.DeviceReading.Models;
using BrightnessSensor.WindowsBrightness;
using Spectre.Console;
using Xunit;

namespace BrightnessSensor.ConsoleApp.Tests;

public sealed class RuntimeStateTests
{
    [Fact]
    public void LifecycleEvent_UpdatesDashboardState()
    {
        var state = new RuntimeStateStore();

        state.SetLifecycle(AppLifecycleState.Running, "Running.");

        var snapshot = state.GetSnapshot();

        Assert.Equal(AppLifecycleState.Running, snapshot.LifecycleState);
        Assert.Equal("Running.", snapshot.StatusMessage);
    }

    [Fact]
    public void StateVersion_Increments_WhenStateChanges()
    {
        var state = new RuntimeStateStore();
        var initialVersion = state.GetVersion();

        state.SetCalibrationStatus("Collecting samples...");

        Assert.True(state.GetVersion() > initialVersion);
    }

    [Fact]
    public void PauseMode_SuppressesBrightnessWrites_ButKeepsTelemetry()
    {
        var state = new RuntimeStateStore();
        var monitor = new FakeMonitorBrightness();
        state.SetMonitors([monitor]);
        state.TogglePause();

        var processor = new MessageProcessor(state);
        var session = new MonitorSession(monitor, CreateProcessor());
        var message = new SensorMessage
        {
            DeviceId = "esp32c6-01",
            SensorId = "light0",
            Timestamp = 123,
            Value = 800,
            Raw = 1900,
            Calibrated = true
        };

        processor.ProcessMessage(message, [session], MeasurementKind.Normalized1000, CancellationToken.None);
        var snapshot = state.GetSnapshot();

        Assert.Equal(0, monitor.SetBrightnessCalls);
        Assert.NotNull(snapshot.LatestSensor);
        Assert.Equal(800, snapshot.LatestSensor!.Value);
        Assert.Null(snapshot.Monitors.Single().LastAppliedBrightness);
    }

    [Fact]
    public void BrightnessControl_DefaultsToAuto()
    {
        var snapshot = new RuntimeStateStore().GetSnapshot();

        Assert.Equal(BrightnessControlMode.Auto, snapshot.BrightnessControlMode);
        Assert.Equal(50, snapshot.ManualBrightnessPercent);
    }

    [Fact]
    public void SettingsSection_DefaultsToGeneral_AndCanSwitch()
    {
        var state = new RuntimeStateStore();

        Assert.Equal(SettingsSection.General, state.GetSnapshot().ActiveSettingsSection);

        state.SetActiveSettingsSection(SettingsSection.Response);
        var snapshot = state.GetSnapshot();

        Assert.Equal(RuntimeScreen.Calibration, snapshot.ActiveScreen);
        Assert.Equal(SettingsSection.Response, snapshot.ActiveSettingsSection);
    }

    [Fact]
    public void LanguageChange_IsAppliedImmediately_AndQueuedForPersistence()
    {
        var state = new RuntimeStateStore();

        state.RequestLanguageChange(UiLanguage.Spanish, "es");

        Assert.Equal(UiLanguage.Spanish, state.GetSnapshot().Language);
        Assert.True(state.TryConsumeLanguageUpdateRequest(out var request));
        Assert.Equal(UiLanguage.Spanish, request.Language);
        Assert.Equal("es", request.Code);
        Assert.False(state.TryConsumeLanguageUpdateRequest(out _));
    }

    [Fact]
    public void ProcessingUpdate_IsQueuedForPersistenceAndRuntimeApply()
    {
        var state = new RuntimeStateStore();

        state.RequestProcessingUpdate(ProcessingParameter.EmaAlpha, "0.5");

        Assert.True(state.TryConsumeProcessingUpdateRequest(out var request));
        Assert.Equal(ProcessingParameter.EmaAlpha, request.Parameter);
        Assert.Equal("0.5", request.Value);
        Assert.False(state.TryConsumeProcessingUpdateRequest(out _));
    }

    [Fact]
    public void AutostartUpdate_IsQueuedAndSnapshotCanBeUpdated()
    {
        var state = new RuntimeStateStore();

        state.RequestAutostartChange(true);

        Assert.True(state.TryConsumeAutostartUpdateRequest(out var request));
        Assert.True(request.Enabled);
        Assert.False(state.TryConsumeAutostartUpdateRequest(out _));

        state.SetAutostartEnabled(true);

        Assert.True(state.GetSnapshot().AutostartEnabled);
    }

    [Fact]
    public void TestBrightness_IsClampedAndQueued()
    {
        var state = new RuntimeStateStore();

        state.RequestTestBrightness(150);

        Assert.True(state.TryConsumeTestBrightnessRequest(out var request));
        Assert.Equal(100, request.BrightnessPercent);
        Assert.False(state.TryConsumeTestBrightnessRequest(out _));
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(42, 42)]
    [InlineData(110, 100)]
    public void ManualBrightness_ClampsToPercentRange(int requested, int expected)
    {
        var state = new RuntimeStateStore();

        state.SetManualBrightnessPercent(requested);

        Assert.Equal(expected, state.GetSnapshot().ManualBrightnessPercent);
    }

    [Fact]
    public void ManualBrightness_AppliesSamePercentToAllEnabledMonitors()
    {
        var state = new RuntimeStateStore();
        var firstMonitor = new FakeMonitorBrightness();
        var secondMonitor = new FakeMonitorBrightness("Secondary");
        state.SetMonitors([firstMonitor, secondMonitor]);
        state.SetBrightnessControlMode(BrightnessControlMode.Manual);
        state.SetManualBrightnessPercent(42);

        var processor = new MessageProcessor(state);
        var message = new SensorMessage
        {
            DeviceId = "esp32c6-01",
            SensorId = "light0",
            Timestamp = 123,
            Value = 800,
            Raw = 1900,
            Calibrated = true
        };

        processor.ProcessMessage(
            message,
            [new MonitorSession(firstMonitor, CreateProcessor()), new MonitorSession(secondMonitor, CreateProcessor())],
            MeasurementKind.Normalized1000,
            CancellationToken.None);

        Assert.Equal(42, firstMonitor.LastSetBrightness);
        Assert.Equal(42, secondMonitor.LastSetBrightness);
        Assert.All(state.GetSnapshot().Monitors, monitor => Assert.Equal(42, monitor.LastAppliedBrightness));
    }

    [Fact]
    public void SwitchingManualBackToAuto_UsesSensorDrivenProcessorAgain()
    {
        var state = new RuntimeStateStore();
        var monitor = new FakeMonitorBrightness();
        state.SetMonitors([monitor]);
        state.SetBrightnessControlMode(BrightnessControlMode.Manual);
        state.SetManualBrightnessPercent(42);
        state.SetBrightnessControlMode(BrightnessControlMode.Auto);

        var processor = new MessageProcessor(state);
        var message = new SensorMessage
        {
            DeviceId = "esp32c6-01",
            SensorId = "light0",
            Timestamp = 123,
            Value = 1000,
            Raw = 1900,
            Calibrated = true
        };

        processor.ProcessMessage(
            message,
            [new MonitorSession(monitor, CreateProcessor())],
            MeasurementKind.Normalized1000,
            CancellationToken.None);

        Assert.Equal(BrightnessControlMode.Auto, state.GetSnapshot().BrightnessControlMode);
        Assert.Equal(100, monitor.LastSetBrightness);
    }

    [Fact]
    public void SwitchingManualBackToAuto_ForcesNextAutoApplyAfterManualWrite()
    {
        var state = new RuntimeStateStore();
        var monitor = new FakeMonitorBrightness();
        var session = new MonitorSession(monitor, CreateProcessor());
        var processor = new MessageProcessor(state);
        var message = new SensorMessage
        {
            DeviceId = "esp32c6-01",
            SensorId = "light0",
            Timestamp = 123,
            Value = 1000,
            Raw = 1900,
            Calibrated = true
        };

        processor.ProcessMessage(message, [session], MeasurementKind.Normalized1000, CancellationToken.None);
        Assert.Equal(100, monitor.LastSetBrightness);

        state.SetBrightnessControlMode(BrightnessControlMode.Manual);
        state.SetManualBrightnessPercent(42);
        processor.ProcessMessage(message, [session], MeasurementKind.Normalized1000, CancellationToken.None);
        Assert.Equal(42, monitor.LastSetBrightness);

        state.SetBrightnessControlMode(BrightnessControlMode.Auto);
        processor.ProcessMessage(message, [session], MeasurementKind.Normalized1000, CancellationToken.None);

        Assert.Equal(3, monitor.SetBrightnessCalls);
        Assert.Equal(100, monitor.LastSetBrightness);
        Assert.Equal(100, state.GetSnapshot().Monitors.Single().LastAppliedBrightness);
    }

    [Fact]
    public void RecalibrationRequest_TransitionsPendingState()
    {
        var state = new RuntimeStateStore();

        Assert.True(state.TryRequestRecalibration(42));

        var pendingSnapshot = state.GetSnapshot();
        Assert.True(pendingSnapshot.RecalibrationPending);
        Assert.Equal(42, pendingSnapshot.PendingCalibrationBrightnessPercent);

        Assert.True(state.TryConsumeRecalibrationRequest(out var targetBrightnessPercent));
        Assert.Equal(42, targetBrightnessPercent);

        var consumedSnapshot = state.GetSnapshot();
        Assert.False(consumedSnapshot.RecalibrationPending);
        Assert.Null(consumedSnapshot.PendingCalibrationBrightnessPercent);
    }

    [Fact]
    public void CalibrationInput_AllowsCustomBrightnessAndCommit()
    {
        var state = new RuntimeStateStore();
        state.BeginCalibrationInput();

        Assert.True(state.TryAppendCalibrationInputDigit('6'));
        Assert.True(state.TryAppendCalibrationInputDigit('5'));
        Assert.True(state.TryCommitCalibrationInput(out var targetBrightnessPercent));

        Assert.Equal(65, targetBrightnessPercent);
        Assert.False(state.GetSnapshot().IsCalibrationInputActive);
    }

    [Fact]
    public void CalibrationInput_BlankCommitUsesCurrentBrightness()
    {
        var state = new RuntimeStateStore();
        state.BeginCalibrationInput();

        Assert.True(state.TryCommitCalibrationInput(out var targetBrightnessPercent));

        Assert.Null(targetBrightnessPercent);
        Assert.False(state.GetSnapshot().IsCalibrationInputActive);
    }

    [Fact]
    public void UiState_SwitchesScreensAndKeepsFocus()
    {
        var state = new RuntimeStateStore();

        state.SwitchScreen(RuntimeScreen.Diagnostics);
        state.MoveScreen(1);

        var snapshot = state.GetSnapshot();

        Assert.Equal(RuntimeScreen.Overview, snapshot.ActiveScreen);
        Assert.Equal(OverviewAction.AutoMode, snapshot.FocusedOverviewAction);
    }

    [Fact]
    public void OverviewFocus_MovesAcrossVisibleActions()
    {
        var state = new RuntimeStateStore();

        state.MoveFocus(1);
        state.MoveFocus(1);

        var snapshot = state.GetSnapshot();

        Assert.Equal(OverviewAction.ManualDecreaseFast, snapshot.FocusedOverviewAction);
    }

    [Fact]
    public void CalibrationWizard_ManualTarget_ReachesReview()
    {
        var state = new RuntimeStateStore();

        state.BeginCalibrationWizard();
        state.SelectCalibrationManualTarget();
        Assert.True(state.TryAppendCalibrationManualDigit('6'));
        Assert.True(state.TryAppendCalibrationManualDigit('5'));
        Assert.True(state.TryReviewManualCalibrationTarget());

        var snapshot = state.GetSnapshot();

        Assert.Equal(RuntimeScreen.Calibration, snapshot.ActiveScreen);
        Assert.Equal(CalibrationWizardStep.Review, snapshot.CalibrationWizardStep);
        Assert.Equal(CalibrationTargetMode.ManualTarget, snapshot.CalibrationTargetMode);
        Assert.True(state.TryGetReviewedCalibrationTarget(out var targetBrightnessPercent));
        Assert.Equal(65, targetBrightnessPercent);
    }

    [Fact]
    public void CalibrationWizard_RejectsManualTargetAbove100()
    {
        var state = new RuntimeStateStore();

        state.BeginCalibrationWizard();
        state.SelectCalibrationManualTarget();

        Assert.True(state.TryAppendCalibrationManualDigit('1'));
        Assert.True(state.TryAppendCalibrationManualDigit('0'));
        Assert.False(state.TryAppendCalibrationManualDigit('1'));

        var snapshot = state.GetSnapshot();

        Assert.Equal("10", snapshot.CalibrationManualInputBuffer);
        Assert.Equal("calibration.invalid", snapshot.CalibrationInputError);
    }

    [Fact]
    public void Localization_HasRequiredKeysForAllLanguages()
    {
        foreach (var language in new[] { UiLanguage.English, UiLanguage.Russian, UiLanguage.Spanish })
        {
            foreach (var key in Localizer.RequiredKeys)
            {
                Assert.True(Localizer.HasTranslation(language, key), $"{language} missing {key}");
            }
        }
    }

    [Theory]
    [InlineData("en", (int)UiLanguage.English)]
    [InlineData("ru", (int)UiLanguage.Russian)]
    [InlineData("es", (int)UiLanguage.Spanish)]
    [InlineData("unknown", (int)UiLanguage.English)]
    public void UiLanguageResolver_ResolvesConfiguredLanguage(string configured, int expected)
    {
        Assert.Equal((UiLanguage)expected, UiLanguageResolver.Resolve(configured));
    }

    [Fact]
    public void MonitorDisable_IsReflectedInDashboardState()
    {
        var state = new RuntimeStateStore();
        state.SetMonitors([new FakeMonitorBrightness()]);

        state.RecordMonitorDisabled("Fake", "Primary", "WMI failure");

        var monitor = state.GetSnapshot().Monitors.Single();
        Assert.False(monitor.IsEnabled);
        Assert.Equal("Disabled after brightness control failure", monitor.LastStatus);
        Assert.Equal("WMI failure", monitor.LastError);
    }

    [Theory]
    [InlineData(ConsoleKey.LeftArrow, (int)UiInputIntentKind.MovePrevious)]
    [InlineData(ConsoleKey.RightArrow, (int)UiInputIntentKind.MoveNext)]
    [InlineData(ConsoleKey.UpArrow, (int)UiInputIntentKind.MoveUp)]
    [InlineData(ConsoleKey.DownArrow, (int)UiInputIntentKind.MoveDown)]
    [InlineData(ConsoleKey.Enter, (int)UiInputIntentKind.Activate)]
    [InlineData(ConsoleKey.Escape, (int)UiInputIntentKind.Back)]
    [InlineData(ConsoleKey.Backspace, (int)UiInputIntentKind.Backspace)]
    public void CommandMapping_MapsNavigationKeys(ConsoleKey key, int expectedKind)
    {
        var keyInfo = new ConsoleKeyInfo('\0', key, shift: false, alt: false, control: false);

        var mapped = RuntimeCommandMapper.TryMap(keyInfo, out var actual);

        Assert.True(mapped);
        Assert.Equal((UiInputIntentKind)expectedKind, actual.Kind);
    }

    [Theory]
    [InlineData(ConsoleKey.Q)]
    [InlineData(ConsoleKey.P)]
    [InlineData(ConsoleKey.C)]
    [InlineData(ConsoleKey.L)]
    public void CommandMapping_IgnoresRemovedLegacyHotkeys(ConsoleKey key)
    {
        var keyInfo = new ConsoleKeyInfo('\0', key, shift: false, alt: false, control: false);

        var mapped = RuntimeCommandMapper.TryMap(keyInfo, out _);

        Assert.False(mapped);
    }

    [Fact]
    public void MouseParser_ParsesSgrClick()
    {
        var parsed = MouseInputParser.TryParseSgrMouseSequence("\u001b[<0;12;3M", out var click);

        Assert.True(parsed);
        Assert.Equal(12, click.X);
        Assert.Equal(3, click.Y);
    }

    [Fact]
    public void Interaction_MouseClick_SwitchesTabs()
    {
        var state = new RuntimeStateStore();
        var controller = new RuntimeInteractionController(state, _ => { });

        controller.HandleMouseClick(new UiMouseClick(60, 2));

        Assert.Equal(RuntimeScreen.Events, state.GetSnapshot().ActiveScreen);
    }

    [Fact]
    public void Interaction_LeftRightSwitchTabsAndUpDownMoveInTabFocus()
    {
        var state = new RuntimeStateStore();
        var controller = new RuntimeInteractionController(state, _ => { });

        controller.ApplyIntent(new UiInputIntent(UiInputIntentKind.MoveNext));
        Assert.Equal(RuntimeScreen.Calibration, state.GetSnapshot().ActiveScreen);

        controller.ApplyIntent(new UiInputIntent(UiInputIntentKind.MovePrevious));
        Assert.Equal(RuntimeScreen.Overview, state.GetSnapshot().ActiveScreen);

        controller.ApplyIntent(new UiInputIntent(UiInputIntentKind.MoveDown));
        Assert.Equal(RuntimeScreen.Overview, state.GetSnapshot().ActiveScreen);
        Assert.Equal(OverviewAction.ManualMode, state.GetSnapshot().FocusedOverviewAction);
    }

    [Fact]
    public void Interaction_MouseClick_ActivatesOverviewManualMode()
    {
        var state = new RuntimeStateStore();
        var controller = new RuntimeInteractionController(state, _ => { });

        controller.HandleMouseClick(new UiMouseClick(30, 8));

        Assert.Equal(BrightnessControlMode.Manual, state.GetSnapshot().BrightnessControlMode);
    }

    [Fact]
    public void Interaction_MouseClick_OverviewDoesNotOpenCalibrationWizard()
    {
        var state = new RuntimeStateStore();
        var controller = new RuntimeInteractionController(state, _ => { });

        controller.HandleMouseClick(new UiMouseClick(80, 8));

        var snapshot = state.GetSnapshot();
        Assert.Equal(RuntimeScreen.Overview, snapshot.ActiveScreen);
        Assert.Equal(CalibrationWizardStep.ChooseTarget, snapshot.CalibrationWizardStep);
        Assert.Equal(BrightnessControlMode.Manual, snapshot.BrightnessControlMode);
    }

    [Fact]
    public void Interaction_MouseClick_CalibrationCurrentBrightnessQueuesRequest()
    {
        var state = new RuntimeStateStore();
        var controller = new RuntimeInteractionController(state, _ => { });

        state.BeginCalibrationWizard();
        controller.HandleMouseClick(new UiMouseClick(10, 8));
        controller.HandleMouseClick(new UiMouseClick(10, 8));

        var snapshot = state.GetSnapshot();
        Assert.True(snapshot.RecalibrationPending);
        Assert.Equal(CalibrationWizardStep.Queued, snapshot.CalibrationWizardStep);
        Assert.Null(snapshot.PendingCalibrationBrightnessPercent);
    }

    [Fact]
    public void Interaction_Keyboard_CalibrationManualTargetQueuesRequest()
    {
        var state = new RuntimeStateStore();
        var controller = new RuntimeInteractionController(state, _ => { });

        state.BeginCalibrationWizard();
        controller.ActivateCalibrationAction(CalibrationAction.SetManualTarget);
        controller.ApplyIntent(UiInputIntent.AppendDigit('4'));
        controller.ApplyIntent(UiInputIntent.AppendDigit('2'));
        controller.ActivateCalibrationAction(CalibrationAction.Confirm);
        controller.ActivateCalibrationAction(CalibrationAction.Confirm);

        var snapshot = state.GetSnapshot();
        Assert.True(snapshot.RecalibrationPending);
        Assert.Equal(42, snapshot.PendingCalibrationBrightnessPercent);
    }

    [Fact]
    public void Renderer_Diagnostics_EscapesProfileSummaryMarkupCharacters()
    {
        var snapshot = new DashboardSnapshot(
            RuntimeScreen.Diagnostics,
            UiLanguage.English,
            IsCompact: false,
            OverviewAction.AutoMode,
            CalibrationWizardStep.ChooseTarget,
            CalibrationAction.UseCurrentBrightness,
            CalibrationTargetMode: null,
            CalibrationManualInputBuffer: string.Empty,
            CalibrationInputError: null,
            AppLifecycleState.Running,
            StatusMessage: "Running.",
            CalibrationStatus: "Startup calibration complete.",
            IsPaused: false,
            ShowEventLog: false,
            RecalibrationPending: false,
            PendingCalibrationBrightnessPercent: null,
            IsCalibrationInputActive: false,
            CalibrationInputBuffer: string.Empty,
            PortName: "COM6",
            BaudRate: 115200,
            ConnectionSummary: "Resolved [COM] port.",
            ProfileId: "esp32c6-analog-ky018",
            ProfileSummary: "Effective settings: adc=[0..1000], calibration={enabled=True}",
            MeasurementKind: "Normalized1000",
            IsGenericProfile: false,
            LatestSensor: new SensorRuntimeSnapshot(
                "esp32c6-01",
                "light0",
                123,
                1000,
                456,
                Calibrated: true,
                DateTimeOffset.Now),
            Monitors:
            [
                new MonitorRuntimeSnapshot(
                    "WMI",
                    "Primary [Internal]",
                    IsEnabled: true,
                    LastAppliedBrightness: 50,
                    LastRequestedBrightness: 52,
                    LastNormalized: 1,
                    LastFiltered: 0.9,
                    LastUpdatedAt: DateTimeOffset.Now,
                    LastError: null,
                    LastStatus: "Applied [50]%")
            ],
            Events: []);

        Assert.Null(Record.Exception(() => AnsiConsole.Write(new ConsoleDashboardRenderer().Build(snapshot))));
    }

    [Fact]
    public void Renderer_Overview_LumaBloomDashboard_UsesSupportedMarkupStyles()
    {
        var snapshot = new DashboardSnapshot(
            RuntimeScreen.Overview,
            UiLanguage.English,
            IsCompact: false,
            OverviewAction.AutoMode,
            CalibrationWizardStep.ChooseTarget,
            CalibrationAction.UseCurrentBrightness,
            CalibrationTargetMode: null,
            CalibrationManualInputBuffer: string.Empty,
            CalibrationInputError: null,
            AppLifecycleState.Running,
            StatusMessage: "Running.",
            CalibrationStatus: "Startup calibration complete.",
            IsPaused: false,
            ShowEventLog: false,
            RecalibrationPending: false,
            PendingCalibrationBrightnessPercent: null,
            IsCalibrationInputActive: false,
            CalibrationInputBuffer: string.Empty,
            PortName: "COM6",
            BaudRate: 115200,
            ConnectionSummary: "Resolved COM6.",
            ProfileId: "esp32c6-analog-ky018",
            ProfileSummary: "Effective settings: adc=[0..1000]",
            MeasurementKind: "Normalized1000",
            IsGenericProfile: false,
            LatestSensor: new SensorRuntimeSnapshot(
                "esp32c6-01",
                "light0",
                123,
                670,
                684,
                Calibrated: true,
                DateTimeOffset.Now),
            Monitors: [],
            Events: []);

        Assert.Null(Record.Exception(() => AnsiConsole.Write(new ConsoleDashboardRenderer().Build(snapshot))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(500)]
    [InlineData(800)]
    [InlineData(1000)]
    public void Renderer_Overview_RendersAllSunStates(int normalizedValue)
    {
        var snapshot = new DashboardSnapshot(
            RuntimeScreen.Overview,
            UiLanguage.English,
            IsCompact: false,
            OverviewAction.AutoMode,
            CalibrationWizardStep.ChooseTarget,
            CalibrationAction.UseCurrentBrightness,
            CalibrationTargetMode: null,
            CalibrationManualInputBuffer: string.Empty,
            CalibrationInputError: null,
            AppLifecycleState.Running,
            StatusMessage: "Running.",
            CalibrationStatus: "Startup calibration complete.",
            IsPaused: false,
            ShowEventLog: false,
            RecalibrationPending: false,
            PendingCalibrationBrightnessPercent: null,
            IsCalibrationInputActive: false,
            CalibrationInputBuffer: string.Empty,
            PortName: "COM6",
            BaudRate: 115200,
            ConnectionSummary: "Resolved COM6.",
            ProfileId: "esp32c6-analog-ky018",
            ProfileSummary: "Effective settings: adc=[0..1000]",
            MeasurementKind: "Normalized1000",
            IsGenericProfile: false,
            LatestSensor: new SensorRuntimeSnapshot(
                "esp32c6-01",
                "light0",
                123,
                normalizedValue,
                684,
                Calibrated: true,
                DateTimeOffset.Now),
            Monitors: [],
            Events: []);

        Assert.Null(Record.Exception(() => AnsiConsole.Write(new ConsoleDashboardRenderer().Build(snapshot))));
    }

    [Fact]
    public void Renderer_Overview_CompactMode_UsesResizeSafeLayout()
    {
        var snapshot = new DashboardSnapshot(
            RuntimeScreen.Overview,
            UiLanguage.English,
            IsCompact: true,
            OverviewAction.AutoMode,
            CalibrationWizardStep.ChooseTarget,
            CalibrationAction.UseCurrentBrightness,
            CalibrationTargetMode: null,
            CalibrationManualInputBuffer: string.Empty,
            CalibrationInputError: null,
            AppLifecycleState.Running,
            StatusMessage: "Running.",
            CalibrationStatus: "Startup calibration complete.",
            IsPaused: false,
            ShowEventLog: false,
            RecalibrationPending: false,
            PendingCalibrationBrightnessPercent: null,
            IsCalibrationInputActive: false,
            CalibrationInputBuffer: string.Empty,
            PortName: "COM6",
            BaudRate: 115200,
            ConnectionSummary: "Resolved COM6.",
            ProfileId: "esp32c6-analog-ky018",
            ProfileSummary: "Effective settings: adc=[0..1000]",
            MeasurementKind: "Normalized1000",
            IsGenericProfile: false,
            LatestSensor: new SensorRuntimeSnapshot(
                "esp32c6-01",
                "light0",
                123,
                1000,
                684,
                Calibrated: true,
                DateTimeOffset.Now),
            Monitors: [],
            Events: []);

        Assert.Null(Record.Exception(() => AnsiConsole.Write(new ConsoleDashboardRenderer().Build(snapshot))));
    }

    [Fact]
    public void CommandMapping_IgnoresUnknownHotkeys()
    {
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.X, shift: false, alt: false, control: false);

        var mapped = RuntimeCommandMapper.TryMap(keyInfo, out _);

        Assert.False(mapped);
    }

    [Fact]
    public void ShouldRefresh_ReturnsFalse_ForUnchangedVersionBeforeIdleDeadline()
    {
        var now = DateTimeOffset.UtcNow;

        var shouldRefresh = ConsoleDashboardHost.ShouldRefresh(
            lastRenderedVersion: 5,
            currentVersion: 5,
            lastRenderedAt: now,
            now: now.AddMilliseconds(500));

        Assert.False(shouldRefresh);
    }

    [Fact]
    public void ShouldRefresh_ReturnsTrue_ForChangedVersion()
    {
        var now = DateTimeOffset.UtcNow;

        var shouldRefresh = ConsoleDashboardHost.ShouldRefresh(
            lastRenderedVersion: 5,
            currentVersion: 6,
            lastRenderedAt: now,
            now: now.AddMilliseconds(10));

        Assert.True(shouldRefresh);
    }

    private static BrightnessProcessor CreateProcessor()
    {
        return new BrightnessProcessor(
            new BrightnessComputationSettings(
                InputIsNormalized1000: true,
                AdcMin: 0,
                AdcMax: 1000,
                Invert: false,
                EmaAlpha: 1.0,
                HysteresisPercent: 1,
                MaxBrightnessStepPercent: 2,
                Gamma: 1.0,
                MinBrightnessPercent: 10,
                MaxBrightnessPercent: 100));
    }

    private sealed class FakeMonitorBrightness(string name = "Primary") : IMonitorBrightness
    {
        public string Source => "Fake";

        public string Name { get; } = name;

        public int SetBrightnessCalls { get; private set; }

        public int? LastSetBrightness { get; private set; }

        public bool TryGetBrightness(out int brightnessPercent, out string? error)
        {
            brightnessPercent = 50;
            error = null;
            return true;
        }

        public bool TrySetBrightness(int brightnessPercent, out string? error)
        {
            SetBrightnessCalls++;
            LastSetBrightness = brightnessPercent;
            error = null;
            return true;
        }
    }
}
