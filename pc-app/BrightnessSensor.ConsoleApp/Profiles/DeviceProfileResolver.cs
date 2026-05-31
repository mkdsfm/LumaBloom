using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.DeviceReading.Models;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal sealed class DeviceProfileResolver(IReadOnlyList<DeviceProfile>? profiles = null)
{
    private readonly IReadOnlyList<DeviceProfile> _profiles = profiles ?? DeviceProfileCatalog.All;

    public DeviceProfile Resolve(AppConfig config, SensorMessage firstMessage, out string resolutionLog)
    {
        if (!string.IsNullOrWhiteSpace(config.DeviceProfile.ProfileId))
        {
            var forcedProfile = _profiles.FirstOrDefault(profile =>
                string.Equals(profile.ProfileId, config.DeviceProfile.ProfileId, StringComparison.OrdinalIgnoreCase));

            if (forcedProfile is null)
            {
                throw new InvalidOperationException(
                    $"Configured deviceProfile.profileId '{config.DeviceProfile.ProfileId}' was not found.");
            }

            resolutionLog =
                $"Using forced hardware profile '{forcedProfile.ProfileId}' for telemetry {firstMessage.DeviceId}/{firstMessage.SensorId}.";
            return forcedProfile;
        }

        if (!config.DeviceProfile.AutoDetect)
        {
            resolutionLog = $"Auto-detect disabled; using generic profile '{DeviceProfileCatalog.Generic.ProfileId}'.";
            return DeviceProfileCatalog.Generic;
        }

        var matchedProfile = _profiles.FirstOrDefault(profile =>
            string.Equals(profile.DeviceId, firstMessage.DeviceId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(profile.SensorId, firstMessage.SensorId, StringComparison.OrdinalIgnoreCase));

        if (matchedProfile is not null)
        {
            resolutionLog =
                $"Auto-detected hardware profile '{matchedProfile.ProfileId}' for telemetry {firstMessage.DeviceId}/{firstMessage.SensorId}.";
            return matchedProfile;
        }

        resolutionLog =
            $"No hardware profile found for telemetry {firstMessage.DeviceId}/{firstMessage.SensorId}; using generic profile '{DeviceProfileCatalog.Generic.ProfileId}'.";
        return DeviceProfileCatalog.Generic;
    }

    public DeviceProfile? TryResolveByProfileId(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        return _profiles.FirstOrDefault(profile =>
            string.Equals(profile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
    }
}
