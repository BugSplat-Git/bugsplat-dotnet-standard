using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Http;
using BugSplatDotNetStandard.Utils;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard.Api
{
    /// <summary>
    /// Used to make requests to the BugSplat Versions API
    /// </summary>
    public class VersionsClient : IDisposable
    {
        internal IZipUtils ZipUtils { get; set; } = new ZipUtils();
        private IBugSplatApiClient bugsplatApiClient;
        private IS3Client s3Client;

        internal VersionsClient(
            IBugSplatApiClient bugsplatApiClient,
            IS3ClientFactory s3ClientFactory
        )
        {
            ThrowIfArgumentIsNull(bugsplatApiClient, "bugsplatApiClient");
            ThrowIfArgumentIsNull(s3ClientFactory, "s3ClientFactory");

            this.bugsplatApiClient = bugsplatApiClient;
            this.s3Client = s3ClientFactory.CreateClient();
        }

        /// <summary>
        /// Create an unauthenticated VersionsClient
        /// </summary>
        /// <param name="bugsplatApiClient">An authenticated instance of BugSplatApiClient that will be used for API requests</param>
        public static VersionsClient Create(IBugSplatApiClient bugsplatApiClient)
        {
            return new VersionsClient(bugsplatApiClient, S3ClientFactory.Default);
        }

        /// <summary>
        /// Get a list of applications and versions with crashes and/or symbols for a BugSplat database
        /// </summary>
        /// <param name="database">BugSplat database for which version information will be retrieved</param>
        public async Task<HttpResponseMessage> GetVersions(string database)
        {
            ThrowIfArgumentIsNullOrEmpty(database, "database");

            ThrowIfNotAuthenticated(bugsplatApiClient);

            var response = await this.bugsplatApiClient.GetAsync($"/api/versions?database={database}");

            ThrowIfHttpRequestFailed(response);

            return response;
        }

        /// <summary>
        /// Upload a symbol file to BugSplat and authenticate if not already authenticated
        /// </summary>
        /// <param name="database">BugSplat database to upload symbols too</param>
        /// <param name="application">BugSplat application value to associate with the symbol file</param>
        /// <param name="version">BugSplat version value to associate with the symbol file</param>
        /// <param name="symbolFileInfo">The symbol file that will be uploaded to BugSplat</param>
        /// <param name="signature">Optional, the unique symbol file signature</param>
        public async Task<HttpResponseMessage> UploadSymbolFile(
            string database,
            string application,
            string version,
            FileInfo symbolFileInfo,
            string optionalSignature = null
        )
        {
            ThrowIfArgumentIsNullOrEmpty(database, "database");
            ThrowIfArgumentIsNullOrEmpty(application, "application");
            ThrowIfArgumentIsNullOrEmpty(version, "version");
            ThrowIfArgumentIsNull(symbolFileInfo, "symbolFileInfo");

            ThrowIfNotAuthenticated(bugsplatApiClient);

            var zipFileFullName = ZipUtils.CreateZipFileFullName(symbolFileInfo.Name);
            try
            {
                var zipFileInfo = ZipUtils.CreateZipFile(zipFileFullName, new List<FileInfo>() { symbolFileInfo });

                string optionalSymbolFileLastWriteTimeStr = null;
                string optionalModuleName = null;
                if (optionalSignature != null)
                {
                    DateTime symbolFileLastWriteTime = File.GetLastWriteTime(symbolFileInfo.FullName);
                    long unixTime = ((DateTimeOffset)symbolFileLastWriteTime).ToUnixTimeSeconds();
                    optionalSymbolFileLastWriteTimeStr = unixTime.ToString();
                    optionalModuleName = symbolFileInfo.Name;
                }

                using (var zipFileStream = ZipUtils.CreateZipFileStream(zipFileFullName))
                using (
                    var presignedUrlResponse = await this.GetSymbolUploadUrl(
                            database,
                            application,
                            version,
                            zipFileInfo.Length,
                            zipFileInfo.Name,
                            optionalModuleName,
                            optionalSymbolFileLastWriteTimeStr,
                            optionalSignature
                        )
                )
                {
                    ThrowIfHttpRequestFailed(presignedUrlResponse);

                    var presignedUrl = await this.GetPresignedUrlFromResponse(presignedUrlResponse);
                    var uploadFileResponse = await s3Client.UploadFileStreamToPresignedURL(presignedUrl, zipFileStream);

                    ThrowIfHttpRequestFailed(uploadFileResponse);

                    return uploadFileResponse;
                }
            }
            finally
            {
                File.Delete(zipFileFullName);
            }
        }

        public void Dispose()
        {
            this.s3Client.Dispose();
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
                throw new Exception("Failed to parse symnbol upload url", ex);
            }
        }

        private async Task<HttpResponseMessage> GetSymbolUploadUrl(
            string database,
            string application,
            string version,
            long symbolFileSize,
            string symbolFileName,
            string moduleName,
            string lastModified = null,
            string signature = null
        )
        {
            var route = "/api/versions";
            var size = symbolFileSize.ToString();
            var formData = new MultipartFormDataContent()
            {
                { new StringContent(database), "database" },
                { new StringContent(application), "appName" },
                { new StringContent(version), "appVersion" },
                { new StringContent(size), "size" },
                { new StringContent(symbolFileName), "symFileName" },
            };

            AddFormDataOptionalSignature(formData, moduleName, signature, lastModified);

            return await this.bugsplatApiClient.PostAsync(route, formData);
        }
        private void AddFormDataOptionalSignature(MultipartFormDataContent formData, string moduleName, string signature, string lastModified)
        {
            if( !string.IsNullOrEmpty(moduleName) && !string.IsNullOrEmpty(lastModified) && !string.IsNullOrEmpty(signature) )
            {
                formData.Add(new StringContent("bsv1"), "SendPdbsVersion");
                formData.Add(new StringContent(moduleName), "moduleName");
                formData.Add(new StringContent(lastModified), "lastModified");
                formData.Add(new StringContent(signature), "dbgId");
            }
        }
    }
}