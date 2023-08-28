using System.Text.Json.Serialization;
using System.Text.Json;
using PhotographyBackupAssistant.Classes;
using Renci.SshNet;

namespace PhotographyBackupAssistant.Models;

public class Config
{
	[JsonIgnore] public string configFilePath { get; set; } = "config.json";

	public ImportModule import { get; set; } = new();
	public List<ExternalHD> external { get; set; } = [new ExternalHD()];
	public List<RemoteHost> remote { get; set; } = [new RemoteHost()];

	public void ReadConfig()
	{
		if (!File.Exists(configFilePath))
		{
			Logger.Log(GetType().Name, $"Config file {configFilePath} not found, creating with defaults");
			File.WriteAllText(configFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
			return;
		}

		try
		{
			Config? tmpConfig = JsonSerializer.Deserialize<Config>(File.ReadAllText(configFilePath));
			if (tmpConfig != null)
			{
				import = tmpConfig.import;
				external = tmpConfig.external;
				remote = tmpConfig.remote;
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem reading config file {configFilePath}: {ex.Message}");
		}
	}
}

public class ImportModule
{
	public List<string> importDirectories { get; set; } = ["import"];
	public string incoming { get; set; } = "spool/import/incoming";
	public string imported { get; set; } = "spool/import/imported";
	public List<FileTypeHandling> fileTypes { get; set; } = new()
	{
		new FileTypeHandling(".jpg"),
		new FileTypeHandling(".jpeg"),
		new FileTypeHandling(".nef"),
		new FileTypeHandling(".nrw"),
		new FileTypeHandling(".mp4", keepOriginalFilename:false),
		new FileTypeHandling(".mov", keepOriginalFilename:false)
	};
}
public class FileTypeHandling(string extension, bool dateRename = true, bool keepOriginalFilename = true)
{
	public string extension { get; set; } = extension;
	public bool dateRename { get; set; } = dateRename;
	public bool keepOriginalFilename { get; set; } = keepOriginalFilename;
}
public class ExternalHD
{
	public string incoming{ get; set; } = "spool/external/incoming";
	public string spool { get; set; } = "spool/external/drive1";
	public string storage { get; set; } = "storage/drive1";
	public bool active { get; set; } = false;
}

public class RemoteHost
{
	[JsonIgnore] public ScpClient? scpClient { get; set; }
	[JsonIgnore] public DateTime connectionCheckTime { get; set; }
	public string incoming { get; set; } = "spool/remote/incoming";
	public string spool { get; set; } = "spool/remote/host1";
	public string username { get; set; } = "username";
	public string pubKeyPath { get; set; } = "host1.pem";
	public string host { get; set; } = "127.0.0.1";
	public int port { get; set; } = 22;
	public string directory { get; set; } = "files";
	public bool active { get; set; } = false;
}
