using System.Collections.Generic;
using Newtonsoft.Json;

namespace FillInTheTextBot.Services.Rotation
{
    public sealed record RotationSnapshot(StoryCatalog Catalog, RotationRules Rules, RotationSources Sources)
    {
        public static bool TryCreate(RotationSources sources, out RotationSnapshot snapshot, out IReadOnlyList<string> errors)
        {
            snapshot = null;

            var catalog = StoryCatalog.FromFileNames(sources.StoryFiles ?? []);

            if (catalog.Numbers.Count == 0)
            {
                errors = ["Не найдено ни одной истории"];

                return false;
            }

            RotationRules rules;

            try
            {
                rules = sources.RulesJson is null ? RotationRules.Empty : RotationRules.Parse(sources.RulesJson);
            }
            catch (JsonException e)
            {
                errors = [e.Message];

                return false;
            }

            errors = rules.Validate(catalog);

            if (errors.Count > 0)
            {
                return false;
            }

            snapshot = new RotationSnapshot(catalog, rules, sources);

            return true;
        }
    }
}
