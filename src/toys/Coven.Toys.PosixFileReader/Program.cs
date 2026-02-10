// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.FileSystem;
using Coven.FileSystem.Posix;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
ConsoleClientConfig console = new()
{
    InputSender = "console",
    OutputSender = "reader"
};

string fsRoot = Environment.GetEnvironmentVariable("FS_ROOT")
    ?? Directory.GetCurrentDirectory();

// ───────────────────────────────────────────────────────────────────────────
// POSIX FILE READER TOY
//
// Type a relative file path at the prompt. The PosixFileSystem daemon
// reads the file from the sandbox root and prints its content. Errors
// (not-found, access denied, etc.) are surfaced as chat messages.
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(console);
    BranchManifest filesystem = coven.UsePosixFileSystem(fsRoot);

    coven.Covenant()
        .Connect(chat)
        .Connect(filesystem)
        .Routes(c =>
        {
            // User input → FileRead command (user types a path)
            c.Route<ChatAfferent, FileRead>(
                (msg, ct) => Task.FromResult(
                    new FileRead(Guid.NewGuid().ToString(), msg.Text.Trim())));

            // Successful read → display content
            c.Route<FileContent, ChatEfferent>(
                (content, ct) => Task.FromResult(
                    new ChatEfferent("reader", content.Content)));

            // Failed read → display error
            c.Route<FileFailure, ChatEfferent>(
                (failure, ct) => Task.FromResult(
                    new ChatEfferent("reader", $"[{failure.FailureKind}] {failure.Message}")));
        });
});

IHost host = builder.Build();

// Execute ritual — daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
