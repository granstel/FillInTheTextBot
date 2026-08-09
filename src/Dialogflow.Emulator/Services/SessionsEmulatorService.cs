using Dialogflow.Emulator.Models;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Dialogflow.Emulator.Services;

/// <summary>
/// Стаб DetectIntent с эмуляцией слот-филлинга. Настоящего разбора языка нет:
/// интент ищется по имени события или точному совпадению обучающей фразы, иначе
/// Default Fallback Intent. Для интентов с обязательными параметрами эмулятор
/// спрашивает их по очереди (промпты), копит ответы в состоянии сессии и, когда
/// все заполнены, подставляет их в шаблон ответа ($param). Этого достаточно, чтобы
/// прогонять основной путь — сочинение истории — на нагрузке.
///
/// Состояние сессий живёт в singleton <see cref="SlotFillingStore"/>: сам gRPC-сервис
/// создаётся на каждый запрос и своё поле не сохранил бы.
/// </summary>
public sealed class SessionsEmulatorService : Sessions.SessionsBase
{
    private readonly IAgentStorage _agentStorage;
    private readonly SlotFillingStore _slotFilling;
    private readonly ILogger<SessionsEmulatorService> _log;

    public SessionsEmulatorService(IAgentStorage agentStorage, SlotFillingStore slotFilling, ILogger<SessionsEmulatorService> log)
    {
        _agentStorage = agentStorage;
        _slotFilling = slotFilling;
        _log = log;
    }

    public override Task<DetectIntentResponse> DetectIntent(DetectIntentRequest request, ServerCallContext context)
    {
        var session = request.Session ?? string.Empty;
        var eventName = request.QueryInput?.Event?.Name;
        var text = request.QueryInput?.Text?.Text;

        // Идёт слот-филлинг и пришёл текст — это ответ на промпт
        if (eventName is null && text is not null && _slotFilling.TryGet(session, out var active))
        {
            return Task.FromResult(FillSlot(session, active, text));
        }

        // Иначе — обычный матч интента (событие/фраза). Новый интент прерывает
        // незавершённый слот-филлинг этой сессии.
        _slotFilling.Remove(session);

        string queryText;
        AgentIntent? intent;

        if (eventName is not null)
        {
            queryText = $"event:{eventName}";
            intent = _agentStorage.FindByEvent(eventName);
        }
        else
        {
            queryText = text ?? string.Empty;
            intent = _agentStorage.FindByText(queryText);
        }

        intent ??= _agentStorage.GetFallback();

        _log.LogInformation("DetectIntent '{QueryText}' matched intent '{IntentName}'", queryText, intent?.Name);

        var pending = GetRequiredSlots(intent);

        if (pending.Count > 0)
        {
            // Запускаем слот-филлинг: спрашиваем первый обязательный параметр
            var state = new SlotFillingState(intent!, pending);
            _slotFilling.TryStart(session, state);

            return Task.FromResult(PromptResponse(state, queryText));
        }

        // Обычный ответ + статические параметры (напр. textKey для GetText)
        var parameters = StaticParameters(intent);
        var fulfillment = AgentStorage.GetText(intent) ?? string.Empty;

        return Task.FromResult(BuildResponse(intent, queryText, session, parameters, fulfillment,
            allRequiredParamsPresent: true, includeAction: true));
    }

    private DetectIntentResponse FillSlot(string session, SlotFillingState state, string text)
    {
        var current = state.Pending[state.Index];
        state.Filled[current.Name] = text;
        state.Index++;

        if (state.Index < state.Pending.Count)
        {
            // Ещё есть незаполненные слоты — следующий промпт
            return PromptResponse(state, text);
        }

        // Все слоты заполнены — подставляем значения в шаблон
        _slotFilling.Remove(session);

        var template = AgentStorage.GetText(state.Intent) ?? string.Empty;
        var composed = Substitute(template, state.Filled);

        var parameters = new Dictionary<string, string>(state.Filled);
        foreach (var pair in StaticParameters(state.Intent))
        {
            parameters[pair.Key] = pair.Value;
        }

        return BuildResponse(state.Intent, text, session, parameters, composed,
            allRequiredParamsPresent: true, includeAction: true);
    }

    /// <summary>
    /// Ответ-промпт: спрашиваем текущий обязательный параметр. Действие интента и
    /// признак завершённости не выставляем — Dialogflow отдаёт их только когда все
    /// слоты заполнены.
    /// </summary>
    private DetectIntentResponse PromptResponse(SlotFillingState state, string queryText)
    {
        var prompt = state.Pending[state.Index].Prompt;
        var session = string.Empty;

        return BuildResponse(state.Intent, queryText, session, state.Filled, prompt,
            allRequiredParamsPresent: false, includeAction: false);
    }

    private static List<PendingSlot> GetRequiredSlots(AgentIntent? intent)
    {
        var slots = new List<PendingSlot>();

        foreach (var parameter in AgentStorage.GetParameters(intent))
        {
            if (!parameter.Required || string.IsNullOrEmpty(parameter.Name))
            {
                continue;
            }

            var prompt = parameter.Prompts?.FirstOrDefault(p => !string.IsNullOrEmpty(p.Value))?.Value
                         ?? $"Назови значение для «{parameter.Name}»";

            slots.Add(new PendingSlot(parameter.Name, prompt));
        }

        return slots;
    }

    private static Dictionary<string, string> StaticParameters(AgentIntent? intent)
    {
        var result = new Dictionary<string, string>();

        foreach (var parameter in AgentStorage.GetParameters(intent))
        {
            if (!parameter.Required && !string.IsNullOrEmpty(parameter.Name) && !string.IsNullOrEmpty(parameter.Value))
            {
                result[parameter.Name] = parameter.Value;
            }
        }

        return result;
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, string> values)
    {
        // Длинные имена первыми, чтобы $number не «съел» начало $numberX
        foreach (var pair in values.OrderByDescending(v => v.Key.Length))
        {
            template = template.Replace($"${pair.Key}", pair.Value);
        }

        return template;
    }

    private static DetectIntentResponse BuildResponse(AgentIntent? intent, string queryText, string session,
        IReadOnlyDictionary<string, string> parameters, string fulfillmentText, bool allRequiredParamsPresent, bool includeAction)
    {
        var queryResult = new QueryResult
        {
            QueryText = queryText,
            FulfillmentText = fulfillmentText,
            Action = includeAction ? AgentStorage.GetAction(intent) ?? string.Empty : string.Empty,
            AllRequiredParamsPresent = allRequiredParamsPresent,
            IntentDetectionConfidence = 1f,
            LanguageCode = "ru",
            Intent = new Intent
            {
                DisplayName = intent?.Name ?? string.Empty,
                Name = $"{session}/intents/{intent?.Id ?? string.Empty}"
            }
        };

        if (parameters.Count > 0)
        {
            var parametersStruct = new Struct();

            foreach (var pair in parameters)
            {
                parametersStruct.Fields[pair.Key] = Value.ForString(pair.Value);
            }

            queryResult.Parameters = parametersStruct;
        }

        queryResult.FulfillmentMessages.Add(new Intent.Types.Message
        {
            Text = new Intent.Types.Message.Types.Text { Text_ = { fulfillmentText } }
        });

        return new DetectIntentResponse
        {
            ResponseId = Guid.NewGuid().ToString("N"),
            QueryResult = queryResult
        };
    }
}
