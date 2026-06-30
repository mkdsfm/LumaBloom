namespace BrightnessSensor.ConsoleApp.Runtime;

internal static class RuntimeCommandMapper
{
    public static bool TryMap(ConsoleKeyInfo keyInfo, out RuntimeCommand command)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Q:
                command = RuntimeCommand.Stop;
                return true;
            case ConsoleKey.P:
                command = RuntimeCommand.TogglePause;
                return true;
            case ConsoleKey.L:
                command = RuntimeCommand.ToggleLogVisibility;
                return true;
            case ConsoleKey.C:
                command = RuntimeCommand.Recalibrate;
                return true;
            default:
                command = default;
                return false;
        }
    }
}
