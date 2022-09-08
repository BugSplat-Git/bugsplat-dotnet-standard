using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Api;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard
{
    public class SymbolUploader: IDisposable
    {
        private IBugSplatApiClient bugsplatApiClient;
        private VersionsClient versionsClient;

        /// <summary>
        /// Create a SymbolUploader via either an OAuth2 or Email/Password based IBugSplatApiClient.
        /// </summary>
        /// <param name="bugsplatApiClient">Either an OAuth2ApiClient or BugSplatApiClient</param>
        public SymbolUploader(IBugSplatApiClient bugsplatApiClient) {
            ThrowIfArgumentIsNull(bugsplatApiClient, "bugsplatApiClient");

            this.bugsplatApiClient = bugsplatApiClient;
            this.versionsClient = VersionsClient.Create(bugsplatApiClient);
        }

        /// <summary>
        /// Create a BugSplat SymbolUploader via an OAuth2 ClientId/ClientSecret pair.
        /// </summary>
        /// <param name="clientId">A BugSplat OAuth2 ClientId</param>
        /// <param name="clientSecret">A BugSplat OAuth2 ClientSecret</param>
        public static SymbolUploader CreateOAuth2SymbolUploader(string clientId, string clientSecret)
        {
            return new SymbolUploader(
                OAuth2ApiClient.Create(clientId, clientSecret)
            );
        }

        /// <summary>
        /// Create a BugSplat SymbolUploader via an BugSplat email/password pair.
        /// </summary>
        /// <param name="email">BugSplat account email</param>
        /// <param name="password">BugSplat account password</param>
        public static SymbolUploader CreateSymbolUploader(string email, string password)
        {
            return new SymbolUploader(
                BugSplatApiClient.Create(email, password)
            );
        }

        /// <summary>
        /// Upload a symbol file to BugSplat
        /// </summary>
        /// <param name="database">BugSplat database to upload symbols too</param>
        /// <param name="application">BugSplat application value to associate with the symbol file</param>
        /// <param name="version">BugSplat version value to associate with the symbol file</param>
        /// <param name="symbolFileInfo">The symbol file that will be uploaded to BugSplat</param>
        /// <param name="signature">Optional, the unique symbol file signature</param>
        public async Task<HttpResponseMessage> UploadSymbolFile(string database, string application, string version, FileInfo symbolFileInfo, string signature=null)
        {
            if (!bugsplatApiClient.Authenticated)
            {
                await bugsplatApiClient.Authenticate();
            }

            return await versionsClient.UploadSymbolFile(database, application, version, symbolFileInfo, signature);
        }

        /// <summary>
        /// Upload a collection of symbol files to BugSplat
        /// </summary>
        /// <param name="database">BugSplat database to upload symbols too</param>
        /// <param name="application">BugSplat application value to associate with the symbol file</param>
        /// <param name="version">BugSplat version value to associate with the symbol file</param>
        /// <param name="symbolFileInfos">The symbol files that will be uploaded to BugSplat</param>
        public async Task<HttpResponseMessage[]> UploadSymbolFiles(string database, string application, string version, IEnumerable<FileInfo> symbolFileInfos)
        {
            ThrowIfArgumentIsNull(symbolFileInfos, "symbolFileInfos");

            if (!bugsplatApiClient.Authenticated)
            {
                await bugsplatApiClient.Authenticate();
            }

            // Unity doesn't support .NET 6 yet.
            // This can use Parallel.ForEachAsync when Unity moves to .NET 6.
            // More info: https://github.com/dotnet/runtime/issues/53709
            using (var semaphore = new SemaphoreSlim(10, 10))
            {
                return await Task.WhenAll(
                    symbolFileInfos.Select(
                        async (symbolFileInfo) =>
                        {
                            try
                            {
                                await semaphore.WaitAsync();
                                return await this.versionsClient.UploadSymbolFile(database, application, version, symbolFileInfo);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }
                    )
                );
            }
        }

        public void Dispose()
        {
            this.versionsClient.Dispose();
        }
    }
}