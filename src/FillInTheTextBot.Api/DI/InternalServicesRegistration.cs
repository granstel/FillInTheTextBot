using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Factories;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace FillInTheTextBot.Api.DI;

internal static class InternalServicesRegistration
{
    internal static void AddInternalServices(this IServiceCollection services)
    {
        services.AddTransient<IConversationService, ConversationService>();
        
        // Регистрируем оба сервиса
        services.AddScoped<DialogflowService>();
        services.AddScoped<RasaService>();
        
        // Регистрируем HttpClientFactory для Rasa
        services.AddHttpClient();
        
        // Регистрируем фабрику и прокси
        services.AddScoped<INluServiceFactory, NluServiceFactory>();
        services.AddScoped<IDialogflowService, NluServiceProxy>();
        
        // Конфигурация по умолчанию
        services.Configure<NluConfiguration>(config =>
        {
            config.Provider = NluProvider.Dialogflow;
        });
    }
}