using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using static BugSplatDotNetStandard.BugSplat;

namespace BugSplatDotNetStandard
{
    public class ExceptionPostOptions : BugSplatPostOptions, IExceptionPostOptions
    {
        public ExceptionTypeId ExceptionType { get; set; } = ExceptionTypeId.Unknown;
        public override int CrashTypeId { get => (int)ExceptionType; }
        public static ExceptionPostOptions Create(IExceptionPostOptions options)
        {
            return new ExceptionPostOptions
            {
                Attachments = options.Attachments,
                FormDataParams = options.FormDataParams,
                Description = options.Description,
                Email = options.Email,
                Key = options.Key,
                IpAddress = options.IpAddress,
                Notes = options.Notes,
                User = options.User,
                ExceptionType = options.ExceptionType
            };
        }
    }

    public class MinidumpPostOptions : BugSplatPostOptions, IMinidumpPostOptions
    {
        public MinidumpTypeId MinidumpType { get; set; } = MinidumpTypeId.Unknown;
        public override int CrashTypeId { get => (int)MinidumpType; }
        public static MinidumpPostOptions Create(IMinidumpPostOptions options)
        {
            return new MinidumpPostOptions
            {
                Attachments = options.Attachments,
                FormDataParams = options.FormDataParams,
                Description = options.Description,
                Email = options.Email,
                Key = options.Key,
                IpAddress = options.IpAddress,
                Notes = options.Notes,
                User = options.User,
                MinidumpType = options.MinidumpType
            };
        }
    }

    public class XmlPostOptions : BugSplatPostOptions, IXmlPostOptions
    {
        public XmlTypeId XmlType { get; set; } = XmlTypeId.Xml;
        public override int CrashTypeId { get => (int)XmlType; }
        public static XmlPostOptions Create(IXmlPostOptions options)
        {
            return new XmlPostOptions
            {
                Attachments = options.Attachments,
                FormDataParams = options.FormDataParams,
                Description = options.Description,
                Email = options.Email,
                Key = options.Key,
                IpAddress = options.IpAddress,
                Notes = options.Notes,
                User = options.User,
                XmlType = options.XmlType
            };
        }
    }

    public class BugSplatPostOptions : IBugSplatPostOptions, IHasCrashTypeId
    {
        public List<FileInfo> Attachments { get; set; } = new List<FileInfo>();

        public List<IFormDataParam> FormDataParams { get; set; } = new List<IFormDataParam>();

        public string Description { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public virtual int CrashTypeId { get; set; } = 0;
    }

    public interface IExceptionPostOptions : IBugSplatPostOptions
    {
        /// <summary>
        /// An exception type to be added to the post that overrides the corresponding default values
        /// </summary>
        ExceptionTypeId ExceptionType { get; set; }
    }

    public interface IMinidumpPostOptions : IBugSplatPostOptions
    {
        /// <summary>
        /// A minidump type to be added to the post that overrides the corresponding default values
        /// </summary>
        MinidumpTypeId MinidumpType { get; set; }
    }

    public interface IXmlPostOptions : IBugSplatPostOptions
    {
        /// <summary>
        /// An XML report type to be added to the post that overrides the corresponding default values
        /// </summary>
        XmlTypeId XmlType { get; set; }
    }

    public interface IHasCrashTypeId
    {
        /// <summary>
        /// An identifier that tells the BugSplat backend how to process uploaded minidumps
        /// </summary>
        int CrashTypeId { get; }
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
        /// An general purpose column for extra crash metadata
        /// </summary>
        string Notes { get; set; }

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
