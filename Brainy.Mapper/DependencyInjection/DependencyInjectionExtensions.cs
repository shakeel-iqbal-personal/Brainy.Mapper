using Brainy.SampleApp.Application.Common.Mappings;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Brainy.Mapper.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddBrainyMapper(
    this IServiceCollection services,
    Assembly assembly)
    {
        var profile = new MappingProfile(assembly);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile(profile);
        });

        services.AddSingleton<IMapper>(config.CreateMapper());

        return services;
    }
}