using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The WASM client hosts the docs' one interactive island — the /playground component,
// which renders Beck diagrams entirely in the browser via the pure-C# engine. The server
// project supplies all other (static SSR) chrome; here we only register the services that
// island needs. HttpClient fetches example .beck.yaml files from the site's own origin.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
