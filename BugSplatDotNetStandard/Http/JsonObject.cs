using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace BugSplatDotNetStandard.Http
{
    // We have opted to not introduce a 3rd-party dependency to better support Unity.
    // bugsplat-unity vendors BugSplatDotNetStandard.dll as a bare plugin with no NuGet
    // restore, so a PackageReference here would have to be hand-shipped into Unity, where
    // vendored System.* copies collide with the engine's own assemblies.
    // System.Text.Json remains the intended replacement once that constraint lifts.
    // More information about Unity's plans to update to .NET 6 can be found here:
    // https://forum.unity.com/threads/unity-future-net-development-status.1092205/

    internal class JsonObject
    {
        // JsonReaderWriterFactory maps each JSON value to an element carrying a type
        // attribute, which is omitted for strings. A property whose name is not a valid
        // XML name is mapped to <item item="the name"> rather than <the name>.
        private const string TypeAttributeName = "type";
        private const string ItemElementName = "item";
        private const string StringType = "string";
        private const string ObjectType = "object";

        private static readonly HashSet<string> ValueTypes = new HashSet<string>()
        {
            StringType, "number", "boolean"
        };

        private readonly XElement root;

        /// <summary>
        /// Parses json. Throws if json is not well formed.
        /// </summary>
        public JsonObject(string json)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var quotas = new XmlDictionaryReaderQuotas();
            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(jsonBytes, quotas);
            root = XElement.Load(jsonReader);
        }

        /// <summary>
        /// Walks path from the root of the document one property at a time and returns true
        /// if it lands on a string, number or boolean, which is written to value. Returns
        /// false if a property along the way is absent, or the value is null, an object or
        /// an array. Never throws, so an absent property costs nothing.
        /// </summary>
        public bool TryGetValue(out string value, params string[] path)
        {
            value = null;

            if (path == null || path.Length == 0)
            {
                return false;
            }

            var element = root;
            foreach (var name in path)
            {
                element = FindProperty(element, name);

                if (element == null)
                {
                    return false;
                }
            }

            if (!ValueTypes.Contains(GetElementType(element)))
            {
                return false;
            }

            value = element.Value;
            return true;
        }

        private static XElement FindProperty(XElement parent, string name)
        {
            if (!ObjectType.Equals(GetElementType(parent)))
            {
                return null;
            }

            // Compare names rather than calling Element(name), which throws when name is
            // not a valid XML name
            return parent.Elements().FirstOrDefault(child => name.Equals(GetPropertyName(child)));
        }

        private static string GetPropertyName(XElement element)
        {
            var itemAttribute = element.Attribute(ItemElementName);

            return ItemElementName.Equals(element.Name.LocalName) && itemAttribute != null
                ? itemAttribute.Value
                : element.Name.LocalName;
        }

        private static string GetElementType(XElement element)
        {
            return element.Attribute(TypeAttributeName)?.Value ?? StringType;
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
