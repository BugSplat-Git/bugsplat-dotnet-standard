using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BugSplatUwp
{
    public class BugSplat
    {
        private readonly string _database;
        private readonly string _application;
        private readonly string _version;
        private readonly List<FileInfo> _files = new List<FileInfo>();

        public BugSplat(string database, string application, string version)
        {
            _database = database;
            _application = application;
            _version = version;
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

                for (var i = 0; i < _files.Count; i++)
                {
                    var bytes = File.ReadAllBytes(_files[i].FullName);
                    body.Add(new StringContent(_files[i].Name), $"fileName{i + 1}");
                    body.Add(new StringContent(Convert.ToBase64String(bytes)), $"optFile{i + 1}");
                }

                return await httpClient.PostAsync(uri, body);
            }
        }

        public void AttachFile(FileInfo file)
        {
            _files.Add(file);
        }
    }
}
