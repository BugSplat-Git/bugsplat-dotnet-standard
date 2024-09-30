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
            var inMemoryExceptionFile = new InMemoryFile() { FileName = "Callstack.txt", Content = Encoding.UTF8.GetBytes(stackTrace) };
            return await PostInMemoryCrashFile(
                database,
                application,
                version,
                inMemoryExceptionFile,
                defaultPostOptions,
                overridePostOptions
            );
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
            var inMemoryDmpFile = TryCreateInMemoryFileFromFileInfo(minidumpFileInfo);
            return await PostInMemoryCrashFile(
                database,
                application,
                version,
                inMemoryDmpFile,
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
            var inMemoryXmlFile = TryCreateInMemoryFileFromFileInfo(xmlFileInfo);
            return await PostInMemoryCrashFile(
                database,
                application,
                version,
                inMemoryXmlFile,
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
            var inMemoryCrashFile = TryCreateInMemoryFileFromFileInfo(crashFileInfo);
            return await PostInMemoryCrashFile(
                database,
                application,
                version,
                inMemoryCrashFile,
                defaultPostOptions,
                overridePostOptions
            );
        }

        private async Task<HttpResponseMessage> PostInMemoryCrashFile(
            string database,
            string application,
            string version,
            InMemoryFile crashFile,
            BugSplatPostOptions defaultPostOptions,
            BugSplatPostOptions overridePostOptions = null
        )
        {
            overridePostOptions = overridePostOptions ?? new MinidumpPostOptions();

            var files = CombineListsWithDuplicatesRemoved(defaultPostOptions.Attachments, overridePostOptions.Attachments, (FileInfo file) => file.FullName)
                .Select(attachment => TryCreateInMemoryFileFromFileInfo(attachment))
                .Where(file => file != null)
                .ToList();

            var additionalFormDataFiles = CombineListsWithDuplicatesRemoved(defaultPostOptions.FormDataParams, overridePostOptions.FormDataParams, (IFormDataParam file) => file.Name)
                .Where(file => !string.IsNullOrEmpty(file.FileName) && file.Content != null)
                .Select(file => new InMemoryFile() { FileName = file.FileName, Content = file.Content.ReadAsByteArrayAsync().Result })
                .ToList();

            files.Add(crashFile);
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
                    var crashTypeId = overridePostOptions?.CrashTypeId != (int)MinidumpTypeId.Unknown ? overridePostOptions.CrashTypeId : defaultPostOptions.CrashTypeId;
                    var commitS3CrashResponse = await CommitS3CrashUpload(
                        database,
                        application,
                        version,
                        md5,
                        s3Key,
                        crashTypeId,
                        defaultPostOptions,
                        overridePostOptions
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

        private List<T> CombineListsWithDuplicatesRemoved<T>(
            List<T> defaultList,
            List<T> overrideList,
            Func<T, string> predicate
        )
        {
            return overrideList
                .Concat(defaultList)
                .GroupBy(predicate)
                .Select(group => group.First())
                .ToList();
        }

        private async Task<HttpResponseMessage> CommitS3CrashUpload(
            string database,
            string application,
            string version,
            string md5,
            string s3Key,
            int crashTypeId,
            IBugSplatPostOptions defaultOptions,
            IBugSplatPostOptions overrideOptions = null
        )
        {
            var description = GetStringValueOrDefault(overrideOptions?.Description, defaultOptions.Description);
            var email = GetStringValueOrDefault(overrideOptions?.Email, defaultOptions.Email);
            var key = GetStringValueOrDefault(overrideOptions?.Key, defaultOptions.Key);
            var notes = GetStringValueOrDefault(overrideOptions?.Notes, defaultOptions.Notes);
            var user = GetStringValueOrDefault(overrideOptions?.User, defaultOptions.User);
            var body = new MultipartFormDataContent()
            {
                { new StringContent(database), "database" },
                { new StringContent(application), "appName" },
                { new StringContent(version), "appVersion" },
                { new StringContent(description), "description" },
                { new StringContent(email), "email" },
                { new StringContent(key), "appKey" },
                { new StringContent(notes), "notes" },
                { new StringContent(user), "user" },
                { new StringContent(crashTypeId.ToString()), "crashTypeId" },
                { new StringContent(s3Key), "s3Key" },
                { new StringContent(md5), "md5" }
            };

            var formDataParams = overrideOptions?.FormDataParams ?? new List<IFormDataParam>();
            formDataParams.AddRange(defaultOptions.FormDataParams);
            foreach (var param in formDataParams)
            {
                if (!string.IsNullOrEmpty(param.FileName))
                {
                    body.Add(param.Content, param.Name, param.FileName);
                    continue;
                }
                body.Add(param.Content, param.Name);
            }
            var baseUrl = this.CreateBaseUrlFromDatabase(database);
            var route = $"{baseUrl}/api/commitS3CrashUpload";

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