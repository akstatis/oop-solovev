using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chat.Common;

/// <summary>Типы сообщений протокола. Одна строка — одно сообщение.</summary>
public static class MessageTypes
{
    public const string Login = "login";
    public const string LoginOk = "login-ok";
    public const string LoginError = "login-error";
    public const string Text = "text";
    public const string Notice = "notice";
    public const string Users = "users";

    /// <summary>Клиент просит прислать список участников (например, сразу после входа).</summary>
    public const string Who = "who";
}

/// <summary>
/// Одно сообщение чата. Сериализуется в JSON и обратно; поля необязательные,
/// поэтому один класс обслуживает и вход, и текст, и список участников.
/// </summary>
public sealed class ChatMessage
{
    public string Type { get; set; } = "";

    public string Nick { get; set; } = "";

    /// <summary>Кому адресовано личное сообщение; null — всем.</summary>
    public string? Target { get; set; }

    public string Text { get; set; } = "";

    public DateTime Time { get; set; }

    /// <summary>Список участников — приходит в сообщении типа users.</summary>
    public List<string>? Users { get; set; }

    /// <summary>Признак того, что сообщение взято из истории сервера.</summary>
    public bool History { get; set; }

    /// <summary>Признак личного сообщения; в JSON не пишется — он вычисляется из Target.</summary>
    [JsonIgnore]
    public bool IsPrivate => !string.IsNullOrWhiteSpace(Target);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static ChatMessage FromJson(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidDataException("Пустая строка вместо сообщения.");
        }

        ChatMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<ChatMessage>(line, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Не удалось разобрать сообщение чата.", ex);
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Type))
        {
            throw new InvalidDataException("В сообщении нет типа.");
        }

        return message;
    }

    public static ChatMessage Login(string nick) => new()
    {
        Type = MessageTypes.Login,
        Nick = nick,
        Time = DateTime.Now,
    };

    public static ChatMessage LoginOk(string nick) => new()
    {
        Type = MessageTypes.LoginOk,
        Nick = nick,
        Text = "Добро пожаловать в чат!",
        Time = DateTime.Now,
    };

    public static ChatMessage LoginError(string error) => new()
    {
        Type = MessageTypes.LoginError,
        Text = error,
        Time = DateTime.Now,
    };

    public static ChatMessage Say(string nick, string text, string? target = null, bool history = false) => new()
    {
        Type = MessageTypes.Text,
        Nick = nick,
        Text = text,
        Target = target,
        Time = DateTime.Now,
        History = history,
    };

    public static ChatMessage Notice(string text) => new()
    {
        Type = MessageTypes.Notice,
        Text = text,
        Time = DateTime.Now,
    };

    public static ChatMessage Online(IEnumerable<string> users) => new()
    {
        Type = MessageTypes.Users,
        Users = users.ToList(),
        Time = DateTime.Now,
    };

    public override string ToString() => $"[{Time:HH:mm}] {Nick}: {Text}";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
