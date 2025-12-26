// CLif001A.cs - TypeRegistrar for DI integration with Spectre.Console.Cli

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Alaris.Host.Application.Cli.Infrastructure;

/// <summary>
/// Integrates Microsoft.Extensions.DependencyInjection with Spectre.Console.Cli.
/// Component ID: CLif001A
/// </summary>
public sealed class CLif001A : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public CLif001A(IServiceCollection services)
    {
        _services = services;
    }

    public ITypeResolver Build()
    {
        return new CLif002A(_services.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }
}
