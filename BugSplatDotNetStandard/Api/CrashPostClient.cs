using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Http;
using BugSplatDotNetStandard.Utils;
using static BugSplatDotNetStandard.BugSplat;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;
using static BugSplatDotNetStandard.Utils.StringUtils;

namespace BugSplatDotNetStandard.Api
{
    internal class CrashPostClient : IDisposable
    {
        internal IZipUtils ZipUtils { get; set; } = new ZipUtils();
        private HttpClient httpClient;
        private IS3Client s3Client;

        public CrashPostClient(
            IHttpClientFactory httpClientFactory,
            IS3ClientFactory s3ClientFactory
        )
        {
            ThrowIfArgumentIsNull(httpClientFactory, "httpClientFactory");
            ThrowIfArgumentIsNull(s3ClientFactory, "s3ClientFactory");
            
            this.httpClient = httpClientFactory.CreateClient();
            this.s3Client = s3ClientFactory.CreateClient();
        }

        public async Task<HttpResponseMessage> PostException(
            string database,
            string application,
            string version,
            string stackTrace,
            ExceptionPostOptions defaultPostOptions,
            ExceptionPostOptions overridePostOptions = null
        )
        {
            overridePostOptions = overridePostOptions ?? new ExceptionPostOptions();

            var files = CombineListsWithDuplicatesRemoved(defaultPostOptions.Attachments, overridePostOptions.Attachments)
                .Select(attachment => TryCreateInMemoryFileFromFileInfo(attachment))
                .Where(file => file != null)
                .ToList();

            var additionalFormDataFiles = overridePostOptions.FormDataParams
                .Where(file => !string.IsNullOrEmpty(file.FileName) && file.Content != null)
                .Select(file => new InMemoryFile() { FileName = file.FileName, Content = file.Content.ReadAsByteArrayAsync().Result })
                .ToList();

            files.Add(new InMemoryFile() { FileName = "Callstack.txt", Content = Encoding.UTF8.GetBytes(stackTrace) });
            files.AddRange(additionalFormDataFiles);

            var zipBytes = ZipUtils.CreateInMemoryZipFile(files);
            using (
                var crashUploadResponse = await GetCrashUploadUrl(
                    database,
                    application,
                    version,
                    zipBytes.Length
                )
            )
            {
                ThrowIfHttpRequestFailed(crashUploadResponse);

                var presignedUrl = await GetPresignedUrlFromResponse(crashUploadResponse);

                using (var uploadFileResponse = await this.s3Client.UploadFileBytesToPresignedURL(presignedUrl, zipBytes))
                {
                    ThrowIfHttpRequestFailed(uploadFileResponse);

                    var s3Key = presignedUrl.ToString();
                    var md5 = GetETagFromResponseHeaders(uploadFileResponse.Headers);
                    var crashTypeId = overridePostOptions?.CrashTypeId != (int)ExceptionTypeId.Unknown ? overridePostOptions.CrashTypeId : defaultPostOptions.CrashTypeId;
                    var commitS3CrashResponse = await CommitS3CrashUpload(
                        database,
                        application,
                        version,
                        md5,
                        s3Key,
                        crashTypeId
                    );

                    ThrowIfHttpRequestFailed(commitS3CrashResponse);

                    return commitS3CrashResponse;
                }
            }
        }

        public async Task<HttpResponseMessage> PostMinidump(
            string database,
            string application,
            string version,
            FileInfo minidumpFileInfo,
            MinidumpPostOptions defaultPostOptions,
            MinidumpPostOptions overridePostOptions = null
        )
        {
            return await PostCrashFile(
                database,
                application,
                version,
                minidumpFileInfo,
                defaultPostOptions,
                overridePostOptions
            );
        }

        public async Task<HttpResponseMessage> PostXmlReport(
            string database,
            string application,
            string version,
            FileInfo xmlFileInfo,
            XmlPostOptions defaultPostOptions,
            XmlPostOptions overridePostOptions = null
        )
        {
            return await PostCrashFile(
                database,
                application,
                version,
                xmlFileInfo,
                defaultPostOptions,
                overridePostOptions
            );
        }

        public async Task<HttpResponseMessage> PostCrashFile(
            string database,
            string application,
            string version,
            FileInfo crashFileInfo,
            BugSplatPostOptions defaultPostOptions,
            BugSplatPostOptions overridePostOptions = null
        )
        {
            overridePostOptions = overridePostOptions ?? new MinidumpPostOptions();

            var files = CombineListsWithDuplicatesRemoved(defaultPostOptions.Attachments, overridePostOptions.Attachments)
                .Select(attachment => TryCreateInMemoryFileFromFileInfo(attachment))
                .Where(file => file != null)
                .ToList();

            files.Add(new InMemoryFile() { FileName = crashFileInfo.Name, Content = File.ReadAllBytes(crashFileInfo.FullName) });

            var zipBytes = ZipUtils.CreateInMemoryZipFile(files);
            using (
                var crashUploadResponse = await GetCrashUploadUrl(
                    database,
                    application,
                    version,
                    zipBytes.Length
                )
            )
            {
                ThrowIfHttpRequestFailed(crashUploadResponse);

                var presignedUrl = await GetPresignedUrlFromResponse(crashUploadResponse);

                using (var uploadFileResponse = await this.s3Client.UploadFileBytesToPresignedURL(presignedUrl, zipBytes))
                {
                    ThrowIfHttpRequestFailed(uploadFileResponse);

                    var s3Key = presignedUrl.ToString();
                    var md5 = GetETagFromResponseHeaders(uploadFileResponse.Headers);
                    var crashTypeId = overridePostOptions?.CrashTypeId != (int)MinidumpTypeId.Unknown ? overridePostOptions.CrashTypeId : defaultPostOptions.CrashTypeId;
                    var commitS3CrashResponse = await CommitS3CrashUpload(
                        database,
                        application,
                        version,
                        md5,
                        s3Key,
                        crashTypeId
                    );

                    ThrowIfHttpRequestFailed(commitS3CrashResponse);

                    return commitS3CrashResponse;
                }
            }
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
            this.s3Client.Dispose();
        }

        private List<FileInfo> CombineListsWithDuplicatesRemoved(
            List<FileInfo> defaultList,
            List<FileInfo> overrideList
        )
        {
            return defaultList
                .Concat(overrideList)
                .GroupBy(file => file.FullName)
                .Select(group => group.First())
                .ToList();
        }

        private async Task<HttpResponseMessage> CommitS3CrashUpload(
            string database,
            string application,
            string version,
            string md5,
            string s3Key,
            int crashTypeId
        )
        {
            var baseUrl = this.CreateBaseUrlFromDatabase(database);
            var route = $"{baseUrl}/api/commitS3CrashUpload";
            var body = new MultipartFormDataContent()
            {
                { new StringContent(database), "database" },
                { new StringContent(application), "appName" },
                { new StringContent(version), "appVersion" },
                { new StringContent(crashTypeId.ToString()), "crashTypeId" },
                { new StringContent(s3Key), "s3Key" },
                { new StringContent(md5), "md5" }
            };

            return await httpClient.PostAsync(route, body);
        }

        private string CreateBaseUrlFromDatabase(string database)
        {
            return $"https://{database}.bugsplat.com";
        }

        private async Task<HttpResponseMessage> GetCrashUploadUrl(
            string database,
            string application,
            string version,
            int crashPostSize
        )
        {
            var baseUrl = this.CreateBaseUrlFromDatabase(database);
            var path = $"{baseUrl}/api/getCrashUploadUrl";
            var route = $"{path}?database={database}&appName={application}&appVersion={version}&crashPostSize={crashPostSize}";


            return await httpClient.GetAsync(route);
        }

        private string GetETagFromResponseHeaders(HttpHeaders headers)
        {
            var etagQuoted = headers.GetValues("ETag").FirstOrDefault();
            var etag = etagQuoted.Replace("\"", "");
            return etag;
        }

        private async Task<Uri> GetPresignedUrlFromResponse(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadAsStringAsync();

                var jsonObj = new JsonObject(json);
                var url = jsonObj.GetValue("url");

                return new Uri(url);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse crash upload url", ex);
            }
        }

        private InMemoryFile TryCreateInMemoryFileFromFileInfo(FileInfo fileInfo)
        {
            try
            {
                return InMemoryFile.FromFileInfo(fileInfo);
            }
            catch
            {
                return null;
            }
        }
    }
}