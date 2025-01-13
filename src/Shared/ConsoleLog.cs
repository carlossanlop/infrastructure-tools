using System;

namespace InfrastructureTools.Shared;

internal static class ConsoleLog
{
    internal enum LogType
    {
        Error = 1,
        Warning = 2,
        Information = 4,
        Success = 8,
        Failure = 16,
    }

    public static void WriteInfo(string message) => WriteInternal(message, LogType.Information);
    public static void WriteWarning(string message) => WriteInternal(message, LogType.Warning);
    public static void WriteError(string message) => WriteInternal(message, LogType.Error);
    public static void WriteFailure(string message) => WriteInternal(message, LogType.Failure);
    public static void WriteSuccess(string message) => WriteInternal(message, LogType.Success);

    private static void WriteInternal(string message, LogType type)
    {
        Validate(message, type);
        ConsoleWrite(message, type);
    }

    private static void ConsoleWrite(string message, LogType type)
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        ConsoleColor newColor = type switch
        {
            LogType.Information => ConsoleColor.White,
            LogType.Warning => ConsoleColor.Yellow,
            LogType.Error => ConsoleColor.Red,
            LogType.Success => ConsoleColor.Green,
            LogType.Failure => ConsoleColor.DarkRed,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        Console.ForegroundColor = newColor;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    private static void Validate(string message, LogType type)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}