using System.Collections.Concurrent;
using Dialogflow.Emulator.Models;

namespace Dialogflow.Emulator.Services;

/// <summary>
/// Состояние слот-филлинга по сессиям. Обязательно singleton: gRPC-сервисы
/// создаются на каждый запрос, поэтому держать состояние в самом сервисе нельзя —
/// оно не переживёт следующий вызов.
/// </summary>
public sealed class SlotFillingStore
{
    /// <summary>
    /// Верхняя граница числа незавершённых сессий — защита от неограниченного роста
    /// памяти под нагрузкой (брошенные на середине истории сессии иначе копятся).
    /// </summary>
    private const int MaxSessions = 20000;

    private readonly ConcurrentDictionary<string, SlotFillingState> _sessions = new();

    public bool TryGet(string session, out SlotFillingState state)
    {
        return _sessions.TryGetValue(session, out state!);
    }

    public void Remove(string session)
    {
        _sessions.TryRemove(session, out _);
    }

    /// <summary>Начинает слот-филлинг для сессии. false — если достигнут лимит сессий.</summary>
    public bool TryStart(string session, SlotFillingState state)
    {
        if (_sessions.Count >= MaxSessions)
        {
            return false;
        }

        _sessions[session] = state;

        return true;
    }
}

public sealed class SlotFillingState
{
    public SlotFillingState(AgentIntent intent, IReadOnlyList<PendingSlot> pending)
    {
        Intent = intent;
        Pending = pending;
    }

    public AgentIntent Intent { get; }

    public IReadOnlyList<PendingSlot> Pending { get; }

    public Dictionary<string, string> Filled { get; } = new();

    /// <summary>Индекс следующего незаполненного слота.</summary>
    public int Index { get; set; }
}

public readonly record struct PendingSlot(string Name, string Prompt);
