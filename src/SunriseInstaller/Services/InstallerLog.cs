using System.Globalization;

namespace Sunrise.Installer.Services;

public sealed class InstallerLog
{
    private readonly object gate = new();
    private readonly string logPath;

    public InstallerLog()
    {
        string directory = Path.Combine(AppConstants.AppDataRoot, "logs");
        Directory.CreateDirectory(directory);
        logPath = Path.Combine(directory, $"installer-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public event Action<string>? MessageWritten;

    public void Info(string eventName, string message, params (string Key, object? Value)[] fields) =>
        Write("info", eventName, message, fields);

    public void Error(string eventName, string message, params (string Key, object? Value)[] fields) =>
        Write("error", eventName, message, fields);

    private void Write(string level, string eventName, string message, params (string Key, object? Value)[] fields)
    {
        string fieldText = string.Join(' ', fields.Select(field => $"{field.Key}={Format(field.Value)}"));
        string line = $"installer ev={eventName} level={level}";
        if (fieldText.Length > 0)
        {
            line += " " + fieldText;
        }

        lock (gate)
        {
            File.AppendAllText(logPath, $"{DateTimeOffset.UtcNow:O} {line}{Environment.NewLine}");
        }

        MessageWritten?.Invoke(message);
    }

    private static string Format(object? value)
    {
        string text = value switch
        {
            null => "none",
            IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "none",
        };

        return text.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_');
    }
}
