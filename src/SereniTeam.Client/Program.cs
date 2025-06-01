using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SereniTeam.Client;
using SereniTeam.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HTTP client for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register client services
builder.Services.AddScoped<ITeamApiService, TeamApiService>();
builder.Services.AddScoped<ICheckInApiService, CheckInApiService>();
builder.Services.AddScoped<ISignalRService, SignalRService>();

await builder.Build().RunAsync();