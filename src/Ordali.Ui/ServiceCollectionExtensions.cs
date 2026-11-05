using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Ui.Localization;
using Ordali.Ui.Services;

namespace Ordali.Ui;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the API client and everything the shared pages need. A host can register its
    /// own <see cref="ISessionStorage"/> or <see cref="ILanguageStore"/> before calling this to keep the
    /// session or the chosen language elsewhere.
    /// </summary>
    public static IServiceCollection AddOrdaliUi(this IServiceCollection services, Uri? apiBaseUrl)
    {
        services.AddOrdaliClient(apiBaseUrl);
        services.AddAuthorizationCore(Policies.Register);
        services.TryAddScoped<ISessionStorage, LocalStorageSessionStorage>();
        services.TryAddScoped<ILanguageStore, LocalStorageLanguageStore>();
        services.AddSingleton<IStringLocalizer, OrdaliStringLocalizer>();
        services.AddScoped<OrdaliAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<OrdaliAuthenticationStateProvider>());
        return services;
    }
}
