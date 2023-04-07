using Octokit;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ServicingTools.Authentication.GitHub;

/// <summary>
/// Allows authentication to GitHub and creates an object that allows executing GitHub queries.
/// </summary>
public class GitHubAuthenticator
{
    private const string _cacheFileName = "GitHubCache.txt";
    private readonly string _cacheFilePath;
    private readonly string _appName;
    private readonly string _clientId;
    private readonly string _userName;
    private readonly string _secret;
    private readonly string[] _scopes;
    private readonly GitHubClient _gitHubClient;

    /// <summary>
    /// Initializes a <see cref="GitHubAuthenticator"/> instance.
    /// </summary>
    /// <param name="appName">The application name registered on GitHub.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="userName">The user to use for the queries.</param>
    /// <param name="secret">The secret provided by GitHub.</param>
    /// <param name="scopes">The desired scopes to access the APIs.</param>
    /// <exception cref="ArgumentException"><paramref name="appName"/> or <paramref name="clientId"/> or <paramref name="userName"/> or <paramref name="secret"/> is <see langword="null"/> or empty.
    /// -or-
    /// <see cref="scopes"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><see cref="scopes"/> is <see langword="null"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Could not find the directory of either the entry assembly or the executing assembly.</exception>
    public GitHubAuthenticator(
        string appName,
        string clientId,
        string userName,
        string secret,
        string[] scopes)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(userName);
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Length == 0)
        {
            throw new ArgumentException("The scopes array is empty.");
        }

        _appName = appName;
        _clientId = clientId;
        _userName = userName;
        _secret = secret;
        _scopes = scopes;

        _gitHubClient = new GitHubClient(new ProductHeaderValue(_appName));

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        string assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? throw new DirectoryNotFoundException("Could not find the assembly directory.");

        _cacheFilePath = Path.Join(assemblyDirectory, _cacheFileName);
    }

    /// <summary>
    /// Authenticates to GitHub using the information specified in the constructor.
    /// </summary>
    /// <returns>A <see cref="GitHubClient"/> instance that can be used to execute GitHub queries.</returns>
    /// <exception cref="UnauthorizedAccessException">Authentication failed.</exception>
    public async Task<GitHubClient> AuthenticateAsync()
    {
        string? accessToken = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new UnauthorizedAccessException("Authentication failed.");
        }

        _gitHubClient.Credentials = new Credentials(accessToken);
        return _gitHubClient;
    }

    /// <summary>
    /// Tries to get the access token from the cache or retrieve a new one interactively.
    /// </summary>
    /// <returns>On success, a string with a valid access token value. Otherwise, a <see cref="null"/> or empty string.</returns>
    private async Task<string?> GetAccessTokenAsync()
    {
        string? accessToken = null;

        if (File.Exists(_cacheFilePath))
        {
            accessToken = File.ReadAllText(_cacheFilePath);
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            OauthLoginRequest oauthLoginRequest = new(_clientId);
            oauthLoginRequest.Scopes.Add(_userName);
            foreach (string scope in _scopes)
            {
                oauthLoginRequest.Scopes.Add(scope);
            }

            string authCode = GetGitHubOAuthCode(oauthLoginRequest);

            OauthTokenRequest oauthTokenRequest = new(_clientId, _secret, authCode);
            OauthToken oauthToken = await _gitHubClient.Oauth.CreateAccessToken(oauthTokenRequest);

            accessToken = oauthToken.AccessToken;
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            File.WriteAllText(_cacheFilePath, accessToken);
        }

        return accessToken;
    }

    /// <summary>
    /// Collects the authentication code interactively with the user by printing instructions to the console.
    /// </summary>
    /// <param name="oauthLoginRequest">An oauth login request instance created for the current clientId.</param>
    /// <returns>The authentication code provided interactively by the user.</returns>
    private string GetGitHubOAuthCode(OauthLoginRequest oauthLoginRequest)
    {
        Uri url = _gitHubClient.Oauth.GetGitHubLoginUrl(oauthLoginRequest);
        
        Console.WriteLine($"Go to the authorization page '{url}' then paste the authentication code in the console.");
        Console.Write("Paste the authentication code: ");
        
        string? authCode = Console.ReadLine();
        ArgumentException.ThrowIfNullOrEmpty(authCode);

        return authCode;
    }
}
