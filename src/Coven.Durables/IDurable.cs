// SPDX-License-Identifier: BUSL-1.1

﻿namespace Coven.Durables;

public interface IDurable<T>
{
    public Task Save(T input);

    public Task<T> Load();
}