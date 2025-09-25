// SPDX-License-Identifier: BUSL-1.1

using System.Linq.Expressions;
using System.Reflection;

namespace Coven.Core.Routing;

internal static class BlockInvokerFactory
{
    // Builds an invoker: (object instance, object input, CancellationToken ct) => Task<object>
    public static Func<object, object, CancellationToken, Task<object>> Create(MagikBlockDescriptor d)
    {
        Type ifaceType = typeof(IMagikBlock<,>).MakeGenericType(d.InputType, d.OutputType);
        MethodInfo doMagik = ifaceType.GetMethod("DoMagik", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("IMagikBlock.DoMagik not found.");

        ParameterExpression instParam = Expression.Parameter(typeof(object), "instance");
        ParameterExpression inParam = Expression.Parameter(typeof(object), "input");
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        UnaryExpression castInst = Expression.Convert(instParam, ifaceType);
        UnaryExpression castArg = Expression.Convert(inParam, d.InputType);
        MethodCallExpression call = Expression.Call(castInst, doMagik, castArg, ctParam); // Task<Out>

        Type taskOutType = typeof(Task<>).MakeGenericType(d.OutputType);
        ParameterExpression tParam = Expression.Parameter(taskOutType, "t");
        MemberExpression resultProp = Expression.Property(tParam, nameof(Task<object>.Result));
        UnaryExpression toObj = Expression.Convert(resultProp, typeof(object));
        LambdaExpression mapLambda = Expression.Lambda(toObj, tParam);

        MethodInfo continueWith = taskOutType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == "ContinueWith" && m.IsGenericMethodDefinition)
            .Select(m => new { m, ps = m.GetParameters() })
            .Where(x => x.ps.Length == 1)
            .Select(x => x.m)
            .First();

        MethodCallExpression contCall = Expression.Call(call, continueWith.MakeGenericMethod(typeof(object)), mapLambda); // Task<object>

        Expression<Func<object, object, CancellationToken, Task<object>>> lambda = Expression.Lambda<Func<object, object, CancellationToken, Task<object>>>(contCall, instParam, inParam, ctParam);
        return lambda.Compile();
    }

    // Builds a pull-mode invoker that runs the block and finalizes via Board with a strict generic type.
    // Signature: (Board board, IOrchestratorSink sink, string? branchId, object input, CancellationToken ct) => Task
    public static Func<Board, IOrchestratorSink, string?, object, CancellationToken, Task> CreatePull(MagikBlockDescriptor d, IReadOnlyList<string> forwardTags)
    {
        object block = d.BlockInstance;
        Type blockType = block.GetType();
        MethodInfo? doMagik = blockType.GetMethod("DoMagik", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Block {blockType.Name} does not implement DoMagik.");

        ParameterExpression boardParam = Expression.Parameter(typeof(Board), "board");
        ParameterExpression sinkParam = Expression.Parameter(typeof(IOrchestratorSink), "sink");
        ParameterExpression branchParam = Expression.Parameter(typeof(string), "branchId");
        ParameterExpression inputParam = Expression.Parameter(typeof(object), "input");
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        UnaryExpression castArg = Expression.Convert(inputParam, d.InputType);
        ConstantExpression blockConst = Expression.Constant(block);
        // For pull flow, pass through the provided cancellationToken.
        MethodCallExpression call = Expression.Call(blockConst, doMagik, castArg, ctParam); // Task<TOut>

        Type taskOutType = typeof(Task<>).MakeGenericType(d.OutputType);

        // Build continuation: t => board.FinalizePullStep<TOut>(sink, t.Result, branchId, blockTypeName)
        ParameterExpression tParam = Expression.Parameter(taskOutType, "t");
        MemberExpression resultProp = Expression.Property(tParam, nameof(Task<object>.Result));
        MethodInfo? boardFinalize = typeof(Board).GetMethod("FinalizePullStep", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Board.FinalizePullStep not found.");

        MethodInfo finalizeClosed = boardFinalize.MakeGenericMethod(d.OutputType);

        ConstantExpression blockNameConst = Expression.Constant(blockType.Name, typeof(string));
        // Build constant string[] for forward tags
        IEnumerable<ConstantExpression> tagConsts = forwardTags.Select(t => Expression.Constant(t, typeof(string)));
        NewArrayExpression tagsArray = Expression.NewArrayInit(typeof(string), tagConsts);

        MethodCallExpression finalizeCall = Expression.Call(boardParam, finalizeClosed, sinkParam, resultProp, branchParam, blockNameConst, tagsArray);
        Type contLambdaType = typeof(Action<>).MakeGenericType(taskOutType);
        LambdaExpression contLambda = Expression.Lambda(contLambdaType, finalizeCall, tParam); // Action<Task<TOut>>

        // Find ContinueWith(Action<Task<TOut>>)
        MethodInfo? continueWith = taskOutType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == "ContinueWith")
            .Select(m => new { m, ps = m.GetParameters() })
            .Where(x => x.ps.Length == 1 && x.ps[0].ParameterType.IsGenericType && x.ps[0].ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
            .Select(x => x.m)
            .FirstOrDefault() ?? throw new InvalidOperationException("Suitable ContinueWith overload not found.");

        MethodCallExpression contCall = Expression.Call(call, continueWith, contLambda); // Task

        Expression<Func<Board, IOrchestratorSink, string?, object, CancellationToken, Task>> lambda = Expression.Lambda<Func<Board, IOrchestratorSink, string?, object, CancellationToken, Task>>(contCall, boardParam, sinkParam, branchParam, inputParam, ctParam);
        return lambda.Compile();
    }
}
