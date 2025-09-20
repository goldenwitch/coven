// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Activation;

internal interface IBlockActivator
{
    object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta);
    string? DisplayName { get; }
}

internal sealed class ConstantInstanceActivator : IBlockActivator
{
    private readonly object _instance;
    internal ConstantInstanceActivator(object instance) { _instance = instance; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta) => _instance;
    public string? DisplayName => _instance?.GetType().Name;
}

internal sealed class DiTypeActivator : IBlockActivator
{
    private readonly Type _blockType;
    internal DiTypeActivator(Type blockType) { _blockType = blockType; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta)
    {
        if (cache.TryGetValue(meta.RegistryIndex, out object? cached))
        {
            return cached;
        }


        if (sp is null)
        {

            throw new InvalidOperationException($"Coven DI: No IServiceProvider available to resolve {_blockType.Name} for entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}.");
        }


        object inst = sp.GetService(_blockType) ?? throw new InvalidOperationException($"Coven DI: Unable to resolve {_blockType.Name} for entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}. Ensure it is registered or provide a factory.");
        cache[meta.RegistryIndex] = inst;
        return inst;
    }
    public string? DisplayName => _blockType.Name;
}

internal sealed class FactoryActivator : IBlockActivator
{
    private readonly Func<IServiceProvider, object> _factory;
    internal FactoryActivator(Func<IServiceProvider, object> factory) { _factory = factory; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta)
    {
        if (cache.TryGetValue(meta.RegistryIndex, out object? cached))
        {
            return cached;
        }


        if (sp is null)
        {
            throw new InvalidOperationException($"Coven DI: No IServiceProvider available for factory activator at entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}.");
        }


        object inst = _factory(sp) ?? throw new InvalidOperationException($"Coven DI: Factory produced null block instance for entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}.");
        cache[meta.RegistryIndex] = inst;
        return inst;
    }
    public string? DisplayName => null; // unknown until factory executes
}