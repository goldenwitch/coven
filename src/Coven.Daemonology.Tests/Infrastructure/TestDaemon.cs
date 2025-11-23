// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;

namespace Coven.Daemonology.Tests.Infrastructure;

internal sealed class TestDaemon(IScrivener<DaemonEvent> scrivener) : ContractDaemon(scrivener)
{

    public override Task Start(CancellationToken cancellationToken = default)
        => Transition(Status.Running, cancellationToken);

    public override Task Shutdown(CancellationToken cancellationToken = default)
        => Transition(Status.Completed, cancellationToken);

    public Task TriggerFailure(Exception error, CancellationToken cancellationToken = default)
        => Fail(error, cancellationToken);
}

