namespace BrightnessSensor.ConsoleApp.Runtime;

internal enum UiInputIntentKind
{
    MovePrevious,
    MoveNext,
    MoveUp,
    MoveDown,
    Activate,
    Back,
    Backspace,
    AppendDigit
}

internal readonly record struct UiInputIntent(UiInputIntentKind Kind, char? Digit = null)
{
    public static UiInputIntent AppendDigit(char digit)
    {
        return new UiInputIntent(UiInputIntentKind.AppendDigit, digit);
    }
}
