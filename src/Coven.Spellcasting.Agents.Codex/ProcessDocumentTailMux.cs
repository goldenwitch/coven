// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Diagnostics;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Tail;

namespace Coven.Spellcasting.Agents.Codex;

/// <summary>
/// Asymmetric tail multiplexer that composes a document tail source and a process send port.
/// Reads from the specified file and writes to a lazily-started child process stdin.
/// </summary>
    internal sealed class ProcessDocumentTailMux : BaseCompositeTailMux<ISendPort, ITailSource>
    {
        internal ProcessDocumentTailMux(
            string documentPath,
            string fileName,
            IReadOnlyList<string>? arguments = null,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string?>? environment = null,
            Action<ProcessStartInfo>? configurePsi = null)
        : base(new ProcessSendPort(fileName, arguments, workingDirectory, environment, configurePsi),
               new DocumentTailSource(documentPath))
        {
        }

    }
