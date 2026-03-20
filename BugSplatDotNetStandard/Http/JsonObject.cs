using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace BugSplatDotNetStandard.Http
{
    // We have opted to not introduce a 3rd-party dependency to better support Unity.
    // When Unity moves to .NET 6 we can replace this with System.Text.Json.
    // More information about Unity's plans to update to .NET 6 can be found here:
    // https://forum.unity.com/threads/unity-future-net-development-status.1092205/

    public class JsonObject
    {
        private string json;

        public JsonObject(string json)
        {
            this.json = json;
        }

        public string GetValue(params string[] path)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var quotas = new XmlDictionaryReaderQuotas();
            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(jsonBytes, quotas);
            var root = XElement.Load(jsonReader);
            var key = string.Join("/", path);
            return root.XPathSelectElement($"//{key}").Value;
        }
        
        public string TryGetValue(params string[] path)
        {
            try
            {
                return GetValue(path);
            }
            catch
            {
                return null;
            }
        }
    }

    public static class JsonSerializer
    {
        public static string Serialize(Dictionary<string, string> dictionary)
        {
            if (dictionary == null)
                return "null";

            var sb = new StringBuilder();
            sb.Append("{");

            var isFirst = true;
            foreach (var kvp in dictionary)
            {
                if (!isFirst)
                    sb.Append(",");

                sb.Append($"\"{EscapeJsonString(kvp.Key)}\":");

                if (kvp.Value == null)
                    sb.Append("null");
                else
                    sb.Append($"\"{EscapeJsonString(kvp.Value)}\"");

                isFirst = false;
            }

            sb.Append("}");
            return sb.ToString();
        }

        public static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            var sb = new StringBuilder(str.Length + 16);
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32 || c > 127)
                        {
                            // Characters above U+FFFF (e.g. emoji) are represented as surrogate pairs
                            // in C# strings. Each half is emitted as a separate \uXXXX escape, which is
                            // valid JSON per RFC 8259.
                            sb.Append($"\\u{(int)c:X4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
