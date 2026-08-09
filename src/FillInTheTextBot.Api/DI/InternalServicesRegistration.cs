using FillInTheTextBot.Services;
using FillInTheTextBot.Services.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class InternalServicesRegistration
    {
        internal static void AddInternalServices(this IServiceCollection services)
        {
            services.AddTransient<IConversationService, ConversationService>();
            services.AddScoped<IDialogflowService, DialogflowService>();

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
