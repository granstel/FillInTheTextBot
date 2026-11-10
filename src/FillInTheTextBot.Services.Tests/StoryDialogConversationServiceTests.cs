using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.StoryDialog;
using Grpc.Core;
using NUnit.Framework;
using StoryDialog.Contracts.V1;
using DialogStatus = StoryDialog.Contracts.V1.Status;

namespace FillInTheTextBot.Services.Tests;

[TestFixture]
public class StoryDialogConversationServiceTests
{
    private LocalClient _client;
    private StoryDialogConversationService _service;

    [SetUp]
    public void SetUp()
    {
        _client = new LocalClient();
        _service = new StoryDialogConversationService(_client, new StoryDialogConfiguration());
    }

    [Test]
    public async Task Welcome_StartsDefaultStory_AndPreservesLegacyProgress()
    {
        var request = Request(0);
        request.NewSession = true;
        request.ConversationState = "старое состояние другого провайдера";
        var response = await _service.GetResponseAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Does.Contain("Привет!").And.Contain("Пикник на Луне"));
            Assert.That(response.Finished, Is.False);
            Assert.That(response.NextTextIndex, Is.EqualTo(17));
            Assert.That(response.RotationState, Is.EqualTo("legacy-progress"));
            Assert.That(response.ScopeKey, Is.EqualTo("test-scope"));
            Assert.That(response.ConversationProvider, Is.EqualTo("StoryDialog"));
            Assert.That(response.ConversationState, Has.Length.LessThanOrEqualTo(700));
            Assert.That(response.ConversationState, Does.Not.Contain("synthetic-session").And.Not.Contain("synthetic-user"));
            Assert.That(_client.Turns.Single().Start.StoryId, Is.EqualTo("moon-picnic"));
            Assert.That(_client.Turns.Single().ExpectedVersion, Is.Zero);
            Assert.That(_client.Turns.Single().ConversationId, Does.Match("^[a-f0-9]{64}$"));
            Assert.That(_client.Turns.Single().RequestId, Does.Match("^[a-f0-9]{64}$"));
            Assert.That(_client.GetCalls, Is.Zero);
        });
    }

    [TestCase("да")]
    [TestCase("Продолжить")]
    public async Task Answers_ContinueAndCompletion_KeepSkillOpenWithoutAnswersInState(string continuation)
    {
        var start = await Welcome();
        _client.NextReply = Reply(DialogStatus.AwaitingContinue, "Первая часть: синтетическое слово.");
        var part = await _service.GetResponseAsync(Request(1, start.ConversationState, "синтетическое слово"));
        Assert.That(part.Buttons.Select(button => button.Text), Does.Contain("Продолжить"));

        _client.NextReply = Reply(DialogStatus.Collecting, "Второй вопрос?");
        var next = await _service.GetResponseAsync(Request(2, part.ConversationState, continuation));
        Assert.That(_client.Turns.Last().Text, Is.EqualTo(continuation));

        _client.NextReply = Reply(DialogStatus.Completed, "Готовая история.");
        var completed = await _service.GetResponseAsync(Request(3, next.ConversationState, "другое слово"));
        Assert.Multiple(() =>
        {
            Assert.That(completed.Text, Does.StartWith("Готовая история."));
            Assert.That(completed.Finished, Is.False);
            Assert.That(completed.Buttons.Select(button => button.Text), Is.EqualTo(new[] { "Повторить", "Другая история" }));
            Assert.That(part.ConversationState, Does.Not.Contain("синтетическое слово"));
            Assert.That(completed.ConversationState, Does.Not.Contain("другое слово").And.Not.Contain("Готовая история"));
        });

        await _service.GetResponseAsync(Request(4, completed.ConversationState, "Повторить"));
        Assert.That(_client.Turns.Last().Text, Is.EqualTo("Повторить"));
    }

    [Test]
    public async Task HttpReplay_UsesOriginalVersionAndRequestId_WithoutConsumingAnotherSlot()
    {
        var start = await Welcome();
        var first = await _service.GetResponseAsync(Request(1, start.ConversationState, "синтетический ответ"));
        var replay = await _service.GetResponseAsync(Request(1, start.ConversationState, "синтетический ответ"));

        Assert.Multiple(() =>
        {
            Assert.That(_client.Turns[1], Is.EqualTo(_client.Turns[2]));
            Assert.That(_client.Turns[2].ExpectedVersion, Is.EqualTo(1));
            Assert.That(_client.Commits, Is.EqualTo(2));
            Assert.That(_client.GetCalls, Is.Zero);
            Assert.That(replay.Text, Is.EqualTo(first.Text));
            Assert.That(replay.ConversationState, Is.EqualTo(first.ConversationState));
        });
    }

    [Test]
    public async Task ReusedMessageIdWithDifferentText_ReportsConflictWithoutRetry()
    {
        var start = await Welcome();
        await _service.GetResponseAsync(Request(1, start.ConversationState, "первый ответ"));
        var conflict = await _service.GetResponseAsync(Request(1, start.ConversationState, "изменённый ответ"));

        Assert.Multiple(() =>
        {
            Assert.That(_client.Turns[1].RequestId, Is.EqualTo(_client.Turns[2].RequestId));
            Assert.That(conflict.Text, Does.Contain("другим содержимым"));
            Assert.That(conflict.ConversationState, Is.EqualTo(start.ConversationState));
            Assert.That(_client.Commits, Is.EqualTo(2));
            Assert.That(_client.GetCalls, Is.Zero);
            Assert.That(_client.Turns, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task ResponseLostAfterCommit_RetriesIdenticalTurnWithinSameBudget()
    {
        var start = await Welcome();
        _client.LoseNextReply = true;
        var response = await _service.GetResponseAsync(Request(1, start.ConversationState, "ответ"));

        Assert.Multiple(() =>
        {
            Assert.That(_client.Turns[1], Is.EqualTo(_client.Turns[2]));
            Assert.That(_client.TurnTokens[1], Is.EqualTo(_client.TurnTokens[2]));
            Assert.That(_client.Commits, Is.EqualTo(2));
            Assert.That(VersionOf(response), Is.EqualTo(2));
        });
    }

    [TestCase(StatusCode.Unavailable)]
    [TestCase(StatusCode.DeadlineExceeded)]
    public async Task TransientFailure_RetriesOnlyOnceAndPreservesIncomingProgress(StatusCode code)
    {
        var start = await Welcome();
        _client.Failures.Enqueue(code);
        _client.Failures.Enqueue(code);
        var response = await _service.GetResponseAsync(Request(1, start.ConversationState, "ответ"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Does.Contain("временно недоступен"));
            Assert.That(response.ConversationState, Is.EqualTo(start.ConversationState));
            Assert.That(response.RotationState, Is.EqualTo("legacy-progress"));
            Assert.That(response.NextTextIndex, Is.EqualTo(17));
            Assert.That(_client.Turns, Has.Count.EqualTo(3));
            Assert.That(_client.Commits, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task FirstRpcFailure_PreservesConversationIdAndZeroVersionForRecovery()
    {
        var request = Request(0);
        _client.ListFailure = StatusCode.Unavailable;
        var response = await _service.GetResponseAsync(request);
        var state = JsonNode.Parse(response.ConversationState);

        Assert.Multiple(() =>
        {
            Assert.That(state["c"].GetValue<string>(), Does.Match("^[a-f0-9]{64}$"));
            Assert.That(state["v"].GetValue<int>(), Is.Zero);
            Assert.That(request.ConversationState, Is.EqualTo(response.ConversationState));
            Assert.That(_client.Turns, Is.Empty);
        });
        _client.ListFailure = null;
        var resumed = await _service.GetResponseAsync(Request(1, response.ConversationState));
        Assert.That(VersionOf(resumed), Is.EqualTo(1));
    }

    [Test]
    public async Task TimeoutBudget_IncludesCatalogLookupAndPreservesInitialState()
    {
        _client.WaitForCancellation = true;
        _service = new StoryDialogConversationService(_client, new StoryDialogConfiguration { TimeoutMilliseconds = 20 });
        var response = await Welcome();

        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Does.Contain("не успел ответить"));
            Assert.That(VersionOf(response), Is.Zero);
            Assert.That(_client.Turns, Is.Empty);
        });
    }

    [Test]
    public async Task StaleVersion_ResynchronizesWithoutApplyingOldAnswerToNextQuestion()
    {
        var start = await Welcome();
        _client.NextReply = Reply(DialogStatus.Collecting, "Следующий вопрос?");
        var latest = await _service.GetResponseAsync(Request(1, start.ConversationState, "ответ"));
        var stale = await _service.GetResponseAsync(Request(2, start.ConversationState, "устаревший ответ"));

        Assert.Multiple(() =>
        {
            Assert.That(stale.Text, Does.Contain("История уже продвинулась").And.Contain("Следующий вопрос?"));
            Assert.That(stale.ConversationState, Is.EqualTo(latest.ConversationState));
            Assert.That(_client.GetCalls, Is.EqualTo(1));
            Assert.That(_client.Commits, Is.EqualTo(2));
            Assert.That(_client.Turns, Has.Count.EqualTo(3));
        });
    }

    [TestCase("не json")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("{\"c\":\"foreign-conversation\",\"v\":1,\"s\":1,\"i\":\"moon-picnic\",\"o\":[]}")]
    public async Task MalformedState_DoesNotStartOrCallAnotherConversation(string state)
    {
        var response = await _service.GetResponseAsync(Request(1, state));
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Does.Contain("Начните новую сессию"));
            Assert.That(response.ConversationState, Is.Null);
            Assert.That(_client.Turns, Is.Empty);
            Assert.That(_client.ListCalls, Is.Zero);
            Assert.That(_client.GetCalls, Is.Zero);
        });
    }

    [Test]
    public async Task OversizedState_IsRejectedBeforeRpc()
    {
        var response = await _service.GetResponseAsync(Request(1, new string('x', 701)));
        Assert.That(response.Text, Does.Contain("состояние истории"));
        Assert.That(response.ConversationState, Is.Null);
        Assert.That(_client.ListCalls, Is.Zero);
        Assert.That(_client.Turns, Is.Empty);
    }

    [TestCase("missing-version")]
    [TestCase("string-version")]
    [TestCase("negative-version")]
    [TestCase("overflow-version")]
    [TestCase("unknown-status")]
    [TestCase("inconsistent-status")]
    [TestCase("unknown-property")]
    [TestCase("duplicate-story")]
    [TestCase("null-order")]
    [TestCase("empty-order")]
    public async Task InvalidStateShape_IsRejectedWithoutRpc(string mutation)
    {
        var welcome = await Welcome();
        var state = JsonNode.Parse(welcome.ConversationState).AsObject();
        switch (mutation)
        {
            case "missing-version": state.Remove("v"); break;
            case "string-version": state["v"] = "1"; break;
            case "negative-version": state["v"] = -1; break;
            case "overflow-version": state["v"] = ulong.MaxValue; break;
            case "unknown-status": state["s"] = 99; break;
            case "inconsistent-status": state["s"] = 0; break;
            case "unknown-property": state["answers"] = "synthetic data"; break;
            case "duplicate-story": state["o"] = new JsonArray("moon-picnic", "moon-picnic"); break;
            case "null-order": state["o"] = null; break;
            case "empty-order": state["o"] = new JsonArray(); break;
        }

        var serialized = state.ToJsonString();
        var request = Request(1, serialized);
        var response = await _service.GetResponseAsync(request);
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Does.Contain("Начните новую сессию"));
            Assert.That(response.ConversationState, Is.Null);
            Assert.That(request.ConversationState, Is.Null);
            Assert.That(_client.Turns, Has.Count.EqualTo(1));
            Assert.That(_client.ListCalls, Is.EqualTo(1));
            Assert.That(_client.GetCalls, Is.Zero);
        });
    }

    [TestCase(null, "synthetic-session")]
    [TestCase(-1, "synthetic-session")]
    [TestCase(0, null)]
    public async Task MissingIdentity_DoesNotCallRpc(long? messageId, string sessionId)
    {
        var request = Request(messageId, new string('x', 701));
        request.SessionId = sessionId;
        var response = await _service.GetResponseAsync(request);
        Assert.That(response.Text, Does.Contain("Не удалось определить сообщение"));
        Assert.That(response.ConversationState, Is.Null);
        Assert.That(_client.ListCalls, Is.Zero);
        Assert.That(_client.Turns, Is.Empty);
    }

    [TestCase("/help")]
    [TestCase("покажи помощь")]
    [TestCase("/repeat")]
    [TestCase("Повтори вопрос")]
    [TestCase("/cancel")]
    [TestCase("отмени историю")]
    [TestCase("/restart")]
    [TestCase("начать заново")]
    [TestCase("/continue")]
    [TestCase("следующая часть")]
    public async Task Commands_AreForwardedAsUnmodifiedText(string text)
    {
        var start = await Welcome();
        await _service.GetResponseAsync(Request(1, start.ConversationState, text));
        Assert.That(_client.Turns.Last().InputCase, Is.EqualTo(TurnRequest.InputOneofCase.Text));
        Assert.That(_client.Turns.Last().Text, Is.EqualTo(text));
    }

    [TestCase("стоп")]
    [TestCase("да")]
    [TestCase("продолжить")]
    [TestCase("повторить")]
    public async Task SingleWordWhileCollecting_RemainsAnAnswer(string text)
    {
        var start = await Welcome();
        await _service.GetResponseAsync(Request(1, start.ConversationState, text));
        Assert.That(_client.Turns.Last().InputCase, Is.EqualTo(TurnRequest.InputOneofCase.Text));
        Assert.That(_client.Turns.Last().Text, Is.EqualTo(text));
    }

    [TestCase("Другая история")]
    [TestCase("/story robot-courier")]
    [TestCase("Повтори вопрос, пожалуйста")]
    [TestCase("Стоп,  не отменяй историю!")]
    public async Task ActiveStory_TextIsForwardedWithoutCatalogLookup(string text)
    {
        var start = await Welcome();
        await _service.GetResponseAsync(Request(1, start.ConversationState, text));
        Assert.That(_client.Turns.Last().InputCase, Is.EqualTo(TurnRequest.InputOneofCase.Text));
        Assert.That(_client.Turns.Last().Text, Is.EqualTo(text));
        Assert.That(_client.ListCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task ExplicitStory_CanStartSelectedStory()
    {
        var response = await _service.GetResponseAsync(Request(0, text: "/story robot-courier"));
        Assert.That(_client.Turns.Single().Start.StoryId, Is.EqualTo("robot-courier"));
        Assert.That(response.Text, Does.Contain("Робот-курьер"));
    }

    [Test]
    public async Task TooLargeCatalog_IsExplicitlyRejectedWithoutTruncation()
    {
        for (var i = 0; i < 3; i++)
            _client.Catalog.Stories.Add(new StorySummary { Id = "extra-" + i, Title = "Дополнительная история" });
        var response = await Welcome();
        Assert.That(response.Text, Does.Contain("Каталог историй превышает ограничения"));
        Assert.That(_client.Turns, Is.Empty);
    }

    [Test]
    public async Task TooLongReply_IsExplicitlyUnsupportedAndRetainsCommittedState()
    {
        _client.NextReply = Reply(DialogStatus.Completed, new string('я', 1025));
        var response = await Welcome();
        Assert.That(response.Text, Does.Contain("превышает ограничение"));
        Assert.That(response.Text, Has.Length.LessThanOrEqualTo(1024));
        Assert.That(VersionOf(response), Is.EqualTo(1));
        Assert.That(response.Finished, Is.False);
    }

    [Test]
    public async Task ConversationIdentity_DependsOnSkillAndSessionButNotUser()
    {
        var first = Request(0);
        await _service.GetResponseAsync(first);
        var changedUser = Request(0);
        changedUser.UserHash = "another-synthetic-user";
        await _service.GetResponseAsync(changedUser);
        var changedSkill = Request(0);
        changedSkill.ChatHash = "another-skill";
        await _service.GetResponseAsync(changedSkill);

        Assert.That(_client.Turns[0].ConversationId, Is.EqualTo(_client.Turns[1].ConversationId));
        Assert.That(_client.Turns[0].RequestId, Is.EqualTo(_client.Turns[1].RequestId));
        Assert.That(_client.Turns[0].ConversationId, Is.Not.EqualTo(_client.Turns[2].ConversationId));
    }

    private static ulong VersionOf(Response response)
    {
        var state = JsonNode.Parse(response.ConversationState);
        var version = state["v"].GetValue<ulong>();
        return version;
    }

    private Task<Response> Welcome()
    {
        var request = Request(0);
        var response = _service.GetResponseAsync(request);
        return response;
    }

    private static Request Request(long? messageId, string state = null, string text = "")
    {
        var request = new Request
        {
            Source = Source.Yandex,
            ChatHash = "synthetic-skill",
            UserHash = "synthetic-user",
            SessionId = "synthetic-session",
            MessageId = messageId,
            Text = text,
            ConversationState = state,
            ConversationProvider = state == null ? null : "StoryDialog",
            NextTextIndex = 17,
            RotationState = "legacy-progress",
            ScopeKey = "test-scope"
        };
        return request;
    }

    private static Reply Reply(DialogStatus status, string text)
    {
        var reply = new Reply { Status = status, Text = text };
        return reply;
    }

    private sealed class LocalClient : IStoryDialogClient
    {
        private readonly Dictionary<string, (TurnRequest Request, Reply Reply)> _receipts = new();
        private readonly Dictionary<string, Reply> _conversations = new();
        public ListStoriesResponse Catalog { get; } = new()
        {
            Stories =
            {
                new StorySummary { Id = "moon-picnic", Title = "Пикник на Луне" },
                new StorySummary { Id = "robot-courier", Title = "Робот-курьер" }
            }
        };
        public List<TurnRequest> Turns { get; } = new();
        public List<CancellationToken> TurnTokens { get; } = new();
        public Queue<StatusCode> Failures { get; } = new();
        public StatusCode? ListFailure { get; set; }
        public bool LoseNextReply { get; set; }
        public bool WaitForCancellation { get; set; }
        public Reply NextReply { get; set; }
        public int Commits { get; private set; }
        public int ListCalls { get; private set; }
        public int GetCalls { get; private set; }

        public async Task<ListStoriesResponse> ListStoriesAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            if (ListFailure is { } code)
            {
                throw Error(code);
            }
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            var catalog = Catalog.Clone();
            return catalog;
        }

        public Task<Reply> TurnAsync(TurnRequest request, CancellationToken cancellationToken)
        {
            Turns.Add(request.Clone());
            TurnTokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Failures.Count > 0)
            {
                var failure = Failures.Dequeue();
                throw Error(failure);
            }

            if (_receipts.TryGetValue(request.RequestId, out var saved))
            {
                if (!saved.Request.Equals(request))
                {
                    throw Error(StatusCode.AlreadyExists);
                }
                var replay = saved.Reply.Clone();
                var result = Task.FromResult(replay);
                return result;
            }
            _conversations.TryGetValue(request.ConversationId, out var current);
            if (request.ExpectedVersion != (current?.Version ?? 0))
            {
                throw Error(StatusCode.Aborted);
            }

            var reply = NextReply?.Clone() ?? Reply(DialogStatus.Collecting, "Назовите слово.");
            NextReply = null;
            reply.ConversationId = request.ConversationId;
            reply.Version = request.ExpectedVersion + 1;
            reply.StoryId = request.Start?.StoryId ?? current.StoryId;
            _conversations[request.ConversationId] = reply;
            var receipt = (request.Clone(), reply.Clone());
            _receipts.Add(request.RequestId, receipt);
            Commits++;
            if (LoseNextReply)
            {
                LoseNextReply = false;
                throw Error(StatusCode.Unavailable);
            }
            var response = reply.Clone();
            var task = Task.FromResult(response);
            return task;
        }

        public Task<Reply> GetConversationAsync(string conversationId, CancellationToken cancellationToken)
        {
            GetCalls++;
            var response = _conversations[conversationId].Clone();
            var task = Task.FromResult(response);
            return task;
        }

        private static RpcException Error(StatusCode code)
        {
            var status = new Grpc.Core.Status(code, "synthetic failure");
            var exception = new RpcException(status);
            return exception;
        }
    }
}
