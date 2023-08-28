namespace PhotographyBackupAssistant.Classes;

public class Logger
{
    public static void Log(string category, string log)
    {
        Console.WriteLine($"[{timeFormat(DateTime.Now)}] [{category}]: {log}");
    }
	private static string timeFormat(DateTime date)
	{
		return $"{date.Year}-{date.Month.ToString().PadLeft(2, '0')}-{date.Day.ToString().PadLeft(2, '0')} {date.Hour.ToString().PadLeft(2, '0')}:{date.Minute.ToString().PadLeft(2, '0')}:{date.Second.ToString().PadLeft(2, '0')}.{date.Millisecond.ToString().PadLeft(3, '0')}";
	}
}
