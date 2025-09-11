// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.IO;
using Xunit;
using Coven.Spellcasting.Agents.Codex.Validation;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class ValidationPlannerTests
{
    [Fact]
    public void Plan_WithShimRequired_IncludesShimProbe_And_Config()
    {
        var exe = "codex";
        var workspace = Path.Combine(Path.GetTempPath(), "coven-test-workspace");
        var shim = OperatingSystem.IsWindows() ? "shim.exe" : "shim";

        var inputs = new CodexValidationPlanner.Inputs(
            ExecutablePath: exe,
            WorkspaceDirectory: workspace,
            ShimExecutablePath: shim,
            ShimRequired: true);

        var plan = CodexValidationPlanner.Create(inputs);

        var codexHome = Path.Combine(workspace, ".codex");

        Assert.Equal(workspace, plan.WorkspaceEnsure.Path);
        Assert.StartsWith(workspace, plan.WorkspaceWriteProbe.Path, System.StringComparison.OrdinalIgnoreCase);

        Assert.Equal(codexHome, plan.CodexHomeEnsure.Path);
        Assert.StartsWith(codexHome, plan.CodexHomeWriteProbe.Path, System.StringComparison.OrdinalIgnoreCase);

        Assert.Equal(exe, plan.VersionProbe.FileName);
        Assert.Equal("--version", plan.VersionProbe.Arguments);
        Assert.Equal(workspace, plan.VersionProbe.WorkingDirectory);
        Assert.Contains("CODEX_HOME", plan.VersionProbe.Environment.Keys);
        Assert.Equal(codexHome, plan.VersionProbe.Environment["CODEX_HOME"]);

        Assert.NotNull(plan.PipeProbe);
        Assert.StartsWith("coven_probe_", plan.PipeProbe.PipeName);

        Assert.NotNull(plan.ShimHelpProbe);
        Assert.Equal(shim, plan.ShimHelpProbe!.FileName);
        Assert.Equal("--help", plan.ShimHelpProbe!.Arguments);

        Assert.NotNull(plan.ConfigMerge);
        Assert.Equal(codexHome, plan.ConfigMerge.CodexHomeDir);
        Assert.Equal(plan.PipeProbe.PipeName, plan.ConfigMerge.PipeName);
        Assert.Equal("coven", plan.ConfigMerge.ServerKey);
        Assert.Equal(shim, plan.ConfigMerge.ShimPath);

        Assert.Equal(exe, plan.SessionsListProbe.FileName);
        Assert.Equal("sessions list", plan.SessionsListProbe.Arguments);
        Assert.Equal(workspace, plan.SessionsListProbe.WorkingDirectory);
        Assert.Contains("CODEX_HOME", plan.SessionsListProbe.Environment.Keys);
        Assert.Equal(codexHome, plan.SessionsListProbe.Environment["CODEX_HOME"]);
    }

    [Fact]
    public void Plan_ShimNotRequired_ExcludesShimProbe()
    {
        var exe = "codex";
        var workspace = Path.Combine(Path.GetTempPath(), "coven-test-workspace");

        var inputs = new CodexValidationPlanner.Inputs(
            ExecutablePath: exe,
            WorkspaceDirectory: workspace,
            ShimExecutablePath: null,
            ShimRequired: false);

        var plan = CodexValidationPlanner.Create(inputs);

        Assert.Null(plan.ShimHelpProbe);
        Assert.Null(plan.ShimPath);
        Assert.NotNull(plan.ConfigMerge);
        Assert.Equal(Path.Combine(workspace, ".codex"), plan.ConfigMerge.CodexHomeDir);
        // When shim is not required, the planner still emits a merge spec with a placeholder path; executor can decide behavior.
        Assert.False(string.IsNullOrWhiteSpace(plan.ConfigMerge.PipeName));
    }
}
