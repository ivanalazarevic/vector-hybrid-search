using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace VectorHybridSearch.Shared.Module;

public interface IModule
{
    void RegisterServices(IServiceCollection services);

}