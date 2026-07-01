using Spectre.Console;
using Spectre.Console.Rendering;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class ConsoleDashboardRenderer
{
    public IRenderable Build(DashboardSnapshot snapshot)
    {
        var localizer = new Localizer(snapshot.Language);
        if (snapshot.ActiveScreen == RuntimeScreen.Overview)
        {
            if (snapshot.IsCompact)
            {
                return BuildCompactOverview(snapshot, localizer);
            }

            return BuildLumaBloomDashboard(snapshot, localizer);
        }

        return new Rows(
            BuildNavigation(snapshot, localizer),
            BuildActiveScreen(snapshot, localizer),
            BuildHint(localizer));
    }

    private static IRenderable BuildLumaBloomDashboard(DashboardSnapshot snapshot, Localizer localizer)
    {
        var percent = GetNormalizedSensorPercent(snapshot);
        var ambient = percent.HasValue ? $"{percent.Value}%" : "--%";
        var adc = snapshot.LatestSensor?.Raw ?? snapshot.LatestSensor?.Value;
        var adcText = adc?.ToString() ?? "---";
        var lux = percent.HasValue ? $"{(int)Math.Round(percent.Value * 3.2, MidpointRounding.AwayFromZero)} lx" : "--- lx";
        var connected = snapshot.LatestSensor is not null;
        var status = connected ? "OK" : "WAIT";
        var mode = snapshot.IsPaused ? "Paused" : "Auto";
        var sunState = GetSunState(percent);
        var lastRead = snapshot.LatestSensor?.ReceivedAt.LocalDateTime.ToString("HH:mm:ss") ?? "--:--:--";
        var uptime = CreateApproxUptime(snapshot);
        var progress = BuildBrightnessBar(percent);
        var sun = BuildSunMarkup(percent);

        const string titleColor = "deepskyblue1";
        const string green = "green";
        const string purple = "mediumpurple1";
        const string blue = "deepskyblue1";
        const string yellow = "yellow";
        const int leftWidth = 24;

        var lines = new List<string>
        {
            $"[white]LumaBloom v1.2.0[/]                       [grey]|[/]  [{titleColor}]Ambient Light Sensor[/]                 [grey]|[/]  [white]{DateTime.Now:HH:mm:ss}[/]  [grey]|[/]  [{green}]* AUTO[/]",
            $"[grey]{new string('-', 112)}[/]",
            JoinDashboardLine(PadMarkupRight($"[{titleColor}]> Dashboard[/]", 11, leftWidth), $"      [white]Ambient Light[/]        [grey]|[/]        [white]ADC Value[/]        [grey]|[/]        [white]Lux (est.)[/]"),
            JoinDashboardLine(PadMarkupRight("[white]  Settings[/]", 10, leftWidth), $"         [{green}]{ambient,5}[/]            [grey]|[/]          [{yellow}]{adcText,5}[/]          [grey]|[/]          [{blue}]{lux,7}[/]"),
            JoinDashboardLine(PadMarkupRight("[white]  Calibration[/]", 13, leftWidth), string.Empty),
            JoinDashboardLine(PadMarkupRight("[white]  Display[/]", 9, leftWidth), $"[grey]{new string('-', 86)}[/]"),
            JoinDashboardLine(PadMarkupRight("[white]  About[/]", 7, leftWidth), $"                            [{purple}]Sun State[/]"),
            JoinDashboardLine(PadMarkupRight("[white]  Exit[/]", 6, leftWidth), sun[0]),
            JoinDashboardLine(new string(' ', leftWidth), sun[1]),
            JoinDashboardLine(new string(' ', leftWidth), sun[2]),
            JoinDashboardLine(BuildStatusCardLine(0, mode, status, green), sun[3]),
            JoinDashboardLine(BuildStatusCardLine(1, mode, status, green), sun[4]),
            JoinDashboardLine(BuildStatusCardLine(2, mode, status, green), sun[5]),
            JoinDashboardLine(BuildStatusCardLine(3, mode, status, green), string.Empty),
            JoinDashboardLine(BuildStatusCardLine(4, mode, status, green), $"                         [{green}]Sun is {sunState}[/]"),
            JoinDashboardLine(BuildStatusCardLine(5, mode, status, green), $"[grey]{new string('-', 86)}[/]"),
            JoinDashboardLine(BuildStatusCardLine(6, mode, status, green), $"                             [{purple}]Brightness[/]"),
            JoinDashboardLine(BuildStatusCardLine(7, mode, status, green), $"  [white]{Markup.Escape("[")}[/] {progress} [white]{Markup.Escape("]")}[/]  [white]{ambient}[/]"),
            JoinDashboardLine(BuildStatusCardLine(8, mode, status, green), $"[grey]{new string('-', 86)}[/]"),
            JoinDashboardLine(new string(' ', leftWidth), $"  [{purple}]Sensor Info[/]                              [grey]|[/]   [{purple}]System Info[/]"),
            JoinDashboardLine(new string(' ', leftWidth), $"  [grey]Type:[/]       [white]{Markup.Escape(snapshot.MeasurementKind ?? "Unknown"),-18}[/] [grey]|[/]   [grey]Uptime:[/]    [{blue}]{uptime}[/]"),
            JoinDashboardLine(new string(' ', leftWidth), $"  [grey]Address:[/]    [white]{Markup.Escape(snapshot.LatestSensor?.SensorId ?? "n/a"),-18}[/] [grey]|[/]   [grey]Mode:[/]      [{green}]{Markup.Escape(mode)}[/]"),
            JoinDashboardLine(new string(' ', leftWidth), $"  [grey]Last read:[/]  [white]{lastRead,-18}[/] [grey]|[/]   [grey]Free Mem:[/]  [{blue}]n/a[/]"),
            JoinDashboardLine(new string(' ', leftWidth), string.Empty),
            JoinDashboardLine(PadMarkupRight($"   [{purple}]LumaBloom[/]", 13, leftWidth), $"      [grey]{Markup.Escape(localizer["nav.hint"])}[/]"),
            JoinDashboardLine(PadMarkupRight($" [{purple}]by nightroot[/]", 13, leftWidth), string.Empty)
        };

        return new Panel(new Markup(string.Join(Environment.NewLine, lines)))
            .Border(BoxBorder.Rounded)
            .Expand();

        static string JoinDashboardLine(string left, string right)
        {
            return $"{left} [grey]|[/] {right}";
        }
    }

    private static IRenderable BuildCompactOverview(DashboardSnapshot snapshot, Localizer localizer)
    {
        var percent = GetNormalizedSensorPercent(snapshot);
        var ambient = percent.HasValue ? $"{percent.Value}%" : "--%";
        var adc = snapshot.LatestSensor?.Raw ?? snapshot.LatestSensor?.Value;
        var adcText = adc?.ToString() ?? "---";
        var lux = percent.HasValue ? $"{(int)Math.Round(percent.Value * 3.2, MidpointRounding.AwayFromZero)} lx" : "--- lx";
        var connected = snapshot.LatestSensor is not null ? "connected" : "disconnected";
        var statusColor = snapshot.LatestSensor is not null ? "green" : "yellow";
        var mode = snapshot.IsPaused ? "Paused" : "Auto";
        var sunState = GetSunState(percent);
        var sun = BuildSunMarkup(percent);
        var progress = BuildBrightnessBar(percent, total: 30);

        var lines = new[]
        {
            $"[white]LumaBloom v1.2.0[/]  [deepskyblue1]Ambient Light Sensor[/]  [white]{DateTime.Now:HH:mm:ss}[/]  [green]* AUTO[/]",
            $"[grey]{new string('-', 76)}[/]",
            $"[deepskyblue1]> Dashboard[/]  [white]Settings[/]  [white]Calibration[/]  [white]Display[/]  [white]About[/]  [white]Exit[/]",
            string.Empty,
            $"[grey]Sensor:[/] [{statusColor}]{connected}[/]  [grey]Ambient:[/] [green]{ambient}[/]  [grey]ADC:[/] [yellow]{adcText}[/]  [grey]Lux:[/] [deepskyblue1]{lux}[/]",
            $"[grey]Mode:[/] [green]{Markup.Escape(mode)}[/]  [grey]Status:[/] [{statusColor}]{(snapshot.LatestSensor is null ? "WAIT" : "OK")}[/]",
            $"[grey]{new string('-', 76)}[/]",
            $"[mediumpurple1]Sun State[/]  [green]Sun is {sunState}[/]",
            sun[0],
            sun[1],
            sun[2],
            sun[3],
            sun[4],
            $"[grey]{new string('-', 76)}[/]",
            $"[mediumpurple1]Brightness[/]  [white]{Markup.Escape("[")}[/] {progress} [white]{Markup.Escape("]")}[/] [white]{ambient}[/]",
            $"[grey]Last read:[/] [white]{snapshot.LatestSensor?.ReceivedAt.LocalDateTime.ToString("HH:mm:ss") ?? "--:--:--"}[/]  [grey]Port:[/] [white]{Markup.Escape(snapshot.PortName ?? "n/a")}[/]",
            $"[grey]{Markup.Escape(localizer["nav.hint"])}[/]"
        };

        return new Panel(new Markup(string.Join(Environment.NewLine, lines)))
            .Border(BoxBorder.Rounded)
            .Expand();
    }

    private static IRenderable BuildNavigation(DashboardSnapshot snapshot, Localizer localizer)
    {
        var tabs = new[]
        {
            FormatTab(snapshot, RuntimeScreen.Overview, localizer["screen.overview"]),
            FormatTab(snapshot, RuntimeScreen.Calibration, localizer["screen.calibration"]),
            FormatTab(snapshot, RuntimeScreen.Events, localizer["screen.events"]),
            FormatTab(snapshot, RuntimeScreen.Diagnostics, localizer["screen.diagnostics"])
        };

        return new Panel(new Markup($"[bold]{Markup.Escape(localizer["app.title"])}[/]  {string.Join("  ", tabs)}"))
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildActiveScreen(DashboardSnapshot snapshot, Localizer localizer)
    {
        return snapshot.ActiveScreen switch
        {
            RuntimeScreen.Calibration => BuildCalibrationScreen(snapshot, localizer),
            RuntimeScreen.Events => BuildEventsScreen(snapshot, localizer),
            RuntimeScreen.Diagnostics => BuildDiagnosticsScreen(snapshot, localizer),
            _ => BuildLumaBloomDashboard(snapshot, localizer)
        };
    }

    private static IRenderable BuildCalibrationScreen(DashboardSnapshot snapshot, Localizer localizer)
    {
        return new Rows(
            BuildCalibrationBody(snapshot, localizer),
            BuildCalibrationActions(snapshot, localizer));
    }

    private static IRenderable BuildCalibrationBody(DashboardSnapshot snapshot, Localizer localizer)
    {
        var lines = new List<string>
        {
            snapshot.CalibrationWizardStep switch
            {
                CalibrationWizardStep.ManualTarget => localizer["calibration.manual"],
                CalibrationWizardStep.Review => localizer["calibration.review"],
                CalibrationWizardStep.Queued => localizer["calibration.queued"],
                _ => localizer["calibration.choose"]
            },
            string.Empty,
            snapshot.CalibrationStatus
        };

        if (snapshot.CalibrationWizardStep == CalibrationWizardStep.ManualTarget)
        {
            var input = string.IsNullOrWhiteSpace(snapshot.CalibrationManualInputBuffer)
                ? localizer["value.none"]
                : $"{snapshot.CalibrationManualInputBuffer}%";
            lines.Add($"{localizer["calibration.input"]}: {input}");
        }

        if (snapshot.CalibrationWizardStep == CalibrationWizardStep.Review)
        {
            lines.Add(snapshot.CalibrationTargetMode == CalibrationTargetMode.ManualTarget
                ? $"{localizer["calibration.manualValue"]}: {snapshot.CalibrationManualInputBuffer}%"
                : localizer["calibration.current"]);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.CalibrationInputError))
        {
            lines.Add(localizer[snapshot.CalibrationInputError]);
        }

        return new Panel(new Markup(Markup.Escape(string.Join(Environment.NewLine, lines))))
            .Header(localizer["screen.calibration"])
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildCalibrationActions(DashboardSnapshot snapshot, Localizer localizer)
    {
        var actions = snapshot.CalibrationWizardStep switch
        {
            CalibrationWizardStep.ChooseTarget => new[]
            {
                FormatCalibrationAction(snapshot, CalibrationAction.UseCurrentBrightness, localizer["action.useCurrent"]),
                FormatCalibrationAction(snapshot, CalibrationAction.SetManualTarget, localizer["action.manualTarget"]),
                FormatCalibrationAction(snapshot, CalibrationAction.Cancel, localizer["action.cancel"])
            },
            CalibrationWizardStep.Queued => [FormatCalibrationAction(snapshot, CalibrationAction.Cancel, localizer["action.cancel"])],
            _ =>
            [
                FormatCalibrationAction(snapshot, CalibrationAction.Confirm, localizer["action.confirm"]),
                FormatCalibrationAction(snapshot, CalibrationAction.Cancel, localizer["action.cancel"])
            ]
        };

        return new Panel(new Markup(string.Join("  ", actions)))
            .Header(localizer["status.actions"])
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildEventsScreen(DashboardSnapshot snapshot, Localizer localizer)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Time");
        table.AddColumn("Level");
        table.AddColumn("Message");

        foreach (var entry in snapshot.Events.TakeLast(snapshot.IsCompact ? 8 : 20))
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
            table.AddRow("-", "-", Markup.Escape(localizer["events.empty"]));
        }

        return new Panel(table)
            .Header(localizer["screen.events"])
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildDiagnosticsScreen(DashboardSnapshot snapshot, Localizer localizer)
    {
        if (snapshot.IsCompact)
        {
            return new Panel(new Markup(Markup.Escape(localizer["compact.notice"])))
                .Header(localizer["screen.diagnostics"])
                .Border(BoxBorder.Rounded);
        }

        return new Rows(
            BuildRawTelemetryPanel(snapshot, localizer),
            BuildProfilePanel(snapshot, localizer),
            BuildMonitorDiagnosticsPanel(snapshot, localizer));
    }

    private static IRenderable BuildRawTelemetryPanel(DashboardSnapshot snapshot, Localizer localizer)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Field");
        table.AddColumn("Value");

        var sensor = snapshot.LatestSensor;
        if (sensor is null)
        {
            table.AddRow("telemetry", Markup.Escape(localizer["status.waitingTelemetry"]));
        }
        else
        {
            table.AddRow("deviceId", Markup.Escape(sensor.DeviceId));
            table.AddRow("sensorId", Markup.Escape(sensor.SensorId));
            table.AddRow("value", Markup.Escape(sensor.Value.ToString()));
            table.AddRow("raw", Markup.Escape(sensor.Raw?.ToString() ?? "null"));
            table.AddRow("calibrated", Markup.Escape(sensor.Calibrated.ToString()));
            table.AddRow("device ts", Markup.Escape(sensor.DeviceTimestamp.ToString()));
            table.AddRow("received", Markup.Escape(sensor.ReceivedAt.LocalDateTime.ToString("HH:mm:ss")));
        }

        return new Panel(table)
            .Header(localizer["diagnostics.rawTelemetry"])
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildProfilePanel(DashboardSnapshot snapshot, Localizer localizer)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("port", Markup.Escape(snapshot.PortName ?? "null"));
        table.AddRow("baud", Markup.Escape(snapshot.BaudRate?.ToString() ?? "null"));
        table.AddRow("profileId", Markup.Escape(snapshot.ProfileId ?? "null"));
        table.AddRow("summary", Markup.Escape(snapshot.ProfileSummary ?? "null"));
        table.AddRow("measurement", Markup.Escape(snapshot.MeasurementKind ?? "unknown"));
        table.AddRow("generic", Markup.Escape(snapshot.IsGenericProfile?.ToString() ?? "n/a"));

        return new Panel(table)
            .Header(localizer["diagnostics.profile"])
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildMonitorDiagnosticsPanel(DashboardSnapshot snapshot, Localizer localizer)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Monitor");
        table.AddColumn("State");
        table.AddColumn("Brightness");
        table.AddColumn("Signal");
        table.AddColumn("Last update");

        if (snapshot.Monitors.Count == 0)
        {
            table.AddRow("none", Markup.Escape(localizer["status.noMonitors"]), "-", "-", "-");
        }
        else
        {
            foreach (var monitor in snapshot.Monitors)
            {
                var brightness = monitor.LastAppliedBrightness.HasValue ? $"{monitor.LastAppliedBrightness}%" : "n/a";
                var requested = monitor.LastRequestedBrightness.HasValue ? $" req={monitor.LastRequestedBrightness}%" : string.Empty;
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
                    Markup.Escape($"{brightness}{requested}"),
                    signal,
                    Markup.Escape(lastUpdate));
            }
        }

        return new Panel(table)
            .Header(localizer["diagnostics.monitor"])
            .Border(BoxBorder.Rounded);
    }

    private static IRenderable BuildHint(Localizer localizer)
    {
        return new Markup($"[grey]{Markup.Escape(localizer["nav.hint"])}[/]");
    }

    private static string FormatTab(DashboardSnapshot snapshot, RuntimeScreen screen, string label)
    {
        var escaped = Markup.Escape(label);
        return snapshot.ActiveScreen == screen
            ? $"[black on deepskyblue1] {escaped} [/]"
            : $"[grey] {escaped} [/]";
    }

    private static string FormatOverviewAction(
        DashboardSnapshot snapshot,
        OverviewAction action,
        string label,
        bool destructive = false)
    {
        return FormatAction(snapshot.FocusedOverviewAction == action, label, destructive);
    }

    private static string FormatCalibrationAction(DashboardSnapshot snapshot, CalibrationAction action, string label)
    {
        return FormatAction(snapshot.FocusedCalibrationAction == action, label, action == CalibrationAction.Cancel);
    }

    private static string FormatAction(bool focused, string label, bool destructive)
    {
        var escaped = Markup.Escape(label);
        if (focused)
        {
            return destructive
                ? $"[white on red] {escaped} [/]"
                : $"[black on green] {escaped} [/]";
        }

        return destructive ? $"[red]{escaped}[/]" : $"[aqua]{escaped}[/]";
    }

    private static int? GetNormalizedSensorPercent(DashboardSnapshot snapshot)
    {
        if (snapshot.LatestSensor is null)
        {
            return null;
        }

        var max = string.Equals(snapshot.MeasurementKind, "Normalized1000", StringComparison.OrdinalIgnoreCase)
            ? 1000
            : 4095;
        var clamped = Math.Clamp(snapshot.LatestSensor.Value, 0, max);
        return (int)Math.Round(clamped * 100.0 / max, MidpointRounding.AwayFromZero);
    }

    private static string CreateApproxUptime(DashboardSnapshot snapshot)
    {
        var reference = snapshot.LatestSensor?.ReceivedAt ?? DateTimeOffset.Now;
        var elapsed = DateTimeOffset.Now - reference.AddSeconds(-snapshot.Events.Count);
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private static string BuildBrightnessBar(int? normalizedPercent, int total = 50)
    {
        var filled = normalizedPercent.HasValue
            ? Math.Clamp((int)Math.Round(normalizedPercent.Value / 100.0 * total, MidpointRounding.AwayFromZero), 0, total)
            : 0;
        var active = new string('#', filled);
        var inactive = new string('-', total - filled);
        return $"[green]{active}[/][grey]{inactive}[/]";
    }

    private static string GetSunState(int? normalizedPercent)
    {
        return normalizedPercent switch
        {
            null => "WAITING",
            < 15 => "DARK",
            < 35 => "LOW",
            < 65 => "MID",
            < 90 => "BRIGHT",
            _ => "MAX"
        };
    }

    private static string[] BuildSunMarkup(int? normalizedPercent)
    {
        var state = GetSunState(normalizedPercent);
        var color = state switch
        {
            "WAITING" => "grey",
            "DARK" => "grey",
            "LOW" => "yellow",
            "MID" => "yellow",
            _ => "green"
        };

        string[] art = state switch
        {
            "DARK" =>
            [
                " .-. ",
                "(   )",
                " '-' ",
                "     ",
                "     "
            ],
            "LOW" =>
            [
                "   .   ",
                " \\.-./ ",
                " (   ) ",
                " /'-'\\ ",
                "   '   "
            ],
            "MID" =>
            [
                "    |    ",
                " \\.-./  ",
                "-- (   ) --",
                " /'-'\\  ",
                "    |    "
            ],
            "BRIGHT" =>
            [
                " \\  |  / ",
                "  \\.-./  ",
                "--- (   ) ---",
                "  /'-'\\  ",
                " /  |  \\ "
            ],
            "MAX" =>
            [
                " \\    |    / ",
                "---\\.-./---",
                "===(   )===",
                "---/'-'\\---",
                " /    |    \\ "
            ],
            _ =>
            [
                "  \\ | /  ",
                "   .-.   ",
                "-- ( ? ) --",
                "   '-'   ",
                "  / | \\  "
            ]
        };

        return
        [
            CenterMarkup($"[{color}]{art[0]}[/]", art[0].Length, 86),
            CenterMarkup($"[{color}]{art[1]}[/]", art[1].Length, 86),
            CenterMarkup($"[{color}]{art[2]}[/]", art[2].Length, 86),
            CenterMarkup($"[{color}]{art[3]}[/]", art[3].Length, 86),
            CenterMarkup($"[{color}]{art[4]}[/]", art[4].Length, 86),
            CenterMarkup($"[{color}]{state}[/]", state.Length, 86)
        ];
    }

    private static string BuildStatusCardLine(int row, string mode, string status, string green)
    {
        return row switch
        {
            0 => "[grey].--------------------.[/]  ",
            1 => "[grey]|[/] [grey]Mode[/]               [grey]|[/]  ",
            2 => $"[grey]|[/] [{green}]{mode,-18}[/] [grey]|[/]  ",
            3 => "[grey]|[/][grey]--------------------[/][grey]|[/]  ",
            4 => "[grey]|[/] [grey]Update[/]             [grey]|[/]  ",
            5 => $"[grey]|[/] [{green}]1000 ms[/]            [grey]|[/]  ",
            6 => "[grey]|[/][grey]--------------------[/][grey]|[/]  ",
            7 => "[grey]|[/] [grey]Status[/]             [grey]|[/]  ",
            8 => $"[grey]|[/] [{green}]{status,-18}[/] [grey]|[/]  ",
            _ => new string(' ', 24)
        };
    }

    private static string CenterMarkup(string markup, int visibleLength, int width)
    {
        var left = Math.Max(0, (width - visibleLength) / 2);
        return new string(' ', left) + markup;
    }

    private static string PadMarkupRight(string markup, int visibleLength, int width)
    {
        return markup + new string(' ', Math.Max(0, width - visibleLength));
    }
}
