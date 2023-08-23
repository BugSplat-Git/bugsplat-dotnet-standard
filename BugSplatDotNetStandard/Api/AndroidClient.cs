using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Utils;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard.Api
{
    /// <summary>
    /// Used to make requests to the BugSplat Versions API
    /// </summary>
    public class AndroidClient
    {
        internal IZipUtils ZipUtils { get; set; } = new ZipUtils();
        private IBugSplatApiClient bugsplatApiClient;

        internal AndroidClient(IBugSplatApiClient bugsplatApiClient)
        {
            ThrowIfArgumentIsNull(bugsplatApiClient, "bugsplatApiClient");

            this.bugsplatApiClient = bugsplatApiClient;
        }

        /// <summary>
        /// Create an authenticated AndroidClient
        /// </summary>
        /// <param name="bugsplatApiClient">An instance of BugSplatApiClient that will be used for API requests</param>
        public static async Task<AndroidClient> Create(string database, IBugSplatApiClient bugsplatApiClient)
        {
            ThrowIfArgumentIsNullOrEmpty(database, "database");
            ThrowIfArgumentIsNull(bugsplatApiClient, "bugsplatApiClient");

            bugsplatApiClient.Host = new Uri($"https://{database}.bugsplat.com");

            if (!bugsplatApiClient.Authenticated)
            {
                await bugsplatApiClient.Authenticate();
            }

            return new AndroidClient(bugsplatApiClient);
        }

        /// <summary>
        /// Run dump_syms on an Android binary file and return the result
        /// </summary>
        /// <param name="fileInfo">FileInfo object that points to an Android binary or .so file</param>
        public async Task<HttpResponseMessage> UploadBinaryFile(FileInfo fileInfo)
        {
            ThrowIfArgumentIsNull(fileInfo, "fileInfo");

            ThrowIfNotAuthenticated(bugsplatApiClient);

            using (var fileStream = fileInfo.OpenRead())
            {
                var content = new MultipartFormDataContent
                {
                    { new StreamContent(fileStream), "file", fileInfo.Name }
                };

                var response = await this.bugsplatApiClient.PostAsync("/post/android/symbols", content);
                
                return response;
            }
        }
    }
}