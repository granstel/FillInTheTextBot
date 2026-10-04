using System;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using GranSteL.Helpers.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Периодически забирает список историй и правила ротации. Последняя валидная версия
    /// кэшируется в Redis, чтобы бот стартовал с ней при недоступном GitHub.
    /// </summary>
    public sealed class RotationRefreshService(
        IRotationSource source,
        IRedisCacheService cache,
        RotationStore store,
        RotationConfiguration configuration,
        ILogger<RotationRefreshService> log) : BackgroundService
    {
        private const string CacheKey = "rotation:sources";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LoadFromCache();

            var interval = TimeSpan.FromMinutes(configuration.RefreshIntervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RefreshAsync(stoppingToken);

                await Task.Delay(interval, stoppingToken);
            }
        }

        public void LoadFromCache()
        {
            try
            {
                if (cache.TryGet(CacheKey, out RotationSources sources) && sources is not null)
                {
                    TryApply(sources, "Redis");
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while loading rotation from cache");
            }
        }

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var current = store.Snapshot?.Sources;

            try
            {
                var stories = await source.GetStoryFilesAsync(current?.StoriesETag, cancellationToken);
                var rules = await source.GetRulesAsync(current?.RulesETag, cancellationToken);

                if (!stories.IsModified && !rules.IsModified)
                {
                    store.ReportCheck(DateTimeOffset.UtcNow, null);

                    return;
                }

                var sources = new RotationSources
                {
                    StoriesETag = stories.IsModified ? stories.ETag : current?.StoriesETag,
                    StoryFiles = stories.IsModified ? stories.Value : current?.StoryFiles,
                    RulesETag = rules.IsModified ? rules.ETag : current?.RulesETag,
                    RulesJson = rules.IsModified ? rules.Value : current?.RulesJson
                };

                if (TryApply(sources, "GitHub"))
                {
                    await cache.AddAsync(CacheKey, sources);
                }
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                log.LogError(e, "Error while refreshing rotation");
                MetricsCollector.Increment("rotation_error", "refresh");

                store.ReportCheck(DateTimeOffset.UtcNow, e.Message);
            }
        }

        private bool TryApply(RotationSources sources, string origin)
        {
            var now = DateTimeOffset.UtcNow;

            if (!RotationSnapshot.TryCreate(sources, now, out var snapshot, out var errors))
            {
                var error = string.Join("; ", errors);

                log.LogError("Rotation from {Origin} is invalid: {Errors}", origin, error);
                MetricsCollector.Increment("rotation_error", "invalid");

                store.ReportCheck(now, error);

                return false;
            }

            store.Apply(snapshot);
            store.ReportCheck(now, null);

            log.LogInformation("Rotation loaded from {Origin}: {StoriesCount} stories, rules ETag {RulesETag}",
                origin, snapshot.Catalog.Numbers.Count, sources.RulesETag);

            return true;
        }
    }
}
