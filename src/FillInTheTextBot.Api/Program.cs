using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FillInTheTextBot.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Web;

var existingHostingStartupAssemblies = Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES") ?? string.Empty;
var hostingStartupAssembliesList = existingHostingStartupAssemblies.Split(';', StringSplitOptions.RemoveEmptyEntries);
var fullList = hostingStartupAssembliesList.Concat(GetAssembliesNames()).Distinct().ToList();
Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", string.Join(';', fullList));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLog();

var startup = new Startup(builder.Configuration, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
startup.ConfigureServices(builder.Services);

var app = builder.Build();

FillInTheTextBot.Services.InternalLoggerFactory.Factory = app.Services.GetRequiredService<ILoggerFactory>();

var appConfiguration = app.Services.GetRequiredService<FillInTheTextBot.Services.Configuration.AppConfiguration>();
startup.Configure(app, appConfiguration);

app.Run();

static ICollection<string> GetAssembliesNames()
{
    var callingAssembly = Assembly.GetCallingAssembly();

    return callingAssembly.GetCustomAttributes<ApplicationPartAttribute>()
        .Where(a => a.AssemblyName.Contains("FillInTheTextBot", StringComparison.InvariantCultureIgnoreCase))
        .Select(a => a.AssemblyName)
        .ToList();
}

public partial class Program;
