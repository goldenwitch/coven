// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;

namespace Coven.Spellcasting.Agents;

public interface ITailMuxFactory
{
    ITailMux CreateForRollout(string rolloutPath, Process? process);
}
