using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using Google.Api.Gax.Grpc;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Tools.ScopeSelector;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    [TestFixture]
    [NonParallelizable]
    public class DialogflowTracingTests
    {
        private const string ErrorDetail = "synthetic-rpc-failure-detail";

        [TestCase(false, TestName = "SetContextAsync_PendingRequest_KeepsSpansOpenUntilSuccess")]
        [TestCase(true, TestName = "SetContextAsync_PendingRequest_KeepsSpansOpenUntilFailure")]
        public async Task SetContextAsync_PendingRequest_KeepsSpansOpenUntilCompletion(bool failRequest)
        {
            using var parent = new Activity("Dialogflow tracing test").SetIdFormat(ActivityIdFormat.W3C).Start();
            var started = new ConcurrentQueue<Activity>();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "FillInTheTextBot",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity =>
                {
                    if (IsContextSpan(activity, parent))
                    {
                        started.Enqueue(activity);
                    }
                },
                ActivityStopped = activity =>
                {
                    if (IsContextSpan(activity, parent))
                    {
                        stopped.Enqueue(activity);
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            var response = new TaskCompletionSource<Context>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = new Mock<ContextsClient>(MockBehavior.Strict);
            client.Setup(c => c.CreateContextAsync(
                    It.Is<SessionName>(s => s.ProjectId == "tracing-test-project" && s.SessionId == "test-session"),
                    It.IsAny<Context>(), It.IsAny<CallSettings>()))
                .Returns(response.Task);

            var scope = new ScopeContext("test-scope", false);
            scope.TryAddParameter(nameof(DialogflowConfiguration.ProjectId), "tracing-test-project");
            var selector = new ScopesSelector<ContextsClient>(new[] { scope }, _ => client.Object);
            var target = new DialogflowService(NullLogger<DialogflowService>.Instance, null, selector);


            var operation = target.SetContextAsync("test-session", "test-scope", "test-context");


            try
            {
                var outer = started.Single(a => a.OperationName == "SetContextAsync");
                var inner = started.Single(a => a.OperationName == "SetContextInternalAsync");
                Assert.Multiple(() =>
                {
                    Assert.That(operation.IsCompleted, Is.False);
                    Assert.That(stopped, Is.Empty, "Both spans must remain open while CreateContextAsync is pending.");
                    Assert.That(outer.Duration, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(inner.Duration, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(outer.Parent, Is.SameAs(parent));
                    Assert.That(inner.Parent, Is.SameAs(outer));
                    Assert.That(inner.TraceId, Is.EqualTo(outer.TraceId));
                });

                if (failRequest)
                {
                    var error = new RpcException(new Status(StatusCode.Unavailable, ErrorDetail));
                    response.SetException(error);
                    var actual = await Assert.ThrowsAsync<RpcException>(async () => await operation);
                    Assert.That(actual, Is.SameAs(error));
                }
                else
                {
                    response.SetResult(new Context());
                    await operation;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(stopped.ToArray(), Is.EqualTo(new[] { inner, outer }),
                        "Both spans must stop exactly once, with the child stopping first.");
                    Assert.That(inner.ParentSpanId, Is.EqualTo(outer.SpanId));
                    Assert.That(outer.ParentSpanId, Is.EqualTo(parent.SpanId));
                    Assert.That(Activity.Current, Is.SameAs(parent));
                });
                AssertRpcTags(inner, "google.cloud.dialogflow.v2.Contexts", "CreateContext", failRequest);
                AssertSafeTags(started, "test-session", ErrorDetail);
                client.VerifyAll();
            }
            finally
            {
                // Завершаем запрос и при падении проверки, чтобы не оставлять открытые spans.
                response.TrySetResult(new Context());
                try
                {
                    await operation;
                }
                catch (RpcException) when (failRequest)
                {
                }
            }
        }

        [TestCase(false, TestName = "GetResponseAsync_PendingDetectIntent_KeepsRpcSpanOpenUntilSuccess")]
        [TestCase(true, TestName = "GetResponseAsync_PendingDetectIntent_KeepsRpcSpanOpenUntilFailure")]
        public async Task GetResponseAsync_PendingDetectIntent_KeepsRpcSpanOpenUntilCompletion(bool failRequest)
        {
            const string requestText = "synthetic-query-text";
            const string sessionId = "synthetic-detect-session";
            using var parent = new Activity("DetectIntent tracing test").SetIdFormat(ActivityIdFormat.W3C).Start();
            var started = new ConcurrentQueue<Activity>();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "FillInTheTextBot",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity =>
                {
                    if (activity.TraceId == parent.TraceId)
                    {
                        started.Enqueue(activity);
                    }
                },
                ActivityStopped = activity =>
                {
                    if (activity.TraceId == parent.TraceId)
                    {
                        stopped.Enqueue(activity);
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            var response = new TaskCompletionSource<DetectIntentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = new Mock<SessionsClient>(MockBehavior.Strict);
            client.Setup(c => c.DetectIntentAsync(
                    It.Is<DetectIntentRequest>(r => r.SessionAsSessionName.ProjectId == "tracing-test-project" &&
                                                   r.SessionAsSessionName.SessionId == sessionId &&
                                                   r.QueryInput.Text.Text == requestText),
                    It.IsAny<CallSettings>()))
                .Returns(response.Task);
            var scope = new ScopeContext("test-scope", false);
            scope.TryAddParameter(nameof(DialogflowConfiguration.ProjectId), "tracing-test-project");
            scope.TryAddParameter(nameof(DialogflowConfiguration.LanguageCode), "en");
            var selector = new ScopesSelector<SessionsClient>(new[] { scope }, _ => client.Object);
            var target = new DialogflowService(NullLogger<DialogflowService>.Instance, selector, null);


            var operation = target.GetResponseAsync(requestText, sessionId, "test-scope");


            try
            {
                var outer = started.Single(a => a.OperationName == "GetResponseAsync");
                var inner = started.Single(a => a.OperationName == "Get response from Dialogflow");
                var rpc = started.Single(a => a.OperationName == "Dialogflow DetectIntent");
                var pendingSpans = new[] { rpc, inner, outer };
                Assert.Multiple(() =>
                {
                    Assert.That(operation.IsCompleted, Is.False);
                    Assert.That(stopped.Where(pendingSpans.Contains), Is.Empty);
                    Assert.That(pendingSpans.Select(a => a.Duration), Is.All.EqualTo(TimeSpan.Zero));
                    Assert.That(outer.Parent, Is.SameAs(parent));
                    Assert.That(inner.Parent, Is.SameAs(outer));
                    Assert.That(rpc.Parent, Is.SameAs(inner));
                    Assert.That(rpc.GetTagItem("rpc.grpc.status_code"), Is.Null);
                });

                if (failRequest)
                {
                    var error = new RpcException(new Status(StatusCode.Unavailable, ErrorDetail));
                    response.SetException(error);
                    var actual = await Assert.ThrowsAsync<RpcException>(async () => await operation);
                    Assert.That(actual, Is.SameAs(error));
                }
                else
                {
                    response.SetResult(new DetectIntentResponse { QueryResult = new QueryResult() });
                    var result = await operation;
                    Assert.That(result.ScopeKey, Is.EqualTo("test-scope"));
                }

                Assert.Multiple(() =>
                {
                    Assert.That(stopped.Where(pendingSpans.Contains), Is.EqualTo(pendingSpans));
                    Assert.That(rpc.Duration, Is.GreaterThan(TimeSpan.Zero));
                    Assert.That(rpc.StartTimeUtc, Is.GreaterThanOrEqualTo(inner.StartTimeUtc));
                    Assert.That(rpc.StartTimeUtc + rpc.Duration,
                        Is.LessThanOrEqualTo(inner.StartTimeUtc + inner.Duration));
                    Assert.That(rpc.ParentSpanId, Is.EqualTo(inner.SpanId));
                    Assert.That(rpc.TraceId, Is.EqualTo(outer.TraceId));
                    Assert.That(Activity.Current, Is.SameAs(parent));
                });
                AssertRpcTags(rpc, "google.cloud.dialogflow.v2.Sessions", "DetectIntent", failRequest);
                AssertSafeTags(started, requestText, sessionId, ErrorDetail);
                client.VerifyAll();
            }
            finally
            {
                response.TrySetResult(new DetectIntentResponse { QueryResult = new QueryResult() });
                try
                {
                    await operation;
                }
                catch (RpcException) when (failRequest)
                {
                }
            }
        }

        private static void AssertRpcTags(Activity activity, string service, string method, bool failed)
        {
            var safeKeys = new[] { "ScopeId", "rpc.system", "rpc.service", "rpc.method", "rpc.grpc.status_code" };
            if (failed)
            {
                safeKeys = safeKeys.Append("error.type").ToArray();
            }

            Assert.Multiple(() =>
            {
                Assert.That(activity.TagObjects.Select(t => t.Key), Is.EquivalentTo(safeKeys));
                Assert.That(activity.GetTagItem("ScopeId"), Is.EqualTo("test-scope"));
                Assert.That(activity.GetTagItem("rpc.system"), Is.EqualTo("grpc"));
                Assert.That(activity.GetTagItem("rpc.service"), Is.EqualTo(service));
                Assert.That(activity.GetTagItem("rpc.method"), Is.EqualTo(method));
                Assert.That(activity.GetTagItem("rpc.grpc.status_code"),
                    Is.EqualTo((int)(failed ? StatusCode.Unavailable : StatusCode.OK)));
                Assert.That(activity.Status, Is.EqualTo(failed ? ActivityStatusCode.Error : ActivityStatusCode.Unset));
                Assert.That(activity.GetTagItem("error.type"), Is.EqualTo(failed ? typeof(RpcException).FullName : null));
                Assert.That(activity.StatusDescription, Is.Null.Or.Empty);
            });
        }

        private static void AssertSafeTags(ConcurrentQueue<Activity> activities, params string[] excludedValues)
        {
            foreach (var activity in activities)
            {
                foreach (var tag in activity.TagObjects)
                {
                    Assert.That(tag.Key, Does.Not.Contain("text").IgnoreCase.And.Not.Contain("session").IgnoreCase);
                    foreach (var excluded in excludedValues)
                    {
                        Assert.That(tag.Value?.ToString() ?? string.Empty, Does.Not.Contain(excluded),
                            $"{activity.OperationName}: {tag.Key} must not expose request or error details.");
                    }
                }
            }
        }

        private static bool IsContextSpan(Activity activity, Activity parent)
        {
            var isContextSpan = activity.TraceId == parent.TraceId &&
                   (activity.OperationName == "SetContextAsync" || activity.OperationName == "SetContextInternalAsync");

            return isContextSpan;
        }
    }
}
