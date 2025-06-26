public static class TrackerLogger
{
    private static readonly string logFolder = @"C:\TorrentLogs\Tracker";
    private static readonly string logFilePath;

    static TrackerLogger()
    {
        // Ensure the log folder exists
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }
        string timestamp = DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");
        logFilePath = Path.Combine(logFolder, $"tracker_log_{timestamp}.txt");
    }

    public static void Log(string message)
    {
        string timestampedMsg = message;
        Console.WriteLine(timestampedMsg);
        try
        {
            File.AppendAllText(logFilePath, timestampedMsg + Environment.NewLine);
        }
        catch { /* Optional: Handle exceptions if needed */ }
    }
}