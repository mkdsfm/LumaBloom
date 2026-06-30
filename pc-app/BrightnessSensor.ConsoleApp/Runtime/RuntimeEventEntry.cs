namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed record RuntimeEventEntry(
    DateTimeOffset Timestamp,
    RuntimeEventSeverity Severity,
    string Message);
