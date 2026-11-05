using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    public sealed class BackgroundTask
    {
        public BackgroundTask(string name, Func<Task> work)
        {
            Name = name;
            Work = work;
            ParentContext = Activity.Current?.Context ?? default;
        }

        public string Name { get; }

        public Func<Task> Work { get; }

        public ActivityContext ParentContext { get; }
    }
}
