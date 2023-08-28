using PhotographyBackupAssistant.Classes;
using PhotographyBackupAssistant.Models;
using Microsoft.Extensions.Hosting;
using Timer = System.Timers.Timer;

namespace PhotographyBackupAssistant.Services;

public class TransferExternalHDService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	public TransferExternalHDService(Config config)
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
			foreach (var directory in config.external.Where(x => x.active))
			{
				if (!Directory.Exists(directory.incoming))
				{
					Logger.Log(GetType().Name, $"Creating missing directory {directory.incoming}");
					Directory.CreateDirectory(directory.incoming);
				}
				if (!Directory.Exists(directory.spool))
				{
					Logger.Log(GetType().Name, $"Creating missing directory {directory.spool}");
					Directory.CreateDirectory(directory.spool);
				}
				if (!Directory.Exists(directory.storage))
				{
					Logger.Log(GetType().Name, $"Creating missing directory {directory.storage}");
					Directory.CreateDirectory(directory.storage);
				}
			}

			foreach (var directory in config.external.Where(x => x.active))
			{
				foreach (string fileFrom in Directory.GetFiles(directory.spool))
				{
					FileInfo fileFromInfo = new(fileFrom);
					Logger.Log(GetType().Name, $"{directory.spool}/{fileFromInfo.Name} -> {directory.storage}/{fileFromInfo.Name} - Export");
					FileOperations.FileCopy("ExternalHD", $"{directory.spool}/{fileFromInfo.Name}", directory.storage);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during external HD export: {ex.Message}");
		}
	}
}