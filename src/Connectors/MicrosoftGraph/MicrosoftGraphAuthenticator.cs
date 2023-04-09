using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

// Some of this code was obtained from Azure-Samples/active-directory-dotnetcore-devicecodeflow-v2
namespace InfrastructureTools.Connectors.MicrosoftGraph;

/// <summary>
/// Allows authenticating to Microsoft Graph and creates an object that allows executing Graph queries.
/// </summary>
public class MicrosoftGraphAuthenticator
{
    private const string _cacheFileName = "MicrosoftGraphCache.txt";
    private readonly string _graphBaseEndPoint;
    private readonly string[] _scopes;
    private readonly IPublicClientApplication _publicClientApplication;
    private readonly StorageCreationProperties _storageCreationProperties;
    private AuthenticationResult? _authResult;

    /// <summary>
    /// The default URL used for Microsoft Graph queries.
    /// </summary>
    public const string DefaultGraphBaseEndpoint = "https://graph.microsoft.com/v1.0/";

    /// <summary>
    /// Initializes a <see cref="MicrosoftGraphAuthenticator"/> instance.
    /// </summary>
    /// <param name="tenantId">The application's tenant identifier.</param>
    /// <param name="clientId">The application's client identifier.</param>
    /// <param name="azureCloudInstance">One of the enumeration values of <see cref="AzureCloudInstance"/>, which represent the Azure login instance to use..</param>
    /// <param name="scopes">The desired scopes to access the APIs.</param>
    /// <param name="graphBaseEndpoint">An optional URL representing the Microsoft Graph endpoint and version. When unspecified, the default value is <see cref="DefaultGraphBaseEndpoint"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="azureCloudInstance"/> is not valid.</exception>
    /// <exception cref="ArgumentException"><paramref name="clientId"/> or <paramref name="tenantId"/> or <paramref name="graphBaseEndpoint"/> or <paramref name="scopes"/> is empty .</exception>
    /// <exception cref="ArgumentNullException"><paramref name="scopes"/> is <see langword="null"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Could not find the directory of either the entry assembly or the executing assembly.</exception>
    public MicrosoftGraphAuthenticator(
        string tenantId,
        string clientId,
        AzureCloudInstance azureCloudInstance,
        string[] scopes,
        string graphBaseEndpoint = DefaultGraphBaseEndpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(graphBaseEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(scopes);

        if (scopes.Length == 0)
        {
            throw new ArgumentException("The scopes array is empty.");
        }

        if (!Enum.IsDefined(azureCloudInstance))
        {
            throw new ArgumentOutOfRangeException(nameof(azureCloudInstance));
        }

        _graphBaseEndPoint = graphBaseEndpoint;
        _scopes = scopes;

        PublicClientApplicationOptions options = new()
        {
            ClientId = clientId,
            AzureCloudInstance = azureCloudInstance,
            TenantId = tenantId
        };
        _publicClientApplication = PublicClientApplicationBuilder
            .CreateWithApplicationOptions(options)
            .Build();

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        string assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? throw new DirectoryNotFoundException("Could not find the assembly directory.");

        _storageCreationProperties = new StorageCreationPropertiesBuilder(_cacheFileName, assemblyDirectory)
            .WithUnprotectedFile()
            .Build();
    }

    /// <summary>
    /// Authenticates to Microsoft Graph using the information specified in the constructor.
    /// </summary>
    /// <returns>A <see cref="MicrosoftGraphCommunicator"/> instance that can be used to execute Microsoft Graph queries.</returns>
    /// <exception cref="UnauthorizedAccessException">Authentication failed.</exception>
    public async Task<MicrosoftGraphCommunicator> AuthenticateAsync()
    {
        MsalCacheHelper cacheHelper = await MsalCacheHelper.CreateAsync(_storageCreationProperties).ConfigureAwait(false);
        cacheHelper.RegisterCache(_publicClientApplication.UserTokenCache);
        _authResult = await AcquireTokenAsync().ConfigureAwait(false);
        if (_authResult == null)
        {
            throw new UnauthorizedAccessException("Authentication failed.");
        }
        return new MicrosoftGraphCommunicator(_authResult, _graphBaseEndPoint);
    }

    /// <summary>
    /// Acquires a token either from the token cache or via code flow.
    /// </summary>
    /// <returns>An authentication result instance when the user successfully signed in. Otherwise, <see langword="null"/>.</returns>
    private async Task<AuthenticationResult?> AcquireTokenAsync()
    {
        AuthenticationResult? result = await AcquireCachedTokenAsync();
        result ??= await AcquireDeviceFlowTokenAsync().ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Silently acquires a cached token or attempts to renew an expired one.
    /// </summary>
    /// <returns>An authentication result instance when the user successfully signed in. Otherwise, <see langword="null"/>.</returns>
    private async Task<AuthenticationResult?> AcquireCachedTokenAsync()
    {
        IEnumerable<IAccount> accounts = await _publicClientApplication.GetAccountsAsync();
        if (accounts.Any())
        {
            Console.WriteLine("Trying to acquire a cached token...");
            try
            {
                // Attempt to get a token from the cache (or refresh it silently if needed)
                return await _publicClientApplication
                    .AcquireTokenSilent(_scopes, accounts.FirstOrDefault()).ExecuteAsync().ConfigureAwait(false);
            }
            catch (MsalUiRequiredException)
            {
                Console.WriteLine("Could not acquire a cached token.");
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a token interactively by asking the user to sign in in the website printed to the console.
    /// </summary>
    /// <returns>An authentication result instance when the user successfully signed in. Otherwise,<see langword="null"/>
    /// if the user canceled the sign in attempt, or did not sign in on a separate device after 15 minutes.</returns>
    private async Task<AuthenticationResult?> AcquireDeviceFlowTokenAsync()
    {
        AuthenticationResult? result;
        try
        {
            Console.WriteLine("Trying to acquire an interactive device flow token...");
            result = await _publicClientApplication.AcquireTokenWithDeviceCode(_scopes,
                deviceCodeCallback =>
                {
                    // This will print the message on the console which tells the user where to go sign-in using 
                    // a separate browser and the code to enter once they sign in.
                    // The AcquireTokenWithDeviceCodeAsync() method will poll the server after firing this
                    // device code callback to look for the successful login of the user via that browser.
                    // This background polling (whose interval and timeout data is also provided as fields in the 
                    // deviceCodeCallback class) will occur until:
                    // * The user has successfully logged in via browser and entered the proper code
                    // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
                    // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
                    //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).
                    Console.WriteLine(deviceCodeCallback.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync().ConfigureAwait(false);
        }
        catch (MsalServiceException ex)
        {
            // Kind of errors you could have (in errorCode and ex.Message)
            Console.WriteLine($"MsalServiceException with error code {ex.ErrorCode}");

            // AADSTS50059: No tenant-identifying information found in either the request or implied by any provided credentials.
            // Mitigation: as explained in the message from Azure AD, the authority needs to be tenanted. you have probably created
            // your public client application with the following authorities:
            // https://login.microsoftonline.com/common or https://login.microsoftonline.com/organizations

            // AADSTS90133: Device Code flow is not supported under /common or /consumers endpoint.
            // Mitigation: as explained in the message from Azure AD, the authority needs to be tenanted

            // AADSTS90002: Tenant <tenantId or domain you used in the authority> not found. This may happen if there are 
            // no active subscriptions for the tenant. Check with your subscription administrator.
            // Mitigation: if you have an active subscription for the tenant this might be that you have a typo in the 
            // tenantId (GUID) or tenant domain name, update the 

            // The issues above are typically programming / app configuration errors, they need to be fixed
            throw;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("OperationCanceledException");
            // If you use an override with a CancellationToken, and call the Cancel() method on it, then this may be triggered
            // to indicate that the operation was cancelled. 
            // See https://docs.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads 
            // for more detailed information on how C# supports cancellation in managed threads.
            result = null;
        }
        catch (MsalClientException ex)
        {
            Console.WriteLine($"MsalClientException with error code {ex.ErrorCode}");
            // Verification code expired before contacting the server
            // This exception will occur if the user does not manage to sign-in before a time out (15 mins) and the
            // call to `AcquireTokenWithDeviceCodeAsync` is not cancelled in between
            result = null;
        }
        return result;
    }
}