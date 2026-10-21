using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Ordali.Ui;
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

await builder.Build().RunAsync();
