# Minimal DI Support

## Scope
- Enable constructor DI for MagikBlocks via IServiceCollection.
- Preserve current routing and compiled pipeline behavior; only activation changes.

## Non‑Goals (v1)
- No named/keyed covens API. Apps can register multiple covens and consume `IEnumerable<ICoven>`.

## User API (Slim)
- `IServiceCollection BuildCoven(Action<CovenServiceBuilder> build)`
  - Provides a builder inside the action. Call `.Done()` to finalize.
  - If `.Done()` is not called explicitly, the extension will auto‑finalize at the end of the action.
- `CovenServiceBuilder BuildCoven()`
  - Returns a builder you can use fluently and then call `.Done()`.
- `CovenServiceBuilder` methods:
  - `AddBlock<TIn,TOut,TBlock>(ServiceLifetime lifetime = ServiceLifetime.Transient) where TBlock : IMagikBlock<TIn,TOut>`
  - `AddBlock<TIn,TOut>(Func<IServiceProvider, IMagikBlock<TIn,TOut>> factory)`
  - `AddLambda<TIn,TOut>(Func<TIn,Task<TOut>> func)`
  - `IServiceCollection Done()` — seals the builder, registers the `ICoven` singleton capturing the ordered registry, and returns the underlying `IServiceCollection` for chaining. Further `AddBlock` calls throw after `Done()`.
  - Lifetimes and auto‑registration: For `AddBlock<... ,TBlock>(...)`, the builder auto‑registers `TBlock` using `TryAdd` with the requested lifetime (default Transient). If the type is already registered, we honor the existing registration and only record order. Factories/lambdas are not added to DI.

## Registration Semantics
- Builder behaves like `MagikBuilder` with DI benefits:
  - Preserves explicit registration order for routing tie‑breaks.
  - Supports heterogeneous chains via generic `TIn/TOut` per call.
- Auto‑registration uses `TryAdd` semantics:
  - If `TBlock` not in the container, register with requested lifetime.
  - If `TBlock` pre‑registered, do not override; use the existing lifetime/implementation.
  - Capabilities can still be associated at registration time within the builder (future addition) without affecting DI lifetime.

## Execution Model
- Ritual creates an `IServiceScope` that matches Tag scope lifetime.
- For each executed step:
  - Resolve or create the block instance from the ritual scope (or factory/lambda). If not cached and the scope cannot resolve the service (e.g., factory/lambda only or custom types), throw a configuration error (see Error Handling).
  - Invoke via a compiled invoker that accepts `(instance, input)` and returns `Task<object>`.
  - Emit `by:<BlockTypeName>`; advance using existing selection rules.
- Maintain a per‑ritual cache of resolved block instances (by registry index) to avoid duplicate resolves. Dispose with ritual scope.

## Error Handling
- Finalization:
  - Calling `.Done()` more than once on the same builder is a no‑op (idempotent). This keeps registration code resilient in compositional setups.
  - Calling `AddBlock/AddLambda` after `.Done()` throws `InvalidOperationException("Cannot modify CovenServiceBuilder after Done().")` and should be considered a hard configuration error.
- Missing block service: If a registered block instance is requested and is neither in the per‑ritual cache nor resolvable from the ritual `IServiceProvider` (and no factory/lambda was supplied), throw an exception with context:
  - Message includes registry index, block type, and `(TIn -> TOut)` signature.
  - Example: `Coven DI: Unable to resolve block StringToLength (string -> int) at registry index 1. Ensure it is registered (auto‑registered by AddBlock unless overridden) or provide a factory/lambda.`
  - Purpose: surface misconfiguration early so users fix DI registrations instead of silent fallbacks.

## Internal Adjustments
- Invokers compiled per block type, not bound instance: `Func<object instance, object input, Task<object>>`.
- Pipeline cache remains per `(startType, targetType)`; stores invokers and registry metadata only.
- Board owns scope creation/disposal around pipeline execution.

## Implementation Plan (Concise)
- Shared registry/factory:
  - Extract a declarative registry entry `(TIn, TOut, Capabilities, Index, Activator)` consumed by a common Coven factory.
  - Builders (MagikBuilder, DI builder) only populate entries and finalize; no runtime logic divergence.
- Activators abstraction:
  - `IBlockActivator { IMagikBlock Create(IServiceProvider scope); }`.
  - Implementations: `DiTypeActivator<TBlock>`, `FactoryActivator`, `ConstantActivator` (instance), `LambdaActivator` (wraps `Func<TIn,Task<TOut>>`).
- Invoker shape change:
  - Update `BlockInvokerFactory` to compile per-type invokers: `Func<object instance, object input, Task<object>>`.
  - Pipelines call invoker with the instance resolved via activator.
- Board + scope:
  - Board creates an `IServiceScope` per ritual, aligns it with Tag scope, keeps a per‑ritual instance cache keyed by registry index, disposes scope on completion.
- Reuse existing runtime:
  - Keep `DefaultSelectionStrategy`, tag semantics, forward‑only routing, and `PipelineCompiler` selection loop unchanged aside from the invoker call site.

## Multiple Covens
- Register multiple via repeated `BuildCoven(...)`. Consume as `IEnumerable<ICoven>` or wrap in your own typed/named service.

## Backward Compatibility
- Existing builder and runtime behavior unchanged. DI construction is additive.

## Future Hooks (Out of Scope)
- Options (timeouts, retries, precompilation) via `IOptions<CovenOptions>`.
- Named/keyed covens.
- DI‑pluggable selection strategies.

## Examples

- Auto‑registration with explicit `.Done()` (recommended):
  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using Coven.Core; // ICoven

  var services = new ServiceCollection();

  services.BuildCoven(coven =>
  {
      coven.AddBlock<string, int, StringToLength>();                  // auto‑registers transient
      coven.AddBlock<int, double, IntToDouble>(ServiceLifetime.Singleton); // auto‑registers singleton
      coven.AddLambda<double, string>(d => Task.FromResult(d.ToString("F2")));
      coven.Done();
  });

  using var provider = services.BuildServiceProvider();
  var coven = provider.GetRequiredService<ICoven>();
  var result = await coven.Ritual<string, string>("hello world");
  ```

- Fluent builder style (no action):
  ```csharp
  services
      .BuildCoven()
      .AddBlock<string, int, StringToLength>()
      .AddBlock<int, double, IntToDouble>(ServiceLifetime.Singleton)
      .AddLambda<double, string>(d => Task.FromResult(d.ToString("F2")))
      .Done();
  ```



- Controlling lifetimes via DI (builder honors existing registration):
  ```csharp
  services.AddScoped<IntToDouble>(); // pre‑register with specific lifetime
  services.BuildCoven(coven =>
  {
      coven.AddBlock<string, int, StringToLength>();
      coven.AddBlock<int, double, IntToDouble>(); // builder does not override scoped
      coven.AddLambda<double, string>(d => Task.FromResult(d.ToString()));
      coven.Done();
  });
  ```

- Using a factory to construct a block with DI dependencies:
  ```csharp
  services.AddSingleton<IClock, SystemClock>();
  services.BuildCoven(coven =>
  {
      coven.AddBlock<string, int, StringToLength>();
      coven.AddBlock<int, string>(sp => new AnnotateWithTime(sp.GetRequiredService<IClock>()));
  });
  ```

- Multiple covens (consume `IEnumerable<ICoven>`):
  ```csharp
  services.BuildCoven(c =>
  {
      c.AddBlock<string, int, StringToLength>();
      c.AddBlock<int, string, IntToString>();
  });

  services.BuildCoven(c =>
  {
      c.AddBlock<string, string, NormalizeWhitespace>();
  });

  var covens = provider.GetRequiredService<IEnumerable<ICoven>>();
  foreach (var coven in covens) { /* choose appropriate one */ }
  ```
