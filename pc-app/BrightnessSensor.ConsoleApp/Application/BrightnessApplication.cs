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
using System.Reflection;
using MathCurvePoint = BrightnessSensor.BrightnessMath.BrightnessCurvePointSetting;

namespace BrightnessSensor.ConsoleApp.Application;

// Orchestrates the app flow: config load, serial read loop, processing, and brightness updates.
internal static class BrightnessApplication
{
    private const string GitHubRepository = "mkdsfm/LumaBloom";

    public static int Run(AppConfig config, string configPath)
    {
        var stateStore = new RuntimeStateStore();
        stateStore.SetLanguage(UiLanguageResolver.Resolve(config.Ui.Language));
        var dashboardHost = new ConsoleDashboardHost(stateStore);
        var applicationDirectory = AppContext.BaseDirectory;
        var executablePath = Environment.ProcessPath ??
                             Path.Combine(applicationDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.exe");
        return dashboardHost.Run(cancellationToken =>
            RunCore(config, configPath, stateStore, applicationDirectory, executablePath, cancellationToken));
    }

    private static int RunCore(
        AppConfig config,
        string configPath,
        RuntimeStateStore stateStore,
        string applicationDirectory,
        string executablePath,
        CancellationToken cancellationToken)
    {
        using var gitHubReleaseClient = new GitHubReleaseClient(GitHubRepository);
        var applicationUpdateService = new ApplicationUpdateService(applicationDirectory, executablePath);
        var firmwareFlashService = new FirmwareFlashService(applicationDirectory);
        var bundledFirmwareLocator = new BundledFirmwareLocator();
        GitHubReleaseInfo? latestRelease = null;
        BundledFirmwareInfo? bundledFirmware = null;

        stateStore.SetLifecycle(AppLifecycleState.Starting, "Loading settings...");
        stateStore.AddEvent("Application started.", RuntimeEventSeverity.Info);
        stateStore.SetAutostartEnabled(WindowsAutostartManager.IsEnabled());
        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
            AppVersion.Current,
            "Unknown",
            "Not checked yet.",
            null,
            UpdateAvailable: false,
            IsBusy: false,
            IncludePrerelease: false,
            IsPrerelease: false));

        if (bundledFirmwareLocator.TryLocate(applicationDirectory, out bundledFirmware, out var bundledFirmwareStatus))
        {
            stateStore.SetBundledFirmwareState(new BundledFirmwareSnapshot(
                bundledFirmware!.Version,
                bundledFirmware.FileName,
                bundledFirmwareStatus,
                IsAvailable: true,
                IsBusy: false));
        }
        else
        {
            stateStore.SetBundledFirmwareState(new BundledFirmwareSnapshot(
                "Unknown",
                "n/a",
                bundledFirmwareStatus,
                IsAvailable: false,
                IsBusy: false));
        }

        stateStore.RequestAppUpdateCheck();

        var effectiveSettings = ResolvedSettingsFactory.Create(config);
        stateStore.SetDeviceSettings(
            effectiveSettings,
            $"Waiting for LumaBloom telemetry. Effective settings: {Describe(effectiveSettings)}");
        stateStore.AddEvent($"Loaded settings: {Describe(effectiveSettings)}", RuntimeEventSeverity.Info);

        var serialBaudRate = effectiveSettings.BaudRate;
        var discoveryTimeoutMs = effectiveSettings.DiscoveryTimeoutMs;

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

        var messageProcessor = new MessageProcessor(stateStore);

        if (!TryConnectSensor(
                configPath,
                serialBaudRate,
                discoveryTimeoutMs,
                monitorSessions,
                stateStore,
                messageProcessor,
                gitHubReleaseClient,
                applicationUpdateService,
                ref latestRelease,
                cancellationToken,
                ref config,
                ref effectiveSettings,
                out var sensorReader,
                out var firstMessage))
        {
            return 0;
        }

        stateStore.SetLifecycle(AppLifecycleState.Running, "Running.");

        if (firstMessage is not null)
        {
            messageProcessor.ProcessMessage(firstMessage, monitorSessions, effectiveSettings.MeasurementKind, cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            ProcessSettingsRequests(
                configPath,
                ref config,
                ref effectiveSettings,
                monitorSessions,
                stateStore);
            if (ProcessApplicationUpdateRequests(
                    stateStore,
                    gitHubReleaseClient,
                    applicationUpdateService,
                    ref latestRelease,
                    cancellationToken))
            {
                sensorReader.Dispose();
                return 0;
            }

            if (!ProcessFirmwareUpdateRequests(
                    configPath,
                    serialBaudRate,
                    discoveryTimeoutMs,
                    monitorSessions,
                    stateStore,
                    messageProcessor,
                    gitHubReleaseClient,
                    applicationUpdateService,
                    ref latestRelease,
                    firmwareFlashService,
                    bundledFirmware,
                    cancellationToken,
                    ref config,
                    ref effectiveSettings,
                    ref sensorReader,
                    out firstMessage))
            {
                return 0;
            }

            if (firstMessage is not null)
            {
                messageProcessor.ProcessMessage(firstMessage, monitorSessions, effectiveSettings.MeasurementKind, cancellationToken);
                firstMessage = null;
            }

            messageProcessor.ApplyPendingManualBrightness(monitorSessions, cancellationToken);

            var readResult = sensorReader.TryReadMessage();
            if (readResult.Status == SensorReadStatus.TimeoutOrEmpty)
            {
                if (IsTelemetryStale(stateStore, discoveryTimeoutMs))
                {
                    stateStore.SetLifecycle(AppLifecycleState.Waiting, "Waiting for sensor telemetry...");
                    stateStore.ClearLatestSensor();
                }

                Thread.Sleep(10);
                continue;
            }

            if (readResult.Status == SensorReadStatus.Error)
            {
                var errorMessage = $"COM read error: {readResult.Error}";
                stateStore.SetLifecycle(AppLifecycleState.Waiting, "Sensor disconnected. Waiting for telemetry...");
                stateStore.ClearLatestSensor();
                stateStore.AddEvent(errorMessage, RuntimeEventSeverity.Error);
                sensorReader.Dispose();

                if (!TryConnectSensor(
                        configPath,
                        serialBaudRate,
                        discoveryTimeoutMs,
                        monitorSessions,
                        stateStore,
                        messageProcessor,
                        gitHubReleaseClient,
                        applicationUpdateService,
                        ref latestRelease,
                        cancellationToken,
                        ref config,
                        ref effectiveSettings,
                        out sensorReader,
                        out firstMessage))
                {
                    break;
                }

                stateStore.SetLifecycle(AppLifecycleState.Running, "Running.");
                if (firstMessage is not null)
                {
                    messageProcessor.ProcessMessage(firstMessage, monitorSessions, effectiveSettings.MeasurementKind, cancellationToken);
                }

                continue;
            }

            if (readResult.Status == SensorReadStatus.InvalidPayload)
            {
                stateStore.AddEvent($"Skipping invalid JSON: {readResult.RawLine}", RuntimeEventSeverity.Warning);
                continue;
            }

            var sensorMessage = readResult.Message!;
            stateStore.SetLatestSensor(sensorMessage);
            messageProcessor.ProcessMessage(sensorMessage, monitorSessions, effectiveSettings.MeasurementKind, cancellationToken);
        }

        sensorReader.Dispose();
        stateStore.SetLifecycle(AppLifecycleState.Stopped, "Stopped.");
        stateStore.AddEvent("Application stopped.", RuntimeEventSeverity.Info);
        return 0;
    }

    private static bool IsTelemetryStale(RuntimeStateStore stateStore, int discoveryTimeoutMs)
    {
        var latestSensor = stateStore.GetSnapshot().LatestSensor;
        if (latestSensor is null)
        {
            return false;
        }

        var staleAfter = TimeSpan.FromMilliseconds(Math.Max(3000, discoveryTimeoutMs * 2));
        return DateTimeOffset.Now - latestSensor.ReceivedAt > staleAfter;
    }

    private static bool TryConnectSensor(
        string configPath,
        int serialBaudRate,
        int discoveryTimeoutMs,
        IReadOnlyList<MonitorSession> monitorSessions,
        RuntimeStateStore stateStore,
        MessageProcessor messageProcessor,
        GitHubReleaseClient gitHubReleaseClient,
        ApplicationUpdateService applicationUpdateService,
        ref GitHubReleaseInfo? latestRelease,
        CancellationToken cancellationToken,
        ref AppConfig config,
        ref ResolvedAppSettings effectiveSettings,
        out SerialSensorReader sensorReader,
        out SensorMessage? firstMessage)
    {
        sensorReader = null!;
        firstMessage = null;
        var discoveryProbeTimeoutMs = Math.Clamp(discoveryTimeoutMs / 3, 300, 750);
        string? lastDiscoveryWarning = null;
        var lastDiscoveryWarningAt = DateTimeOffset.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            ProcessSettingsRequests(
                configPath,
                ref config,
                ref effectiveSettings,
                monitorSessions,
                stateStore);
            if (ProcessApplicationUpdateRequests(
                    stateStore,
                    gitHubReleaseClient,
                    applicationUpdateService,
                    ref latestRelease,
                    cancellationToken))
            {
                return false;
            }

            messageProcessor.ApplyPendingManualBrightness(monitorSessions, cancellationToken);

            stateStore.SetLifecycle(AppLifecycleState.Waiting, "Waiting for sensor telemetry...");
            stateStore.ClearLatestSensor();

            var discovery = new SerialPortDiscovery(
                serialBaudRate,
                discoveryProbeTimeoutMs);
            var discoveryResult = discovery.ResolveFirstTelemetry();
            if (discoveryResult.Status != SerialPortDiscoveryStatus.Success || string.IsNullOrWhiteSpace(discoveryResult.PortName))
            {
                var warning = discoveryResult.Error ?? "Sensor is not connected yet.";
                var now = DateTimeOffset.Now;
                if (!string.Equals(lastDiscoveryWarning, warning, StringComparison.Ordinal) ||
                    now - lastDiscoveryWarningAt > TimeSpan.FromSeconds(10))
                {
                    stateStore.AddEvent(warning, RuntimeEventSeverity.Warning);
                    lastDiscoveryWarning = warning;
                    lastDiscoveryWarningAt = now;
                }

                SleepBeforeReconnect(cancellationToken);
                continue;
            }

            var portName = discoveryResult.PortName;
            var candidateReader = new SerialSensorReader(portName, serialBaudRate);

            try
            {
                candidateReader.Open();
            }
            catch (Exception exception)
            {
                candidateReader.Dispose();
                stateStore.AddEvent($"Failed to open COM port '{portName}': {exception.Message}", RuntimeEventSeverity.Warning);
                SleepBeforeReconnect(cancellationToken);
                continue;
            }

            stateStore.SetConnection(
                portName,
                serialBaudRate,
                "Resolved port via telemetry probe.");
            stateStore.AddEvent($"Resolved COM port {portName} @ {serialBaudRate}.", RuntimeEventSeverity.Success);

            stateStore.SetLifecycle(AppLifecycleState.Waiting, "Waiting for first valid telemetry...");
            firstMessage = ReadFirstValidMessage(candidateReader, discoveryTimeoutMs, stateStore, cancellationToken);
            if (firstMessage is null)
            {
                candidateReader.Dispose();
                stateStore.AddEvent("Connected port did not produce valid telemetry yet.", RuntimeEventSeverity.Warning);
                SleepBeforeReconnect(cancellationToken);
                continue;
            }

            stateStore.SetLatestSensor(firstMessage);

            foreach (var session in monitorSessions)
            {
                session.ReplaceProcessor(new BrightnessProcessor(CreateBrightnessSettings(effectiveSettings)));
            }

            stateStore.SetDeviceSettings(
                effectiveSettings,
                $"Connected to LumaBloom protocol '{effectiveSettings.ProtocolId}'. Effective settings: {Describe(effectiveSettings)}");
            stateStore.AddEvent(
                $"Validated LumaBloom telemetry on {portName} using protocol '{effectiveSettings.ProtocolId}'.",
                RuntimeEventSeverity.Success);
            stateStore.AddEvent($"Effective settings: {Describe(effectiveSettings)}", RuntimeEventSeverity.Info);

            sensorReader = candidateReader;
            return true;
        }

        return false;
    }

    private static bool ProcessApplicationUpdateRequests(
        RuntimeStateStore stateStore,
        GitHubReleaseClient gitHubReleaseClient,
        ApplicationUpdateService applicationUpdateService,
        ref GitHubReleaseInfo? latestRelease,
        CancellationToken cancellationToken)
    {
        while (stateStore.TryConsumePrereleasePreferenceUpdateRequest(out var preferenceRequest))
        {
            var current = stateStore.GetSnapshot().AppUpdate ??
                          new AppUpdateSnapshot(AppVersion.Current, "Unknown", "Not checked yet.", null, false, false);
            stateStore.SetAppUpdateState(current with
            {
                IncludePrerelease = preferenceRequest.IncludePrerelease
            });
            latestRelease = null;
            stateStore.RequestAppUpdateCheck();
        }

        while (stateStore.TryConsumeAppUpdateRequest(out var request))
        {
            switch (request)
            {
                case AppUpdateActionRequest.CheckLatestRelease:
                    var includePrerelease = stateStore.GetSnapshot().AppUpdate?.IncludePrerelease == true;
                    stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                        AppVersion.Current,
                        latestRelease?.Version ?? "Checking...",
                        "Checking GitHub Releases...",
                        latestRelease?.PackageName,
                        latestRelease is not null && IsRemoteVersionNewer(AppVersion.Current, latestRelease.Version),
                        IsBusy: true,
                        IncludePrerelease: includePrerelease,
                        IsPrerelease: latestRelease?.IsPrerelease == true));
                    if (gitHubReleaseClient.TryGetLatestPortableRelease(includePrerelease, out latestRelease, out var statusMessage))
                    {
                        var updateAvailable = latestRelease is not null &&
                                              IsRemoteVersionNewer(AppVersion.Current, latestRelease.Version);
                        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                            AppVersion.Current,
                            latestRelease?.Version ?? "Unknown",
                            updateAvailable
                                ? $"Update available from GitHub Releases. {statusMessage}"
                                : $"Application is up to date. {statusMessage}",
                            latestRelease?.PackageName,
                            updateAvailable,
                            IsBusy: false,
                            IncludePrerelease: includePrerelease,
                            IsPrerelease: latestRelease?.IsPrerelease == true));
                        stateStore.AddEvent(statusMessage, RuntimeEventSeverity.Success);
                    }
                    else
                    {
                        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                            AppVersion.Current,
                            latestRelease?.Version ?? "Unknown",
                            statusMessage,
                            latestRelease?.PackageName,
                            latestRelease is not null && IsRemoteVersionNewer(AppVersion.Current, latestRelease.Version),
                            IsBusy: false,
                            IncludePrerelease: includePrerelease,
                            IsPrerelease: latestRelease?.IsPrerelease == true));
                        stateStore.AddEvent(statusMessage, RuntimeEventSeverity.Warning);
                    }

                    break;

                case AppUpdateActionRequest.ApplyLatestRelease:
                    var includePrereleaseForApply = stateStore.GetSnapshot().AppUpdate?.IncludePrerelease == true;
                    if (latestRelease is null)
                    {
                        if (!gitHubReleaseClient.TryGetLatestPortableRelease(includePrereleaseForApply, out latestRelease, out var fetchStatus))
                        {
                            stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                                AppVersion.Current,
                                "Unknown",
                                fetchStatus,
                                null,
                                UpdateAvailable: false,
                                IsBusy: false,
                                IncludePrerelease: includePrereleaseForApply,
                                IsPrerelease: false));
                            stateStore.AddEvent(fetchStatus, RuntimeEventSeverity.Warning);
                            break;
                        }
                    }

                    if (latestRelease is null)
                    {
                        var missingReleaseStatus = "Latest GitHub release information is unavailable.";
                        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                            AppVersion.Current,
                            "Unknown",
                            missingReleaseStatus,
                            null,
                            UpdateAvailable: false,
                            IsBusy: false,
                            IncludePrerelease: includePrereleaseForApply,
                            IsPrerelease: false));
                        stateStore.AddEvent(missingReleaseStatus, RuntimeEventSeverity.Warning);
                        break;
                    }

                    if (!IsRemoteVersionNewer(AppVersion.Current, latestRelease.Version))
                    {
                        var noUpdateStatus = "Current application version is already up to date.";
                        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                            AppVersion.Current,
                            latestRelease.Version,
                            noUpdateStatus,
                            latestRelease.PackageName,
                            UpdateAvailable: false,
                            IsBusy: false,
                            IncludePrerelease: includePrereleaseForApply,
                            IsPrerelease: latestRelease.IsPrerelease));
                        stateStore.AddEvent(noUpdateStatus, RuntimeEventSeverity.Info);
                        break;
                    }

                    try
                    {
                        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                            AppVersion.Current,
                            latestRelease.Version,
                            "Downloading and preparing the new application package...",
                            latestRelease.PackageName,
                            UpdateAvailable: true,
                            IsBusy: true,
                            IncludePrerelease: includePrereleaseForApply,
                            IsPrerelease: latestRelease.IsPrerelease));
                        applicationUpdateService.PrepareAndLaunchUpdate(latestRelease, gitHubReleaseClient, cancellationToken);
                        stateStore.AddEvent(
                            $"Prepared application update to {latestRelease.Version}. The app will restart after exit.",
                            RuntimeEventSeverity.Success);
                        return true;
                    }
                    catch (Exception exception)
                    {
                        stateStore.SetAppUpdateState(new AppUpdateSnapshot(
                            AppVersion.Current,
                            latestRelease.Version,
                            $"Application update failed: {exception.Message}",
                            latestRelease.PackageName,
                            UpdateAvailable: true,
                            IsBusy: false,
                            IncludePrerelease: includePrereleaseForApply,
                            IsPrerelease: latestRelease.IsPrerelease));
                        stateStore.AddEvent(
                            $"Application update failed: {exception.Message}",
                            RuntimeEventSeverity.Error);
                    }

                    break;
            }
        }

        return false;
    }

    private static bool ProcessFirmwareUpdateRequests(
        string configPath,
        int serialBaudRate,
        int discoveryTimeoutMs,
        IReadOnlyList<MonitorSession> monitorSessions,
        RuntimeStateStore stateStore,
        MessageProcessor messageProcessor,
        GitHubReleaseClient gitHubReleaseClient,
        ApplicationUpdateService applicationUpdateService,
        ref GitHubReleaseInfo? latestRelease,
        FirmwareFlashService firmwareFlashService,
        BundledFirmwareInfo? bundledFirmware,
        CancellationToken cancellationToken,
        ref AppConfig config,
        ref ResolvedAppSettings effectiveSettings,
        ref SerialSensorReader sensorReader,
        out SensorMessage? firstMessage)
    {
        firstMessage = null;

        while (stateStore.TryConsumeFirmwareUpdateRequest(out _))
        {
            var currentFirmwareState = stateStore.GetSnapshot().BundledFirmware ??
                                       new BundledFirmwareSnapshot("Unknown", "n/a", "Bundled firmware state unavailable.", false, false);

            if (bundledFirmware is null)
            {
                var missingStatus = "Bundled firmware is not available in this application package.";
                stateStore.SetBundledFirmwareState(currentFirmwareState with { StatusMessage = missingStatus, IsBusy = false });
                stateStore.AddEvent(missingStatus, RuntimeEventSeverity.Warning);
                continue;
            }

            var portName = stateStore.GetSnapshot().PortName;
            if (string.IsNullOrWhiteSpace(portName))
            {
                var portStatus = "Connect the ESP32-C6 device before flashing bundled firmware.";
                stateStore.SetBundledFirmwareState(currentFirmwareState with { StatusMessage = portStatus, IsBusy = false });
                stateStore.AddEvent(portStatus, RuntimeEventSeverity.Warning);
                continue;
            }

            stateStore.SetBundledFirmwareState(currentFirmwareState with
            {
                StatusMessage = $"Flashing bundled firmware {bundledFirmware.Version} on {portName}...",
                IsBusy = true
            });

            sensorReader.Dispose();
            stateStore.ClearLatestSensor();

            try
            {
                firmwareFlashService.Flash(bundledFirmware, portName);
                stateStore.SetBundledFirmwareState(currentFirmwareState with
                {
                    Version = bundledFirmware.Version,
                    FileName = bundledFirmware.FileName,
                    StatusMessage = $"Bundled firmware {bundledFirmware.Version} flashed successfully.",
                    IsAvailable = true,
                    IsBusy = false
                });
                stateStore.AddEvent(
                    $"Bundled firmware {bundledFirmware.Version} flashed successfully on {portName}.",
                    RuntimeEventSeverity.Success);
            }
            catch (Exception exception)
            {
                stateStore.SetBundledFirmwareState(currentFirmwareState with
                {
                    Version = bundledFirmware.Version,
                    FileName = bundledFirmware.FileName,
                    StatusMessage = $"Firmware flashing failed: {exception.Message}",
                    IsAvailable = true,
                    IsBusy = false
                });
                stateStore.AddEvent($"Firmware flashing failed: {exception.Message}", RuntimeEventSeverity.Error);
            }

            if (!TryConnectSensor(
                    configPath,
                    serialBaudRate,
                    discoveryTimeoutMs,
                    monitorSessions,
                    stateStore,
                    messageProcessor,
                    gitHubReleaseClient,
                    applicationUpdateService,
                    ref latestRelease,
                    cancellationToken,
                    ref config,
                    ref effectiveSettings,
                    out sensorReader,
                    out firstMessage))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRemoteVersionNewer(string currentVersion, string remoteVersion)
    {
        if (Version.TryParse(NormalizeVersion(remoteVersion), out var remote) &&
            Version.TryParse(NormalizeVersion(currentVersion), out var current))
        {
            return remote > current;
        }

        return !string.Equals(
            NormalizeVersion(currentVersion),
            NormalizeVersion(remoteVersion),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string version)
    {
        return version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? version[1..]
            : version;
    }

    private static void SleepBeforeReconnect(CancellationToken cancellationToken)
    {
        try
        {
            Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void ProcessSettingsRequests(
        string configPath,
        ref AppConfig config,
        ref ResolvedAppSettings effectiveSettings,
        IReadOnlyList<MonitorSession> monitorSessions,
        RuntimeStateStore stateStore)
    {
        while (stateStore.TryConsumeLanguageUpdateRequest(out var languageRequest))
        {
            try
            {
                AppConfigWriter.UpdateUiLanguage(configPath, languageRequest.Code);
                config = AppConfigLoader.Load(configPath);
                stateStore.SetLanguage(languageRequest.Language);
                stateStore.AddEvent($"UI language saved: {languageRequest.Code}.", RuntimeEventSeverity.Success);
            }
            catch (Exception exception)
            {
                stateStore.AddEvent($"Failed to save UI language: {exception.Message}", RuntimeEventSeverity.Error);
            }
        }

        while (stateStore.TryConsumeAutostartUpdateRequest(out var autostartRequest))
        {
            try
            {
                WindowsAutostartManager.SetEnabled(autostartRequest.Enabled);
                stateStore.SetAutostartEnabled(WindowsAutostartManager.IsEnabled());
                stateStore.AddEvent(
                    autostartRequest.Enabled
                        ? "Windows autostart enabled."
                        : "Windows autostart disabled.",
                    RuntimeEventSeverity.Success);
            }
            catch (Exception exception)
            {
                stateStore.SetAutostartEnabled(WindowsAutostartManager.IsEnabled());
                stateStore.AddEvent($"Failed to update Windows autostart: {exception.Message}", RuntimeEventSeverity.Error);
            }
        }

        while (stateStore.TryConsumeProcessingUpdateRequest(out var processingRequest))
        {
            try
            {
                AppConfigWriter.UpdateProcessing(configPath, processingRequest.Parameter, processingRequest.Value);
                var updatedConfig = AppConfigLoader.Load(configPath);
                var updatedSettings = ResolvedSettingsFactory.Create(updatedConfig);

                foreach (var session in monitorSessions)
                {
                    session.ReplaceProcessor(new BrightnessProcessor(CreateBrightnessSettings(updatedSettings)));
                }

                config = updatedConfig;
                effectiveSettings = updatedSettings;
                stateStore.SetEffectiveSettings(updatedSettings, $"Effective settings: {Describe(updatedSettings)}");
                stateStore.AddEvent(
                    $"Processing setting saved and applied: {processingRequest.Parameter}={processingRequest.Value}.",
                    RuntimeEventSeverity.Success);
            }
            catch (Exception exception)
            {
                stateStore.AddEvent(
                    $"Failed to save/apply processing setting {processingRequest.Parameter}: {exception.Message}",
                    RuntimeEventSeverity.Error);
            }
        }

        while (stateStore.TryConsumeBrightnessCurveUpdateRequest(out var curveRequest))
        {
            try
            {
                AppConfigWriter.UpdateBrightnessCurvePoint(
                    configPath,
                    curveRequest.LightPercent,
                    curveRequest.BrightnessPercent);
                var updatedConfig = AppConfigLoader.Load(configPath);
                var updatedSettings = ResolvedSettingsFactory.Create(updatedConfig);

                foreach (var session in monitorSessions)
                {
                    session.ReplaceProcessor(new BrightnessProcessor(CreateBrightnessSettings(updatedSettings)));
                }

                config = updatedConfig;
                effectiveSettings = updatedSettings;
                stateStore.SetEffectiveSettings(updatedSettings, $"Effective settings: {Describe(updatedSettings)}");
                stateStore.AddEvent(
                    $"Brightness curve point saved and applied: {curveRequest.LightPercent}% -> {curveRequest.BrightnessPercent}%.",
                    RuntimeEventSeverity.Success);
            }
            catch (Exception exception)
            {
                stateStore.AddEvent(
                    $"Failed to save/apply brightness curve point {curveRequest.LightPercent}%: {exception.Message}",
                    RuntimeEventSeverity.Error);
            }
        }

        while (stateStore.TryConsumeAnchorCurrentLightCurveRequest(out var anchorRequest))
        {
            try
            {
                var snapshot = stateStore.GetSnapshot();
                if (!BrightnessCurveAnchorHelper.TryGetAmbientPercent(snapshot.LatestSensor, effectiveSettings, out var ambientPercent))
                {
                    stateStore.AddEvent(
                        "Cannot anchor the brightness curve yet: live sensor data is not available.",
                        RuntimeEventSeverity.Warning);
                    continue;
                }

                var rebuiltCurve = BrightnessCurveAnchorHelper.RebuildAnchoredCurve(
                    effectiveSettings.Brightness.Curve,
                    ambientPercent,
                    anchorRequest.DesiredBrightnessPercent);
                AppConfigWriter.UpdateBrightnessCurve(configPath, rebuiltCurve);
                var updatedConfig = AppConfigLoader.Load(configPath);
                var updatedSettings = ResolvedSettingsFactory.Create(updatedConfig);

                foreach (var session in monitorSessions)
                {
                    session.ReplaceProcessor(new BrightnessProcessor(CreateBrightnessSettings(updatedSettings)));
                }

                config = updatedConfig;
                effectiveSettings = updatedSettings;
                stateStore.SetEffectiveSettings(updatedSettings, $"Effective settings: {Describe(updatedSettings)}");
                stateStore.ForceNextAutoBrightnessApply();

                if (stateStore.IsPaused)
                {
                    stateStore.AddEvent(
                        $"Anchored curve at current light {ambientPercent}% -> {anchorRequest.DesiredBrightnessPercent}%. Saved the curve and queued the next auto apply for after pause.",
                        RuntimeEventSeverity.Success);
                    continue;
                }

                foreach (var session in monitorSessions)
                {
                    if (!session.IsEnabled)
                    {
                        continue;
                    }

                    if (!session.Monitor.TrySetBrightness(anchorRequest.DesiredBrightnessPercent, out var error))
                    {
                        session.Disable();
                        stateStore.RecordMonitorDisabled(session.Monitor.Source, session.Monitor.Name, error ?? "Unknown error");
                        stateStore.AddEvent(
                            $"Immediate anchored-brightness update failed ({session.Monitor.Source}:{session.Monitor.Name}): {error}",
                            RuntimeEventSeverity.Error);
                        continue;
                    }

                    stateStore.RecordBrightnessApplied(
                        session.Monitor.Source,
                        session.Monitor.Name,
                        anchorRequest.DesiredBrightnessPercent,
                        anchorRequest.DesiredBrightnessPercent,
                        ambientPercent / 100.0,
                        ambientPercent / 100.0);
                }

                stateStore.AddEvent(
                    $"Anchored curve at current light {ambientPercent}% -> {anchorRequest.DesiredBrightnessPercent}%. Saved and applied immediately.",
                    RuntimeEventSeverity.Success);
            }
            catch (Exception exception)
            {
                stateStore.AddEvent(
                    $"Failed to anchor the brightness curve from current light: {exception.Message}",
                    RuntimeEventSeverity.Error);
            }
        }

        while (stateStore.TryConsumeTestBrightnessRequest(out var testRequest))
        {
            foreach (var session in monitorSessions)
            {
                if (!session.IsEnabled)
                {
                    continue;
                }

                if (!session.Monitor.TrySetBrightness(testRequest.BrightnessPercent, out var error))
                {
                    session.Disable();
                    stateStore.RecordMonitorDisabled(session.Monitor.Source, session.Monitor.Name, error ?? "Unknown error");
                    stateStore.AddEvent(
                        $"Test brightness failed ({session.Monitor.Source}:{session.Monitor.Name}): {error}",
                        RuntimeEventSeverity.Error);
                    continue;
                }

                stateStore.RecordBrightnessApplied(
                    session.Monitor.Source,
                    session.Monitor.Name,
                    testRequest.BrightnessPercent,
                    testRequest.BrightnessPercent,
                    normalized: 0,
                    filtered: 0);
            }

            stateStore.AddEvent($"Test brightness applied: {testRequest.BrightnessPercent}%.", RuntimeEventSeverity.Info);
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
            settings.Processing.MaxBrightnessStepPercent,
            settings.Processing.Gamma,
            settings.Brightness.MinPercent,
            settings.Brightness.MaxPercent,
            settings.Brightness.Curve
                .Select(point => new MathCurvePoint(point.LightPercent, point.BrightnessPercent))
                .ToArray());
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

    private static int GetCalibrationSample(SensorMessage sensorMessage)
    {
        if (!sensorMessage.Raw.HasValue)
        {
            throw new InvalidOperationException("Calibration requires raw telemetry.");
        }

        return sensorMessage.Raw.Value;
    }

    private static string Describe(ResolvedAppSettings settings)
    {
        return $"protocolId={settings.ProtocolId}, measurement={settings.MeasurementKind}, baudRate={settings.BaudRate}, discoveryTimeoutMs={settings.DiscoveryTimeoutMs}, adc=[{settings.Processing.AdcMin}..{settings.Processing.AdcMax}], invert={settings.Processing.Invert}, emaAlpha={FormatNumber(settings.Processing.EmaAlpha)}, hysteresisPercent={settings.Processing.HysteresisPercent}, maxBrightnessStepPercent={settings.Processing.MaxBrightnessStepPercent}, gamma={FormatNullableNumber(settings.Processing.Gamma)}, brightness=[{settings.Brightness.MinPercent}..{settings.Brightness.MaxPercent}], curve={FormatCurve(settings.Brightness.Curve)}";
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

    private static string FormatCurve(IReadOnlyList<BrightnessCurvePoint> curve)
    {
        return string.Join(",", curve.OrderBy(point => point.LightPercent).Select(point => $"{point.LightPercent}->{point.BrightnessPercent}"));
    }
}
