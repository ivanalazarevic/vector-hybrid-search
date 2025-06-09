using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace VectorHybridSearch.Shared.Module;

public static class ModuleLoader
{
    public static void RegisterModules(IServiceCollection services)
    {
        const string prefix = "VectorHybridSearch";
        LoadAssemblies(prefix);
        var moduleTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith(prefix) == true)
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        moduleTypes.ForEach(moduleType => {
            var module = (IModule)Activator.CreateInstance(moduleType)!;
            module.RegisterServices(services);
        });
    }

    private static void LoadAssemblies(string prefix)
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var loadedNames = loadedAssemblies.Select(a => a.GetName().Name).ToHashSet();

        var binPath = AppContext.BaseDirectory;
        var candidateDlls = Directory.GetFiles(binPath, $"{prefix}*.dll");

        foreach (var dll in candidateDlls) {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (!loadedNames.Contains(name)) {
                Assembly.Load(name);
            }
        }
    }
}