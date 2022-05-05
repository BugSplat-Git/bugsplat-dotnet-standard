namespace BugSplatDotNetStandard.Utils
{
    internal class StringUtils
    {
        public static string GetStringValueOrDefault(string value, string defaultValue)
        {
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }
    }
}
