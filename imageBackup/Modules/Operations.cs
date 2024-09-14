using imageBackup.Classes;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor;
using Renci.SshNet;
using System.Text.Json;

namespace imageBackup.Modules;

public class Operations
{
	public Config config { get; set; } = new();
	private Dictionary<string, ScpConnection> scpConnection = new();

	public void ReadConfig()
	{
		if (!File.Exists(config.configFilePath))
		{
			Console.WriteLine($"Config file {config.configFilePath} not found, creating with defaults");
			File.WriteAllText(config.configFilePath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
			return;
		}

		if (DateTime.Now < config.nextCheck)
			return;

		if (File.Exists(config.configFilePath))
		{
			try
			{
				Config? tmpConfig = JsonSerializer.Deserialize<Config>(File.ReadAllText(config.configFilePath));
				if (tmpConfig != null)
					config = tmpConfig;
			}
			catch (Exception ex)
			{
				Logger.Log("Config", $"Problem reading config file {config.configFilePath}: {ex.Message}");
			}
		}
		config.nextCheck = DateTime.Now.AddSeconds(10);
	}

	public void Import()
	{
		DirectoryInfo importFrom = new DirectoryInfo(config.import.spool);
		foreach (DirectoryInfo directory in importFrom.GetDirectories())
			ImportFiles(directory);

		ImportFiles(importFrom);
	}
	private void ImportFiles(DirectoryInfo importFrom)
	{
		foreach (FileInfo fileFrom in importFrom.EnumerateFiles())
		{
			string filenameTo = fileFrom.Name.Replace(fileFrom.Extension, "");

			FileTypeHandling? fileTypeHandling = config.import.fileTypes.FirstOrDefault(x => x.extension == fileFrom.Extension.ToLower());
			if (fileTypeHandling != null)
			{
				if (fileTypeHandling.dateRename)
				{
					string prefix = "";
					try
					{
						prefix = dateFormat(ImageMetadataReader.ReadMetadata(fileFrom.FullName).OfType<ExifSubIfdDirectory>().Last().GetDateTime(ExifDirectoryBase.TagDateTimeOriginal));
					}
					catch
					{
						Logger.Log("Import", $"{config.import.storage} - exif read failed, fallback to creationtime");
						prefix = dateFormat(fileFrom.CreationTime);
					}

					if (fileTypeHandling.keepOriginalFilename)
						filenameTo = $"{prefix}+{filenameTo}";
					else
						filenameTo = prefix;
				}
				if (fileTypeHandling.compress)
					FileOperations.MoveFile("Import", importFrom.FullName, fileFrom.Name, [config.compress.spool], filenameTo, fileTypeHandling.compress);
				else
					FileOperations.MoveFile("Import", importFrom.FullName, fileFrom.Name, [config.import.storage], filenameTo);
			}

		}
	}

	private string dateFormat(DateTime date)
	{
		return $"{date.Year}-{date.Month.ToString().PadLeft(2, '0')}-{date.Day.ToString().PadLeft(2, '0')} {date.Hour.ToString().PadLeft(2, '0')}-{date.Minute.ToString().PadLeft(2, '0')}-{date.Second.ToString().PadLeft(2, '0')}";
	}

	public void Compress()
	{
		DirectoryInfo compressFrom = new DirectoryInfo(config.compress.spool);
		foreach (FileInfo fileFrom in compressFrom.GetFiles())
			FileOperations.CompressFile(config.compress.spool, fileFrom.Name, config.compress.storage);
	}

	public void Demux()
	{
		DirectoryInfo directoryFrom = new DirectoryInfo(config.import.storage);
		List<string> directoriesTo = new List<string>();
		foreach (var directory in config.local.Where(x => x.active))
			directoriesTo.Add(directory.spool);
		foreach (var directory in config.remote.Where(x => x.active))
			directoriesTo.Add(directory.spool);
		foreach (FileInfo fileFrom in directoryFrom.GetFiles())
			FileOperations.MoveFile("Demux", config.import.storage, fileFrom.Name, directoriesTo.ToArray(), fileFrom.Name);

	}
	public void TransferLocal()
	{
		foreach (var directory in config.local.Where(x => x.active))
		{
			DirectoryInfo dirFrom = new(directory.spool);
			DirectoryInfo dirTo = new(directory.storage);

			if (!dirTo.Exists)
				continue;

			foreach (FileInfo fileFrom in dirFrom.GetFiles())
				FileOperations.MoveFile("TransferLocal", directory.spool, fileFrom.Name, [directory.storage], fileFrom.Name);
		}
	}
	public void TransferRemote()
	{
		foreach (var remoteHost in config.remote.Where(x => x.active))
		{
			string connectionString = $"{remoteHost.host}:{remoteHost.port}";
			DirectoryInfo dirfrom = new(remoteHost.spool);
			if (dirfrom.GetFiles().Length > 0)
			{
				if (!scpConnection.ContainsKey(connectionString) || (scpConnection[connectionString].client == null && DateTime.Now >= scpConnection[connectionString].connectionRecheckTime))
				{
					try
					{
						Logger.Log("TransferRemote", $"{remoteHost.host} - initialize connection configuration");
						scpConnection[connectionString] = new(new ScpClient(remoteHost.host, remoteHost.port, remoteHost.username, [new PrivateKeyFile(remoteHost.pubKeyPath)]));
						Logger.Log("TransferRemote", $"{remoteHost.host} - connection configuration setup");
					}
					catch (Exception e)
					{
						Logger.Log("TransferRemote", $"{remoteHost.host} - initialize connection configuration failed: {e.Message}");
						scpConnection[connectionString] = new();
						scpConnection[connectionString].connectionRecheckTime = DateTime.Now.AddSeconds(30);
						continue;
					}
				}
				//Console.WriteLine(scpConnection[connectionString].connectionRecheckTime.ToString());
				if (scpConnection[connectionString].client != null)
				{
					if (!scpConnection[connectionString].client!.IsConnected && DateTime.Now >= scpConnection[connectionString].connectionRecheckTime)
					{
						try
						{
							Logger.Log("TransferRemote", $"{remoteHost.host} - establish connection");
							scpConnection[connectionString].client!.Connect();
							Logger.Log("TransferRemote", $"{remoteHost.host} - connection established");
						}
						catch (Exception ex)
						{
							Logger.Log("TransferRemote", $"{remoteHost.host} - connect failed: {ex.Message}");
							scpConnection[connectionString].client!.Disconnect();
							scpConnection[connectionString].connectionRecheckTime = DateTime.Now.AddSeconds(30);
							continue;
						}
					}

					if (scpConnection[connectionString].client!.IsConnected)
					{
						foreach (FileInfo fileFrom in dirfrom.GetFiles())
						{
							try
							{
								scpConnection[connectionString].client!.Upload(File.OpenRead(fileFrom.FullName), $"{remoteHost.directory}/{fileFrom.Name}");
								Logger.Log("TransferRemote", $"{fileFrom.Name} -> {remoteHost.host}:{remoteHost.directory}/{fileFrom.Name} - file uploaded");
								File.Delete(fileFrom.FullName);
								Logger.Log("TransferRemote", $"{remoteHost.directory}/{fileFrom.Name} - file removed");
							}
							catch (Exception ex)
							{
								Logger.Log("TransferRemote", $"{remoteHost.host} - transfer failed: {ex.Message}");
								scpConnection[connectionString].client!.Disconnect();
								scpConnection[connectionString].connectionRecheckTime = DateTime.Now.AddSeconds(3);
							}
						}
					}
				}
			}
		}
	}
}