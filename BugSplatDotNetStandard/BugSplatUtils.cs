
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BugSplatDotNetStandard.Test")]
namespace BugSplatDotNetStandard.Utils
{
    internal class BugSplatUtils
    {
        public static string GetStringValueOrDefault(string value, string defaultValue)
        {
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }
    }
}
