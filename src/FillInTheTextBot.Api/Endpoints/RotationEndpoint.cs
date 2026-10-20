using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Rotation;
using GranSteL.Helpers.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FillInTheTextBot.Api.Endpoints
{
    /// <summary>Загруженная версия ротации — для проверки после мерджа правил.</summary>
    public static class RotationEndpoint
    {
        public const string Path = "/rotation";

        internal static void MapRotation(this IEndpointRouteBuilder endpoints, int servicePort)
        {
            // Порт соединения, а не заголовок Host: заголовок задаёт клиент, а прокси передаёт его как есть
            endpoints.MapGet(Path, Get).AddEndpointFilter((context, next) =>
                context.HttpContext.Connection.LocalPort == servicePort
                    ? next(context)
                    : ValueTask.FromResult<object>(Results.NotFound()));
        }

        private static IResult Get(RotationStore store, IRedisCacheService cache)
        {
            var snapshot = store.Snapshot;
            var catalog = snapshot?.Catalog ?? StoryCatalog.Empty;

            var stories = catalog.Numbers.Select(catalog.GetEntryEvent).ToArray();
            var firstCycle = snapshot?.Rules.GetFirstCycle(catalog).Select(catalog.GetEntryEvent).ToArray() ?? [];

            var today = StoryComputations.MoscowToday();

            var campaigns = snapshot?.Rules.Campaigns
                .Select(campaign => new { campaign, active = campaign.IsActive(today) })
                .ToArray();

            cache.TryGet("Texts", out string[] legacyTexts);
            legacyTexts ??= [];

            var result = new
            {
                snapshot?.LoadedAt,
                snapshot?.Sources.StoriesETag,
                snapshot?.Sources.RulesETag,
                store.LastCheck,
                stories,
                snapshot?.Rules.Hidden,
                snapshot?.Rules.Chains,
                campaigns,
                firstCycle,
                legacyTexts = new
                {
                    missing = legacyTexts.Except(stories),
                    extra = stories.Except(legacyTexts),
                    sameFirstCycle = legacyTexts.SequenceEqual(firstCycle)
                }
            };

            return Results.Json(result);
        }
    }
}
