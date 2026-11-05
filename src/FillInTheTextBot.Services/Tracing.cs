using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FillInTheTextBot.Services
{
    public static class Tracing
    {
        /// <summary>
        /// Имя источника активностей. Его нужно передать в AddSource при настройке
        /// OpenTelemetry, иначе активности будут создаваться, но никуда не уедут.
        /// </summary>
        public const string ActivitySourceName = Telemetry.ScopeName;

        private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        /// <summary>
        /// Открывает активность. Если слушателей нет (например, в юнит-тестах),
        /// StartActivity возвращает null — using с null работает штатно, а действие
        /// над активностью не вызывается.
        /// </summary>
        public static Activity Trace(Action<Activity> activityAction = null, string operationName = null,
            ActivityContext parentContext = default,
            [CallerMemberName] string caller = null)
        {
            var activity = ActivitySource.StartActivity(operationName ?? caller, ActivityKind.Internal, parentContext);

            if (activity is not null)
            {
                activityAction?.Invoke(activity);
            }

            return activity;
        }
    }
}
