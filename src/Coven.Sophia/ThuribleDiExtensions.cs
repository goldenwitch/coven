// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Di;
using Coven.Durables;

namespace Coven.Sophia;

public static class ThuribleDiExtensions
{
    public static CovenServiceBuilder AddThurible<TIn, TOut>(
        this CovenServiceBuilder builder,
        string label,
        Func<TIn, IDurableList<string>, Task<TOut>> func,
        Func<IServiceProvider, IDurableList<string>> storageFactory,
        IEnumerable<string>? capabilities = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (func is null) throw new ArgumentNullException(nameof(func));
        if (storageFactory is null) throw new ArgumentNullException(nameof(storageFactory));

        return builder.AddBlock<TIn, TOut>(
            sp => new ThuribleBlock<TIn, TOut>(label, storageFactory(sp), func),
            capabilities);
    }
}
