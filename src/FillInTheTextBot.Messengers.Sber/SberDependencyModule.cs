﻿using Autofac;

namespace FillInTheTextBot.Messengers.Sber
{
    /// <summary>
    /// Probably, registered at DependencyConfiguration of main project
    /// </summary>
    public class SberDependencyModule : MessengerModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(context =>
            {
                var config = GetConfigurationFromFile<SberConfiguration>("appsettings.Sber.json");

                return config;
            }).SingleInstance().AsSelf();

            builder.RegisterType<SberController>().AsSelf();

            builder.RegisterType<SberService>().As<ISberService>();
            builder.RegisterType<SberProfile>().AsSelf();
        }
    }
}
