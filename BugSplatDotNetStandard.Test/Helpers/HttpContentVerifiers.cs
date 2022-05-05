using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace Tests
{
    static class HttpContentVerifiers
    {
        public static bool ContainsValues(string haystack, IEnumerable<string> needles)
        {
            return needles.All(needle => haystack.Contains(needle));    
        }

        public static bool ContainsHeader(HttpRequestHeaders requestHeaders, string expectedKey, string expectedValue)
        {
            return requestHeaders.Any(
                header => header.Key.Equals(expectedKey) && header.Value.Any(value => value.Contains(expectedValue))
            );
        }
    }
}