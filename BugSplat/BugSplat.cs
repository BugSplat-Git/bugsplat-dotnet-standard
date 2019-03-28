using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUwp
{
    public class BugSplat
    {
        private readonly string _database;
        private readonly string _application;
        private readonly string _version;

        public BugSplat(string database, string application, string version)
        {
            _database = database;
            _application = application;
            _version = version;
        }

        public void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Post(e.ExceptionObject as Exception).Wait();
        }

        public async Task<HttpResponseMessage> Post(Exception exception)
        {
            using (var httpClient = new HttpClient())
            {
                var database = _database;
                var appName = _application;
                var appVersion = _version;
                var callstack = exception.ToString();

                var uri = new Uri($"https://{database}.bugsplat.com/post/unity/");
                var body = new MultipartFormDataContent
                {
                    { new StringContent(database), "database" },
                    { new StringContent(appName), "appName" },
                    { new StringContent(appVersion), "appVersion" },
                    { new StringContent(callstack), "callstack" }
                };

                return await httpClient.PostAsync(uri, body);
            }
        }
    }
}
