# Coven.Daemonology

The Daemonology library contains light-weight scaffolding for building long-running services.

## IDaemon
IDaemon represents the very minimum necessary to run a long-running service.
- Start(cancellation): Begins running a long-running service.
- Shutdown(cancellation): Shuts down a long-running service gracefully.
- Status: A property that represents what the service is currently doing when it is called.

## ContractDaemon
ContractDaemon is an abstract base that coordinates status and failure waiters without polling. It offers:

- `Task WaitFor(Status, cancellation)` — await a specific status.
- `Task<Exception> WaitForFailure(cancellation)` — observe the first failure.
- `protected void Transition(Status)` — single mutation point for status changes.
- `protected void Fail(Exception)` — publish first failure and wake failure waiters.

## Status enum
We use a simple enum to represent different states. Failure is not a state; it is a separate signal.

Possible values are:
- Stopped
- Running
- Completed
