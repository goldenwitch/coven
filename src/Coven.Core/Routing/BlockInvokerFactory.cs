// SPDX-License-Identifier: BUSL-1.1

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Coven.Core.Routing;

internal static class BlockInvokerFactory
{
    // Builds an invoker: (object instance, object input) => Task<object>
    public static Func<object, object, Task<object>> Create(MagikBlockDescriptor d)
    {
        var ifaceType = typeof(IMagikBlock<,>).MakeGenericType(d.InputType, d.OutputType);
        var doMagik = ifaceType.GetMethod("DoMagik", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("IMagikBlock.DoMagik not found.");

        var instParam = Expression.Parameter(typeof(object), "instance");
        var inParam = Expression.Parameter(typeof(object), "input");
        var castInst = Expression.Convert(instParam, ifaceType);
        var castArg = Expression.Convert(inParam, d.InputType);
        var call = Expression.Call(castInst, doMagik, castArg); // Task<Out>

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

        var lambda = Expression.Lambda<Func<object, object, Task<object>>>(contCall, instParam, inParam);
        return lambda.Compile();
    }

    // Builds a pull-mode invoker that runs the block and finalizes via Board with a strict generic type.
    // Signature: (Board board, IOrchestratorSink sink, string? branchId, object input) => Task
    public static Func<Board, IOrchestratorSink, string?, object, Task> CreatePull(MagikBlockDescriptor d, IReadOnlyList<string> forwardTags)
    {
        var block = d.BlockInstance;
        var blockType = block.GetType();
        var doMagik = blockType.GetMethod("DoMagik", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (doMagik is null)
            throw new InvalidOperationException($"Block {blockType.Name} does not implement DoMagik.");

        var boardParam = Expression.Parameter(typeof(Board), "board");
        var sinkParam = Expression.Parameter(typeof(IOrchestratorSink), "sink");
        var branchParam = Expression.Parameter(typeof(string), "branchId");
        var inputParam = Expression.Parameter(typeof(object), "input");

        var castArg = Expression.Convert(inputParam, d.InputType);
        var blockConst = Expression.Constant(block);
        var call = Expression.Call(blockConst, doMagik, castArg); // Task<TOut>

        var taskOutType = typeof(Task<>).MakeGenericType(d.OutputType);

        // Build continuation: t => board.FinalizePullStep<TOut>(sink, t.Result, branchId, blockTypeName)
        var tParam = Expression.Parameter(taskOutType, "t");
        var resultProp = Expression.Property(tParam, nameof(Task<object>.Result));
        var boardFinalize = typeof(Board).GetMethod("FinalizePullStep", BindingFlags.Instance | BindingFlags.NonPublic);
        if (boardFinalize is null)
            throw new InvalidOperationException("Board.FinalizePullStep not found.");
        var finalizeClosed = boardFinalize.MakeGenericMethod(d.OutputType);

        var blockNameConst = Expression.Constant(blockType.Name, typeof(string));
        // Build constant string[] for forward tags
        var tagConsts = forwardTags.Select(t => Expression.Constant(t, typeof(string)));
        var tagsArray = Expression.NewArrayInit(typeof(string), tagConsts);

        var finalizeCall = Expression.Call(boardParam, finalizeClosed, sinkParam, resultProp, branchParam, blockNameConst, tagsArray);
        var contLambdaType = typeof(Action<>).MakeGenericType(taskOutType);
        var contLambda = Expression.Lambda(contLambdaType, finalizeCall, tParam); // Action<Task<TOut>>

        // Find ContinueWith(Action<Task<TOut>>)
        var continueWith = taskOutType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == "ContinueWith")
            .Select(m => new { m, ps = m.GetParameters() })
            .Where(x => x.ps.Length == 1 && x.ps[0].ParameterType.IsGenericType && x.ps[0].ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
            .Select(x => x.m)
            .FirstOrDefault();

        if (continueWith is null)
            throw new InvalidOperationException("Suitable ContinueWith overload not found.");

        var contCall = Expression.Call(call, continueWith, contLambda); // Task

        var lambda = Expression.Lambda<Func<Board, IOrchestratorSink, string?, object, Task>>(contCall, boardParam, sinkParam, branchParam, inputParam);
        return lambda.Compile();
    }
}