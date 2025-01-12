using AutoGpt.Options;

using Microsoft.Extensions.DependencyInjection;

namespace AutoGpt;

public static class ServiceExtensions
{
    public static IServiceCollection AddAutoGpt(this IServiceCollection services,
        Action<AutoGptOptions> configure,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        var options = new AutoGptOptions();

        services.Configure(configure);

        configure(options);

        options.Validate();

        services.AddSingleton<IPromptManager, PromptManager>();

        switch (serviceLifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<AutoGptClient>();
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped<AutoGptClient>();
                break;
            case ServiceLifetime.Transient:
                services.AddTransient<AutoGptClient>();
                break;
        }


        services.AddHttpClient();

        return services;
    }
}