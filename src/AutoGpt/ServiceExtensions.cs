using AutoGpt.Internal;
using AutoGpt.Options;
using Microsoft.Extensions.DependencyInjection;
using OpenAI_API;

namespace AutoGpt;

public static class ServiceExtensions
{
    public static IServiceCollection AddAutoGpt(this IServiceCollection services, Action<AutoGptOptions> configure)
    {
        var options = new AutoGptOptions();

        configure(options);

        options.Validate();

        services.AddSingleton<IPromptManager, PromptManager>();

        services.Configure(configure);

        services.AddScoped<AutoGptClient>();
        
        services.AddHttpClient().ConfigureHttpClientDefaults((builder =>
        {
            builder.AddHttpMessageHandler((_) =>
            {
                var handler = new AutoGptHttpClientHandler(options);

                return handler;
            });
        }));
        services.AddSingleton<OpenAIClientFactory>();

        return services;
    }
}