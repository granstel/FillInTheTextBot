using System;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;

namespace FillInTheTextBot.Api.DI
{
    /// <summary>
    /// URL вебхука содержит токен площадки. Очищаем завершённый входящий span,
    /// сохраняя шаблон маршрута, длительность и безопасные HTTP-атрибуты.
    /// </summary>
    internal sealed class IncomingRequestTelemetryProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            if (activity.Kind != ActivityKind.Server
                || activity.Source.Name != "Microsoft.AspNetCore")
            {
                return;
            }

            foreach (var tag in activity.TagObjects.ToArray())
            {
                if (IsSensitive(tag.Key))
                {
                    activity.SetTag(tag.Key, null);
                }
            }

            var method = activity.GetTagItem("http.request.method")
                         ?? activity.GetTagItem("http.method") ?? "HTTP";
            var route = activity.GetTagItem("http.route") as string;
            activity.DisplayName = route is null ? method.ToString() : $"{method} {route}";
            // Текст ошибки может содержать исходный URL; код статуса сохраняется.
            activity.SetStatus(activity.Status);
        }

        private static bool IsSensitive(string key)
        {
            var isSensitive = key is "url.path" or "url.query" or "url.full" or "url.fragment"
                or "http.url" or "http.target" or "http.request.uri"
                or "http.client_ip" or "http.user_agent" or "user_agent.original"
                or "client.address" or "client.port" or "network.peer.address" or "network.peer.port"
                or "net.peer.ip" or "net.peer.port" or "net.sock.peer.addr" or "net.sock.peer.port"
                || key.StartsWith("http.request.header.", StringComparison.Ordinal)
                || key.StartsWith("http.response.header.", StringComparison.Ordinal);

            return isSensitive;
        }
    }
}
