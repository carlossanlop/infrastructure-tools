using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class AzureDevOpsCommunicator : IDisposable
{
    private readonly string _personalAccessToken;
    private readonly HttpClient _httpClient;

    public AzureDevOpsCommunicator(string personalAccessToken)
    {
        _personalAccessToken = personalAccessToken;

        MediaTypeWithQualityHeaderValue jsonMediaType = new("application/json");

        byte[] bytes = Encoding.ASCII.GetBytes($":{_personalAccessToken}");
        string base64String = Convert.ToBase64String(bytes);
        AuthenticationHeaderValue headerValue = new("Basic", base64String);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(jsonMediaType);
        _httpClient.DefaultRequestHeaders.Authorization = headerValue;
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// </summary>
    public void Dispose() => _httpClient.Dispose();

    /// <summary>
    /// Executes an Azure DevOps API query.
    /// </summary>
    /// <param name="api">Represents the value of the API to invoke.</param>
    /// <returns>The response content of the query.</returns>
    public Task<string> CallAsync(string organization, string project, string api, Dictionary<string, string> arguments)
    {
        string query = GetQueryUrl(organization, project, api, arguments);
        return CallAsync(query);
    }

    public async Task<string> CallAsync(string query)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(query);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public string GetQueryUrl(string organization, string project, string api, Dictionary<string, string> arguments)
    {
        string joinedArguments = "";
        bool first = true;
        foreach ((string key, string value) in arguments)
        {
            string separator = "&";
            if (first)
            {
                separator = "?";
                first = false;
            }
            joinedArguments += $"{separator}{key}={value}";
        }

        return $"https://dev.azure.com/{organization}/{project}/_apis/{api}{joinedArguments}";
    }
}