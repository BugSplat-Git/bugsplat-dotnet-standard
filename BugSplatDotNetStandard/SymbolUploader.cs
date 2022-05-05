using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Api;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard
{
    public class SymbolUploader
    {
        private IBugSplatApiClient bugsplatApiClient;

        public SymbolUploader(IBugSplatApiClient bugsplatApiClient) {
            ThrowIfArgumentIsNull(bugsplatApiClient, "bugsplatApiClient");

            this.bugsplatApiClient = bugsplatApiClient;
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
        public async Task<HttpResponseMessage> UploadSymbolFile(string database, string application, string version, FileInfo symbolFileInfo)
        {
            if (!bugsplatApiClient.Authenticated)
            {
                await bugsplatApiClient.Authenticate();
            }

            using (var versionsClient = VersionsClient.Create(bugsplatApiClient))
            {
                return await versionsClient.UploadSymbolFile(database, application, version, symbolFileInfo);
            }
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

            return await Task.WhenAll(
                symbolFileInfos.Select(
                    async symbolFileInfo => await this.UploadSymbolFile(database, application, version, symbolFileInfo)
                )
            );
        }
    }
}