using PhotographyBackupAssistant.Classes;
using PhotographyBackupAssistant.Models;
using Microsoft.Extensions.Hosting;
using Timer = System.Timers.Timer;

namespace PhotographyBackupAssistant.Services;

public class ConfigService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;
	private DateTime lastModified = DateTime.MinValue;

	public ConfigService(Config config)
	{
		this.config = config;
		config.ReadConfig();
		mainTimer = new();
		mainTimer.Interval = 50;
		mainTimer.Elapsed += OnTimerEvent;
	}
	public async Task StartAsync(CancellationToken stoppingToken)
	{
		Logger.Log(GetType().Name, "Starting");
		await Task.Run(() => mainTimer.Start());
		mainTimer.Interval = 10000;
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
			if (new FileInfo(config.configFilePath).LastWriteTimeUtc > lastModified)
			{
				Logger.Log(GetType().Name, $"Config file read");
				config.ReadConfig();
				lastModified = new FileInfo(config.configFilePath).LastWriteTimeUtc;
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during configuration read: {ex.Message}");
		}
	}
}