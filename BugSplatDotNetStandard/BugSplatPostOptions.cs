using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using static BugSplatDotNetStandard.BugSplat;

namespace BugSplatDotNetStandard
{
    public class ExceptionPostOptions: BugSplatPostOptions
    {
        /// <summary>
        /// An exception type to be added to the post that overrides the corresponding default values
        /// </summary>
        public ExceptionTypeId ExceptionType { get; set; } = ExceptionTypeId.Unknown;
    }

    public class MinidumpPostOptions: BugSplatPostOptions
    {
        /// <summary>
        /// A minidump type to be added to the post that overrides the corresponding default values
        /// </summary>
        public MinidumpTypeId MinidumpType { get; set; } = MinidumpTypeId.Unknown;
    }

    public abstract class BugSplatPostOptions
    {
        /// <summary>
        /// A list of attachments to be added to the post
        /// </summary>
        public List<FileInfo> AdditionalAttachments { get; } = new List<FileInfo>();

        /// <summary>
        /// A list of form data key value pairs to be added to the post
        /// </summary>
        public List<FormDataParam> AdditionalFormDataParams { get; } = new List<FormDataParam>();

        /// <summary>
        /// A description to be added to the post that overrides the corresponding default value
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// An email to be added to the post that overrides the corresponding default value
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// A key to be added to the post that overrides the corresponding default value
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// An user to be added to the post that overrides the corresponding default value
        /// </summary>
        public string User { get; set; } = string.Empty;
    }

    public class FormDataParam
    {
        /// <summary>
        /// Name to be added to MultipartFormDataContent
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Content to be added to MultipartFormDataContent
        /// </summary>
        public HttpContent Content { get; set; }

        /// <summary>
        /// Optional FileName to be added to MultipartFormDataContent if not null or empty
        /// </summary>
        public string FileName { get; set; } = string.Empty;
    }

    internal class GetPresignedUrlResponse
    {
        public string Url;
    }

}
