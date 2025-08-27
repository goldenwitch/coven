# Minimal DI Support

## Scope
- Enable constructor DI for MagikBlocks via `IServiceCollection` and `IHostApplicationBuilder`.
- Preserve current routing and compiled pipeline behavior; only activation changes.

## Non‑Goals (v1)
- No named/keyed covens API. Apps can register multiple covens and consume `IEnumerable<ICoven>`.

- `IServiceCollection BuildCoven(Action<CovenServiceBuilder> build)`
  - Provides a builder inside the action. Call `.Done()` (or `.Done(pull: true)`) to finalize.
  - If `.Done()` is not called explicitly, the extension will auto‑finalize at the end of the action.
- `CovenServiceBuilder BuildCoven()`
  - Returns a builder you can use fluently and then call `.Done()` (or `.Done(pull: true)`).
- `IHostApplicationBuilder BuildCoven(Action<CovenServiceBuilder> build)` / `BuildCoven()`
  - Forwards to `builder.Services` and supports the same semantics.
- `CovenServiceBuilder` methods:
  - `AddBlock<TIn,TOut,TBlock>(ServiceLifetime lifetime = ServiceLifetime.Transient) where TBlock : IMagikBlock<TIn,TOut>`
  - `AddBlock<TIn,TOut>(Func<IServiceProvider, IMagikBlock<TIn,TOut>> factory)`
  - `AddLambda<TIn,TOut>(Func<TIn,Task<TOut>> func)`
  - `IServiceCollection Done()` — seals the builder, registers the `ICoven` singleton capturing the ordered registry, and returns the underlying `IServiceCollection` for chaining. Further `AddBlock` calls throw after `Done()`.
  - Lifetimes and auto‑registration: For `AddBlock<... ,TBlock>(...)`, the builder auto‑registers `TBlock` (TryAdd semantics) with the requested lifetime (default Transient). If the type is already registered, we honor the existing registration and only record order. Factories/lambdas are not added to DI.

## Registration Semantics
- Builder behaves like `MagikBuilder` with DI benefits:
  - Preserves explicit registration order for routing tie‑breaks.
  - Supports heterogeneous chains via generic `TIn/TOut` per call.
- Auto‑registration uses `TryAdd` semantics:
  - If `TBlock` not in the container, register with requested lifetime.
  - If `TBlock` pre‑registered, do not override; use the existing lifetime/implementation.
  - Capabilities can be associated at registration time within the builder without affecting DI lifetime.

## Execution Model
- `Coven` creates an `IServiceScope` per ritual; Board continues to own Tag scope.
- For each executed step:
  - Resolve or create the block instance from the ritual scope (or factory/lambda). If not cached and the scope cannot resolve the service (e.g., factory/lambda only or custom types), throw a configuration error (see Error Handling).
  - Invoke via a compiled invoker that accepts `(instance, input)` and returns `Task<object>`.
  - Emit `by:<BlockTypeName>`; advance using existing selection rules.
- Maintain a per‑ritual cache of resolved block instances (by registry index) to avoid duplicate resolves. Dispose with ritual scope.

Capabilities discovery (DI)
- Fixed at `.Done()` time; merged from:
  - Builder‑provided capabilities (argument to `AddBlock`).
  - `[TagCapabilities(...)]` attribute applied to the block type.
  - Optional paramless `ITagCapabilities` implementation (instantiated once at registration if available).
  - No DI instance resolution occurs at registration time.

## Error Handling
- Finalization:
  - Calling `.Done()` more than once on the same builder is a no‑op (idempotent). This keeps registration code resilient in compositional setups.
  - Calling `AddBlock/AddLambda` after `.Done()` throws `InvalidOperationException("Cannot modify CovenServiceBuilder after Done().")` and should be considered a hard configuration error.
- Missing block service: If a registered block instance is requested and is neither in the per‑ritual cache nor resolvable from the ritual `IServiceProvider` (and no factory/lambda was supplied), throw an exception with context:
  - Message includes registry index, display name, and `(TIn -> TOut)` signature.
  - Example: `Coven DI: Unable to resolve StringToLength for entry #1 (StringToLength) string -> int. Ensure it is registered or provide a factory.`
  - Purpose: surface misconfiguration early so users fix DI registrations instead of silent fallbacks.

## Internal Adjustments
- Invokers compiled per block type, not bound instance: `Func<object instance, object input, Task<object>>`.
- Pipeline cache remains per `(startType, targetType)`; stores invokers and registry metadata only.
- Per‑ritual instance cache keyed by registry index; tag epochs and selection unchanged.
- `IBlockActivator.GetInstance(IServiceProvider? sp, Dictionary<int,object> cache, RegisteredBlock meta)` resolves instances per ritual.
- Pull mode DI: pull wrappers resolve the instance via the activator, invoke, then call `FinalizePullStep<TOut>`; DI works in pull as well as push.

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

- Host builder sugar (auto‑finalize inside action):
  ```csharp
  var builder = Host.CreateApplicationBuilder();
  builder.BuildCoven(c =>
  {
      c.AddBlock<string,int, StringToInt>();
      // .Done() optional here; auto‑finalizes at end of action
  });
  using var host = builder.Build();
  var coven = host.Services.GetRequiredService<ICoven>();
  ```

- Building a DI coven in Pull mode:
  ```csharp
  services.BuildCoven(c =>
  {
      c.AddBlock<string,int, StringToInt>();
      c.AddBlock<int,double, IntToDouble>();
      c.Done(pull: true);
  });
  ```

- Host builder sugar:
  ```csharp
  var builder = Host.CreateApplicationBuilder();
  builder.BuildCoven(c =>
  {
      c.AddBlock<string,int, StringToInt>();
      // .Done() optional in action; auto‑finalizes
  });
  using var host = builder.Build();
  var coven = host.Services.GetRequiredService<ICoven>();
  ```
