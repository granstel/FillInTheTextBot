﻿using Autofac;
using AutoMapper;

namespace FillInTheTextBot.Api.DependencyModules
{
    public class MappingModule : Module
    {
        private readonly string[] _names;

        public MappingModule(string[] names)
        {
            _names = names;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(b => new Mapper(new MapperConfiguration(c =>
            {
                c.AddProfiles(_names);
            }))).As<IMapper>().SingleInstance();
        }
    }
}
