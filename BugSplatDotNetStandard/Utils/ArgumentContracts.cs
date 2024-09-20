using System;
using System.Net.Http;
using BugSplatDotNetStandard.Api;

namespace BugSplatDotNetStandard.Utils
{
    internal static class ArgumentContracts {
        internal static void ThrowIfArgumentIsNull(object argument, string name)
        {
            if (argument == null)
            {
                throw new ArgumentNullException($"{name} cannot be null!");
            }
        }

        internal static void ThrowIfArgumentIsNullOrNegative(int argument, string name)
        {
            if (argument <= 0)
            {
                throw new ArgumentException($"{name} cannot be null or less than zero!");
            }
        }

        internal static void ThrowIfArgumentIsNullOrEmpty(string argument, string name)
        {
            if (string.IsNullOrEmpty(argument))
            {
                throw new ArgumentException($"{name} cannot be null or empty!");
            }
        }

        internal static void ThrowIfHttpRequestFailed(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.Content.ReadAsStringAsync().Result);
            }
        }

        internal static void ThrowIfNotAuthenticated(IBugSplatApiClient bugsplatApiClient)
        {
            if (!bugsplatApiClient.Authenticated)
            {
                throw new ArgumentException("BugSplatApiClient must be authenticated first!");
            }
        }
    }
}