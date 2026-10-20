using Microsoft.Extensions.DependencyInjection;
using Ordali.Client.Auth;
using Ordali.Client.Demo;
using Ordali.Client.Http;

namespace Ordali.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the API client. With no base URL the app runs in demo mode against
    /// an in-memory backend, which is what the GitHub Pages build uses.
    /// </summary>
    public static IServiceCollection AddOrdaliClient(this IServiceCollection services, Uri? apiBaseUrl)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SessionState>();
        services.AddScoped<AuthService>();
        services.AddSingleton(new OrdaliClientOptions(apiBaseUrl));

        if (apiBaseUrl is null)
        {
            services.AddSingleton<IOrdaliApi, DemoOrdaliApi>();
        }
        else
        {
            services.AddHttpClient<IOrdaliApi, HttpOrdaliApi>(http => http.BaseAddress = apiBaseUrl);
        }

        return services;
    }
}

public sealed record OrdaliClientOptions(Uri? ApiBaseUrl)
{
    public bool IsDemo => ApiBaseUrl is null;
}
