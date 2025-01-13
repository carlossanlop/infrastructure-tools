using InfrastructureTools.Shared;
using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace InfrastructureTools.Connectors.GitHub;

/// <summary>
/// Allows authentication to GitHub and creates an object that allows executing GitHub queries.
/// </summary>
public class GitHubAuthenticator
{
    private readonly string[] DefaultScopes = ["public_repo", "user"];
    private readonly GitHubOptions _options;
    private GitHubClient? _client;

    public static async Task<GitHubClient> GetClientAsync(string optionsFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(optionsFilePath);
        ConsoleLog.WriteInfo($"Reading GitHub options from '{optionsFilePath}'...");
        using FileStream fileStream = File.OpenRead(optionsFilePath);
        GitHubOptions options = await System.Text.Json.JsonSerializer.DeserializeAsync<GitHubOptions>(fileStream) ?? throw new InvalidOperationException("The options file could not be deserialized.");
        GitHubAuthenticator authenticator = new(options);
        ConsoleLog.WriteSuccess("Successfully generated a GitHub client.");
        return await authenticator.GetAuthenticatedClientAsync();
    }

    /// <summary>
    /// Initializes a <see cref="GitHubAuthenticator"/> instance.
    /// </summary>
    /// <param name="options">The options to authenticate to GitHub.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="log"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> is missing required properties.</exception>
    public GitHubAuthenticator(GitHubOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            ArgumentException.ThrowIfNullOrEmpty(options.AppName);
            ArgumentException.ThrowIfNullOrEmpty(options.ClientId);
            ArgumentException.ThrowIfNullOrEmpty(options.UserName);
            ArgumentException.ThrowIfNullOrEmpty(options.Secret);
        }

        if (options.Scopes == null || options.Scopes.Length == 0)
        {
            ConsoleLog.WriteInfo($"Setting default scopes: [{string.Join(", ", DefaultScopes)}]");
            options.Scopes = DefaultScopes;
        }

        _options = options;
    }

    private async Task<GitHubClient> GetAuthenticatedClientAsync()
    {
        _client ??= new GitHubClient(new ProductHeaderValue(_options.AppName));
        string accessToken = await GetAccessTokenAsync();
        _client.Credentials = new Credentials(accessToken);
        return _client;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        Debug.Assert(_client != null);

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            ConsoleLog.WriteInfo("No access token provided. Authenticating to GitHub...");
            OauthLoginRequest oauthLoginRequest = new(_options.ClientId);
            foreach (string scope in _options.Scopes)
            {
                oauthLoginRequest.Scopes.Add(scope);
            }
            string authCode = GetGitHubOAuthCode(oauthLoginRequest);
            OauthTokenRequest oauthTokenRequest = new(_options.ClientId, _options.Secret, authCode);
            OauthToken accessToken = await _client.Oauth.CreateAccessToken(oauthTokenRequest);
            _options.AccessToken = accessToken.AccessToken;
        }

        ConsoleLog.WriteSuccess("Successfully obtained a GitHub access token.");
        return _options.AccessToken;
    }

    private string GetGitHubOAuthCode(OauthLoginRequest oauthLoginRequest)
    {
        Debug.Assert(_client != null);

        Uri url = _client.Oauth.GetGitHubLoginUrl(oauthLoginRequest);
        ConsoleLog.WriteWarning($"Go to the authorization page '{url}' then paste the authentication code in the console.");
        ConsoleLog.WriteWarning("Paste the authentication code: ");
        string? authCode = Console.ReadLine();
        ArgumentException.ThrowIfNullOrWhiteSpace(authCode);

        return authCode;
    }
}
