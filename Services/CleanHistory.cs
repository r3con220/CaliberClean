using System.Text.Json;

namespace CaliberClean.Services;

public record CleanHistoryRecord(DateTime LastCleanDate, long LastCleanFreedBytes);

public static class CleanHistory
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CaliberClean", "history.json");

    public static CleanHistoryRecord Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<CleanHistoryRecord>(File.ReadAllText(FilePath))
                    ?? new CleanHistoryRecord(DateTime.MinValue, 0);
        }
        catch { }
        return new CleanHistoryRecord(DateTime.MinValue, 0);
    }

    public static void Save(DateTime date, long freedBytes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new CleanHistoryRecord(date, freedBytes),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
