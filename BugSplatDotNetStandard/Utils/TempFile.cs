using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard.Utils
{
    public interface ITempFileFactory
    {
        ITempFile TryCreateFromBytes(string fileName, byte[] bytes);
        ITempFile CreateFromBytes(string fileName, byte[] bytes);
        ITempFile CreateTempZip(IEnumerable<FileInfo> files);
    }

    public class TempFileFactory : ITempFileFactory
    {
        public ITempFile TryCreateFromBytes(string fileName, byte[] bytes)
        {
            try
            {
                return CreateFromBytes(fileName, bytes);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating temp file: {ex.Message}");
                return null;
            }
        }

        public ITempFile CreateFromBytes(string fileName, byte[] bytes)
        {
            ThrowIfArgumentIsNullOrEmpty(fileName, "fileName");
            ThrowIfArgumentIsNullOrEmpty(bytes, "bytes");

            var tempFolder = CreateTempFolder();
            var tempFileName = Path.Combine(tempFolder.FullName, fileName);

            File.WriteAllBytes(tempFileName, bytes);

            return new TempFile(new FileInfo(tempFileName));
        }

        public ITempFile CreateTempZip(IEnumerable<FileInfo> filesToZip)
        {
            ThrowIfArgumentIsNullOrEmpty(filesToZip, "filesToZip");

            var tempFolder = CreateTempFolder();
            var zipFileName = Path.GetRandomFileName().Replace(".", "_") + ".zip";
            var zipFileFullName = Path.Combine(tempFolder.FullName, zipFileName);

            using (var zipFile = ZipFile.Open(zipFileFullName, ZipArchiveMode.Create))
            {
                foreach (var file in filesToZip)
                {
                    SafeCreateZipEntry(zipFile, file);
                }
            }

            return new TempFile(new FileInfo(zipFileFullName));
        }

        private static DirectoryInfo CreateTempFolder()
        {
            var tempFileFolder = Path.Combine(Path.GetTempPath(), "bugsplat-unity", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFileFolder);
            return new DirectoryInfo(tempFileFolder);
        }

        private static void WriteChunkedBytesToFile()
        {
            
        }

        private static void SafeCreateZipEntry(ZipArchive zipArchive, FileInfo file)
        {
            try
            {
                zipArchive.CreateEntryFromFile(file.FullName, file.Name);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating zip entry for {file.Name}: {ex.Message}, skipping...");
            }
        }
    }

    public interface ITempFile : IDisposable
    {
        FileInfo File { get; }
        Stream CreateFileStream(FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read);
    }

    public class TempFile : ITempFile
    {
        public FileInfo File { get; private set; }

        internal TempFile(FileInfo file)
        {
            File = file;
        }

        public Stream CreateFileStream(FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read)
        {
            return new FileStream(File.FullName, mode, access);
        }

        public void Dispose()
        {
            try
            {
                var tempFolder = File.Directory;

                File.Delete();

                if (tempFolder != null && tempFolder.Exists)
                {
                    tempFolder.Delete(true);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error disposing temp file {File.FullName}: {ex.Message}");
            }
        }
    }
}

