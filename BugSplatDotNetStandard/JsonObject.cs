using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

[assembly: InternalsVisibleTo("BugSplatDotNetStandard.Test")]
namespace BugSplatDotNetStandard.Utils
{
    internal class JsonObject
    {
        private string _json;

        public JsonObject(string json)
        {
            _json = json;
        }

        public string GetValue(params string[] path)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(_json);
            var quotas = new XmlDictionaryReaderQuotas();
            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(jsonBytes, quotas);
            var root = XElement.Load(jsonReader);
            var key = string.Join("/", path);
            return root.XPathSelectElement($"//{key}").Value;
        }
    }
}
