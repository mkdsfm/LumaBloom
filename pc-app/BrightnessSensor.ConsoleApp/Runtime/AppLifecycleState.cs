namespace BrightnessSensor.ConsoleApp.Runtime;

internal enum AppLifecycleState
{
    Starting,
    Waiting,
    Running,
    Stopping,
    Stopped,
    Error
}
