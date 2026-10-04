using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Выбор историй: первый круг — в заданном порядке, дальше — случайная из незакрытых в круге.
    /// Закрыта на круг дособранная или брошенная история.
    /// </summary>
    public sealed class StoryRotation(RotationStore store, RotationConfiguration configuration, Random random, TimeProvider time)
    {
        public const string TextsOverEvent = "texts-over";

        private const string SaveToRepeatAction = "saveToRepeat";

        private readonly ConcurrentDictionary<int, DateTimeOffset> _unavailableUntil = new();

        /// <summary>null — ротация ещё не загружена.</summary>
        public RotationState GetState(string serialized, int legacyIndex)
        {
            var snapshot = store.Snapshot;

            if (snapshot is null)
            {
                return null;
            }

            var state = RotationState.Parse(serialized);

            if (state is null)
            {
                var firstCycle = snapshot.Rules.GetFirstCycle(snapshot.Catalog);

                state = RotationState.FromLegacyIndex(legacyIndex, firstCycle);
            }

            return state;
        }

        /// <summary>
        /// Входное событие следующей истории или <see cref="TextsOverEvent"/>, если круг закончился:
        /// тогда состояние уже переведено на следующий круг.
        /// </summary>
        public string SelectEvent(RotationState state)
        {
            var snapshot = store.Snapshot;
            var today = StoryComputations.ToMoscowDate(time.GetUtcNow());

            CloseCurrent(state);

            var next = state.Next;
            state.Next = null;

            if (next is not null && snapshot.Catalog.Contains(next.Value) && IsAvailable(next.Value))
            {
                return snapshot.Catalog.GetEntryEvent(next.Value);
            }

            var campaignStory = SelectFromCampaigns(snapshot, state, today);

            if (campaignStory is not null)
            {
                return snapshot.Catalog.GetEntryEvent(campaignStory.Value);
            }

            var fresh = GetUnits(snapshot, today)
                .Where(number => !state.IsDone(number) && IsAvailable(number))
                .ToList();

            if (fresh.Count == 0)
            {
                state.StartNextCycle();

                return TextsOverEvent;
            }

            var selected = state.Cycle == 0 ? fresh[0] : SelectRandom(fresh, state.Last);

            var eventName = snapshot.Catalog.GetEntryEvent(selected);

            return eventName;
        }

        /// <summary>
        /// Отмечает выданную историю. false — Dialogflow ответил не интентом этой истории.
        /// </summary>
        public bool TryMarkIssued(RotationState state, string eventName, string intentName)
        {
            if (!StoryCatalog.TryParseEvent(eventName, out var number, out var part))
            {
                return true;
            }

            if (!StoryCatalog.TryParseIntentName(intentName, out var intentNumber, out _) || intentNumber != number)
            {
                return false;
            }

            if (part <= 1 && state.Current != number)
            {
                CloseCurrent(state);

                state.Last = number;
            }

            state.Current = number;

            return true;
        }

        /// <summary>
        /// Дособранная история закрывается на круг, а следующее звено её цепочки встаёт первым.
        /// </summary>
        public void TrackCompletion(RotationState state, Dialog dialog)
        {
            var snapshot = store.Snapshot;

            var isSaved = dialog?.ParametersIncomplete == false
                          && string.Equals(dialog.Action, SaveToRepeatAction, StringComparison.InvariantCultureIgnoreCase);

            if (!isSaved
                || !StoryCatalog.TryParseIntentName(dialog.IntentName, out var number, out var part)
                || !snapshot.Catalog.IsLastPart(number, part))
            {
                return;
            }

            state.MarkDone(number);

            if (state.Current == number)
            {
                state.Current = null;
            }

            var next = GetNextLink(snapshot, number);

            if (next is not null && !state.IsDone(next.Value))
            {
                state.Next = next;
            }
        }

        public void MarkUnavailable(string eventName)
        {
            if (StoryCatalog.TryParseEvent(eventName, out var number, out _))
            {
                _unavailableUntil[number] = time.GetUtcNow().AddMinutes(configuration.UnavailableMinutes);
            }
        }

        private bool IsAvailable(int number)
        {
            var isAvailable = !_unavailableUntil.TryGetValue(number, out var until) || until <= time.GetUtcNow();

            return isAvailable;
        }

        private static void CloseCurrent(RotationState state)
        {
            if (state.Current is null)
            {
                return;
            }

            state.MarkDone(state.Current.Value);
            state.Current = null;
        }

        /// <summary>Первая непройденная история активной кампании; кампании — в порядке файла.</summary>
        private int? SelectFromCampaigns(RotationSnapshot snapshot, RotationState state, DateTime today)
        {
            foreach (var campaign in snapshot.Rules.Campaigns.Where(campaign => campaign.IsActive(today)))
            {
                var fresh = RotationRules.ToNumbers(campaign.Texts, snapshot.Catalog)
                    .Where(number => !state.IsDone(number) && IsAvailable(number))
                    .ToList();

                if (fresh.Count > 0)
                {
                    return campaign.Shuffle ? SelectRandom(fresh, state.Last) : fresh[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Истории в порядке первого круга. Звенья цепочек, кроме первого, выдаются только следом,
        /// истории кампаний с hideOutside — только в даты кампании.
        /// </summary>
        private static IEnumerable<int> GetUnits(RotationSnapshot snapshot, DateTime today)
        {
            var followers = snapshot.Rules.Chains.SelectMany(chain => chain.Skip(1));

            var seasonal = snapshot.Rules.Campaigns
                .Where(campaign => campaign.HideOutside && !campaign.IsActive(today))
                .SelectMany(campaign => campaign.Texts);

            var excluded = RotationRules.ToNumbers(followers.Concat(seasonal), snapshot.Catalog).ToHashSet();

            var units = snapshot.Rules.GetFirstCycle(snapshot.Catalog).Where(number => !excluded.Contains(number));

            return units;
        }

        private static int? GetNextLink(RotationSnapshot snapshot, int number)
        {
            var entryEvent = snapshot.Catalog.GetEntryEvent(number);

            foreach (var chain in snapshot.Rules.Chains)
            {
                var index = Array.IndexOf(chain, entryEvent);

                if (index >= 0 && index < chain.Length - 1
                    && snapshot.Catalog.TryGetStoryByEntryEvent(chain[index + 1], out var next))
                {
                    return next;
                }
            }

            return null;
        }

        private int SelectRandom(List<int> fresh, int? last)
        {
            var candidates = fresh.Count > 1 ? fresh.Where(number => number != last).ToList() : fresh;

            var selected = candidates[random.Next(candidates.Count)];

            return selected;
        }
    }
}
