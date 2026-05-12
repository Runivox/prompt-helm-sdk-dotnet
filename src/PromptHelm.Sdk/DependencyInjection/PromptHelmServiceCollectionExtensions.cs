using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PromptHelm.Sdk.DependencyInjection;

/// <summary>
/// DI extensions for registering <see cref="IPromptHelmClient"/> via
/// <c>IServiceCollection</c> with an <c>IHttpClientFactory</c>-managed <see cref="HttpClient"/>.
/// </summary>
public static class PromptHelmServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IPromptHelmClient"/> as a singleton wired to a typed HttpClient.
    /// </summary>
    public static IServiceCollection AddPromptHelm(
        this IServiceCollection services,
        Action<PromptHelmConfig> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.AddSingleton(_ =>
        {
            var config = new PromptHelmConfig();
            configure(config);
            // Validation runs inside the client constructor, but we also surface
            // a clearer error here if the caller forgot to set the API key.
            return config;
        });

        services
            .AddHttpClient(nameof(PromptHelmClient))
            .ConfigureHttpClient((sp, client) =>
            {
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            });

        services.AddSingleton<IPromptHelmClient>(sp =>
        {
            var config = sp.GetRequiredService<PromptHelmConfig>();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            HttpClient http = factory.CreateClient(nameof(PromptHelmClient));
            return new PromptHelmClient(config, http);
        });

        return services;
    }
}
