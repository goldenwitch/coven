# Toys

Small, focused console apps that each demonstrate a single Coven feature in isolation. These are intentionally minimal so you can test one concept at a time.

- ConsoleEcho: Wires the console chat adapter to echo back whatever you type.
- ConsoleAgentChat: Minimal agent that responds via the chat journal.

## Requirements

- Applications only: every Toy is an executable console app (no libraries) and should set `OutputType` to `Exe`.
- VS Code start: add a launch configuration for each Toy in `.vscode/launch.json`.
- Solution entry: add each Toy project to the solution so it’s easy to discover and build together.

## VS Code: Run/Debug

- Pick the Toy’s launch configuration under Run and Debug (e.g., `.NET Launch (Console) - ConsoleEcho`).
- Interact via the Terminal panel (not Debug Console). Use the terminal tab matching the launch config name.
- Prefer a separate window? Change `console` to `externalTerminal` for that launch.

Template launch entry

```json
{
  "name": ".NET Launch (Console) - <ToyName>",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "build <ToyName>",
  "program": "${workspaceFolder}/Toys/<ToyPath>/bin/Debug/net10.0/<AssemblyName>.dll",
  "args": [],
  "cwd": "${workspaceFolder}/Toys/<ToyPath>",
  "console": "integratedTerminal",
  "stopAtEntry": false
}
```

## Add to Solution

- Using CLI: run `dotnet sln add Toys/<ToyPath>/<Project>.csproj` from repo root.
- Or with IDE: open the solution, right‑click Solution → Add → Existing Project… and select the Toy `.csproj`.
