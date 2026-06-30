using BrightnessSensor.BrightnessMath;
using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.ConsoleApp.Runtime;
using BrightnessSensor.DeviceReading;
using BrightnessSensor.DeviceReading.Discovery;
using BrightnessSensor.DeviceReading.Models;
using BrightnessSensor.DeviceReading.Reading;
using BrightnessSensor.WindowsBrightness;
using System.Globalization;

namespace BrightnessSensor.ConsoleApp.Application;

// Orchestrates the app flow: config load, serial read loop, processing, and brightness updates.
internal static class BrightnessApplication
{
    public static int Run(AppConfig config)
    {
        var stateStore = new RuntimeStateStore();
        var dashboardHost = new ConsoleDashboardHost(stateStore);
        return dashboardHost.Run(cancellationToken => RunCore(config, stateStore, cancellationToken));
    }

    private static int RunCore(AppConfig config, RuntimeStateStore stateStore, CancellationToken cancellationToken)
    {
        stateStore.SetLifecycle(AppLifecycleState.Starting, "Resolving serial port...");
        stateStore.AddEvent("Application started.", RuntimeEventSeverity.Info);

        var profileResolver = new DeviceProfileResolver();
        var forcedProfile = profileResolver.TryResolveByProfileId(config.DeviceProfile.ProfileId);
        var fallbackProfile = forcedProfile ?? DeviceProfileCatalog.Generic;
        var serialBaudRate = config.Serial.BaudRate ?? fallbackProfile.BaudRate;
        var discoveryTimeoutMs = config.Serial.DiscoveryTimeoutMs ?? fallbackProfile.DiscoveryTimeoutMs;
        var discoveryDeviceId = !string.IsNullOrWhiteSpace(config.Serial.DeviceId)
            ? config.Serial.DeviceId
            : forcedProfile?.IsGeneric == false
                ? forcedProfile.DeviceId
                : null;

        var discovery = new SerialPortDiscovery(
            discoveryDeviceId,
            serialBaudRate,
            discoveryTimeoutMs);
        var discoveryResult = string.IsNullOrWhiteSpace(discoveryDeviceId)
            ? discovery.ResolveFirstTelemetry()
            : discovery.ResolveByDeviceId();
        if (discoveryResult.Status != SerialPortDiscoveryStatus.Success || string.IsNullOrWhiteSpace(discoveryResult.PortName))
        {
            var errorMessage = discoveryResult.Error ?? "Failed to resolve COM port.";
            stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            return 1;
        }

        var portName = discoveryResult.PortName;
        stateStore.SetConnection(
            portName,
            serialBaudRate,
            string.IsNullOrWhiteSpace(discoveryDeviceId)
                ? $"Resolved port via telemetry probe."
                : $"Resolved port for deviceId={discoveryDeviceId}.");
        stateStore.AddEvent($"Resolved COM port {portName} @ {serialBaudRate}.", RuntimeEventSeverity.Success);

        using var sensorReader = new SerialSensorReader(portName, serialBaudRate);

        try
        {
            sensorReader.Open();
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to open resolved COM port '{portName}': {exception.Message}";
            stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            return 1;
        }

        stateStore.SetLifecycle(AppLifecycleState.Starting, "Waiting for first valid telemetry...");
        var firstMessage = ReadFirstValidMessage(sensorReader, discoveryTimeoutMs, stateStore, cancellationToken);
        if (firstMessage is null)
        {
            const string errorMessage = "Failed to read an initial valid telemetry message after opening the COM port.";
            stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            return 1;
        }

        stateStore.SetLatestSensor(firstMessage);
        var resolvedProfile = profileResolver.Resolve(config, firstMessage, out var profileLog);
        var effectiveSettings = ResolvedSettingsFactory.Create(config, resolvedProfile);

        stateStore.SetProfile(effectiveSettings, $"{profileLog} Effective settings: {Describe(effectiveSettings)}");
        stateStore.AddEvent(profileLog, RuntimeEventSeverity.Info);
        stateStore.AddEvent($"Effective settings: {Describe(effectiveSettings)}", RuntimeEventSeverity.Info);

        var monitors = MonitorDiscovery.DiscoverMonitors();
        stateStore.SetMonitors(monitors);
        if (monitors.Count == 0)
        {
            stateStore.AddEvent("No brightness-capable monitors detected.", RuntimeEventSeverity.Warning);
        }
        else
        {
            foreach (var group in monitors.GroupBy(monitor => monitor.Source))
            {
                stateStore.AddEvent(
                    $"{group.Key}: detected {group.Count()} monitor(s): {string.Join(", ", group.Select(monitor => monitor.Name))}",
                    RuntimeEventSeverity.Success);
            }
        }

        var monitorSessions = monitors
            .Select(monitor => new MonitorSession(
                monitor,
                new BrightnessProcessor(CreateBrightnessSettings(effectiveSettings))))
            .ToList();

        stateStore.SetLifecycle(AppLifecycleState.Starting, "Running startup calibration...");
        if (!TryCalibration(
                sensorReader,
                monitorSessions,
                effectiveSettings,
                firstMessage,
                stateStore,
                cancellationToken,
                isStartup: true,
                requestedBrightnessPercent: null))
        {
            return 1;
        }

        stateStore.SetLifecycle(AppLifecycleState.Running, "Running.");
        var messageProcessor = new MessageProcessor(stateStore);

        if (ShouldProcessTelemetry(firstMessage, effectiveSettings.MeasurementKind))
        {
            messageProcessor.ProcessMessage(firstMessage, monitorSessions, effectiveSettings.MeasurementKind, cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (stateStore.TryConsumeRecalibrationRequest(out var requestedBrightnessPercent))
            {
                stateStore.SetCalibrationStatus(requestedBrightnessPercent.HasValue
                    ? $"Manual recalibration in progress for target brightness {requestedBrightnessPercent}%..."
                    : "Manual recalibration in progress using current monitor brightness...");
                var recalibrationSucceeded = TryCalibration(
                    sensorReader,
                    monitorSessions,
                    effectiveSettings,
                    initialMessage: null,
                    stateStore,
                    cancellationToken,
                    isStartup: false,
                    requestedBrightnessPercent: requestedBrightnessPercent);

                if (recalibrationSucceeded)
                {
                    stateStore.AddEvent("Manual recalibration completed.", RuntimeEventSeverity.Success);
                }
                else
                {
                    stateStore.AddEvent("Manual recalibration failed; continuing runtime.", RuntimeEventSeverity.Error);
                    stateStore.SetLifecycle(AppLifecycleState.Running, stateStore.IsPaused
                        ? "Paused: telemetry continues, brightness writes are suspended."
                        : "Running.");
                }
            }

            var readResult = sensorReader.TryReadMessage();
            if (readResult.Status == SensorReadStatus.TimeoutOrEmpty)
            {
                Thread.Sleep(10);
                continue;
            }

            if (readResult.Status == SensorReadStatus.Error)
            {
                var errorMessage = $"COM read error: {readResult.Error}";
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
                stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
                return 1;
            }

            if (readResult.Status == SensorReadStatus.InvalidPayload)
            {
                stateStore.AddEvent($"Skipping invalid JSON: {readResult.RawLine}", RuntimeEventSeverity.Warning);
                continue;
            }

            var sensorMessage = readResult.Message!;
            stateStore.SetLatestSensor(sensorMessage);

            if (!ShouldProcessTelemetry(sensorMessage, effectiveSettings.MeasurementKind))
            {
                stateStore.SetCalibrationStatus("Waiting for calibrated telemetry from device...");
                continue;
            }

            messageProcessor.ProcessMessage(sensorMessage, monitorSessions, effectiveSettings.MeasurementKind, cancellationToken);
        }

        stateStore.SetLifecycle(AppLifecycleState.Stopped, "Stopped.");
        stateStore.AddEvent("Application stopped.", RuntimeEventSeverity.Info);
        return 0;
    }

    private static bool TryCalibration(
        SerialSensorReader sensorReader,
        IReadOnlyList<MonitorSession> monitorSessions,
        ResolvedAppSettings settings,
        SensorMessage? initialMessage,
        RuntimeStateStore stateStore,
        CancellationToken cancellationToken,
        bool isStartup,
        int? requestedBrightnessPercent)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var calibrationLabel = isStartup
            ? "Startup calibration"
            : requestedBrightnessPercent.HasValue
                ? $"Manual recalibration to {requestedBrightnessPercent}%"
                : "Manual recalibration";
        var calibrationSettings = settings.Calibration;

        if (!calibrationSettings.Enabled)
        {
            if (settings.MeasurementKind == MeasurementKind.Normalized1000)
            {
                var errorMessage = $"{calibrationLabel} is required for this device profile but is disabled.";
                stateStore.SetCalibrationStatus(errorMessage);
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
                stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
                return false;
            }

            stateStore.SetCalibrationStatus($"{calibrationLabel} disabled.");
            stateStore.AddEvent($"{calibrationLabel} disabled.", RuntimeEventSeverity.Warning);
            return true;
        }

        if (monitorSessions.Count == 0)
        {
            var noMonitorsMessage = $"{calibrationLabel} skipped: no monitors available.";
            stateStore.SetCalibrationStatus(noMonitorsMessage);
            stateStore.AddEvent(noMonitorsMessage, RuntimeEventSeverity.Warning);
            if (settings.MeasurementKind == MeasurementKind.Normalized1000 && isStartup)
            {
                stateStore.SetLifecycle(AppLifecycleState.Error, noMonitorsMessage);
                return false;
            }

            return settings.MeasurementKind != MeasurementKind.Normalized1000 || !isStartup;
        }

        stateStore.SetCalibrationStatus($"{calibrationLabel}: collecting samples...");
        stateStore.AddEvent($"{calibrationLabel}: collecting samples...", RuntimeEventSeverity.Info);

        var samples = new List<int>(calibrationSettings.SampleCount);
        if (initialMessage is not null)
        {
            stateStore.SetLatestSensor(initialMessage);
            samples.Add(GetCalibrationSample(initialMessage));
        }

        var attempts = 0;
        while (attempts < calibrationSettings.MaxReadAttempts &&
            samples.Count < calibrationSettings.SampleCount &&
            !cancellationToken.IsCancellationRequested)
        {
            attempts++;

            var readResult = sensorReader.TryReadMessage();
            switch (readResult.Status)
            {
                case SensorReadStatus.TimeoutOrEmpty or SensorReadStatus.InvalidPayload:
                    continue;
                case SensorReadStatus.Error:
                {
                    var message = $"{calibrationLabel} skipped: COM read error ({readResult.Error}).";
                    stateStore.SetCalibrationStatus(message);
                    stateStore.AddEvent(message, RuntimeEventSeverity.Error);
                    return settings.MeasurementKind != MeasurementKind.Normalized1000 || !isStartup;
                }
                default:
                    stateStore.SetLatestSensor(readResult.Message!);
                    samples.Add(GetCalibrationSample(readResult.Message!));
                    stateStore.SetCalibrationStatus($"{calibrationLabel}: collected {samples.Count}/{calibrationSettings.SampleCount} samples...");
                    break;
            }
        }

        if (samples.Count == 0)
        {
            var message = $"{calibrationLabel} skipped: no valid sensor data received.";
            stateStore.SetCalibrationStatus(message);
            stateStore.AddEvent(message, RuntimeEventSeverity.Warning);
            return settings.MeasurementKind != MeasurementKind.Normalized1000 || !isStartup;
        }

        if (samples.Count < calibrationSettings.SampleCount)
        {
            var message = $"{calibrationLabel} skipped: not enough samples ({samples.Count}/{calibrationSettings.SampleCount}).";
            stateStore.SetCalibrationStatus(message);
            stateStore.AddEvent(message, RuntimeEventSeverity.Warning);
            return settings.MeasurementKind != MeasurementKind.Normalized1000 || !isStartup;
        }

        var averageSample = (int)Math.Round(samples.Average(), MidpointRounding.AwayFromZero);
        if (settings.MeasurementKind == MeasurementKind.Normalized1000)
        {
            return TryDeviceCalibration(
                sensorReader,
                monitorSessions,
                averageSample,
                samples.Count,
                settings.DiscoveryTimeoutMs,
                stateStore,
                isStartup,
                calibrationLabel,
                requestedBrightnessPercent);
        }

        var anyCalibrationSucceeded = false;
        foreach (var session in monitorSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!session.IsEnabled)
            {
                continue;
            }

            if (!TryResolveCalibrationBrightness(
                    session.Monitor,
                    requestedBrightnessPercent,
                    stateStore,
                    calibrationLabel,
                    out var currentBrightness,
                    out var brightnessError))
            {
                var message = $"{calibrationLabel} skipped ({session.Monitor.Source}:{session.Monitor.Name}): cannot read current brightness ({brightnessError}).";
                stateStore.RecordMonitorStatus(session.Monitor.Source, session.Monitor.Name, "Current brightness unavailable");
                stateStore.AddEvent(message, RuntimeEventSeverity.Warning);
                continue;
            }

            if (!session.Processor.TryCalibrate(averageSample, currentBrightness, out var error))
            {
                var message = $"{calibrationLabel} skipped ({session.Monitor.Source}:{session.Monitor.Name}): {error}";
                stateStore.RecordMonitorStatus(session.Monitor.Source, session.Monitor.Name, "Local calibration failed");
                stateStore.AddEvent(message, RuntimeEventSeverity.Warning);
                continue;
            }

            stateStore.MarkMonitorCalibration(
                session.Monitor.Source,
                session.Monitor.Name,
                currentBrightness,
                averageSample,
                samples.Count);
            stateStore.AddEvent(
                $"{calibrationLabel} ({session.Monitor.Source}:{session.Monitor.Name}): screen={currentBrightness}% sensorAvg={averageSample} ({samples.Count} samples)",
                RuntimeEventSeverity.Success);
            anyCalibrationSucceeded = true;
        }

        if (anyCalibrationSucceeded || monitorSessions.All(session => !session.IsEnabled))
        {
            stateStore.SetCalibrationStatus($"{calibrationLabel} complete.");
        }
        else
        {
            stateStore.SetCalibrationStatus($"{calibrationLabel} did not calibrate any enabled monitors.");
        }

        return anyCalibrationSucceeded || monitorSessions.All(session => !session.IsEnabled);
    }

    private static bool TryDeviceCalibration(
        SerialSensorReader sensorReader,
        IReadOnlyList<MonitorSession> monitorSessions,
        int averageSample,
        int sampleCount,
        int timeoutMs,
        RuntimeStateStore stateStore,
        bool isStartup,
        string calibrationLabel,
        int? requestedBrightnessPercent)
    {
        var calibrationSession = monitorSessions.FirstOrDefault(session => session.IsEnabled);
        if (calibrationSession is null)
        {
            var errorMessage = $"{calibrationLabel} failed: no enabled monitors available.";
            stateStore.SetCalibrationStatus(errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            if (isStartup)
            {
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            }

            return false;
        }

        if (monitorSessions.Count(session => session.IsEnabled) > 1)
        {
            stateStore.AddEvent(
                $"{calibrationLabel}: using the first enabled monitor for device calibration ({calibrationSession.Monitor.Source}:{calibrationSession.Monitor.Name}).",
                RuntimeEventSeverity.Warning);
        }

        if (!TryResolveCalibrationBrightness(
                calibrationSession.Monitor,
                requestedBrightnessPercent,
                stateStore,
                calibrationLabel,
                out var currentBrightness,
                out var brightnessError))
        {
            var errorMessage =
                $"{calibrationLabel} failed ({calibrationSession.Monitor.Source}:{calibrationSession.Monitor.Name}): cannot read current brightness ({brightnessError}).";
            stateStore.SetCalibrationStatus(errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            if (isStartup)
            {
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            }

            return false;
        }

        if (!sensorReader.TryWriteLine(
                new CalibrationCommand
                {
                    ScreenBrightnessPercent = currentBrightness,
                    SensorAverageRaw = averageSample
                },
                out var writeError))
        {
            var errorMessage = $"{calibrationLabel} failed: unable to send calibration command ({writeError}).";
            stateStore.SetCalibrationStatus(errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            if (isStartup)
            {
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            }

            return false;
        }

        stateStore.SetCalibrationStatus($"{calibrationLabel}: waiting for device response...");
        if (!TryAwaitCalibrationResponse(sensorReader, timeoutMs, stateStore, out var response, out var responseError))
        {
            var errorMessage = $"{calibrationLabel} failed: {responseError}";
            stateStore.SetCalibrationStatus(errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            if (isStartup)
            {
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            }

            return false;
        }

        if (!response!.Success || !response.Calibrated)
        {
            var errorMessage =
                $"{calibrationLabel} failed ({calibrationSession.Monitor.Source}:{calibrationSession.Monitor.Name}): {response.Message}";
            stateStore.SetCalibrationStatus(errorMessage);
            stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
            if (isStartup)
            {
                stateStore.SetLifecycle(AppLifecycleState.Error, errorMessage);
            }

            return false;
        }

        stateStore.MarkMonitorCalibration(
            calibrationSession.Monitor.Source,
            calibrationSession.Monitor.Name,
            currentBrightness,
            averageSample,
            sampleCount);
        stateStore.SetCalibrationStatus($"{calibrationLabel} complete. offset={FormatNullableNumber(response.NormalizedOffset)}");
        stateStore.AddEvent(
            $"{calibrationLabel} ({calibrationSession.Monitor.Source}:{calibrationSession.Monitor.Name}): screen={currentBrightness}% sensorAvg={averageSample} ({sampleCount} samples) offset={FormatNullableNumber(response.NormalizedOffset)}",
            RuntimeEventSeverity.Success);
        return true;
    }

    private static bool TryResolveCalibrationBrightness(
        IMonitorBrightness monitor,
        int? requestedBrightnessPercent,
        RuntimeStateStore stateStore,
        string calibrationLabel,
        out int brightnessPercent,
        out string? error)
    {
        if (requestedBrightnessPercent.HasValue)
        {
            brightnessPercent = requestedBrightnessPercent.Value;
            if (!monitor.TrySetBrightness(brightnessPercent, out error))
            {
                return false;
            }

            stateStore.AddEvent(
                $"{calibrationLabel} ({monitor.Source}:{monitor.Name}): set monitor brightness to requested {brightnessPercent}% before calibration.",
                RuntimeEventSeverity.Info);
            error = null;
            return true;
        }

        return monitor.TryGetBrightness(out brightnessPercent, out error);
    }

    private static BrightnessComputationSettings CreateBrightnessSettings(ResolvedAppSettings settings)
    {
        return new BrightnessComputationSettings(
            settings.MeasurementKind == MeasurementKind.Normalized1000,
            settings.Processing.AdcMin,
            settings.Processing.AdcMax,
            settings.Processing.Invert,
            settings.Processing.EmaAlpha,
            settings.Processing.HysteresisPercent,
            settings.Processing.MaxBrightnessStepPercent,
            settings.Processing.Gamma,
            settings.Brightness.MinPercent,
            settings.Brightness.MaxPercent);
    }

    private static SensorMessage? ReadFirstValidMessage(
        SerialSensorReader sensorReader,
        int discoveryTimeoutMs,
        RuntimeStateStore stateStore,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(discoveryTimeoutMs);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var readResult = sensorReader.TryReadMessage();
            switch (readResult.Status)
            {
                case SensorReadStatus.Success:
                    stateStore.SetLatestSensor(readResult.Message!);
                    return readResult.Message;
                case SensorReadStatus.InvalidPayload:
                case SensorReadStatus.TimeoutOrEmpty:
                    continue;
                case SensorReadStatus.Error:
                    stateStore.AddEvent(
                        $"COM read error during profile detection: {readResult.Error}",
                        RuntimeEventSeverity.Error);
                    return null;
            }
        }

        return null;
    }

    private static bool ShouldProcessTelemetry(SensorMessage sensorMessage, MeasurementKind measurementKind)
    {
        if (measurementKind != MeasurementKind.Normalized1000)
        {
            return true;
        }

        return sensorMessage.Calibrated;
    }

    private static int GetCalibrationSample(SensorMessage sensorMessage)
    {
        return sensorMessage.Raw ?? sensorMessage.Value;
    }

    private static bool TryAwaitCalibrationResponse(
        SerialSensorReader sensorReader,
        int timeoutMs,
        RuntimeStateStore stateStore,
        out CalibrationResponse? response,
        out string? error)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var lineResult = sensorReader.TryReadLine();
            if (lineResult.Status == SensorReadStatus.TimeoutOrEmpty)
            {
                continue;
            }

            if (lineResult.Status == SensorReadStatus.Error)
            {
                response = null;
                error = $"COM read error while waiting for calibration response ({lineResult.Error}).";
                return false;
            }

            if (CalibrationResponseParser.TryParse(lineResult.Line!, out var parsedResponse))
            {
                response = parsedResponse;
                error = null;
                return true;
            }

            if (SensorMessageParser.TryParse(lineResult.Line!, out var sensorMessage))
            {
                stateStore.SetLatestSensor(sensorMessage);
            }
        }

        response = null;
        error = $"timed out after {timeoutMs} ms while waiting for calibration response.";
        return false;
    }

    private static string Describe(ResolvedAppSettings settings)
    {
        return $"profileId={settings.ProfileId}, measurement={settings.MeasurementKind}, generic={settings.IsGenericProfile}, adc=[{settings.Processing.AdcMin}..{settings.Processing.AdcMax}], invert={settings.Processing.Invert}, emaAlpha={FormatNumber(settings.Processing.EmaAlpha)}, hysteresisPercent={settings.Processing.HysteresisPercent}, maxBrightnessStepPercent={settings.Processing.MaxBrightnessStepPercent}, gamma={FormatNullableNumber(settings.Processing.Gamma)}, brightness=[{settings.Brightness.MinPercent}..{settings.Brightness.MaxPercent}], calibration={{enabled={settings.Calibration.Enabled}, sampleCount={settings.Calibration.SampleCount}, maxReadAttempts={settings.Calibration.MaxReadAttempts}}}";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.0###############", CultureInfo.InvariantCulture);
    }

    private static string FormatNullableNumber(double? value)
    {
        return value.HasValue
            ? FormatNumber(value.Value)
            : "null";
    }
}
