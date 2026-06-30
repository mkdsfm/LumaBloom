using BrightnessSensor.BrightnessMath;
using BrightnessSensor.ConsoleApp.Application;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
using BrightnessSensor.DeviceReading.Models;
using BrightnessSensor.WindowsBrightness;
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
    [InlineData(ConsoleKey.Q, (int)RuntimeCommand.Stop)]
    [InlineData(ConsoleKey.P, (int)RuntimeCommand.TogglePause)]
    [InlineData(ConsoleKey.C, (int)RuntimeCommand.Recalibrate)]
    [InlineData(ConsoleKey.L, (int)RuntimeCommand.ToggleLogVisibility)]
    public void CommandMapping_MapsKnownHotkeys(ConsoleKey key, int expectedValue)
    {
        var keyInfo = new ConsoleKeyInfo('\0', key, shift: false, alt: false, control: false);

        var mapped = RuntimeCommandMapper.TryMap(keyInfo, out var actual);

        Assert.True(mapped);
        Assert.Equal((RuntimeCommand)expectedValue, actual);
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

    private sealed class FakeMonitorBrightness : IMonitorBrightness
    {
        public string Source => "Fake";

        public string Name => "Primary";

        public int SetBrightnessCalls { get; private set; }

        public bool TryGetBrightness(out int brightnessPercent, out string? error)
        {
            brightnessPercent = 50;
            error = null;
            return true;
        }

        public bool TrySetBrightness(int brightnessPercent, out string? error)
        {
            SetBrightnessCalls++;
            error = null;
            return true;
        }
    }
}
