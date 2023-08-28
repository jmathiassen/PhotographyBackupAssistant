using PhotographyBackupAssistant.Classes;
using PhotographyBackupAssistant.Models;
using PhotographyBackupAssistant.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Config config = new();

IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
	.ConfigureServices(services =>
	{
		services.AddHostedService(sp => new ConfigService(config));
		services.AddHostedService(sp => new ImportService(config));
		services.AddHostedService(sp => new DemuxService(config));
		services.AddHostedService(sp => new TransferExternalHDService(config));
		services.AddHostedService(sp => new TransferRemoteService(config));
	})
	.Build();
Logger.Log("Main", "Starting");
await host.RunAsync();
