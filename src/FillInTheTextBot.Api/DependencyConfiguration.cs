using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FillInTheTextBot.Api.DependencyModules;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api
{
    internal static class DependencyConfiguration
    {
        internal static void Configure(IServiceCollection services, IConfiguration appConfiguration)
        {
            var configuration = appConfiguration.GetSection($"{nameof(AppConfiguration)}").Get<AppConfiguration>();

            services.AddSingleton(configuration);
            services.AddSingleton(configuration.HttpLog);
            services.AddSingleton(configuration.Redis);
            services.AddSingleton(configuration.DialogflowScopes);
            services.AddSingleton(configuration.Tracing);

            services.AddInternalServices();
            services.AddExternalServices();

            var names = GetAssembliesNames();
            services.AddMapping(names);

            //RegisterFromMessengersAssemblies(containerBuilder, names);
        }

        private static void RegisterFromMessengersAssemblies(ContainerBuilder containerBuilder, ICollection<string> names)
        {
            foreach (var name in names)
            {
                var assembly = Assembly.Load(name);

                containerBuilder.RegisterAssemblyModules(assembly);
            }
        }

        private static ICollection<string> GetAssembliesNames()
        {
            var callingAssemble = Assembly.GetCallingAssembly();

            var names = callingAssemble.GetCustomAttributes<ApplicationPartAttribute>()
                .Where(a => a.AssemblyName.Contains("messenger", StringComparison.InvariantCultureIgnoreCase))
                .Select(a => a.AssemblyName).ToList();

            return names;
        }
    }
}
