// SPDX-License-Identifier: BUSL-1.1

using System;

namespace Coven.Spellcasting.Agents;

// Generic tailing events used by ITailMux
public abstract record TailEvent(string Line, DateTimeOffset Timestamp);
public sealed record Line(string Line, DateTimeOffset Timestamp) : TailEvent(Line, Timestamp);
public sealed record ErrorLine(string Line, DateTimeOffset Timestamp) : TailEvent(Line, Timestamp);
