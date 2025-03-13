using System.Net.Http;
using System.Threading.Tasks;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard.Api
{
    /// <summary>
    /// Used to make requests to the BugSplat Versions API
    /// </summary>
    public class CrashDetailsClient
    {
        private IBugSplatApiClient bugsplatApiClient;

        internal CrashDetailsClient(IBugSplatApiClient bugsplatApiClient)
        {
            ThrowIfArgumentIsNull(bugsplatApiClient, "bugsplatApiClient");

            this.bugsplatApiClient = bugsplatApiClient;
        }

        /// <summary>
        /// Create a CrashDetailsApiClient, consumer is responsible for authentication
        /// </summary>
        /// <param name="bugsplatApiClient">An authenticated instance of BugSplatApiClient that will be used for API requests</param>
        public static CrashDetailsClient Create(IBugSplatApiClient bugsplatApiClient)
        {
            return new CrashDetailsClient(bugsplatApiClient);
        }

        /// <summary>
        /// Get details of a crash from a BugSplat database by ID
        /// </summary>
        /// <param name="database">BugSplat database for which crash information will be retrieved</param>
        /// <param name="id">Id of the crash to retrieve</param>
        public async Task<HttpResponseMessage> GetCrashDetails(string database, int id)
        {
            ThrowIfArgumentIsNullOrEmpty(database, "database");
            ThrowIfArgumentIsNullOrNegative(id, "id");

            ThrowIfNotAuthenticated(bugsplatApiClient);

            var response = await this.bugsplatApiClient.GetAsync($"/api/crash/details?database={database}&id={id}");

            ThrowIfHttpRequestFailed(response);

            return response;
        }
    }
}