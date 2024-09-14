using System.Text.Json.Serialization;

namespace imageBackup.Classes;

public class Config
{
	[JsonIgnore] public string configFilePath { get; set; } = "config.json";
	[JsonIgnore] public DateTime nextCheck { get; set; } = DateTime.MinValue;

	public ImportDirectory import { get; set; } = new();
	public CompressDirectory compress { get; set; } = new();
	public List<LocalDirectory> local { get; set; } = [ new LocalDirectory() ];
	public List<RemoteHost> remote { get; set; } = [ new RemoteHost() ];
}

public class ImportDirectory
{
	public string spool { get; set; } = "import";
	public string storage { get; set; } = "spool/incoming";
	public List<FileTypeHandling> fileTypes { get; set; } = new()
	{
		new FileTypeHandling(".jpg"),
		new FileTypeHandling(".jepg"),
		new FileTypeHandling(".nef"),
		new FileTypeHandling(".nrw"),
		new FileTypeHandling(".mp4", keepOriginalFilename:false),
		new FileTypeHandling(".mov", keepOriginalFilename:false)
	};
}
public class CompressDirectory
{
	public string spool { get; set; } = "spool/compress";
	public string storage { get; set; } = "spool/incoming";
}

public class FileTypeHandling(string extension, bool dateRename = true, bool compress = true, bool keepOriginalFilename = true)
{
	public string extension { get; set; } = extension;
	public bool dateRename { get; set; } = dateRename;
	public bool compress { get; set; } = compress;
	public bool keepOriginalFilename { get; set; } = keepOriginalFilename;
}
public class LocalDirectory
{
	public string spool { get; set; } = "spool/local/drive1";
	public string storage { get; set; } = "storage/drive1";
	public bool active { get; set; } = false;
}

public class RemoteHost
{
	public string spool { get; set; } = "spool/remote/host1";
	public string username { get; set; } = "username";
	public string pubKeyPath { get; set; } = "host1.pem";
	public string host { get; set; } = "127.0.0.1";
	public int port { get; set; } = 22;
	public string directory { get; set; } = "files";
	public bool active { get; set; } = false;
}