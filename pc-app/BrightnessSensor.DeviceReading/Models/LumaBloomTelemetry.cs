namespace BrightnessSensor.DeviceReading.Models;

public static class LumaBloomTelemetry
{
    public const string ProtocolId = "lumabloom";

    public static bool HasSupportedProtocolId(string? id)
    {
        return string.Equals(id, ProtocolId, StringComparison.Ordinal);
    }
}
