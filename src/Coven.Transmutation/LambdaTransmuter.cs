namespace Coven.Transmutation;

/// <summary>
/// Adapts a delegate into an <see cref="ITransmuter{TIn, TOut}"/>.
/// </summary>
/// <typeparam name="TIn">Input type.</typeparam>
/// <typeparam name="TOut">Output type.</typeparam>
public sealed class LambdaTransmuter<TIn, TOut> : ITransmuter<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, Task<TOut>> _func;

    /// <summary>
    /// Creates a new transmuter from the provided asynchronous delegate.
    /// </summary>
    /// <param name="func">Delegate implementing the transmutation logic.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is null.</exception>
    public LambdaTransmuter(Func<TIn, CancellationToken, Task<TOut>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _func = func;
    }

    /// <summary>
    /// Executes the transmutation logic.
    /// </summary>
    /// <param name="Input">Input value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transmuted output.</returns>
    public Task<TOut> Transmute(TIn Input, CancellationToken cancellationToken = default)
        => _func(Input, cancellationToken);
}

