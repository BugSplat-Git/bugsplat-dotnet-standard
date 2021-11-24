using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Utils;

namespace BugSplatDotNetStandard
{
    /// <summary>
    /// A class for uploading Exceptions and minidump files to BugSplat
    /// </summary>
    public class BugSplat
    {
        /// <summary>
        /// A list of files to be added to the upload at post time
        /// </summary>
        public List<FileInfo> Attachments { get; } = new List<FileInfo>();

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded exceptions
        /// </summary>
        public ExceptionTypeId ExceptionType { get; set; } = ExceptionTypeId.DotNetStandard;

        /// <summary>
        /// A default description added to the upload that can be overriden at post time
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A default email added to the upload that can be overriden at post time
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// A default key added to the upload that can be overriden at post time
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded minidumps
        /// </summary>
        public MinidumpTypeId MinidumpType { get; set; } = MinidumpTypeId.WindowsNative;

        /// <summary>
        /// A default user added to the upload that can be overriden at post time
        /// </summary>
        public string User { get; set; } = string.Empty;

        public enum ExceptionTypeId
        {
            Unknown = 0,
            UnityLegacy = 12,
            DotNetStandard = 18,
            Unity = 24
        }

        public enum MinidumpTypeId
        {
            Unknown = 0,
            WindowsNative = 1,
            DotNet = 8,
            UnityNativeWindows = 15
        }

        private readonly string database;
        private readonly string application;
        private readonly string version;

        private string baseUrl => $"https://{database}.bugsplat.com";

        /// <summary>
        /// Post Exceptions and minidump files to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        public BugSplat(string database, string application, string version)
        {
            ThrowIfArgumentIsNullOrEmpty(database, "database");
            ThrowIfArgumentIsNullOrEmpty(application, "application");
            ThrowIfArgumentIsNullOrEmpty(version, "version");

            this.database = database;
            this.application = application;
            this.version = version;
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="stackTrace">A string representation of an Exception's stack trace</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options = null)
        {
            ThrowIfArgumentIsNull(stackTrace, "stackTrace");

            using (var httpClient = new HttpClient())
            {
                options = options ?? new ExceptionPostOptions();

                var uri = new Uri($"{baseUrl}/post/dotnetstandard/");
                var body = CreateMultiPartFormDataContent(options);
                var crashTypeId = options?.ExceptionType != ExceptionTypeId.Unknown ? options.ExceptionType : ExceptionType;
                body.Add(new StringContent(stackTrace), "callstack");
                body.Add(new StringContent($"{(int)crashTypeId}"), "crashTypeId");

                return await httpClient.PostAsync(uri, body);
            }
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="ex">The Exception that will be serialized and posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(Exception ex, ExceptionPostOptions options = null)
        {
            ThrowIfArgumentIsNull(ex, "ex");

            return await Post(ex.ToString(), options);
        }

        /// <summary>
        /// Post a minidump file to BugSplat
        /// </summary>
        /// <param name="ex">The minidump file that will be posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options = null)
        {
            ThrowIfArgumentIsNull(minidumpFileInfo, "minidumpFileInfo");

            options = options ?? new MinidumpPostOptions();

            var zipBytes = CreateInMemoryCrashZipFile(minidumpFileInfo, options);
            var crashUploadResponse = await GetCrashUploadUrl(zipBytes.Length);

            ThrowIfHttpRequestFailed(crashUploadResponse);

            var presignedUrl = await ParseCrashUploadUrl(crashUploadResponse);
            var uploadFileResponse = await UploadFileToPresignedURL(presignedUrl, zipBytes);

            ThrowIfHttpRequestFailed(uploadFileResponse);

            var md5 = GetETagFromResponseHeaders(uploadFileResponse.Headers);
            var commitS3CrashResponse = await CommitS3CrashUpload(options, presignedUrl.ToString(), md5);

            ThrowIfHttpRequestFailed(commitS3CrashResponse);

            return commitS3CrashResponse;
        }

        private byte[] CreateInMemoryCrashZipFile(FileInfo minidumpFileInfo, MinidumpPostOptions options)
        {
            var files = new List<InMemoryFile>();
            files.Add(new InMemoryFile() { FileName = minidumpFileInfo.Name, Content = File.ReadAllBytes(minidumpFileInfo.FullName) });

            foreach (var attachment in options.AdditionalAttachments)
            {
                files.Add(new InMemoryFile() { FileName = attachment.Name, Content = File.ReadAllBytes(attachment.FullName) });
            }

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

        private async Task<HttpResponseMessage> GetCrashUploadUrl(int crashPostSize)
        {
            var path = $"{baseUrl}/api/getCrashUploadUrl";
            var route = $"{path}?database={database}&appName={application}&appVersion={version}&crashPostSize={crashPostSize}";

            using (var httpClient = new HttpClient())
            {
                return await httpClient.GetAsync(route);
            }
        }

        private string GetETagFromResponseHeaders(HttpHeaders headers)
        {
            var etagQuoted = headers.GetValues("ETag").FirstOrDefault();
            var etag = etagQuoted.Replace("\"", "");
            return etag;
        }

        private async Task<Uri> ParseCrashUploadUrl(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadAsStringAsync();

                // We have opted to not introduce a 3rd-party dependency to better support Unity.
                // When Unity moves to .NET 6 we can replace this with System.Text.Json.
                // More information about Unity's plans to update to .NET 6 can be found here:
                // https://forum.unity.com/threads/unity-future-net-development-status.1092205/
                var jsonObj = new JsonObject(json);
                var url = jsonObj.GetValue("url");

                return new Uri(url);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse crash upload url", ex);
            }
        }

        private async Task<HttpResponseMessage> UploadFileToPresignedURL(Uri uri, byte[] bytes)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                return await httpClient.PutAsync(uri, new ByteArrayContent(bytes));
            }
        }

        private async Task<HttpResponseMessage> CommitS3CrashUpload(MinidumpPostOptions options, string s3Key, string md5)
        {
            using (var httpClient = new HttpClient())
            {
                var route = $"{baseUrl}/api/commitS3CrashUpload";

                var crashTypeId = options?.MinidumpType != MinidumpTypeId.Unknown ? options.MinidumpType : MinidumpType;
                var body = CreateMultiPartFormDataContent(options);
                body.Add(new StringContent($"{(int)crashTypeId}"), "crashTypeId");
                body.Add(new StringContent(s3Key), "s3Key");
                body.Add(new StringContent(md5), "md5");

                return await httpClient.PostAsync(route, body);
            }
        }

        private void ThrowIfArgumentIsNull(object argument, string name)
        {
            if (argument == null)
            {
                throw new ArgumentNullException($"{name} cannot be null!");
            }
        }

        private void ThrowIfArgumentIsNullOrEmpty(string argument, string name)
        {
            if (string.IsNullOrEmpty(argument))
            {
                throw new ArgumentException($"{name} cannot be null or empty!");
            }
        }

        private void ThrowIfHttpRequestFailed(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.Content.ReadAsStringAsync().Result);
            }
        }

        private MultipartFormDataContent CreateMultiPartFormDataContent(BugSplatPostOptions options = null)
        {
            var additionalFormDataParams = options?.AdditionalFormDataParams ?? new List<IFormDataParam>();
            var description = BugSplatUtils.GetStringValueOrDefault(options?.Description, Description);
            var email = BugSplatUtils.GetStringValueOrDefault(options?.Email, Email);
            var key = BugSplatUtils.GetStringValueOrDefault(options?.Key, Key);
            var user = BugSplatUtils.GetStringValueOrDefault(options?.User, User);

            var body = new MultipartFormDataContent
            {
                { new StringContent(database), "database" },
                { new StringContent(application), "appName" },
                { new StringContent(version), "appVersion" },
                { new StringContent(description), "description" },
                { new StringContent(email), "email" },
                { new StringContent(key), "appKey" },
                { new StringContent(user), "user" }
            };

            foreach (var param in additionalFormDataParams)
            {
                if (!string.IsNullOrEmpty(param.FileName))
                {
                    body.Add(param.Content, param.Name, param.FileName);
                    continue;
                }

                body.Add(param.Content, param.Name);
            }

            if (options != null)
            {
                Attachments.AddRange(options.AdditionalAttachments);
            }

            for (var i = 0; i < Attachments.Count; i++)
            {
                byte[] bytes = null;
                using (var fileStream = File.Open(Attachments[i].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }
                }

                if (bytes != null)
                {
                    var name = Attachments[i].Name;
                    body.Add(new ByteArrayContent(bytes), name, name);
                }
            }

            return body;
        }
    }

    internal class InMemoryFile
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }
    }
}
