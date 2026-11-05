using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using Google.Api.Gax.Grpc;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Tools.ScopeSelector;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    [TestFixture]
    [NonParallelizable]
    public class DialogflowTracingTests
    {
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
                    var error = new InvalidOperationException("Synthetic Dialogflow failure");
                    response.SetException(error);
                    var actual = Assert.ThrowsAsync<InvalidOperationException>(async () => await operation);
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
                catch (InvalidOperationException) when (failRequest)
                {
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
