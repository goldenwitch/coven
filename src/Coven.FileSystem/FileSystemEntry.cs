// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.FileSystem;

/// <summary>
/// Base entry type for file system journals.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FileRead), nameof(FileRead))]
[JsonDerivedType(typeof(FileContent), nameof(FileContent))]
[JsonDerivedType(typeof(FileFailure), nameof(FileFailure))]
public abstract record FileSystemEntry : Entry;

// ── Efferent (commands) ──

/// <summary>Read file content at the specified path.</summary>
public sealed record FileRead(string CorrelationId, string Path) : FileSystemEntry;

// ── Afferent (results) ──

/// <summary>File content response.</summary>
public sealed record FileContent(string CorrelationId, string Content) : FileSystemEntry;

/// <summary>File operation failure.</summary>
public sealed record FileFailure(string CorrelationId, string FailureKind, string Message) : FileSystemEntry;
