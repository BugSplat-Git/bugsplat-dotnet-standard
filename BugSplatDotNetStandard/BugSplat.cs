using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Http;
using BugSplatDotNetStandard.Api;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard
{
    /// <summary>
    /// A class for uploading Exceptions and minidump files to BugSplat
    /// </summary>
    public class BugSplat : IExceptionPostOptions, IMinidumpPostOptions, IXmlPostOptions
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
        /// A default description added to the upload that can be overridden at post time
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A default email added to the upload that can be overridden at post time
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// A list of form data key value pairs to be added to the upload at post time
        /// </summary>
        public List<IFormDataParam> FormDataParams { get; } = new List<IFormDataParam>();

        /// <summary>
        /// A default key added to the upload that can be overridden at post time
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// A default IP Address value added to the upload that can be overridden at post time
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded minidumps
        /// </summary>
        public MinidumpTypeId MinidumpType { get; set; } = MinidumpTypeId.WindowsNative;

        /// <summary>
        /// An general purpose column for extra crash metadata
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// A default user added to the upload that can be overridden at post time
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded XML reports
        /// </summary>
        public XmlTypeId XmlType { get; set; } = XmlTypeId.Xml;

        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded reports
        /// </summary>
        public int CrashTypeId { get; set; } = 0;

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

        public enum XmlTypeId
        {
            Xml = 21,
            Asan = 25
        }

        public string Database { get; private set; }
        public string Application { get; private set; }
        public string Version { get; private set; }

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

            this.Database = database;
            this.Application = application;
            this.Version = version;
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="stackTrace">A string representation of an Exception's stack trace</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(string stackTrace, ExceptionPostOptions options = null)
        {
            ThrowIfArgumentIsNull(stackTrace, "stackTrace");

            using (var crashPostClient = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default))
            {
                return await crashPostClient.PostException(
                    Database,
                    Application,
                    Version,
                    stackTrace,
                    ExceptionPostOptions.Create(this),
                    options
                );
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

            using (var crashPostClient = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default))
            {
                return await crashPostClient.PostMinidump(
                    Database,
                    Application,
                    Version,
                    minidumpFileInfo,
                    MinidumpPostOptions.Create(this),
                    options
                );
            }
        }

        /// <summary>
        /// Post an XML report to BugSplat
        /// </summary>
        /// <param name="ex">The XML file that will be posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(FileInfo xmlFileInfo, XmlPostOptions options = null)
        {
            ThrowIfArgumentIsNull(xmlFileInfo, "xmlFileInfo");

            using (var crashPostClient = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default))
            {
                return await crashPostClient.PostXmlReport(
                    Database,
                    Application,
                    Version,
                    xmlFileInfo,
                    XmlPostOptions.Create(this),
                    options
                );
            }
        }

        /// <summary>
        /// Post a report to BugSplat, caller is responsible for setting the correct CrashTypeId
        /// </summary>
        /// <param name="ex">The report file that will be posted to BugSplat, can be a minidump or XML report</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(FileInfo crashFileInfo, BugSplatPostOptions options = null)
        {
            ThrowIfArgumentIsNull(crashFileInfo, "crashFileInfo");

            using (var crashPostClient = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default))
            {
                return await crashPostClient.PostCrashFile(
                    Database,
                    Application,
                    Version,
                    crashFileInfo,
                    BugSplatPostOptions.Create(this, CrashTypeId),
                    options
                );
            }
        }
    }
}
