using Microsoft.JSInterop;

namespace Ordali.Web.Install;

/// <summary>
/// Whether the app runs in a mobile browser and can be installed. Shared by the install sheet
/// and the menu link, so either one can open the other.
/// </summary>
public sealed class InstallState : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<InstallState>? _self;

    public InstallState(IJSRuntime js) => _js = js;

    public event Action? Changed;

    public BrowserInstallInfo Info { get; private set; } = new(false, false, false, false, false);

    public bool SheetOpen { get; private set; }

    /// <summary>Phones in the browser (not already installed) are offered the app.</summary>
    public bool Offer => Info is { Mobile: true, Standalone: false };

    public async Task StartAsync()
    {
        if (_self is not null)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("ordaliInstall.subscribe", _self);
        await RefreshAsync();
        SheetOpen = Offer && !Info.Dismissed;
        Changed?.Invoke();
    }

    [JSInvokable]
    public async Task OnInstallStateChanged()
    {
        await RefreshAsync();
        Changed?.Invoke();
    }

    public void Open()
    {
        SheetOpen = true;
        Changed?.Invoke();
    }

    public async Task DismissAsync()
    {
        SheetOpen = false;
        await _js.InvokeVoidAsync("ordaliInstall.dismiss");
        Changed?.Invoke();
    }

    public async Task<string> PromptAsync()
    {
        var outcome = await _js.InvokeAsync<string>("ordaliInstall.prompt");
        if (outcome == "accepted")
        {
            SheetOpen = false;
        }

        await RefreshAsync();
        Changed?.Invoke();
        return outcome;
    }

    public async ValueTask DisposeAsync()
    {
        if (_self is not null)
        {
            try
            {
                await _js.InvokeVoidAsync("ordaliInstall.unsubscribe", _self);
            }
            catch (JSDisconnectedException)
            {
            }

            _self.Dispose();
        }
    }

    private async Task RefreshAsync() => Info = await _js.InvokeAsync<BrowserInstallInfo>("ordaliInstall.state");
}

public sealed record BrowserInstallInfo(bool Standalone, bool Mobile, bool Ios, bool CanPrompt, bool Dismissed);
