using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DependencyModules
{
    internal static class MappingRegistration
    {
        internal static void AddMapping(this IServiceCollection services, string[] names)
        {
            services.AddSingleton<IMapper>(p => new Mapper(new MapperConfiguration(c =>
            {
                c.AddProfiles(names);
            })));
        }
    }
}
