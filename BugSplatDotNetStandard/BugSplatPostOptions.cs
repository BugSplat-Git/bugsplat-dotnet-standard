using System.Collections.Generic;
using System.Net.Http;

namespace BugSplatDotNetStandard
{
    public class BugSplatPostOptions
    {
        public IEnumerable<KeyValuePair<string, HttpContent>> AdditionalFormDataParams { get; } = new List<KeyValuePair<string, HttpContent>>();
        public string Description { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;

    }
}
