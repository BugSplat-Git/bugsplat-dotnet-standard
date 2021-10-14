using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Utils;

namespace BugSplatDotNetStandard
{
    /// <summary>
    /// A class for uploading Exceptions and minidump files to BugSplat
    /// </summary>
    public class BugSplat
    {
        /// <summary>
        /// A list of files to be added to the upload at post time
        /// </summary>
        public List<FileInfo> Attachments { get; } = new List<FileInfo>();

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded exceptions
        /// </summary>
        public ExceptionTypeId ExceptionType { get; set; } = ExceptionTypeId.DotNetStandard;

        /// <summary>
        /// A default description added to the upload that can be overriden at post time
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A default email added to the upload that can be overriden at post time
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// A default key added to the upload that can be overriden at post time
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded minidumps
        /// </summary>
        public MinidumpTypeId MinidumpType { get; set; } = MinidumpTypeId.WindowsNative;

        /// <summary>
        /// A default user added to the upload that can be overriden at post time
        /// </summary>
        public string User { get; set; } = string.Empty;

        public enum ExceptionTypeId
        {
            Unknown = 0,
            UnityLegacy = 12,
            DotNetStandard = 18,
            Unity = 24
        }

        public enum MinidumpTypeId
        {
            Unknown = 0,
            WindowsNative = 1,
            DotNet = 8,
            UnityNativeWindows = 15
        }

        private readonly string database;
        private readonly string application;
        private readonly string version;

        /// <summary>
        /// Post Exceptions and minidump files to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        public BugSplat(string database, string application, string version)
        {
            ThrowIfArgumentIsNullOrEmpty(database, "database");
            ThrowIfArgumentIsNullOrEmpty(application, "application");
            ThrowIfArgumentIsNullOrEmpty(version, "version");

            this.database = database;
            this.application = application;
            this.version = version;
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="stackTrace">A string representation of an Exception's stack trace</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options = null)
        {
            ThrowIfArgumentIsNull(stackTrace, "stackTrace");

            using (var httpClient = new HttpClient())
            {
                options = options ?? new ExceptionPostOptions();

                var uri = new Uri($"https://{database}.bugsplat.com/post/dotnetstandard/");
                var body = CreateMultiPartFormDataContent(options);
                var crashTypeId = options?.ExceptionType != ExceptionTypeId.Unknown ? options.ExceptionType : ExceptionType;
                body.Add(new StringContent(stackTrace), "callstack");
                body.Add(new StringContent($"{(int)crashTypeId}"), "crashTypeId");

                return await httpClient.PostAsync(uri, body);
            }
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="ex">The Exception that will be serialized and posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(Exception ex, ExceptionPostOptions options = null)
        {
            ThrowIfArgumentIsNull(ex, "ex");

            return await Post(ex.ToString(), options);
        }

        /// <summary>
        /// Post a minidump file to BugSplat
        /// </summary>
        /// <param name="ex">The minidump file that will be posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, MinidumpPostOptions options = null)
        {
            ThrowIfArgumentIsNull(minidumpFileInfo, "minidumpFileInfo");

            using (var httpClient = new HttpClient())
            {
                options = options ?? new MinidumpPostOptions();

                var uri = new Uri($"https://{database}.bugsplat.com/api/upload/manual/crash.php");
                var crashTypeId = options?.MinidumpType != MinidumpTypeId.Unknown ? options.MinidumpType : MinidumpType;
                var minidump = File.ReadAllBytes(minidumpFileInfo.FullName);
                var body = CreateMultiPartFormDataContent(options);
                body.Add(new ByteArrayContent(minidump), "minidump", minidumpFileInfo.Name);
                body.Add(new StringContent($"{(int)crashTypeId}"), "crashTypeId");

                return await httpClient.PostAsync(uri, body);
            }
        }

        private void ThrowIfArgumentIsNull(object argument, string name)
        {
            if (argument == null)
            {
                throw new ArgumentNullException($"{name} cannot be null!");
            }
        }

        private void ThrowIfArgumentIsNullOrEmpty(string argument, string name)
        {
            if (string.IsNullOrEmpty(argument))
            {
                throw new ArgumentException($"{name} cannot be null or empty!");
            }
        }

        private MultipartFormDataContent CreateMultiPartFormDataContent(BugSplatPostOptions options = null)
        {
            var additionalFormDataParams = options?.AdditionalFormDataParams ?? new List<FormDataParam>();
            var description = BugSplatUtils.GetStringValueOrDefault(options?.Description, Description);
            var email = BugSplatUtils.GetStringValueOrDefault(options?.Email, Email);
            var key = BugSplatUtils.GetStringValueOrDefault(options?.Key, Key);
            var user = BugSplatUtils.GetStringValueOrDefault(options?.User, User);
            
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

            foreach (var param in additionalFormDataParams)
            {
                if (!string.IsNullOrEmpty(param.FileName))
                {
                    body.Add(param.Content, param.Name, param.FileName);
                    continue;
                }

                body.Add(param.Content, param.Name);
            }

            if (options != null)
            {
                Attachments.AddRange(options.AdditionalAttachments);
            }

            for (var i = 0; i < Attachments.Count; i++)
            {
                byte[] bytes = null;
                using (var fileStream = File.Open(Attachments[i].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }
                }

                if (bytes != null)
                {
                    var name = Attachments[i].Name;
                    body.Add(new ByteArrayContent(bytes), name, name);
                }
            }

            return body;
        }
    }
}
