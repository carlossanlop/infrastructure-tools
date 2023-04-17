using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class AzureDevOpsCommunicator : IDisposable
{
    private readonly string _personalAccessToken;
    private readonly string _organization;
    private readonly string _project;
    private readonly HttpClient _httpClient;
    readonly JsonSerializerOptions _options;

    internal string AzureDevOpsUrl => "https://dev.azure.com";
    internal string Organization => _organization;
    internal string Project => _project;

    public AzureDevOpsCommunicator(string organization, string project, string personalAccessToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(organization);
        ArgumentException.ThrowIfNullOrEmpty(project);
        ArgumentException.ThrowIfNullOrEmpty(personalAccessToken);

        _organization = organization;
        _project = project;
        _personalAccessToken = personalAccessToken;

        MediaTypeWithQualityHeaderValue jsonMediaType = new("application/json");

        byte[] bytes = Encoding.ASCII.GetBytes($":{_personalAccessToken}");
        string base64String = Convert.ToBase64String(bytes);
        AuthenticationHeaderValue headerValue = new("Basic", base64String);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(jsonMediaType);
        _httpClient.DefaultRequestHeaders.Authorization = headerValue;

        _options = new JsonSerializerOptions()
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
            PropertyNameCaseInsensitive = true,
        };
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
    public Task<string> CallAsync(string api, Dictionary<string, string> arguments)
    {
        string query = GetQueryUrl(api, arguments);
        return CallAsync(query);
    }

    private async Task<string> CallAsync(string query)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(query);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public string GetQueryUrl(string api, Dictionary<string, string> arguments)
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

        return $"{AzureDevOpsUrl}/{_organization}/{_project}/_apis/{api}{joinedArguments}";
    }

    public async Task<AzureDevOpsList<T>> ExecuteAsync<T>(string api, Dictionary<string, string> arguments)
    {
        string response = await CallAsync(api, arguments);
        return DeserializeAsList<T>(response);
    }

    public AzureDevOpsList<T> DeserializeAsList<T>(string response) => Deserialize<AzureDevOpsList<T>>(response);

    public T Deserialize<T>(string response) =>
        JsonSerializer.Deserialize<T>(response, _options) ?? throw new Exception("Could not deserialize the response.");

    public Task<AzureDevOpsList<BuildDefinition>> GetBuildDefinitions(BuildDefinitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.BuildDefinitionName))
        {
            options.Arguments["name"] = options.BuildDefinitionName;
        }

        return ExecuteAsync<BuildDefinition>("build/definitions", options.Arguments);
    }

    public Task<AzureDevOpsList<Build>> GetBuilds(BuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string api = options.BuildId != null && options.BuildId.HasValue ? $"build/builds/{options.BuildId.Value}" : "build/builds";
        
        return ExecuteAsync<Build>(api, options.Arguments);
    }

    public Task<AzureDevOpsList<Pipeline>> GetPipelines(PipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string api = options.PipelineId != null && options.PipelineId.HasValue ? $"pipelines/{options.PipelineId}" : "pipelines";
        
        return ExecuteAsync<Pipeline>(api, options.Arguments);
    }

    public Task<AzureDevOpsList<Commit>> GetCommits(CommitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // TODO: There are other branch search options, for now this is enough
        //if (string.IsNullOrWhiteSpace(options.BranchName))
        {
            options.Arguments["searchCriteria.itemVersion.versionType"] = "branch";
            options.Arguments["searchCriteria.itemVersion.version"] = options.BranchName;
        }
        if (options.Top > 0)
        {
            options.Arguments["searchCriteria.$top"] = $"{options.Top}";
        }

        return ExecuteAsync<Commit>($"git/repositories/{options.Repo}/commits", options.Arguments);
    }

    public Task<AzureDevOpsList<PullRequest>> GetPullRequests(PullRequestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.Repo);
        ArgumentException.ThrowIfNullOrEmpty(options.TargetBranch);
        if(!Enum.IsDefined(options.Status))
        {
            throw new ArgumentOutOfRangeException("PullRequestOptions.Status");
        }

        string prefix = "refs/heads/";
        if (!options.TargetBranch.StartsWith(prefix))
        {
            options.TargetBranch = $"{prefix}{options.TargetBranch}";
        }
        options.Arguments["searchCriteria.targetRefName"] = options.TargetBranch;

        string str = options.Status.ToString();
        options.Arguments["searchCriteria.status"] = char.ToLower(str[0]) + str[1..];

        return ExecuteAsync<PullRequest>($"git/repositories/{options.Repo}/pullrequests", options.Arguments);
    }

    public Task<AzureDevOpsList<Artifact>> GetArtifacts(ArtifactsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ArtifactName))
        {
            options.Arguments["artifactName"] = options.ArtifactName;
        }

        return ExecuteAsync<Artifact>($"build/builds/{options.BuildNumber}/artifacts", options.Arguments);
    }

    public Task<AzureDevOpsList<Artifact>> GetArtifactsFromUrl(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        Match match = Regex.Match(url, @$"{AzureDevOpsUrl}{Organization}/{Project}/_build/results?.*buildId=(?<BuildId>\d+).*");
        if (!match.Success)
        {
            throw new Exception("Incorrect URL format.");
        }

        ArtifactsOptions options = new()
        {
            BuildNumber = int.Parse(match.Groups["BuildId"].Value)
        };

        return GetArtifacts(options);
    }
}