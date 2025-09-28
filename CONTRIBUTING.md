# Coding guidelines
Read, understand, and follow these coding guidelines.

Above all else, remember this: Code is a liability. If you can accomplish the same objectives with simpler means (or even no code), that's preferrable.

## Side-effects
We hate side-effects. Prefer pure functions.
For example, when given the option to use an out variable, prefer to return a tuple.

Furthermore, avoid using timed delays. Prefer using a TaskCompletionSource and awaiting the result when it is available.

## Nullable
We have enabled the Nullable flag globally.
Avoid nullable elements. Use defaults where possible.

Coalesce nulls as early as possible to minimize or eliminate ternary logic.
In a constructor that might be invalidly given null inputs, use ArgumentNullException.ThrowIfNull(value);

## Cancellation
Every truly async function should have cancellation wired in.
If a process takes more than 200ms 50% of the time, that process is truly async and needs to be cancellable.

Cancellation is orchestrated from the top level down. Don't create new cancellation tokens when there is an upstream one that can be brought down.

## Dependency Injection
We love dependency injection, specifically constructor injection.
Avoid the word "new" for anything other than data records.

Hide internal constructors for abstract components behind a ServiceCollectionExtension for building the concrete types.
When defining a "builder" extension for a library, always use TryAdd. Add is reserved for the end user.

## Disposal
1. Leverage using statements to ensure that disposed objects are guaranteed to dispose when out of scope.
2. Avoid manually calling dispose on any objects.
3. Implement proper thread-safe disposal.
4. Avoid long-lived references that might keep scope alive.

## Logging
Always leverage ILogger for logging.
Prefer to think of logs as breadcrumbs that follow every fork in the code.
If you can't tell which fork happened, there needs to be a breadcrumb at the split.

## Component scope
Easy to replace is better than good.
Minimize entanglement between components so that each component (down to a single function or interface), can be swapped out for a new version trivially.

Lock in what needs to happen, not how it gets accomplished.

## Testing
Leverage shared test infrastructure.
Group tests into categories and files based on their purpose and the components they are validating.

Ensure that tests complete deterministically. Avoid redundant tests.
The test infrastructure MUST be simpler than the program itself or we lose the value of testing.

## Linting
We use draconian linting to elevate the importance of specific style guidelines so that we can keep the code smaller and simpler.

## Samples
Samples are full end to end examples that a user could leverage to accomplish actual business goals.
Keep samples rich, follow best practices when building them, and minimize necessary configuration.

In many ways, the only difference between a sample and a product should be whether the business logic is locked in stone or flexible.

All samples go in /src/samples/ and are included in the global solution.

## Toys
A "toy" is a standalone application that leverages functionality from the libraries.
It enables developers to play around with the concepts and debug through the flows more easily.

Toys should be simple, accomplish one specific thing well, and represent production quality code.

Remember, toys are meant to be played with so keep in mind WHERE the developer is invited to tinker.
For example, configuration COULD live in env variables or it COULD live in code.

For toys, prefer configuration in program.cs. Assume developers will change code to configure things, but minimize the amount of changes they need to make to see new outputs from the toy.

All toys go in /src/toys/ and are included in the global solution.

## Locking and synchronization
Use SemaphoreSlim over lock in async contexts.

Use System.Threading.Lock for the lock.

## Misc
One class per file unless describing "dumb" data types like records.

We have enabled implicit usings globally.

This means you should never add one of the following usings:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
```