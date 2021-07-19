using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NLog.Web;
using System.Reflection;
using System;

namespace FillInTheTextBot.Api
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES: " +
                Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"));
            Console.WriteLine("DOTNET_ADDITIONAL_DEPS: " +
                Environment.GetEnvironmentVariable("DOTNET_ADDITIONAL_DEPS"));
            Console.WriteLine("DOTNET_STARTUP_HOOKS: " +
                Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS"));

            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var builder = WebHost
            .CreateDefaultBuilder(args);

            var k = builder.GetSetting(WebHostDefaults.HostingStartupAssembliesKey);
            var k1 = builder.GetSetting(WebHostDefaults.PreventHostingStartupKey);

            var builded = builder
            .UseSetting(WebHostDefaults.ApplicationKey, Assembly.GetEntryAssembly().GetName().Name)
            .UseSetting(
                    WebHostDefaults.PreventHostingStartupKey, "false")
                .UseStartup<Startup>()
                .UseNLog()
                .Build();

            return builded;
        }
            
    }
}
