using System;
using System.Runtime.CompilerServices;
using OpenTracing;
using OpenTracing.Util;

namespace FillInTheTextBot.Services
{
    public static class Tracing
    {
        public static IScope Trace(Action<ISpanBuilder> spanBuilderAction = null, string operationName = null, [CallerMemberName] string caller = null)
        {
            var spanBuilder = GlobalTracer.Instance.BuildSpan(operationName ?? caller);

            spanBuilderAction?.Invoke(spanBuilder);

            var scope = spanBuilder.StartActive(true);

            return scope;
        }
    }
}