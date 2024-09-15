using imageBackup.Classes;
using imageBackup.Modules;
f
Operations operations = new();
Logger.Log("Main", "Startup");

while (true)
{
	try
	{
		Thread.Sleep(1000);
		operations.ReadConfig();
		FileOperations.EnsureDirectoryStructure(operations.config);
		operations.Import();
		operations.Compress();
		operations.Demux();
		operations.TransferLocal();
		operations.TransferRemote();
	}
	catch (Exception e)
	{
		Logger.Log("Main", $"Unhandled exception: {e.Message}: {e.StackTrace}");
	}
}