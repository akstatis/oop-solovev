namespace Chat.Common;

/// <summary>Правила ника: одно место, где решается, годится ли имя.</summary>
public static class Nicknames
{
    public const int MaxLength = 20;

    public static string Normalize(string? nick) => (nick ?? "").Trim();

    /// <summary>Проверить ник. Возвращает false и причину, если ник не годится.</summary>
    public static bool IsValid(string? nick, out string error)
    {
        var value = Normalize(nick);

        if (value.Length == 0)
        {
            error = "Ник не может быть пустым.";
            return false;
        }

        if (value.Length > MaxLength)
        {
            error = $"Ник длиннее {MaxLength} символов.";
            return false;
        }

        if (value.Any(ch => char.IsControl(ch) || ch == '\u2028'))
        {
            error = "В нике есть недопустимые символы.";
            return false;
        }

        if (!value.Any(char.IsLetterOrDigit) && value.All(ch => ch is '_' or '-' or ' '))
        {
            error = "Ник должен содержать хотя бы одну букву или цифру.";
            return false;
        }

        error = "";
        return true;
    }

    public static bool IsValid(string? nick) => IsValid(nick, out _);
}
