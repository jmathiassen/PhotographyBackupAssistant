using PhotographyBackupAssistant.Classes;
using PhotographyBackupAssistant.Models;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Timer = System.Timers.Timer;

namespace PhotographyBackupAssistant.Services;

public class DemuxService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	public DemuxService(Config config)
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
			List<string> demuxDirectories = [];
			demuxDirectories.AddRange(config.external.Where(x => x.active).Select(x => x.incoming).ToList());
			demuxDirectories.AddRange(config.remote.Where(x => x.active).Select(x => x.incoming).ToList());
			int copied = 0;

			if (demuxDirectories.Count == 0)
				return;

			DirectoryInfo dirFrom = new(config.import.imported);
			foreach (FileInfo fileFrom in dirFrom.GetFiles())
			{
				foreach (var directory in config.external.Where(x => x.active))
				{
					string? fileToName = FileOperations.FileCopy(GetType().Name, $"{config.import.imported}/{fileFrom.Name}", directory.incoming, deleteAfterCopy: false);
					if (fileToName != null)
					{
						FileOperations.FileMove(GetType().Name, $"{directory.incoming}/{fileToName}", directory.spool);
						copied++;
					}
				}

				foreach (var directory in config.remote.Where(x => x.active))
				{
					string? fileToName = FileOperations.FileCopy(GetType().Name, $"{config.import.imported}/{fileFrom.Name}", directory.incoming, deleteAfterCopy: false);
					if (fileToName != null)
					{
						FileOperations.FileMove(GetType().Name, $"{directory.incoming}/{fileToName}", directory.spool);
						copied++;
					}
				}

				if (copied == demuxDirectories.Count)
				{
					File.Delete(fileFrom.FullName);
					copied = 0;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during demux: {ex.Message}");
		}
	}
}