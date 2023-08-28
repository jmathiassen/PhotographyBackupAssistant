using PhotographyBackupAssistant.Classes;
using PhotographyBackupAssistant.Models;
using Microsoft.Extensions.Hosting;
using Renci.SshNet;
using Timer = System.Timers.Timer;

namespace PhotographyBackupAssistant.Services;

public class TransferRemoteService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	public TransferRemoteService(Config config)
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

	private bool CheckConnection(RemoteHost remoteHost)
	{
		try
		{
			if (remoteHost.scpClient == null)
			{
				try
				{
					Logger.Log(GetType().Name, $"{remoteHost.host} - initialize connection configuration");
					remoteHost.scpClient = new ScpClient(remoteHost.host, remoteHost.port, remoteHost.username, [new PrivateKeyFile(remoteHost.pubKeyPath)]);
				}
				catch (Exception e)
				{
					Logger.Log(GetType().Name, $"{remoteHost.host} - initialize connection configuration failed: {e.Message}");
					remoteHost.connectionCheckTime = DateTime.Now.AddSeconds(30);
					throw;
				}
			}

			if (!remoteHost.scpClient.IsConnected && DateTime.Now > remoteHost.connectionCheckTime)
			{
				try
				{
					Logger.Log(GetType().Name, $"{remoteHost.host} - Open connection");
					remoteHost.scpClient.Connect();
				}
				catch (Exception e)
				{
					Logger.Log(GetType().Name, $"{remoteHost.host} - Open connection failed: {e.Message}");
					remoteHost.scpClient.Disconnect();
					remoteHost.connectionCheckTime = DateTime.Now.AddSeconds(30);
					throw;
				}
			}
		}
		catch (Exception e)
		{
			Logger.Log(GetType().Name, $"Problem with SCP connection: {e.Message}");
			return false;
		}

		return true;
	}
	private void OnTimerEvent(object? source, System.Timers.ElapsedEventArgs e)
	{
		try
		{
			foreach (var directory in config.remote.Where(x => x.active))
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
			}

			foreach (var remoteHost in config.remote.Where(x => x.active))
			{
				DirectoryInfo remoteSpoolDirectory = new(remoteHost.spool);
				if (remoteSpoolDirectory.GetFiles().Length == 0)
					continue;

				if (!CheckConnection(remoteHost))
					continue;

				foreach (FileInfo fileFrom in remoteSpoolDirectory.GetFiles())
				{
					try
					{
						using (var streamFrom = File.OpenRead(fileFrom.FullName))
						{
							remoteHost.scpClient!.Upload(streamFrom, $"{remoteHost.directory}/{fileFrom.Name}");
							Logger.Log(GetType().Name, $"{remoteHost.spool}/{fileFrom.Name} -> {remoteHost.host}:{remoteHost.directory}/{fileFrom.Name} - file uploaded");
						}
						File.Delete(fileFrom.FullName);
						Logger.Log(GetType().Name, $"{remoteHost.directory}/{fileFrom.Name} - file removed");
					}
					catch (Exception ex)
					{
						Logger.Log(GetType().Name, $"{remoteHost.host} - transfer failed: {ex.Message}");
						remoteHost.scpClient!.Disconnect();
						//remoteHost.connectionCheckTime = DateTime.Now.AddSeconds(3);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during remote host export: {ex.Message}");
		}
	}
}