using Spectre.Console;
using Spectre.Console.Rendering;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class ConsoleDashboardRenderer
{
    public IRenderable Build(DashboardSnapshot snapshot)
    {
        return BuildLayout(snapshot);
    }

    private static Rows BuildLayout(DashboardSnapshot snapshot)
    {
        var rows = new List<IRenderable>
        {
            BuildHeader(snapshot),
            BuildStatusGrid(snapshot),
            BuildSensorAndCalibrationGrid(snapshot),
            BuildMonitorsPanel(snapshot)
        };

        if (snapshot.ShowEventLog)
        {
            rows.Add(BuildEventsPanel(snapshot));
        }

        rows.Add(BuildFooter(snapshot));
        return new Rows([.. rows]);
    }

    private static IRenderable BuildHeader(DashboardSnapshot snapshot)
    {
        var lifecycleColor = snapshot.LifecycleState switch
        {
            AppLifecycleState.Running => "green",
            AppLifecycleState.Error => "red",
            AppLifecycleState.Stopping => "yellow",
            _ => "deepskyblue1"
        };

        var pausedBadge = snapshot.IsPaused ? " [black on yellow]PAUSED[/]" : string.Empty;
        var pendingBadge = snapshot.RecalibrationPending ? " [black on yellow]RECAL PENDING[/]" : string.Empty;
        var inputBadge = snapshot.IsCalibrationInputActive ? " [black on deepskyblue1]CAL INPUT[/]" : string.Empty;
        var text = new Markup(
            $"[bold]Brightness Sensor Console[/] [{lifecycleColor}]{Markup.Escape(snapshot.LifecycleState.ToString())}[/]{pausedBadge}{pendingBadge}{inputBadge}\n" +
            $"{Markup.Escape(snapshot.StatusMessage)}");

        return new Panel(text)
            .Border(BoxBorder.Rounded)
            .Header("Runtime");
    }

    private static IRenderable BuildStatusGrid(DashboardSnapshot snapshot)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Connection");
        table.AddColumn("Profile");
        table.AddColumn("Mode");

        table.AddRow(
            EscapeOrFallback(snapshot.ConnectionSummary, "Resolving...") +
            (snapshot.PortName is null || snapshot.BaudRate is null
                ? string.Empty
                : $"\n{Markup.Escape(snapshot.PortName)} @ {snapshot.BaudRate}"),
            EscapeOrFallback(snapshot.ProfileSummary, "Waiting for telemetry...") +
            (snapshot.ProfileId is null
                ? string.Empty
                : $"\nprofileId={Markup.Escape(snapshot.ProfileId)}"),
            $"measurement={EscapeOrFallback(snapshot.MeasurementKind, "unknown")}\n" +
            $"generic={Markup.Escape(snapshot.IsGenericProfile?.ToString() ?? "n/a")}");

        return table;
    }

    private static IRenderable BuildSensorAndCalibrationGrid(DashboardSnapshot snapshot)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(
            BuildSensorPanel(snapshot),
            BuildCalibrationPanel(snapshot));
        return grid;
    }

    private static IRenderable BuildSensorPanel(DashboardSnapshot snapshot)
    {
        var sensor = snapshot.LatestSensor;
        var table = new Table().Border(TableBorder.MinimalHeavyHead).Expand();
        table.AddColumn("Field");
        table.AddColumn("Value");

        if (sensor is null)
        {
            table.AddRow("Latest telemetry", "Waiting for first valid message");
        }
        else
        {
            table.AddRow("deviceId", Markup.Escape(sensor.DeviceId));
            table.AddRow("sensorId", Markup.Escape(sensor.SensorId));
            table.AddRow("value", sensor.Value.ToString());
            table.AddRow("raw", sensor.Raw?.ToString() ?? "null");
            table.AddRow("calibrated", sensor.Calibrated.ToString());
            table.AddRow("device ts", sensor.DeviceTimestamp.ToString());
            table.AddRow("received", sensor.ReceivedAt.LocalDateTime.ToString("HH:mm:ss"));
        }

        return new Panel(table)
            .Header("Sensor")
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildCalibrationPanel(DashboardSnapshot snapshot)
    {
        var lines = new List<string>
        {
            snapshot.CalibrationStatus
        };

        if (snapshot.RecalibrationPending)
        {
            lines.Add(snapshot.PendingCalibrationBrightnessPercent.HasValue
                ? $"Pending target brightness: {snapshot.PendingCalibrationBrightnessPercent}%"
                : "Pending target brightness: current monitor brightness");
        }

        if (snapshot.IsCalibrationInputActive)
        {
            var currentValue = string.IsNullOrWhiteSpace(snapshot.CalibrationInputBuffer)
                ? "(current brightness)"
                : $"{snapshot.CalibrationInputBuffer}%";
            lines.Add("Type target brightness 0..100, Enter to confirm, Esc to cancel.");
            lines.Add($"Current input: {currentValue}");
        }

        var text = new Markup(Markup.Escape(string.Join(Environment.NewLine, lines)));
        return new Panel(text)
            .Header("Calibration")
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildMonitorsPanel(DashboardSnapshot snapshot)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Monitor");
        table.AddColumn("State");
        table.AddColumn("Brightness");
        table.AddColumn("Signal");
        table.AddColumn("Last update");

        if (snapshot.Monitors.Count == 0)
        {
            table.AddRow("No monitors", "No brightness-capable monitors detected", "-", "-", "-");
        }
        else
        {
            foreach (var monitor in snapshot.Monitors)
            {
                var brightness = monitor.LastAppliedBrightness.HasValue
                    ? $"{monitor.LastAppliedBrightness}%"
                    : "n/a";
                var requested = monitor.LastRequestedBrightness.HasValue
                    ? $" req={monitor.LastRequestedBrightness}%"
                    : string.Empty;
                var signal = monitor.LastNormalized.HasValue && monitor.LastFiltered.HasValue
                    ? $"norm={monitor.LastNormalized.Value:F3}\nfilt={monitor.LastFiltered.Value:F3}"
                    : "n/a";
                var lastUpdate = monitor.LastUpdatedAt?.LocalDateTime.ToString("HH:mm:ss") ?? "-";
                var state = Markup.Escape(monitor.LastStatus);
                if (!string.IsNullOrWhiteSpace(monitor.LastError))
                {
                    state += $"\n[red]{Markup.Escape(monitor.LastError)}[/]";
                }

                table.AddRow(
                    $"{Markup.Escape(monitor.Source)}\n{Markup.Escape(monitor.Name)}",
                    state,
                    $"{brightness}{requested}",
                    signal,
                    lastUpdate);
            }
        }

        return new Panel(table)
            .Header("Monitors")
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildEventsPanel(DashboardSnapshot snapshot)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Time");
        table.AddColumn("Level");
        table.AddColumn("Message");

        foreach (var entry in snapshot.Events.TakeLast(12))
        {
            var color = entry.Severity switch
            {
                RuntimeEventSeverity.Success => "green",
                RuntimeEventSeverity.Warning => "yellow",
                RuntimeEventSeverity.Error => "red",
                _ => "grey"
            };

            table.AddRow(
                entry.Timestamp.LocalDateTime.ToString("HH:mm:ss"),
                $"[{color}]{Markup.Escape(entry.Severity.ToString())}[/]",
                Markup.Escape(entry.Message));
        }

        if (snapshot.Events.Count == 0)
        {
            table.AddRow("-", "-", "No events yet");
        }

        return new Panel(table)
            .Header("Recent Events")
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildFooter(DashboardSnapshot snapshot)
    {
        var logState = snapshot.ShowEventLog ? "on" : "off";
        var pauseState = snapshot.IsPaused ? "resume" : "pause";
        var footerText = snapshot.IsCalibrationInputActive
            ? "[bold]Calibration Input[/]  digits=target %  [aqua]Backspace[/]=delete  [aqua]Enter[/]=confirm  [aqua]Esc[/]=cancel"
            : $"[bold]Hotkeys[/]  [aqua]q[/]=quit  [aqua]p[/]={pauseState}  [aqua]c[/]=calibrate target  [aqua]l[/]=logs ({logState})";
        var footer = new Markup(footerText);

        return new Panel(footer)
            .Border(BoxBorder.Rounded);
    }

    private static string EscapeOrFallback(string? value, string fallback)
    {
        return Markup.Escape(string.IsNullOrWhiteSpace(value) ? fallback : value);
    }
}
