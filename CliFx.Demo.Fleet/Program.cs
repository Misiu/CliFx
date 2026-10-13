using System;
using System.Linq;
using System.Net.Http;
using CliFx;
using CliFx.Demo.Fleet.Services;
using Microsoft.Extensions.DependencyInjection;

return await new CommandLineApplicationBuilder()
    .SetExecutableName("fleet")
    .SetVersion("v0.1.0")
    .SetDescription("Demo application showcasing dynamic shell completion in CliFx.")
    // Enables the `[completion] <shell>` and `[suggest] <text>` directives
    .AllowCompletion()
    .AddCommandsFromThisAssembly()
    .UseTypeInstantiator(commands =>
    {
        var services = new ServiceCollection();

        services.AddSingleton<FleetPaths>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<DeviceSuggestionCache>();
        services.AddSingleton(
            new HttpClient
            {
                BaseAddress = new Uri(
                    Environment.GetEnvironmentVariable("FLEET_API_URL") is { Length: > 0 } url
                        ? url.TrimEnd('/') + '/'
                        : "https://dummyjson.com/"
                ),
                Timeout = TimeSpan.FromSeconds(10),
            }
        );
        services.AddSingleton<FleetApiClient>();

        // Register all commands as transient services
        foreach (var command in commands)
            services.AddTransient(command.Type);

        // Register all completion providers too, so that they can take dependencies just like commands
        foreach (
            var providerType in commands
                .SelectMany(c => c.Inputs)
                .Select(i => i.CompletionProviderType)
                .OfType<Type>()
                .Distinct()
        )
        {
            services.AddTransient(providerType);
        }

        return services.BuildServiceProvider();
    })
    .Build()
    .RunAsync();
