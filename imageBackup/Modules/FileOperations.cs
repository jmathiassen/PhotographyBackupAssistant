using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using imageBackup.Classes;

namespace imageBackup.Modules;

public class FileOperations
{
	public static void EnsureDirectoryStructure(Config config)
	{
		// Import
		EnsureDirectoryExists(config.import.spool);
		EnsureDirectoryExists(config.import.storage);

		// compress
		EnsureDirectoryExists(config.compress.spool);
		EnsureDirectoryExists(config.compress.storage);

		// Local spool
		foreach (LocalDirectory directory in config.local.Where(x => x.active))
			EnsureDirectoryExists(directory.spool);

		// Remote spool
		foreach (RemoteHost host in config.remote.Where(x => x.active))
			EnsureDirectoryExists(host.spool);
	}

	private static void EnsureDirectoryExists(string directoryPath)
	{
		if (!Directory.Exists(directoryPath))
		{
			Logger.Log("EnsureDirectoryStructure", $"Creating missing directory {directoryPath}");
			Directory.CreateDirectory(directoryPath);
		}
	}
	public static void MoveFile(string moveType, string directoryFrom, string filenameFrom, string[] directoriesTo, string filenameTo, bool compress = false)
	{
		FileInfo fileFrom = new FileInfo($"{directoryFrom}/{filenameFrom}");
		if (!fileFrom.Exists)
		{
			Logger.Log(moveType, $"{fileFrom.FullName} - file not found, skipping");
			return;
		}
		List<string> directoriesLeft = new(directoriesTo);
		foreach (string directoryTo in directoriesTo)
		{
			if (!Directory.Exists(directoryTo))
			{
				Logger.Log(moveType, $"{directoryTo} - directory not found, skipping");
				continue;
			}
			int iteration = 0;
			bool processed = false;
			while (!processed)
			{
				string pad = iteration > 0 ? $"_{iteration}" : "";
				FileInfo fileTo = new FileInfo($"{directoryTo}/{filenameTo.Replace(fileFrom.Extension, "")}{pad}{fileFrom.Extension}");
				if (!fileTo.Exists)
				{
					Logger.Log(moveType, $"{fileFrom.Name} -> {directoryTo}/{fileTo.Name} - copy");
					fileFrom.CopyTo(fileTo.FullName);
					if (CompareFile(fileFrom.FullName, fileTo.FullName))
						processed = true;
				}
				else
				{
					if (CompareFile(fileFrom.FullName, fileTo.FullName))
					{
						Logger.Log(moveType, $"{fileFrom.Name} -> {directoryTo}/{fileTo.Name} - same name, same content, no action");
						processed = true;
					}
					else
					{
						Logger.Log(moveType, $"{fileFrom.Name} -> {directoryTo}/{fileTo.Name} - same name, different content, try new name");
						iteration++;
					}
				}
			}
			directoriesLeft.Remove(directoryTo);
		}
		if (directoriesLeft.Count == 0)
		{
			Logger.Log(moveType, $"{directoryFrom}/{fileFrom.Name} - removed");
			fileFrom.Delete();
		}
	}
	public static void CompressFile(string directoryFrom, string filenameFrom, string directoryTo)
	{
		if (!Directory.Exists(directoryTo))
		{
			Logger.Log("Compress", $"{directoryTo} - directory not found, skipping");
			return;
		}
		string fileFrom = $"{directoryFrom}/{filenameFrom}";
		string fileTo = $"{directoryTo}/{filenameFrom}";
		if (!File.Exists(fileFrom))
		{
			Logger.Log("Compress", $"{fileFrom} - file from not found");
			return;
		}
		Logger.Log("Compress", $"{directoryFrom}/{filenameFrom} -> {directoryFrom}/{filenameFrom}.gz - compress");

		using FileStream originalFileStream = File.Open(fileFrom, FileMode.Open);
		using FileStream compressedFileStream = File.Create($"{fileTo}.gz");
		using var compressor = new GZipStream(compressedFileStream, CompressionMode.Compress);
		originalFileStream.CopyTo(compressor);
		compressor.Close();
		originalFileStream.Close();
		compressedFileStream.Close();

		using FileStream compressedFileStreamCompare = File.Open($"{fileTo}.gz", FileMode.Open);
		using FileStream outputFileStreamCompare = File.Create($"{fileTo}.compare");
		using var decompressor = new GZipStream(compressedFileStreamCompare, CompressionMode.Decompress);
		decompressor.CopyTo(outputFileStreamCompare);
		decompressor.Close();
		compressedFileStreamCompare.Close();
		outputFileStreamCompare.Close();

		if (!CompareFile(fileFrom, $"{fileTo}.compare"))
		{
			Logger.Log("Compress", $"{fileFrom} -> {fileTo} - different content after copy/compress, retry");
			File.Delete($"{fileTo}.compare");
			File.Delete($"{fileTo}.gz");
		}
		else
		{
			Logger.Log("Compress", $"{fileFrom} -> {fileTo}.compare - same content after copy/compress");
			File.Delete($"{fileTo}.compare");
			File.Delete(fileFrom);
		}
	}
	public static bool CompareFile(string from, string to)
	{
		FileInfo fromfile = new FileInfo(from);
		FileInfo tofile = new FileInfo(to);

		if (!tofile.Exists)
			return false;

		if (fromfile.Length != tofile.Length)
			return false;

		return Compare(File.ReadAllBytes(from), File.ReadAllBytes(to));
	}
	public static bool Compare(byte[] fileA, byte[] fileB)
	{
		byte[] firstHash = MD5.Create().ComputeHash(fileA);
		byte[] secondHash = MD5.Create().ComputeHash(fileB);

		for (int i = 0; i < firstHash.Length; i++)
		{
			if (firstHash[i] != secondHash[i])
				return false;
		}

		return true;
	}
}
