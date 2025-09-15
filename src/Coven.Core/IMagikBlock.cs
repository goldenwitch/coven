// SPDX-License-Identifier: BUSL-1.1

﻿namespace Coven.Core;

public interface IMagikBlock<T, TOutput>
{
    Task<TOutput> DoMagik(T input, CancellationToken cancellationToken = default);
}
