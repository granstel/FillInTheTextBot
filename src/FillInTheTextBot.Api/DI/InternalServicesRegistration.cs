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

            services.AddSingleton<BackgroundTaskQueue>();
            services.AddSingleton<IBackgroundTaskQueue>(provider => provider.GetRequiredService<BackgroundTaskQueue>());
            services.AddHostedService<BackgroundTaskProcessor>();
        }
    }
}
