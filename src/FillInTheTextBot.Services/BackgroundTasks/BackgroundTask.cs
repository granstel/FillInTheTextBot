using System;
using System.Threading.Tasks;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    public sealed class BackgroundTask
    {
        public BackgroundTask(string name, Func<Task> work)
        {
            Name = name;
            Work = work;
        }

        public string Name { get; }

        public Func<Task> Work { get; }
    }
}
