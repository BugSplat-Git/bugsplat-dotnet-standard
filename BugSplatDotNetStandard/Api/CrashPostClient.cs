using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private HttpClient httpClient;
        private S3Client s3Client;

        public CrashPostClient(
            IHttpClientFactory httpClientFactory,
            IS3ClientFactory s3ClientFactory
        )
        {
            ThrowIfArgumentIsNull(httpClientFactory, "httpClientFactory");
            ThrowIfArgumentIsNull(s3ClientFactory, "s3ClientFactory");
            
            this.httpClient = httpClientFactory.CreateClient();
            this.s3Client = s3ClientFactory.Create();
        }

        public async Task<HttpResponseMessage> PostException(
            string database,
            string application,
            string version,
            string stackTrace,
            IExceptionPostOptions defaultPostOptions,
            IExceptionPostOptions overridePostOptions = null
        )
        {
            overridePostOptions = overridePostOptions ?? new ExceptionPostOptions();

            var baseUrl = this.CreateBaseUrlFromDatabase(database);
            var uri = new Uri($"{baseUrl}/post/dotnetstandard/");
            var body = CreateMultiPartFormDataContent(database, application, version, defaultPostOptions, overridePostOptions);
            var crashTypeId = overridePostOptions?.ExceptionType != ExceptionTypeId.Unknown ? overridePostOptions.ExceptionType : defaultPostOptions.ExceptionType;
            body.Add(new StringContent(stackTrace), "callstack");
            body.Add(new StringContent($"{(int)crashTypeId}"), "crashTypeId");

            return await httpClient.PostAsync(uri, body);
        }

        public async Task<HttpResponseMessage> PostMinidump(
            string database,
            string application,
            string version,
            FileInfo minidumpFileInfo,
            IMinidumpPostOptions defaultPostOptions,
            IMinidumpPostOptions overridePostOptions = null
        )
        {
            overridePostOptions = overridePostOptions ?? new MinidumpPostOptions();

            var files = new List<FileInfo>();
            files.Add(minidumpFileInfo);
            files.AddRange(overridePostOptions.Attachments);

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
                    var commitS3CrashResponse = await CommitS3CrashUpload(
                        database,
                        application,
                        version,
                        md5,
                        s3Key,
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

        private async Task<HttpResponseMessage> CommitS3CrashUpload(
            string database,
            string application,
            string version,
            string md5,
            string s3Key,
            IMinidumpPostOptions defaultPostOptions,
            IMinidumpPostOptions overridePostOptions
        )
        {
            var baseUrl = this.CreateBaseUrlFromDatabase(database);
            var route = $"{baseUrl}/api/commitS3CrashUpload";
            var crashTypeId = overridePostOptions?.MinidumpType != MinidumpTypeId.Unknown ? overridePostOptions.MinidumpType : defaultPostOptions.MinidumpType;
            var body = CreateMultiPartFormDataContent(
                database,
                application,
                version,
                defaultPostOptions,
                overridePostOptions
            );
            body.Add(new StringContent($"{(int)crashTypeId}"), "crashTypeId");
            body.Add(new StringContent(s3Key), "s3Key");
            body.Add(new StringContent(md5), "md5");

            return await httpClient.PostAsync(route, body);
        }

        private string CreateBaseUrlFromDatabase(string database)
        {
            return $"https://{database}.bugsplat.com";
        }

        private MultipartFormDataContent CreateMultiPartFormDataContent(
            string database,
            string application,
            string version,
            IBugSplatPostOptions defaultOptions,
            IBugSplatPostOptions overrideOptions = null
        )
        {
            var description = GetStringValueOrDefault(overrideOptions?.Description, defaultOptions.Description);
            var email = GetStringValueOrDefault(overrideOptions?.Email, defaultOptions.Email);
            var key = GetStringValueOrDefault(overrideOptions?.Key, defaultOptions.Key);
            var user = GetStringValueOrDefault(overrideOptions?.User, defaultOptions.User);

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

            var attachments = new List<FileInfo>();
            attachments.AddRange(defaultOptions.Attachments);
            if (overrideOptions != null)
            {
                attachments.AddRange(overrideOptions.Attachments);
            }

            for (var i = 0; i < attachments.Count; i++)
            {
                byte[] bytes = null;
                using (var fileStream = File.Open(attachments[i].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }
                }

                if (bytes != null)
                {
                    var name = attachments[i].Name;
                    body.Add(new ByteArrayContent(bytes), name, name);
                }
            }

            return body;
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
    }
}