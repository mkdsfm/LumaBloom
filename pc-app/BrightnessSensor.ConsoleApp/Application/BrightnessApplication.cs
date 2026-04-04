using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.BrightnessMath;
using BrightnessSensor.DeviceReading;
using BrightnessSensor.WindowsBrightness;

namespace BrightnessSensor.ConsoleApp.Application;

// Orchestrates the app flow: config load, serial read loop, processing, and brightness updates.
internal static class BrightnessApplication
{
    public static int Run(AppConfig config)
    {
        using var sensorReader = new SerialSensorReader(config.Serial.PortName, config.Serial.BaudRate);

        try
        {
            sensorReader.Open();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to open COM port: {exception.Message}");
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
            Console.WriteLine($"Port opened: {config.Serial.PortName} @ {config.Serial.BaudRate}");
            Console.WriteLine("Running. Press Ctrl+C to stop.");

            var monitors = MonitorDiscovery.DiscoverMonitors();
            MonitorDiscovery.LogDetectedMonitors(monitors);

            var monitorContexts = monitors
                .Select(monitor => new MonitorContext(
                    monitor,
                    new BrightnessProcessor(CreateBrightnessSettings(config))))
                .ToList();

            TryStartupCalibration(sensorReader, monitorContexts, config.Calibration);

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

                if (monitorContexts.Count == 0)
                {
                    continue;
                }

                var sensorMessage = readResult.Message!;
                var monitorTasks = new List<Task>();
                foreach (var context in monitorContexts)
                {
                    var task = Task.Run(() =>
                    {
                        var evaluationResult = context.Processor.Evaluate(sensorMessage.Value);
                        if (!evaluationResult.ShouldApply)
                        {
                            return;
                        }

                        if (!context.Monitor.TrySetBrightness(evaluationResult.TargetBrightness, out var error))
                        {
                            Console.Error.WriteLine(
                                $"Brightness update failed ({context.Monitor.Source}:{context.Monitor.Name}): {error}");
                            return ;
                        }

                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss}] {context.Monitor.Source}:{context.Monitor.Name} raw={sensorMessage.Value,4} norm={evaluationResult.Normalized:F3} filt={evaluationResult.Filtered:F3} -> brightness={evaluationResult.TargetBrightness}%");
                    }, cancellationTokenSource.Token);
                    
                    monitorTasks.Add(task);
                }
                
                Task.WhenAll(monitorTasks);
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
        CalibrationSettings calibrationSettings)
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

    private static BrightnessComputationSettings CreateBrightnessSettings(AppConfig config)
    {
        return new BrightnessComputationSettings(
            config.Processing.AdcMin,
            config.Processing.AdcMax,
            config.Processing.Invert,
            config.Processing.EmaAlpha,
            config.Processing.HysteresisPercent,
            config.Processing.Gamma,
            config.Brightness.MinPercent,
            config.Brightness.MaxPercent);
    }

    private sealed record MonitorContext(
        IMonitorBrightness Monitor,
        BrightnessProcessor Processor);
}
