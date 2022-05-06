using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using static BugSplatDotNetStandard.BugSplat;

namespace BugSplatDotNetStandard
{
    public class ExceptionPostOptions: BugSplatPostOptions, IExceptionPostOptions
    {
        public ExceptionTypeId ExceptionType { get; set; } = ExceptionTypeId.Unknown;
    }

    public class MinidumpPostOptions: BugSplatPostOptions, IMinidumpPostOptions
    {
        public MinidumpTypeId MinidumpType { get; set; } = MinidumpTypeId.Unknown;
    }

    public abstract class BugSplatPostOptions : IBugSplatPostOptions
    {
        public List<FileInfo> Attachments { get; } = new List<FileInfo>();

        public List<IFormDataParam> FormDataParams { get; } = new List<IFormDataParam>();

        public string Description { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }

    public interface IExceptionPostOptions: IBugSplatPostOptions
    {
        /// <summary>
        /// An exception type to be added to the post that overrides the corresponding default values
        /// </summary>
        ExceptionTypeId ExceptionType { get; set; }
    }

    public interface IMinidumpPostOptions: IBugSplatPostOptions
    {
        /// <summary>
        /// A minidump type to be added to the post that overrides the corresponding default values
        /// </summary>
        MinidumpTypeId MinidumpType { get; set; }
    }

    public interface IBugSplatPostOptions
    {
        /// <summary>
        /// A list of additional attachments to be added to the post
        /// </summary>
        List<FileInfo> Attachments { get; }

        /// <summary>
        /// A list of form data key value pairs that will be appended to the corresponding default value
        /// </summary>
        List<IFormDataParam> FormDataParams { get; }

        /// <summary>
        /// A description to be added to the post that overrides the corresponding default value
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// An email to be added to the post that overrides the corresponding default value
        /// </summary>
        string Email { get; set; }

        /// <summary>
        /// A key to be added to the post that overrides the corresponding default value
        /// </summary>
        string Key { get; set; }

        /// <summary>
        /// An IP Address to be added to the post that overrides the corresponding default value
        /// </summary>
        string IpAddress { get; set; }

        /// <summary>
        /// A user to be added to the post that overrides the corresponding default value
        /// </summary>
        string User { get; set; }
    }

    public interface IFormDataParam
    {
        /// <summary>
        /// Name to be added to MultipartFormDataContent
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Content to be added to MultipartFormDataContent
        /// </summary>
        HttpContent Content { get; set; }

        /// <summary>
        /// Optional FileName to be added to MultipartFormDataContent if not null or empty
        /// </summary>
        string FileName { get; set; }
    }

    public class FormDataParam : IFormDataParam
    {
        public string Name { get; set; }
        public HttpContent Content { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    internal class GetPresignedUrlResponse
    {
        public string Url;
    }

}
