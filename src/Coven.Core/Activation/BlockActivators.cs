// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

internal interface IBlockActivator
{
    object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta);
    string? DisplayName { get; }
}

internal sealed class ConstantInstanceActivator : IBlockActivator
{
    private readonly object instance;
    internal ConstantInstanceActivator(object instance) { this.instance = instance; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta) => instance;
    public string? DisplayName => instance?.GetType().Name;
}

internal sealed class DiTypeActivator : IBlockActivator
{
    private readonly Type blockType;
    internal DiTypeActivator(Type blockType) { this.blockType = blockType; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta)
    {
        if (cache.TryGetValue(meta.RegistryIndex, out var cached)) return cached;
        if (sp is null)
            throw new InvalidOperationException($"Coven DI: No IServiceProvider available to resolve {blockType.Name} for entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}.");
        var inst = sp.GetService(blockType) ?? throw new InvalidOperationException($"Coven DI: Unable to resolve {blockType.Name} for entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}. Ensure it is registered or provide a factory.");
        cache[meta.RegistryIndex] = inst;
        return inst;
    }
    public string? DisplayName => blockType.Name;
}

internal sealed class FactoryActivator : IBlockActivator
{
    private readonly Func<IServiceProvider, object> factory;
    internal FactoryActivator(Func<IServiceProvider, object> factory) { this.factory = factory; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, Routing.RegisteredBlock meta)
    {
        if (cache.TryGetValue(meta.RegistryIndex, out var cached)) return cached;
        if (sp is null) throw new InvalidOperationException($"Coven DI: No IServiceProvider available for factory activator at entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}.");
        var inst = factory(sp) ?? throw new InvalidOperationException($"Coven DI: Factory produced null block instance for entry #{meta.RegistryIndex} ({meta.BlockTypeName}) {meta.InputType.Name} -> {meta.OutputType.Name}.");
        cache[meta.RegistryIndex] = inst;
        return inst;
    }
    public string? DisplayName => null; // unknown until factory executes
}