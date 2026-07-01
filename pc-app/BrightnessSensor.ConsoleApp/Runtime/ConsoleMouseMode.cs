namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class ConsoleMouseMode : IDisposable
{
    private const string EnableMouse = "\u001b[?1000h\u001b[?1006h";
    private const string DisableMouse = "\u001b[?1000l\u001b[?1006l";
    private bool _disposed;

    public static ConsoleMouseMode Enable()
    {
        Console.Write(EnableMouse);
        return new ConsoleMouseMode();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Console.Write(DisableMouse);
        _disposed = true;
    }
}
