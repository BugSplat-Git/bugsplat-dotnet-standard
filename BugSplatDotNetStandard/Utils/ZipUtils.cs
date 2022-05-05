using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace BugSplatDotNetStandard.Utils
{
    internal class InMemoryFile
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }
    }

    internal class ZipUtils
    {
        public static byte[] CreateInMemoryZipFile(IEnumerable<FileInfo> files)
        {
            var inMemoryFiles = new List<InMemoryFile>();
            foreach (var attachment in files)
            {
                inMemoryFiles.Add(new InMemoryFile() { FileName = attachment.Name, Content = File.ReadAllBytes(attachment.FullName) });
            }

            byte[] zipBytes;
            using (var archiveStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in inMemoryFiles)
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


    }
}