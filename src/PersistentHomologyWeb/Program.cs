using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using PersistentHomologyWeb;
using PersistentHomologyWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<HomologyRunner>();
builder.Services.AddSingleton<BoundaryExplorerState>();

await builder.Build().RunAsync();
