using System.Text;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal static class MouseInputParser
{
    public static bool TryParseSgrMouseSequence(string sequence, out UiMouseClick click)
    {
        click = default;

        if (!sequence.StartsWith("\u001b[<", StringComparison.Ordinal) ||
            !sequence.EndsWith('M'))
        {
            return false;
        }

        var body = sequence[3..^1];
        var parts = body.Split(';');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var button) ||
            !int.TryParse(parts[1], out var x) ||
            !int.TryParse(parts[2], out var y))
        {
            return false;
        }

        if ((button & 0b11) != 0)
        {
            return false;
        }

        click = new UiMouseClick(x, y);
        return true;
    }

    public static string ReadEscapeSequenceIfAvailable(ConsoleKeyInfo firstKey)
    {
        if (firstKey.Key != ConsoleKey.Escape)
        {
            return firstKey.KeyChar.ToString();
        }

        var builder = new StringBuilder();
        builder.Append('\u001b');
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(100);
        while (DateTimeOffset.UtcNow <= deadline)
        {
            while (Console.KeyAvailable)
            {
                var next = Console.ReadKey(intercept: true);
                builder.Append(next.KeyChar);
                if (next.KeyChar is 'M' or 'm')
                {
                    return builder.ToString();
                }
            }

            Thread.Sleep(1);
        }

        return builder.ToString();
    }
}
