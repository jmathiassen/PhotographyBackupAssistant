using Renci.SshNet;

namespace imageBackup.Classes;

public class ScpConnection
{
	public ScpClient? client { get; set; }
    public DateTime connectionRecheckTime { get; set; } = DateTime.MinValue;
	public ScpConnection()
	{
	}
	public ScpConnection(ScpClient client)
	{
		this.client = client;
	}
}