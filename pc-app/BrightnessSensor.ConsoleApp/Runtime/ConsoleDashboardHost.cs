using Spectre.Console;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class ConsoleDashboardHost(RuntimeStateStore stateStore)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan IdleRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly ConsoleDashboardRenderer _renderer = new();
    private readonly RuntimeStateStore _stateStore = stateStore;

    public int Run(Func<CancellationToken, int> applicationRunner)
    {
        ArgumentNullException.ThrowIfNull(applicationRunner);

        using var cancellationTokenSource = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            _stateStore.SetLifecycle(AppLifecycleState.Stopping, "Stopping...");
            _stateStore.AddEvent("Stop requested via Ctrl+C.", RuntimeEventSeverity.Warning);
            cancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += handler;
        Console.CursorVisible = false;

        try
        {
            var worker = Task.Run(() => applicationRunner(cancellationTokenSource.Token), CancellationToken.None);
            var initialSnapshot = _stateStore.GetSnapshot();
            var initialVersion = _stateStore.GetVersion();

            return AnsiConsole
                .Live(_renderer.Build(initialSnapshot))
                .AutoClear(false)
                .Start(ctx =>
            {
                var lastRenderedVersion = initialVersion;
                var lastRenderedAt = DateTimeOffset.UtcNow;

                while (!worker.IsCompleted)
                {
                    HandleInput(cancellationTokenSource);

                    var currentVersion = _stateStore.GetVersion();
                    var now = DateTimeOffset.UtcNow;
                    if (ShouldRefresh(lastRenderedVersion, currentVersion, lastRenderedAt, now))
                    {
                        ctx.UpdateTarget(_renderer.Build(_stateStore.GetSnapshot()));
                        ctx.Refresh();
                        lastRenderedVersion = currentVersion;
                        lastRenderedAt = now;
                    }

                    Thread.Sleep(PollInterval);
                }

                ctx.UpdateTarget(_renderer.Build(_stateStore.GetSnapshot()));
                ctx.Refresh();
                return worker.GetAwaiter().GetResult();
            });
        }
        finally
        {
            Console.CursorVisible = true;
            Console.CancelKeyPress -= handler;
        }
    }

    internal static bool ShouldRefresh(
        long lastRenderedVersion,
        long currentVersion,
        DateTimeOffset lastRenderedAt,
        DateTimeOffset now)
    {
        if (currentVersion != lastRenderedVersion)
        {
            return true;
        }

        return (now - lastRenderedAt) >= IdleRefreshInterval;
    }

    private void HandleInput(CancellationTokenSource cancellationTokenSource)
    {
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            if (_stateStore.IsCalibrationInputActive)
            {
                HandleCalibrationInputModeKey(keyInfo);
                continue;
            }

            if (!RuntimeCommandMapper.TryMap(keyInfo, out var command))
            {
                continue;
            }

            switch (command)
            {
                case RuntimeCommand.Stop:
                    _stateStore.SetLifecycle(AppLifecycleState.Stopping, "Stopping...");
                    _stateStore.AddEvent("Stop requested from keyboard.", RuntimeEventSeverity.Warning);
                    cancellationTokenSource.Cancel();
                    break;
                case RuntimeCommand.TogglePause:
                    var paused = _stateStore.TogglePause();
                    _stateStore.AddEvent(
                        paused
                            ? "Paused brightness application."
                            : "Resumed brightness application.",
                        RuntimeEventSeverity.Info);
                    break;
                case RuntimeCommand.ToggleLogVisibility:
                    var visible = _stateStore.ToggleLogVisibility();
                    _stateStore.AddEvent(
                        visible
                            ? "Expanded recent event log."
                            : "Collapsed recent event log.",
                        RuntimeEventSeverity.Info);
                    break;
                case RuntimeCommand.Recalibrate:
                    _stateStore.BeginCalibrationInput();
                    _stateStore.SetCalibrationStatus("Enter desired brightness 0..100, then press Enter. Leave blank to use current monitor brightness.");
                    _stateStore.AddEvent("Calibration input mode opened.", RuntimeEventSeverity.Info);
                    break;
            }
        }
    }

    private void HandleCalibrationInputModeKey(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Enter:
                if (_stateStore.TryCommitCalibrationInput(out var targetBrightnessPercent))
                {
                    if (_stateStore.TryRequestRecalibration(targetBrightnessPercent))
                    {
                        _stateStore.AddEvent(
                            targetBrightnessPercent.HasValue
                                ? $"Recalibration requested with target brightness {targetBrightnessPercent}%."
                                : "Recalibration requested using current monitor brightness.",
                            RuntimeEventSeverity.Warning);
                    }
                    else
                    {
                        _stateStore.AddEvent("Recalibration already pending.", RuntimeEventSeverity.Warning);
                    }
                }

                break;
            case ConsoleKey.Escape:
                _stateStore.CancelCalibrationInput();
                _stateStore.SetCalibrationStatus("Calibration input canceled.");
                _stateStore.AddEvent("Calibration input canceled.", RuntimeEventSeverity.Warning);
                break;
            case ConsoleKey.Backspace:
                _stateStore.TryBackspaceCalibrationInput();
                break;
            default:
                if (char.IsDigit(keyInfo.KeyChar))
                {
                    var appended = _stateStore.TryAppendCalibrationInputDigit(keyInfo.KeyChar);
                    if (!appended)
                    {
                        _stateStore.AddEvent("Brightness target must stay in range 0..100.", RuntimeEventSeverity.Warning);
                    }
                }

                break;
        }
    }
}
