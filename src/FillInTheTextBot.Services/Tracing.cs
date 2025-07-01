using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FillInTheTextBot.Services;

public static class Tracing
{
    // Создаем один экземпляр ActivitySource для всех вызовов
    private static readonly ActivitySource ActivitySource = new("FillInTheTextBot");

    public static IDisposable Trace(Action<Activity> activityAction = null, string operationName = null,
        [CallerMemberName] string caller = null)
    {
        var activity = ActivitySource.StartActivity(operationName ?? caller);

        activityAction?.Invoke(activity);

        return new ActivityScope(activity);
    }

    private class ActivityScope(Activity activity) : IDisposable
    {
        public void Dispose()
        {
            activity?.Dispose();
        }
    }
}