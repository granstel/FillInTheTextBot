using System.Collections.Generic;
using AutoMapper;
using FillInTheTextBot.Services.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class MappingRegistration
    {
        internal static void AddMapping(this IServiceCollection services, IEnumerable<string> names)
        {
            services.AddSingleton<IMapper>(p => new Mapper(new MapperConfiguration(c =>
            {
                c.AddMaps(names);
            })));
        }
    }
}
