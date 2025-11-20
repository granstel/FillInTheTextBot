using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FillInTheTextBot.Api;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using NLog.Web;

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

host.Run();

static ICollection<string> GetAssembliesNames()
{
    var callingAssemble = Assembly.GetCallingAssembly();

    var names = callingAssemble.GetCustomAttributes<ApplicationPartAttribute>()
        .Where(a => a.AssemblyName.Contains("FillInTheTextBot", StringComparison.InvariantCultureIgnoreCase))
        .Select(a => a.AssemblyName).ToList();

    return names;
}