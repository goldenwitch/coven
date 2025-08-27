using System;
using System.Collections.Generic;

namespace Coven.Core;

internal interface IBlockActivator
{
    object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, int index);
}

internal sealed class ConstantInstanceActivator : IBlockActivator
{
    private readonly object instance;
    internal ConstantInstanceActivator(object instance) { this.instance = instance; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, int index) => instance;
}

internal sealed class DiTypeActivator : IBlockActivator
{
    private readonly Type blockType;
    internal DiTypeActivator(Type blockType) { this.blockType = blockType; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, int index)
    {
        if (cache.TryGetValue(index, out var cached)) return cached;
        if (sp is null) throw new InvalidOperationException($"Coven DI: No IServiceProvider available to resolve {blockType.Name}.");
        var inst = sp.GetService(blockType) ?? throw new InvalidOperationException($"Coven DI: Unable to resolve {blockType.Name}.");
        cache[index] = inst;
        return inst;
    }
}

internal sealed class FactoryActivator : IBlockActivator
{
    private readonly Func<IServiceProvider, object> factory;
    internal FactoryActivator(Func<IServiceProvider, object> factory) { this.factory = factory; }
    public object GetInstance(IServiceProvider? sp, Dictionary<int, object> cache, int index)
    {
        if (cache.TryGetValue(index, out var cached)) return cached;
        if (sp is null) throw new InvalidOperationException("Coven DI: No IServiceProvider available for factory activator.");
        var inst = factory(sp) ?? throw new InvalidOperationException("Coven DI: Factory produced null block instance.");
        cache[index] = inst;
        return inst;
    }
}

