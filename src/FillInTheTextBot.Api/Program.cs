using System;
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NLog.Web;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace FillInTheTextBot.Api
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var builder = WebHost.CreateDefaultBuilder(args);

            var hostingStartupAssemblies = builder.GetSetting(WebHostDefaults.HostingStartupAssembliesKey) ?? string.Empty;
            var hostingStartupAssembliesList = hostingStartupAssemblies.Split(';');

            var names = GetAssembliesNames();
            var fullList = hostingStartupAssembliesList.Concat(names).Distinct().ToList();
            var concatenatedNames = string.Join(';', fullList);

            var host = builder
                .UseSetting(WebHostDefaults.HostingStartupAssembliesKey, concatenatedNames)
                .UseStartup<Startup>()
                .UseNLog()
                .Build();

            return host;
        }

        private static ICollection<string> GetAssembliesNames()
        {
            var callingAssemble = Assembly.GetCallingAssembly();

            var names = callingAssemble.GetCustomAttributes<ApplicationPartAttribute>()
                .Where(a => a.AssemblyName.Contains("FillInTheTextBot", StringComparison.InvariantCultureIgnoreCase))
                .Select(a => a.AssemblyName).ToList();

            return names;
        }
    }
}
