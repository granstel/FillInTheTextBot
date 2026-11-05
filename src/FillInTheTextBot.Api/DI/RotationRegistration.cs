using System;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class RotationRegistration
    {
        private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(10);

        internal static void AddRotation(this IServiceCollection services, RotationConfiguration configuration)
        {
            services.AddSingleton<RotationStore>();
            services.AddSingleton<Random>(Random.Shared);
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddSingleton<StoryRotation>();

            if (configuration?.Enabled == false)
            {
                return;
            }

            services.AddHttpClient<IRotationSource, GitHubRotationSource>(client => client.Timeout = SourceTimeout);
            services.AddHostedService<RotationRefreshService>();
        }
    }
}
