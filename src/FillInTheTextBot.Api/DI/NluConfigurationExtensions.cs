using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI;

public static class NluConfigurationExtensions
{
    /// <summary>
    /// Настраивает NLU провайдера (Dialogflow/Rasa) через конфигурацию
    /// </summary>
    public static IServiceCollection ConfigureNluProvider(this IServiceCollection services, IConfiguration configuration)
    {
        // Конфигурация NLU уже регистрируется в ConfigurationRegistration.cs
        // Этот метод сохранен для совместимости
        return services;
    }

    /// <summary>
    /// Настраивает использование Rasa как NLU провайдера
    /// </summary>
    public static IServiceCollection UseRasaAsNluProvider(this IServiceCollection services, string baseUrl = "http://localhost:5005")
    {
        // Перезаписываем существующую регистрацию
        services.AddSingleton(new NluConfiguration { Provider = NluProvider.Rasa });

        return services;
    }

    /// <summary>
    /// Настраивает использование Dialogflow как NLU провайдера (по умолчанию)
    /// </summary>
    public static IServiceCollection UseDialogflowAsNluProvider(this IServiceCollection services)
    {
        // Перезаписываем существующую регистрацию
        services.AddSingleton(new NluConfiguration { Provider = NluProvider.Dialogflow });

        return services;
    }
}