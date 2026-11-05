using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Ordali.Client.Localization;
using Ordali.Ui;
using Ordali.Ui.Localization;
using Ordali.Web;
using Ordali.Web.Install;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// wwwroot/appsettings.json sets Api:BaseUrl. Left empty, the app runs in demo mode with
// sample data in the browser, which is how the GitHub Pages build works without a server.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
builder.Services.AddOrdaliUi(string.IsNullOrWhiteSpace(apiBaseUrl) ? null : new Uri(apiBaseUrl.TrimEnd('/') + "/"));

builder.Services.AddScoped<InstallState>();
builder.Services.AddSingleton(new Translations(InstallTexts.English));

var host = builder.Build();

// Opens in the language picked last time, or in the browser's language the first time.
await host.Services.ApplySavedLanguageAsync();

await host.RunAsync();
