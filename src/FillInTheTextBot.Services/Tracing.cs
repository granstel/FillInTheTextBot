using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FillInTheTextBot.Services;

public static class Tracing
{
    // Создаем один экземпляр ActivitySource для всех вызовов
    private static readonly ActivitySource _activitySource = new("FillInTheTextBot");

    public static IDisposable Trace(Action<Activity> activityAction = null, string operationName = null,
        [CallerMemberName] string caller = null)
    {
        var activity = _activitySource.StartActivity(operationName ?? caller);

        activityAction?.Invoke(activity);

        return new ActivityScope(activity);
    }

    private class ActivityScope : IDisposable
    {
        private readonly Activity _activity;

        public ActivityScope(Activity activity)
        {
            _activity = activity;
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }
    }
}