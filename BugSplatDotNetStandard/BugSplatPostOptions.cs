using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace BugSplatDotNetStandard
{
    public class BugSplatPostOptions
    {
        /// <summary>
        /// A list of attachments to be added to the post
        /// </summary>
        public List<FileInfo> AdditionalAttachments { get; } = new List<FileInfo>();

        /// <summary>
        /// A list of form data key value pairs to be added to the post
        /// </summary>
        public List<KeyValuePair<string, HttpContent>> AdditionalFormDataParams { get; } = new List<KeyValuePair<string, HttpContent>>();
        
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
}
