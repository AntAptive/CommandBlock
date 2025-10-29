using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using static CommandBlock.Utils;

namespace CommandBlock
{
    internal static class HttpService
    {
        // Simplified class of HttpResponseMessage to make our code easier
        internal class HttpResponse
        {
            internal HttpStatusCode status { get; set; }
            internal string response { get; set; } = "";
            internal string error { get; set; } = "";
        }

        internal static async Task<HttpResponse> SendPOST(string callback, string token)
        {
            string url = Config.CurrentConfig.serverHTTPUrl + callback;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);

                    HttpResponseMessage response = await client.PostAsync(url, null); // no body
                    string result = await response.Content.ReadAsStringAsync();

                    Print($"Status: {response.StatusCode}");
                    Print($"Response: {result}");

                    JObject json = JObject.Parse(result);
                    string errorValue = (string?)json["error"] ?? ""; // Blank if no error field

                    return new HttpResponse
                    {
                        status = response.StatusCode,
                        response = result,
                        error = errorValue
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                PrintError($"A connection error occurred while sending an HTTP POST request: {ex.Message}");
                return new HttpResponse()
                {
                    status = HttpStatusCode.Forbidden,
                    response = ""
                };
            }
        }

        internal static async Task<HttpResponse> SendGET(string callback, string token, bool silent = false)
        {
            string url = Config.CurrentConfig.serverHTTPUrl + callback;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);

                    HttpResponseMessage response = await client.GetAsync(url); // no body
                    string result = await response.Content.ReadAsStringAsync();

                    if (!silent)
                    {
                        Print($"Status: {response.StatusCode}");
                        Print($"Response: {result}");
                    }

                    return new HttpResponse
                    {
                        status = response.StatusCode,
                        response = result
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                if (!silent)
                    PrintError($"A connection error occurred while sending an HTTP GET request: {ex.Message}");

                return new HttpResponse()
                {
                    status = HttpStatusCode.Forbidden,
                    response = ""
                };
            }
        }

    }
}
