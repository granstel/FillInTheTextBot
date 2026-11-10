using FillInTheTextBot.Services;
using FillInTheTextBot.Services.BackgroundTasks;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.StoryDialog;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class InternalServicesRegistration
    {
        internal static void AddInternalServices(this IServiceCollection services, ConversationConfiguration configuration)
        {
            if (configuration.Provider == ConversationProvider.StoryDialog)
            {
                services.AddTransient<StoryDialogConversationService>();
                services.AddSingleton<IStoryDialogClient, GrpcStoryDialogClient>();
            }
            else
            {
                services.AddTransient<ConversationService>();
                services.AddScoped<IDialogflowService, DialogflowService>();
            }
            services.AddScoped<IConversationService>(provider =>
            {
                var router = new ConversationProviderService(configuration, () =>
                    configuration.Provider == ConversationProvider.StoryDialog
                        ? provider.GetRequiredService<StoryDialogConversationService>()
                        : provider.GetRequiredService<ConversationService>());
                return router;
            });

            // Продюсеры (IBackgroundTaskQueue) и обработчик (IBackgroundTaskReader) должны
            // работать с одним и тем же каналом, поэтому оба интерфейса форвардятся на
            // единственный экземпляр BackgroundTaskQueue.
            services.AddSingleton<BackgroundTaskQueue>();
            services.AddSingleton<IBackgroundTaskQueue>(provider => provider.GetRequiredService<BackgroundTaskQueue>());
            services.AddSingleton<IBackgroundTaskReader>(provider => provider.GetRequiredService<BackgroundTaskQueue>());
            services.AddHostedService<BackgroundTaskProcessor>();
        }
    }
}
