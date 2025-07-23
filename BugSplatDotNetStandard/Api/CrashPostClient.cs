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
        public ITempFileFactory TempFileFactory { get; set; } = new TempFileFactory();
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
            using (var stackTraceTempFile = TempFileFactory.CreateFromBytes("Callstack.txt", Encoding.UTF8.GetBytes(stackTrace)))
            {
                return await PostCrashFile(
                    database,
                    application,
                    version,
                    stackTraceTempFile.File,
                    defaultPostOptions,
                    overridePostOptions
                );
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

            var additionalFormDataTempFiles = new List<ITempFile>();

            try
            {
                additionalFormDataTempFiles = CombineWithDuplicatesRemoved(defaultPostOptions.FormDataParams, overridePostOptions.FormDataParams, (param) => param.Name)
                    .Where(file => !string.IsNullOrEmpty(file.FileName) && file.Content != null)
                    .Select(param => TempFileFactory.TryCreateFromBytes(param.FileName, param.Content.ReadAsByteArrayAsync().Result))
                    .Where(temp => temp != null && temp.File.Exists)
                    .ToList();

                var additionalFormDataFiles = additionalFormDataTempFiles.Select(temp => temp.File).ToList();
                var attachments = CombineWithDuplicatesRemoved(defaultPostOptions.Attachments, overridePostOptions.Attachments, (file) => file.FullName)
                    .Where(file => file != null && file.Exists)
                    .ToList();

                attachments.AddRange(additionalFormDataFiles);

                return await PostCrash(
                    database,
                    application,
                    version,
                    crashFileInfo,
                    attachments,
                    defaultPostOptions,
                    overridePostOptions
                );
            }
            finally
            {
                foreach (var tempFile in additionalFormDataTempFiles)
                {
                    tempFile?.Dispose();
                }
            }
        }

        private async Task<HttpResponseMessage> PostCrash(
            string database,
            string application,
            string version,
            FileInfo crashFileInfo,
            IEnumerable<FileInfo> attachments,
            BugSplatPostOptions defaultPostOptions,
            BugSplatPostOptions overridePostOptions = null
        )
        {
            var files = new List<FileInfo> { crashFileInfo };
            files.AddRange(attachments);

            using (var tempZipFile = TempFileFactory.CreateTempZip(files))
            using (
                var crashUploadResponse = await GetCrashUploadUrl(
                    database,
                    application,
                    version,
                    tempZipFile.File.Length
                )
            )
            {
                ThrowIfHttpRequestFailed(crashUploadResponse);

                var presignedUrl = await GetPresignedUrlFromResponse(crashUploadResponse);

                using (var zipFileStream = tempZipFile.CreateFileStream())
                using (var uploadFileResponse = await s3Client.UploadFileStreamToPresignedURL(presignedUrl, zipFileStream))
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

        private IEnumerable<T> CombineWithDuplicatesRemoved<T>(
            IEnumerable<T> defaultList,
            IEnumerable<T> overrideList,
            Func<T, string> predicate
        )
        {
            return overrideList
                .Concat(defaultList)
                .GroupBy(predicate)
                .Select(group => group.First());
        }

        private async Task<HttpResponseMessage> CommitS3CrashUpload(
            string database,
            string application,
            string version,
            string md5,
            string s3Key,
            int crashTypeId,
            IBugSplatPostOptions defaultOptions,
            IBugSplatPostOptions overrideOptions
        )
        {
            var description = GetStringValueOrDefault(overrideOptions.Description, defaultOptions.Description);
            var email = GetStringValueOrDefault(overrideOptions.Email, defaultOptions.Email);
            var key = GetStringValueOrDefault(overrideOptions.Key, defaultOptions.Key);
            var notes = GetStringValueOrDefault(overrideOptions.Notes, defaultOptions.Notes);
            var user = GetStringValueOrDefault(overrideOptions.User, defaultOptions.User);
            var attributes = CombineWithDuplicatesRemoved(overrideOptions.Attributes, defaultOptions.Attributes, (entry) => entry.Key).ToDictionary(x => x.Key, x => x.Value);
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
                { new StringContent(md5), "md5" },
                { new StringContent(JsonSerializer.Serialize(attributes)), "attributes" }
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
            long crashPostSize
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
            var json = await response.Content.ReadAsStringAsync();

            var jsonObj = new JsonObject(json);
            var url = jsonObj.TryGetValue("url");
            var message = jsonObj.TryGetValue("message");

            if (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(message))
            {
                throw new Exception($"Failed to parse upload url: {message}");
            }

            if (string.IsNullOrEmpty(url))
            {
                throw new Exception("Failed to parse upload url");
            }

            return new Uri(url);
        }
    }
}