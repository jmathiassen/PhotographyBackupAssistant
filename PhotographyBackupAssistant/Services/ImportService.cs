using PhotographyBackupAssistant.Classes;
using PhotographyBackupAssistant.Models;
using Microsoft.Extensions.Hosting;
using Timer = System.Timers.Timer;

namespace PhotographyBackupAssistant.Services;

public class ImportService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	public ImportService(Config config)
    {
		this.config = config;
		mainTimer = new();
		mainTimer.Interval = 1;
		mainTimer.Elapsed += OnTimerEvent;
	}
	public async Task StartAsync(CancellationToken stoppingToken)
	{
		Logger.Log(GetType().Name, "Starting");
		await Task.Run(() => mainTimer.Start());
		mainTimer.Interval = 1000;
	}
	public async Task StopAsync(CancellationToken stoppingToken)
	{
		Logger.Log(GetType().Name, "Stopping");
		await Task.Run(() => mainTimer.Stop());
	}

	private void OnTimerEvent(object? source, System.Timers.ElapsedEventArgs e)
	{
		try
		{
			foreach (string importDirectory in config.import.importDirectories)
				if (!Directory.Exists(importDirectory))
					Directory.CreateDirectory(importDirectory);

			if (!Directory.Exists(config.import.incoming))
				Directory.CreateDirectory(config.import.incoming);
			if (!Directory.Exists(config.import.imported))
				Directory.CreateDirectory(config.import.imported);

			string? fileFound = null;
			foreach (string importDirectory in config.import.importDirectories)
			{
				do
				{
					fileFound = FileOperations.FindFile(importDirectory, config.import.fileTypes.Select(x => x.extension).ToArray());
					string? fileImported = null;
					if (fileFound != null)
					{
						FileInfo found = new(fileFound);
						if (found.LastAccessTimeUtc == found.LastWriteTimeUtc)
						{
							Logger.Log(GetType().Name, $"Ignoring {fileFound} because it's still being written.");
							break;
						}
						fileImported = FileOperations.FileCopy(GetType().Name, fileFound, config.import.incoming, dateRename: config.import.fileTypes.Where(x => x.extension == new FileInfo(fileFound).Extension.ToLower()).First().dateRename);
					}
					if (fileImported != null)
						FileOperations.FileMove(GetType().Name, $"{config.import.incoming}/{fileImported}", config.import.imported);
				}
				while (fileFound != null);
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during import: {ex.Message}, {ex.StackTrace}");
		}
	}
}