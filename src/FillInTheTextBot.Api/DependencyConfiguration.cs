using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FillInTheTextBot.Api.DependencyModules;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api
{
    internal static class DependencyConfiguration
    {
        internal static IContainer Configure(IServiceCollection services, IConfiguration appConfiguration)
        {
            var containerBuilder = new ContainerBuilder();

            containerBuilder.Populate(services);

            var configuration = appConfiguration.GetSection($"{nameof(AppConfiguration)}").Get<AppConfiguration>();

            services.AddSingleton(configuration);
            services.AddSingleton(configuration.HttpLog);
            services.AddSingleton(configuration.Redis);
            services.AddSingleton(configuration.DialogflowScopes);
            services.AddSingleton(configuration.Tracing);

            services.AddInternalServices();
            services.AddExternalServices();

            var names = GetAssembliesNames();
            containerBuilder.RegisterModule(new MappingModule(names));
            RegisterFromMessengersAssemblies(containerBuilder, names);

            return containerBuilder.Build();
        }

        private static void RegisterFromMessengersAssemblies(ContainerBuilder containerBuilder, string[] names)
        {
            foreach (var name in names)
            {
                var assembly = Assembly.Load(name);

                containerBuilder.RegisterAssemblyModules(assembly);
            }
        }

        private static string[] GetAssembliesNames()
        {
            var result = new List<string>();

            var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "FillInTheTextBot*.dll");

            foreach (var file in files)
            {
                var info = new FileInfo(file);

                var name = info.Name.Replace(info.Extension, string.Empty);

                if (name.Equals(AppDomain.CurrentDomain.FriendlyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                result.Add(name);
            }

            return result.ToArray();
        }
    }
}
