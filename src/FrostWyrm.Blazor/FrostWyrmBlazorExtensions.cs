using System.Reflection;
using FrostWyrm.Archive;
using FrostWyrm.Archive.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace FrostWyrm.Blazor;

public static class FrostWyrmBlazorExtensions
{
    public static IServiceCollection AddFrostWyrm(this IServiceCollection services, params Assembly[] assemblies)
    {
        Archive.FrostWyrm.Initialize(assemblies.Append(typeof(FrostWyrmBlazorExtensions).Assembly).ToArray());

        var dependencyManager = Archive.FrostWyrm.Resolve<IDependencyManager>();
        foreach (var (type, instance) in dependencyManager.EnumerateDependencies())
        {
            if (!type.IsClass || type.IsAbstract)
                continue;

            services.AddSingleton(type, instance);
            foreach (var i in type.GetInterfaces())
            {
                if (i == typeof(IManager))
                    continue;

                services.AddSingleton(i, instance);
            }
        }

        return services;
    }
}