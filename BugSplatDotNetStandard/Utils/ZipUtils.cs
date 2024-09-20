using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace BugSplatDotNetStandard.Utils
{
    internal class InMemoryFile
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }

        public static InMemoryFile FromFileInfo(FileInfo fileInfo)
        {
            using (FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                fileStream.CopyTo(memoryStream);
                return new InMemoryFile()
                {
                    FileName = fileInfo.Name,
                    Content = memoryStream.ToArray()
                };
            }
        }
    }

    internal interface IZipUtils
    {
        byte[] CreateInMemoryZipFile(IEnumerable<InMemoryFile> files);
        FileInfo CreateZipFile(string zipFileFullName, IEnumerable<FileInfo> files);
        string CreateZipFileFullName(string inputFileName);
        Stream CreateZipFileStream(string zipFileFullName);
        
    }

    internal class ZipUtils : IZipUtils
    {
        public byte[] CreateInMemoryZipFile(IEnumerable<InMemoryFile> files)
        {
            byte[] zipBytes;
            using (var archiveStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in files)
                    {
                        var zipArchiveEntry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                        using (var zipStream = zipArchiveEntry.Open())
                        {
                            zipStream.Write(file.Content, 0, file.Content.Length);
                        }
                    }
                }

                zipBytes = archiveStream.ToArray();
            }

            return zipBytes;
        }

        public FileInfo CreateZipFile(string zipFileFullName, IEnumerable<FileInfo> files)
        {
            using (var zipFile = ZipFile.Open(zipFileFullName, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    zipFile.CreateEntryFromFile(file.FullName, file.Name);
                }
            }

            return new FileInfo(zipFileFullName);
        }

        public string CreateZipFileFullName(string inputFileName)
        {
            var random = Path.GetRandomFileName();
            var zipFileName = $"bugsplat-{inputFileName}-{random}.zip";
            return Path.Combine(Path.GetTempPath(), zipFileName);
        }

        public Stream CreateZipFileStream(string zipFileFullName)
        {
            return new FileStream(zipFileFullName, FileMode.Open);
        }
    }
}