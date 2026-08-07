using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace FillInTheTextBot.Api
{
    // Не static: тип используется как параметр WebApplicationFactory в интеграционных тестах
    public class Program
    {
        private Program()
        {
        }

        public static void Main(string[] args)
        {
            var app = BuildApplication(args);

            app.Run();
        }

        public static WebApplication BuildApplication(string[] args)
        {
            var builder = WebApplication.CreateBuilder(WithHostingStartupAssemblies(args));

            builder.Host.UseNLog();

            var startup = new Startup(builder.Configuration);
            startup.ConfigureServices(builder.Services);

            var app = builder.Build();

            // Статические мапперы берут логгер отсюда, поэтому фабрику нужно выставить
            // до того, как приложение начнёт принимать запросы
            InternalLoggerFactory.Factory = app.Services.GetRequiredService<ILoggerFactory>();

            var configuration = app.Services.GetRequiredService<AppConfiguration>();
            startup.Configure(app, configuration);

            return app;
        }

        /// <summary>
        /// Каждый мессенджер регистрирует себя через IHostingStartup, поэтому список сборок
        /// нужно передать до создания билдера — в момент его создания хост уже выполняет
        /// hosting startup'ы, и более поздний UseSetting на них не влияет.
        /// </summary>
        private static string[] WithHostingStartupAssemblies(string[] args)
        {
            var assembliesNames = GetAssembliesNames();

            if (assembliesNames.Count == 0)
            {
                return args;
            }

            var names = string.Join(';', assembliesNames);

            var extendedArgs = args
                .Concat(new[] { $"--{WebHostDefaults.HostingStartupAssembliesKey}={names}" })
                .ToArray();

            return extendedArgs;
        }

        private static ICollection<string> GetAssembliesNames()
        {
            var assembly = typeof(Program).Assembly;

            var names = assembly.GetCustomAttributes<ApplicationPartAttribute>()
                .Where(a => a.AssemblyName.Contains("FillInTheTextBot", StringComparison.InvariantCultureIgnoreCase))
                .Select(a => a.AssemblyName)
                .ToList();

            return names;
        }
    }
}
