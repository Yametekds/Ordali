namespace Ordali.App.Services;

/// <summary>
/// Which backend the app talks to, kept between launches. With no URL saved the app
/// runs in demo mode with sample data, so it works straight after installing it.
/// </summary>
public sealed class ApiSettings(IPreferences preferences)
{
    private const string BaseUrlKey = "api_base_url";

    // The Android emulator reaches the host machine through 10.0.2.2, not localhost.
    public static Uri SuggestedBaseUrl { get; } = DeviceInfo.Platform == DevicePlatform.Android
        ? new Uri("http://10.0.2.2:8080/")
        : new Uri("http://localhost:8080/");

    public Uri? BaseUrl
    {
        get
        {
            string? saved = preferences.Get<string?>(BaseUrlKey, null);
            return Uri.TryCreate(saved, UriKind.Absolute, out Uri? url) ? url : null;
        }
    }

    public void UseDemo() => preferences.Remove(BaseUrlKey);

    public void UseApi(Uri url) => preferences.Set(BaseUrlKey, Normalize(url).AbsoluteUri);

    // HttpClient drops the last path segment of a base address without a trailing slash.
    public static Uri Normalize(Uri url) =>
        url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
}
