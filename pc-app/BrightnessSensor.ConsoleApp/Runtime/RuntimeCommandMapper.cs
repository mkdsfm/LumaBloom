namespace BrightnessSensor.ConsoleApp.Runtime;

internal static class RuntimeCommandMapper
{
    public static bool TryMap(ConsoleKeyInfo keyInfo, out UiInputIntent intent)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.LeftArrow:
                intent = new UiInputIntent(UiInputIntentKind.MovePrevious);
                return true;
            case ConsoleKey.RightArrow:
                intent = new UiInputIntent(UiInputIntentKind.MoveNext);
                return true;
            case ConsoleKey.UpArrow:
                intent = new UiInputIntent(UiInputIntentKind.MoveUp);
                return true;
            case ConsoleKey.DownArrow:
                intent = new UiInputIntent(UiInputIntentKind.MoveDown);
                return true;
            case ConsoleKey.Enter:
                intent = new UiInputIntent(UiInputIntentKind.Activate);
                return true;
            case ConsoleKey.Escape:
                intent = new UiInputIntent(UiInputIntentKind.Back);
                return true;
            case ConsoleKey.Backspace:
                intent = new UiInputIntent(UiInputIntentKind.Backspace);
                return true;
            default:
                if (char.IsDigit(keyInfo.KeyChar))
                {
                    intent = UiInputIntent.AppendDigit(keyInfo.KeyChar);
                    return true;
                }

                intent = default;
                return false;
        }
    }
}
