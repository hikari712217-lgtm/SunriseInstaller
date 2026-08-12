namespace Sunrise.Installer;

public sealed record OperationProgress(string Message, int? Percent = null);
