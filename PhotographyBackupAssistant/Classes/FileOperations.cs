using MetadataExtractor.Formats.Exif;
using MetadataExtractor;

namespace PhotographyBackupAssistant.Classes;

public class FileOperations
{
	public static string? FindFile(string currentDirectoryPath, string[]? filter = null)
	{
		DirectoryInfo currentDirectory = new(currentDirectoryPath);
		foreach (DirectoryInfo directory in currentDirectory.GetDirectories())
		{
			string? file = FindFile($"{currentDirectoryPath}/{directory.Name}", filter);
			if (file != null)
				return file;
		}

		foreach (FileInfo file in currentDirectory.GetFiles())
		{
			if (filter != null && !filter.Contains(file.Extension.ToLower()))
				continue;

			return $"{currentDirectoryPath}/{file.Name}";
		}

		return null;
	}
	public static string EnsureUniqueFilename(string context, FileInfo sourceFile, string destinationPath, bool dateRename = false, bool keepOriginalFilename = true)
	{
		int retries = 0;
		string postfix = "";

		string filePath = "";
		do
		{
			string filenameTo = sourceFile.Name;
			if (dateRename)
			{
				string prefix = "";
				DateTime fileDate;
				try
				{
					fileDate = ImageMetadataReader.ReadMetadata(sourceFile.FullName).OfType<ExifSubIfdDirectory>().Last().GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
				}
				catch
				{
					Logger.Log(context, $"{sourceFile.FullName} - exif read failed, fallback to last write time");
					fileDate = sourceFile.LastWriteTime;
				}
				prefix = $"{fileDate.Year}-{fileDate.Month.ToString().PadLeft(2, '0')}-{fileDate.Day.ToString().PadLeft(2, '0')} {fileDate.Hour.ToString().PadLeft(2, '0')}-{fileDate.Minute.ToString().PadLeft(2, '0')}-{fileDate.Second.ToString().PadLeft(2, '0')}";
				if (keepOriginalFilename)
					filenameTo = $"{prefix}+{filenameTo}";
				else
					filenameTo = prefix;
			}
			filePath = $"{destinationPath}/{filenameTo.Replace(sourceFile.Extension, "")}{postfix}{sourceFile.Extension}";
			if (!File.Exists(filePath))
			{
				Logger.Log(context, $"{sourceFile} -> {filePath} File is unique");
				break;
			}

			if (CompareFile(sourceFile.FullName, filePath))
			{
				Logger.Log(context, $"{sourceFile} -> {filePath} File exist with same content");
				break;
			}

			Logger.Log(context, $"{sourceFile} -> {filePath} File exist with different content, continue looking");
			postfix = $" ({++retries})";
		}
		while (File.Exists(filePath));
		return filePath;
	}
	public static bool CompareFile(string from, string to, int bufferSize = sizeof(Int64) * 1024)
	{
		byte[] buffer1 = new byte[bufferSize];
		byte[] buffer2 = new byte[bufferSize];

		using (var streamFrom = File.OpenRead(from))
		using (var streamTo = File.OpenRead(to))
		{
			int count1 = streamFrom.Read(buffer1, 0, bufferSize);
			int count2 = streamTo.Read(buffer2, 0, bufferSize);

			if (count1 != count2)
				return false;

			if (count1 > 0)
			{
				int iterations = (int)Math.Ceiling((double)count1 / sizeof(Int64));
				for (int i = 0; i < iterations; i++)
				{
					if (BitConverter.ToInt64(buffer1, i * sizeof(Int64)) != BitConverter.ToInt64(buffer2, i * sizeof(Int64)))
						return false;
				}
			}
		}

		return true;
	}
	public static string? FileCopy(string context, string fileFrom, string directoryTo, bool dateRename = false, bool deleteAfterCopy = true)
	{
		string fileTo = EnsureUniqueFilename(context, new FileInfo(fileFrom), directoryTo, dateRename: dateRename);

		if (!File.Exists(fileTo))
		{
			Logger.Log(context, $"{fileFrom} -> {fileTo} - Copy");
			try
			{
				File.Copy(fileFrom, fileTo);
			}
			catch(Exception e)
			{
				Logger.Log(context, $"{fileFrom} -> {fileTo} - Copy failed, retrying: {e.Message}");
				return null;
			}

			if (!CompareFile(fileFrom, fileTo))
			{
				Logger.Log(context, $"{fileFrom} -> {fileTo} - Copy failed, retrying");
				return null;
			}
		}

		Logger.Log(context, $"{fileFrom} -> {fileTo} - Copy succeeded");
		if (deleteAfterCopy)
		{
			Logger.Log(context, $"{fileFrom} -> {fileTo} - Removing source");
			File.Delete(fileFrom);
		}

		return new FileInfo(fileTo).Name;
	}
	public static string? FileMove(string context, string fileFrom, string directoryTo, bool dateRename = false)
	{
		FileInfo file = new FileInfo(fileFrom);
		string fileTo = EnsureUniqueFilename(context, file, directoryTo, dateRename: dateRename);

		if (File.Exists(fileTo))
		{
			if (!CompareFile(fileFrom, fileTo))
			{
				Logger.Log(context, $"{fileFrom} -> {fileTo} - File already exists with different content, retry");
				return null;
			}
			else
			{
				Logger.Log(context, $"{fileFrom} -> {fileTo} - File already exists with different content, delete source");
				File.Delete(fileFrom);
				return new FileInfo(fileTo).Name;
			}
		}
		else
		{
			Logger.Log(context, $"{fileFrom} -> {fileTo} - Move");
			File.Move(fileFrom, fileTo);

			Logger.Log(context, $"{fileFrom} -> {fileTo} - Move succeeded");
			return new FileInfo(fileTo).Name;
		}
	}
}
