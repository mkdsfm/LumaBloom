using Spectre.Console;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

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
        TrySetCursorVisible(false);
        var alternateScreenEnabled = false;
        var interactionController = new RuntimeInteractionController(
            _stateStore,
            message => RequestStop(cancellationTokenSource, message));

        try
        {
            if (Console.IsOutputRedirected || Console.IsInputRedirected)
            {
                return RunWithoutLiveDashboard(applicationRunner, cancellationTokenSource.Token);
            }

            var worker = Task.Run(() => applicationRunner(cancellationTokenSource.Token), CancellationToken.None);
            using var app = Terminal.Gui.App.Application.Create();
            app.Init();

            var dashboard = new TerminalGuiDashboard(
                _stateStore,
                interactionController,
                () => app.RequestStop());
            var window = dashboard.Build();
            dashboard.Refresh();
            var lastRenderedVersion = _stateStore.GetVersion();
            var lastRenderedAt = DateTimeOffset.Now;

            app.Keyboard.KeyDown += (_, args) =>
            {
                var keyCode = args.KeyCode;
                if (dashboard.IsModalOpen)
                {
                    if (keyCode == KeyCode.Esc)
                    {
                        dashboard.HandleBack();
                    }
                    else if (keyCode == KeyCode.Enter)
                    {
                        dashboard.HandleEnter();
                    }

                    return;
                }

                if (keyCode == KeyCode.CursorLeft)
                {
                    dashboard.HandleLeft();
                }
                else if (keyCode == KeyCode.CursorRight)
                {
                    dashboard.HandleRight();
                }
                else if (keyCode == KeyCode.CursorUp)
                {
                    interactionController.ApplyIntent(new UiInputIntent(UiInputIntentKind.MoveUp));
                    dashboard.Refresh();
                }
                else if (keyCode == KeyCode.CursorDown)
                {
                    interactionController.ApplyIntent(new UiInputIntent(UiInputIntentKind.MoveDown));
                    dashboard.Refresh();
                }
                else if (keyCode == KeyCode.Esc)
                {
                    dashboard.HandleBack();
                }
                else if (keyCode == KeyCode.Enter)
                {
                    dashboard.HandleEnter();
                }
                else if (keyCode == (KeyCode.Q | KeyCode.CtrlMask))
                {
                    RequestStop(cancellationTokenSource, "Stop requested via Ctrl+Q.");
                    dashboard.RequestStop();
                }
                else if (TryGetDigit(keyCode, out var digit))
                {
                    interactionController.ApplyIntent(UiInputIntent.AppendDigit(digit));
                    dashboard.Refresh();
                }
            };

            app.AddTimeout(PollInterval, () =>
            {
                if (worker.IsCompleted)
                {
                    app.RequestStop();
                    return false;
                }

                _stateStore.SetCompactMode(Console.WindowWidth < 100 || Console.WindowHeight < 24);
                var now = DateTimeOffset.Now;
                var currentVersion = _stateStore.GetVersion();
                if (!dashboard.IsModalOpen && ShouldRefresh(lastRenderedVersion, currentVersion, lastRenderedAt, now))
                {
                    dashboard.Refresh();
                    lastRenderedVersion = currentVersion;
                    lastRenderedAt = now;
                }

                return true;
            });

            app.Run(window);
            cancellationTokenSource.Cancel();
            return worker.GetAwaiter().GetResult();
        }
        finally
        {
            if (alternateScreenEnabled)
            {
                TryExitAlternateScreen();
            }

            TrySetCursorVisible(true);
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

    private void HandleInput(RuntimeInteractionController interactionController)
    {
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Escape)
            {
                var sequence = MouseInputParser.ReadEscapeSequenceIfAvailable(keyInfo);
                if (MouseInputParser.TryParseSgrMouseSequence(sequence, out var click))
                {
                    interactionController.HandleMouseClick(click);
                    continue;
                }
            }

            if (!RuntimeCommandMapper.TryMap(keyInfo, out var intent))
            {
                continue;
            }

            interactionController.ApplyIntent(intent);
        }
    }

    private void RequestStop(CancellationTokenSource cancellationTokenSource, string message)
    {
        _stateStore.SetLifecycle(AppLifecycleState.Stopping, "Stopping...");
        _stateStore.AddEvent(message, RuntimeEventSeverity.Warning);
        cancellationTokenSource.Cancel();
    }

    private int RunWithoutLiveDashboard(Func<CancellationToken, int> applicationRunner, CancellationToken cancellationToken)
    {
        var worker = Task.Run(() => applicationRunner(cancellationToken), CancellationToken.None);
        var lastVersion = -1L;

        while (!worker.IsCompleted)
        {
            var version = _stateStore.GetVersion();
            if (version != lastVersion)
            {
                WritePlainStatus(_stateStore.GetSnapshot());
                lastVersion = version;
            }

            Thread.Sleep(IdleRefreshInterval);
        }

        WritePlainStatus(_stateStore.GetSnapshot());
        return worker.GetAwaiter().GetResult();
    }

    private static void WritePlainStatus(DashboardSnapshot snapshot)
    {
        var sensor = snapshot.LatestSensor is null
            ? "sensor=none"
            : $"sensor={snapshot.LatestSensor.DeviceId}/{snapshot.LatestSensor.SensorId} value={snapshot.LatestSensor.Value} raw={snapshot.LatestSensor.Raw?.ToString() ?? "null"} calibrated={snapshot.LatestSensor.Calibrated}";
        var connection = snapshot.PortName is null
            ? "port=unresolved"
            : $"port={snapshot.PortName}@{snapshot.BaudRate}";

        Console.WriteLine(
            $"[{DateTimeOffset.Now:HH:mm:ss}] {snapshot.LifecycleState}: {snapshot.StatusMessage} | {connection} | calibration={snapshot.CalibrationStatus} | {sensor}");
    }

    private static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (IOException)
        {
            // Redirected/non-interactive consoles can reject cursor operations.
        }
    }

    private static void TryRestoreWindowSize(int width, int height)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (Console.WindowWidth == width && Console.WindowHeight == height)
            {
                return;
            }

            Console.SetWindowSize(width, height);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Windows Terminal may reject resize requests while the user is dragging the window.
        }
        catch (IOException)
        {
            // Pseudo terminals can expose size but reject programmatic resizing.
        }
        catch (PlatformNotSupportedException)
        {
            // Non-Windows terminals cannot be locked this way.
        }
    }

    private static bool TryEnterAlternateScreen()
    {
        try
        {
            Console.Write("\u001b[?1049h");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TryExitAlternateScreen()
    {
        try
        {
            Console.Write("\u001b[?1049l");
        }
        catch (IOException)
        {
            // Ignore cleanup failure in a closing terminal.
        }
    }

    private static bool TryGetDigit(KeyCode keyCode, out char digit)
    {
        if (keyCode is >= KeyCode.D0 and <= KeyCode.D9)
        {
            digit = (char)('0' + (keyCode - KeyCode.D0));
            return true;
        }

        digit = '\0';
        return false;
    }
}
