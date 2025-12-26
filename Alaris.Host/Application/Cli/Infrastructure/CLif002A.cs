// CLif002A.cs - TypeResolver for DI container

using Spectre.Console.Cli;

namespace Alaris.Host.Application.Cli.Infrastructure;

/// <summary>
/// Resolves types from the DI container for Spectre.Console.Cli commands.
/// Component ID: CLif002A
/// </summary>
public sealed class CLif002A : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public CLif002A(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        return type == null ? null : _provider.GetService(type);
    }
}
