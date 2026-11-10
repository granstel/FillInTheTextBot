using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;
using Grpc.Core;
using StoryDialog.Contracts.V1;
using DialogStatus = StoryDialog.Contracts.V1.Status;

namespace FillInTheTextBot.Services.StoryDialog;

public sealed class StoryDialogConversationService : IConversationService
{
    private const string Provider = nameof(ConversationProvider.StoryDialog);
    private readonly IStoryDialogClient _client;
    private readonly StoryDialogConfiguration _configuration;

    public StoryDialogConversationService(IStoryDialogClient client, StoryDialogConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public async Task<Response> GetResponseAsync(Request request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || !request.MessageId.HasValue || request.MessageId < 0)
        {
            request.ConversationState = null;
            var invalidMessage = ResponseFor(request, "Не удалось определить сообщение. Начните новую сессию навыка.");
            return invalidMessage;
        }

        var conversationId = Hash($"{request.Source}\n{request.ChatHash}\n{request.SessionId}");
        StoryDialogState state;
        if (request.NewSession == true || string.IsNullOrEmpty(request.ConversationState))
        {
            state = new StoryDialogState { ConversationId = conversationId, StoryId = _configuration.DefaultStoryId };
        }
        else if (!StoryDialogState.TryRead(request.ConversationState, conversationId, out state))
        {
            request.ConversationState = null;
            var invalidState = ResponseFor(request, "Не удалось прочитать состояние истории. Начните новую сессию навыка.");
            return invalidState;
        }

        var startId = state.Version == 0 ? state.StoryId : null;
        if (state.Version == 0 && request.Text?.StartsWith("/story ", StringComparison.Ordinal) == true)
        {
            startId = request.Text.Substring(7).Trim();
        }

        if (startId != null && !StoryDialogState.IsStoryId(startId))
        {
            var invalidStory = ResponseFor(request, "Укажите идентификатор истории после /story.");
            return invalidStory;
        }

        // Сохраняем версию до RPC: неизвестный исход не разрешает переносить ответ на следующий слот.
        if (state.Version == 0)
        {
            state.StoryId = startId;
        }
        request.ConversationProvider = Provider;
        request.ConversationState = state.Serialize();

        using var budget = new CancellationTokenSource(_configuration.TimeoutMilliseconds);
        try
        {
            string title = null;
            if (startId != null)
            {
                var catalog = await _client.ListStoriesAsync(budget.Token);
                if (catalog.Stories.Count > StoryDialogState.MaxStories ||
                    catalog.Stories.Any(story => !StoryDialogState.IsStoryId(story.Id)))
                {
                    var unsupportedCatalog = ResponseFor(request, "Каталог историй превышает ограничения этого навыка.");
                    return unsupportedCatalog;
                }
                var selected = catalog.Stories.FirstOrDefault(story => story.Id == startId);
                if (selected == null)
                {
                    var missingStory = ResponseFor(request, "Эта история недоступна. Выберите другую историю.");
                    return missingStory;
                }

                title = selected.Title;
                if (state.StoryOrder.Length == 0)
                {
                    var catalogIds = catalog.Stories.Select(story => story.Id).OrderBy(id => id, StringComparer.Ordinal);
                    state.StoryOrder = new[] { startId }.Concat(catalogIds).Distinct(StringComparer.Ordinal).ToArray();
                    request.ConversationState = state.Serialize();
                }
            }

            var messageId = request.MessageId.Value.ToString(CultureInfo.InvariantCulture);
            var requestId = Hash(conversationId + "\n" + messageId);
            var turn = new TurnRequest
            {
                ConversationId = conversationId,
                RequestId = requestId,
                ExpectedVersion = state.Version
            };
            if (startId != null)
            {
                turn.Start = new StartStory { StoryId = startId };
            }
            else
            {
                turn.Text = request.Text ?? "";
            }

            Reply reply;
            var resynchronized = false;
            try
            {
                reply = await TurnWithRetryAsync(turn, budget.Token);
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.Aborted)
            {
                reply = await _client.GetConversationAsync(conversationId, budget.Token);
                resynchronized = true;
            }

            state.Version = reply.Version;
            state.Status = reply.Status;
            state.StoryId = reply.StoryId;
            var response = ResponseFor(request, reply.Text);
            response.ConversationState = state.Serialize();
            if (resynchronized)
            {
                response.Text = "История уже продвинулась. Вот её текущее состояние. " + response.Text;
            }
            else if (startId != null)
            {
                response.Text = (turn.ExpectedVersion == 0 ? "Привет! Давайте сочиним историю. " : "") +
                    $"История «{title}». " + response.Text;
            }

            if (reply.Status == DialogStatus.AwaitingContinue)
            {
                response.Text += " Продолжим историю?";
                response.Buttons = Buttons("Продолжить", "Повтори результат");
            }
            else if (reply.Status is DialogStatus.Completed or DialogStatus.Cancelled)
            {
                response.Text += reply.Status == DialogStatus.Completed
                    ? " Можно повторить результат или выбрать другую историю."
                    : " Можно начать заново или выбрать другую историю.";
                response.Buttons = reply.Status == DialogStatus.Completed
                    ? Buttons("Повторить", "Другая история")
                    : Buttons("Начать заново", "Другая история");
            }
            if (response.Text.Length > 1024)
            {
                response.Text = "Ответ истории превышает ограничение навыка. Можно выбрать другую историю.";
            }
            return response;
        }
        catch (RpcException exception)
        {
            var errorText = exception.StatusCode switch
            {
                StatusCode.AlreadyExists => "Это сообщение уже обработано с другим содержимым. Отправьте новое сообщение.",
                StatusCode.InvalidArgument => "Не удалось принять ответ. Проверьте его или скажите «покажи помощь».",
                StatusCode.FailedPrecondition => "Историю нельзя продолжить. Начните новую сессию навыка.",
                StatusCode.NotFound => "История недоступна. Начните новую сессию навыка.",
                _ => "Сервис историй временно недоступен. Попробуйте ещё раз."
            };
            var response = ResponseFor(request, errorText);
            return response;
        }
        catch (OperationCanceledException)
        {
            var response = ResponseFor(request, "Сервис историй не успел ответить. Попробуйте ещё раз.");
            return response;
        }
    }

    private async Task<Reply> TurnWithRetryAsync(TurnRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await _client.TurnAsync(request, cancellationToken);
            return reply;
        }
        catch (RpcException exception) when (!cancellationToken.IsCancellationRequested &&
            exception.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            var reply = await _client.TurnAsync(request, cancellationToken);
            return reply;
        }
    }

    private static Button[] Buttons(params string[] texts)
    {
        var buttons = texts.Select(text => new Button { Text = text, IsQuickReply = true }).ToArray();
        return buttons;
    }

    private static string Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        var result = Convert.ToHexString(hash).ToLowerInvariant();
        return result;
    }

    private static Response ResponseFor(Request request, string text)
    {
        var response = new Response
        {
            Text = text,
            Finished = false,
            ConversationProvider = Provider,
            ConversationState = request.ConversationState,
            NextTextIndex = request.NextTextIndex,
            RotationState = request.RotationState,
            ScopeKey = request.ScopeKey
        };
        return response;
    }
}
