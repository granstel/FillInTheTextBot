using AutoMapper;
using FillInTheTextBot.Services.Mapping;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace FillInTheTextBot.Api.DependencyModules
{
    internal static class MappingRegistration
    {
        internal static void AddMapping(this IServiceCollection services, IEnumerable<string> names)
        {
            services.AddSingleton<IMapper>(p => new Mapper(new MapperConfiguration(c =>
            {
                c.AddMaps(names);

                c.AddProfile<InternalProfile>();
                c.AddProfile<DialogflowProfile>();
            })));
        }
    }
}
