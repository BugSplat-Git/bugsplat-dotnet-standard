using System;
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
            const int bufferSize = 81920; // 80 KB buffer
            using (var fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize))
            {
                var bytes = new byte[fileInfo.Length];
                int bytesRead, totalBytesRead = 0;

                while ((bytesRead = fileStream.Read(bytes, totalBytesRead, Math.Min(bufferSize, bytes.Length - totalBytesRead))) > 0)
                {
                    totalBytesRead += bytesRead;
                }

                return new InMemoryFile
                {
                    FileName = fileInfo.Name,
                    Content = bytes
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