using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Coven.Core.Routing;

internal static class BlockInvokerFactory
{
    // Builds an invoker: (object in) => Task<object> by calling block.DoMagik and converting the Task<T> to Task<object>
    public static Func<object, Task<object>> Create(MagikBlockDescriptor d)
    {
        var block = d.BlockInstance;
        var blockType = block.GetType();
        var doMagik = blockType.GetMethod("DoMagik", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (doMagik is null)
            throw new InvalidOperationException($"Block {blockType.Name} does not implement DoMagik.");

        var objParam = Expression.Parameter(typeof(object), "obj");
        var castArg = Expression.Convert(objParam, d.InputType);
        var blockConst = Expression.Constant(block);
        var call = Expression.Call(blockConst, doMagik, castArg); // Task<Out>

        var taskOutType = typeof(Task<>).MakeGenericType(d.OutputType);
        var tParam = Expression.Parameter(taskOutType, "t");
        var resultProp = Expression.Property(tParam, nameof(Task<object>.Result));
        var toObj = Expression.Convert(resultProp, typeof(object));
        var mapLambda = Expression.Lambda(toObj, tParam);

        var continueWith = taskOutType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == "ContinueWith" && m.IsGenericMethodDefinition)
            .Select(m => new { m, ps = m.GetParameters() })
            .Where(x => x.ps.Length == 1)
            .Select(x => x.m)
            .First();

        var contCall = Expression.Call(call, continueWith.MakeGenericMethod(typeof(object)), mapLambda); // Task<object>

        var lambda = Expression.Lambda<Func<object, Task<object>>>(contCall, objParam);
        return lambda.Compile();
    }
}
