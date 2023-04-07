using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

// Some of this code was obtained from Azure-Samples/active-directory-dotnetcore-devicecodeflow-v2
namespace ServicingTools.Authentication.MicrosoftGraph;

/// <summary>
/// Allows executing Microsoft Graph queries.
/// </summary>
public class MicrosoftGraphCommunicator
{
    private const string JsonMediaType = "application/json";

    private readonly string _graphBaseEndpoint;
    private readonly AuthenticationResult _authResult;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a <see cref="MicrosoftGraphCommunicator"/> instance.
    /// </summary>
    /// <param name="authResult">A previously generated authentication object, containing a valid authentication state to Microsoft Graph.</param>
    /// <param name="graphBaseEndpoint">The URL string representing the endpoint and Graph version.</param>
    internal MicrosoftGraphCommunicator(AuthenticationResult authResult, string graphBaseEndpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(graphBaseEndpoint);
        ArgumentNullException.ThrowIfNull(authResult);
        _graphBaseEndpoint = graphBaseEndpoint;
        _authResult = authResult;

        _httpClient = new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Accept.Any(m => m.MediaType == JsonMediaType))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        }
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", _authResult.AccessToken);
    }

    /// <summary>
    /// Executes a Graph API query.
    /// Examples:
    /// - Retrieve the current user's information: 'me'.
    /// - Retrieve the current user's manager's information: 'me/manager'.
    /// </summary>
    /// <param name="api">Represents the arguments of the query, which come after the base endpoint URL and the version.</param>
    /// <returns>A boolean-string tuple where the boolean value indicates whether the status code was success or not, and the string contains the response content.</returns>
    public async Task<(bool, string?)> TryCallAsync(string api)
    {
        ArgumentException.ThrowIfNullOrEmpty(api);
        string query = $"{_graphBaseEndpoint}{api}";
        HttpResponseMessage response = await _httpClient.GetAsync(query);
        bool success = response.IsSuccessStatusCode;
        string result = await response.Content.ReadAsStringAsync();
        return (success, result);
    }
}
