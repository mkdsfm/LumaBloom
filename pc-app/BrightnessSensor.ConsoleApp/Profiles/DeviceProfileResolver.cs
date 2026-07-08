using BrightnessSensor.ConsoleApp.Configuration;
using BrightnessSensor.DeviceReading.Models;

namespace BrightnessSensor.ConsoleApp.Profiles;

internal sealed class DeviceProfileResolver(IReadOnlyList<DeviceProfile>? profiles = null)
{
    private readonly IReadOnlyList<DeviceProfile> _profiles = profiles ?? DeviceProfileCatalog.All;

    public DeviceProfile Resolve(SensorMessage firstMessage, out string resolutionLog)
    {
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
}
