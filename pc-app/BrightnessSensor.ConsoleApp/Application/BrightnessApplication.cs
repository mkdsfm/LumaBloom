using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.BrightnessMath;
using BrightnessSensor.DeviceReading;
using BrightnessSensor.DeviceReading.Discovery;
using BrightnessSensor.DeviceReading.Models;
using BrightnessSensor.DeviceReading.Reading;
using BrightnessSensor.ConsoleApp.Profiles;
using BrightnessSensor.WindowsBrightness;
using System.Globalization;

namespace BrightnessSensor.ConsoleApp.Application;

// Orchestrates the app flow: config load, serial read loop, processing, and brightness updates.
internal static class BrightnessApplication
{
    public static int Run(AppConfig config)
    {
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
            Console.Error.WriteLine(discoveryResult.Error ?? "Failed to resolve COM port.");
            return 1;
        }

        var portName = discoveryResult.PortName;
        using var sensorReader = new SerialSensorReader(portName, serialBaudRate);

        try
        {
            sensorReader.Open();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to open resolved COM port '{portName}': {exception.Message}");
            return 1;
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += handler;
        
        try
        {
            var firstMessage = ReadFirstValidMessage(sensorReader, discoveryTimeoutMs);
            if (firstMessage is null)
            {
                Console.Error.WriteLine("Failed to read an initial valid telemetry message after opening the COM port.");
                return 1;
            }

            var resolvedProfile = profileResolver.Resolve(config, firstMessage, out var profileLog);
            var effectiveSettings = ResolvedSettingsFactory.Create(config, resolvedProfile);

            Console.WriteLine(
                string.IsNullOrWhiteSpace(discoveryDeviceId)
                    ? $"Resolved port: {portName} using telemetry probe"
                    : $"Resolved port: {portName} for deviceId={discoveryDeviceId}");
            Console.WriteLine($"Port opened: {portName} @ {serialBaudRate}");
            Console.WriteLine(profileLog);
            Console.WriteLine($"Effective settings: {Describe(effectiveSettings)}");
            Console.WriteLine("Running. Press Ctrl+C to stop.");

            var monitors = MonitorDiscovery.DiscoverMonitors();
            MonitorDiscovery.LogDetectedMonitors(monitors);

            var monitorContexts = monitors
                .Select(monitor => new MonitorContext(
                    monitor,
                    new BrightnessProcessor(CreateBrightnessSettings(effectiveSettings))))
                .ToList();

            TryStartupCalibration(sensorReader, monitorContexts, effectiveSettings.Calibration, firstMessage);
            ProcessMessage(firstMessage, monitorContexts, cancellationTokenSource.Token);

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var readResult = sensorReader.TryReadMessage();
                if (readResult.Status == SensorReadStatus.TimeoutOrEmpty)
                {
                    Thread.Sleep(10);
                    continue;
                }
                if (readResult.Status == SensorReadStatus.Error)
                {
                    Console.Error.WriteLine($"COM read error: {readResult.Error}");
                    return 1;
                }
                if (readResult.Status == SensorReadStatus.InvalidPayload)
                {
                    Console.WriteLine($"Skipping invalid JSON: {readResult.RawLine}");
                    continue;
                }

                ProcessMessage(readResult.Message!, monitorContexts, cancellationTokenSource.Token);
            }
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }

        return 0;
    }

    private static void TryStartupCalibration(
        SerialSensorReader sensorReader,
        IReadOnlyList<MonitorContext> monitorContexts,
        CalibrationSettings calibrationSettings,
        SensorMessage? initialMessage)
    {
        if (!calibrationSettings.Enabled)
        {
            Console.WriteLine("Startup calibration disabled.");
            return;
        }

        if (monitorContexts.Count == 0)
        {
            Console.WriteLine("Startup calibration skipped: no monitors available.");
            return;
        }

        var samples = new List<int>(calibrationSettings.SampleCount);
        if (initialMessage is not null)
        {
            samples.Add(initialMessage.Value);
        }

        var attempts = 0;

        while (attempts < calibrationSettings.MaxReadAttempts &&
            samples.Count < calibrationSettings.SampleCount)
        {
            attempts++;

            var readResult = sensorReader.TryReadMessage();
            switch (readResult.Status)
            {
                case SensorReadStatus.TimeoutOrEmpty or SensorReadStatus.InvalidPayload:
                    continue;
                case SensorReadStatus.Error:
                    Console.WriteLine($"Startup calibration skipped: COM read error ({readResult.Error}).");
                    return;
                default:
                    samples.Add(readResult.Message!.Value);
                    break;
            }
        }

        if (samples.Count == 0)
        {
            Console.WriteLine("Startup calibration skipped: no valid sensor data received.");
            return;
        }

        if (samples.Count < calibrationSettings.SampleCount)
        {
            Console.WriteLine(
                $"Startup calibration skipped: not enough samples ({samples.Count}/{calibrationSettings.SampleCount}).");
            return;
        }

        var averageSample = (int)Math.Round(samples.Average(), MidpointRounding.AwayFromZero);

        foreach (var context in monitorContexts)
        {
            if (!context.IsEnabled)
            {
                continue;
            }

            if (!context.Monitor.TryGetBrightness(out var currentBrightness, out var brightnessError))
            {
                Console.WriteLine(
                    $"Startup calibration skipped ({context.Monitor.Source}:{context.Monitor.Name}): cannot read current brightness ({brightnessError}).");
                continue;
            }

            if (!context.Processor.TryCalibrate(averageSample, currentBrightness, out var error))
            {
                Console.WriteLine(
                    $"Startup calibration skipped ({context.Monitor.Source}:{context.Monitor.Name}): {error}");
                continue;
            }

            Console.WriteLine(
                $"Startup calibration ({context.Monitor.Source}:{context.Monitor.Name}): screen={currentBrightness}% sensorAvg={averageSample} ({samples.Count} samples)");
        }
    }

    private static BrightnessComputationSettings CreateBrightnessSettings(ResolvedAppSettings settings)
    {
        return new BrightnessComputationSettings(
            settings.Processing.AdcMin,
            settings.Processing.AdcMax,
            settings.Processing.Invert,
            settings.Processing.EmaAlpha,
            settings.Processing.HysteresisPercent,
            settings.Processing.Gamma,
            settings.Brightness.MinPercent,
            settings.Brightness.MaxPercent);
    }

    private static SensorMessage? ReadFirstValidMessage(SerialSensorReader sensorReader, int discoveryTimeoutMs)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(discoveryTimeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var readResult = sensorReader.TryReadMessage();
            switch (readResult.Status)
            {
                case SensorReadStatus.Success:
                    return readResult.Message;
                case SensorReadStatus.InvalidPayload:
                case SensorReadStatus.TimeoutOrEmpty:
                    continue;
                case SensorReadStatus.Error:
                    Console.Error.WriteLine($"COM read error during profile detection: {readResult.Error}");
                    return null;
            }
        }

        return null;
    }

    private static void ProcessMessage(
        SensorMessage sensorMessage,
        IReadOnlyList<MonitorContext> monitorContexts,
        CancellationToken cancellationToken)
    {
        if (monitorContexts.Count == 0)
        {
            return;
        }

        var monitorTasks = new List<Task>();
        foreach (var context in monitorContexts)
        {
            if (!context.IsEnabled)
            {
                continue;
            }

            var task = Task.Run(() =>
            {
                var evaluationResult = context.Processor.Evaluate(sensorMessage.Value);
                if (!evaluationResult.ShouldApply)
                {
                    return;
                }

                if (!context.Monitor.TrySetBrightness(evaluationResult.TargetBrightness, out var error))
                {
                    context.Disable();
                    Console.Error.WriteLine(
                        $"Brightness update failed ({context.Monitor.Source}:{context.Monitor.Name}): {error}");
                    Console.WriteLine(
                        $"Monitor disabled after brightness control failure: {context.Monitor.Source}:{context.Monitor.Name}");
                    return;
                }

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] {context.Monitor.Source}:{context.Monitor.Name} raw={sensorMessage.Value,4} norm={evaluationResult.Normalized:F3} filt={evaluationResult.Filtered:F3} -> brightness={evaluationResult.TargetBrightness}%");
            }, cancellationToken);

            monitorTasks.Add(task);
        }

        Task.WaitAll([.. monitorTasks], cancellationToken);
    }

    private static string Describe(ResolvedAppSettings settings)
    {
        return $"profileId={settings.ProfileId}, measurement={settings.MeasurementKind}, generic={settings.IsGenericProfile}, adc=[{settings.Processing.AdcMin}..{settings.Processing.AdcMax}], invert={settings.Processing.Invert}, emaAlpha={FormatNumber(settings.Processing.EmaAlpha)}, hysteresisPercent={settings.Processing.HysteresisPercent}, gamma={FormatNullableNumber(settings.Processing.Gamma)}, brightness=[{settings.Brightness.MinPercent}..{settings.Brightness.MaxPercent}], calibration={{enabled={settings.Calibration.Enabled}, sampleCount={settings.Calibration.SampleCount}, maxReadAttempts={settings.Calibration.MaxReadAttempts}}}";
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

    private sealed class MonitorContext(IMonitorBrightness monitor, BrightnessProcessor processor)
    {
        public IMonitorBrightness Monitor { get; } = monitor;

        public BrightnessProcessor Processor { get; } = processor;

        public bool IsEnabled { get; private set; } = true;

        public void Disable()
        {
            IsEnabled = false;
        }
    }
}
