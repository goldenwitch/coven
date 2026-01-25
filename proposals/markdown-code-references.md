# Proposal: Markdown Code Reference Expansion

## Summary

This proposal defines a **context-free grammar** for embedding live code references in markdown documentation. References expand to actual source code on commit (for GitHub rendering) and contract back to references on pull (for maintainable editing). This ensures documentation examples always reflect the actual implementation.

## Motivation

From [guidelines/documentation.md](../guidelines/documentation.md): *"Reference actual code rather than duplicating it in markdown."*

Currently, this guideline is aspirational. Code examples in markdown become stale when implementations change. The divergence is invisible until someone notices the lie.

The project already has canonical code locations:
- `src/samples/` — business-facing sample projects
- `src/toys/` — developer-facing sample projects  
- `src/Coven.*/` — real implementations

We need tooling that makes these the **single source of truth** for documentation examples.

## Design Goals

1. **Bidirectional transformation** — expand on commit, contract on pull
2. **GitHub-compatible output** — expanded form renders correctly without tooling
3. **Formally specified grammar** — unambiguous parsing, no edge case surprises
4. **Graceful failure** — clear errors when references break
5. **Language-aware** — automatic syntax highlighting based on file extension

---

## Grammar Specification

### EBNF Grammar

```ebnf
(* Top-level reference block *)
reference_block    = open_tag, newline, expanded_content?, close_tag ;

(* Opening tag with reference specification *)
open_tag           = "<!-- coven:include", whitespace, reference_spec, whitespace?, "-->" ;

(* Closing tag marks end of expanded content *)
close_tag          = "<!-- /coven:include -->" ;

(* Reference specification *)
reference_spec     = file_path, fragment? ;

(* File path relative to repository root *)
file_path          = path_segment, { "/", path_segment } ;
path_segment       = identifier, [ ".", extension ] ;
identifier         = letter, { letter | digit | "_" | "-" } ;
extension          = letter, { letter | digit } ;

(* Fragment specifies what to extract *)
fragment           = "#", fragment_type ;
fragment_type      = line_range | region_ref | symbol_ref ;

(* Line range: L10 or L10-L25 *)
line_range         = "L", line_number, [ "-", "L"?, line_number ] ;
line_number        = digit, { digit } ;

(* Region reference: #region:RegionName *)
region_ref         = "region:", region_name ;
region_name        = identifier ;

(* Symbol reference: #symbol:ClassName.MethodName *)
symbol_ref         = "symbol:", symbol_path ;
symbol_path        = identifier, { ".", identifier } ;

(* Expanded content between tags *)
expanded_content   = code_fence, newline, source_lines, code_fence, newline ;
code_fence         = "```", language_hint? ;
language_hint      = letter, { letter | digit } ;
source_lines       = { any_char, newline } ;

(* Primitives *)
whitespace         = " ", { " " } ;
newline            = "\n" | "\r\n" ;
letter             = "a" | ... | "z" | "A" | ... | "Z" ;
digit              = "0" | ... | "9" ;
any_char           = ? any character except newline ? ;
```

### Railroad Diagram (Conceptual)

```
reference_block:
┌──────────────────────────────────────────────────────────────────┐
│ <!-- coven:include ─┬─ file_path ─┬────────────┬─ --> ─ newline │
│                     │             │            │                │
│                     │             └─ fragment ─┘                │
│                     │                                           │
│ ┌───────────────────┴───────────────────────────────────────┐   │
│ │ (optional expanded content)                               │   │
│ │ ```language                                               │   │
│ │ ... source lines ...                                      │   │
│ │ ```                                                       │   │
│ └───────────────────────────────────────────────────────────┘   │
│                                                                 │
│ <!-- /coven:include -->                                         │
└──────────────────────────────────────────────────────────────────┘
```

---

## Reference Types

### 1. Entire File Inclusion

**Syntax:**
```markdown
<!-- coven:include src/samples/BasicUsage/Program.cs -->
<!-- /coven:include -->
```

**Expanded Form:**
```markdown
<!-- coven:include src/samples/BasicUsage/Program.cs -->
```cs
using Coven.Core;

public class Program
{
    public static async Task Main()
    {
        var daemon = new CovenDaemon();
        await daemon.RunAsync();
    }
}
```
<!-- /coven:include -->
```

### 2. Line Range

**Syntax:**
```markdown
<!-- coven:include src/Coven.Core/CovenDaemon.cs#L45-L60 -->
<!-- /coven:include -->
```

Line numbers are 1-indexed. The range is inclusive on both ends.

**Variant — Single Line:**
```markdown
<!-- coven:include src/Coven.Core/CovenDaemon.cs#L42 -->
<!-- /coven:include -->
```

### 3. Region Markers

**Syntax:**
```markdown
<!-- coven:include src/toys/WindowingDemo/Demo.cs#region:BasicExample -->
<!-- /coven:include -->
```

**Source File Convention:**
```csharp
// In Demo.cs
public class Demo
{
    #region BasicExample
    public void ShowBasicUsage()
    {
        // This code appears in documentation
        var window = new SlidingWindow(maxTokens: 4096);
        window.Add(entry);
    }
    #endregion BasicExample
}
```

The `#region` and `#endregion` directives are **excluded** from expanded output.

**Language-Specific Region Markers:**

| Language | Start | End |
|----------|-------|-----|
| C#, C++  | `#region Name` | `#endregion` |
| Python   | `# region Name` | `# endregion` |
| JavaScript/TypeScript | `// #region Name` | `// #endregion` |
| HTML/XML | `<!-- region Name -->` | `<!-- endregion -->` |
| Rust     | `// region: Name` | `// endregion` |

### 4. Symbol Extraction

**Syntax:**
```markdown
<!-- coven:include src/Coven.Core/ITransmuter.cs#symbol:ITransmuter -->
<!-- /coven:include -->

<!-- coven:include src/Coven.Core/CovenDaemon.cs#symbol:CovenDaemon.ProcessAsync -->
<!-- /coven:include -->
```

Symbol extraction uses semantic analysis to locate:
- **Class/Interface/Struct/Enum** — entire type declaration
- **Method/Property** — full member including signature and body
- **Nested types** — `OuterClass.InnerClass`

**Implementation Note:** Symbol extraction requires language-aware parsing. For C#, this could use Roslyn. Initial implementation may support only C# with other languages added incrementally.

---

## Transformation Rules

### Expansion (Pre-Commit)

```
EXPAND(reference_block) →
    IF reference_block.expanded_content IS EMPTY:
        content ← RESOLVE(reference_block.reference_spec)
        language ← DETECT_LANGUAGE(reference_block.file_path)
        RETURN format_expanded(open_tag, content, language, close_tag)
    ELSE:
        content ← RESOLVE(reference_block.reference_spec)
        IF content ≠ reference_block.expanded_content.source_lines:
            RETURN format_expanded(open_tag, content, language, close_tag)
        ELSE:
            RETURN reference_block  (* unchanged *)
```

### Contraction (Post-Pull)

```
CONTRACT(reference_block) →
    RETURN open_tag + newline + close_tag
```

### Resolution

```
RESOLVE(reference_spec) →
    file ← READ_FILE(reference_spec.file_path)
    IF file IS NULL:
        ERROR "File not found: {file_path}"
    
    SWITCH reference_spec.fragment:
        CASE line_range(start, end):
            lines ← file.lines[start..end]
            IF end > file.line_count:
                ERROR "Line {end} exceeds file length ({file.line_count})"
            RETURN lines
            
        CASE region_ref(name):
            region ← FIND_REGION(file, name)
            IF region IS NULL:
                ERROR "Region '{name}' not found in {file_path}"
            RETURN region.content  (* excludes markers *)
            
        CASE symbol_ref(path):
            symbol ← FIND_SYMBOL(file, path)
            IF symbol IS NULL:
                ERROR "Symbol '{path}' not found in {file_path}"
            RETURN symbol.source
            
        CASE NULL:
            RETURN file.content
```

---

## Language Detection

Language is inferred from file extension for syntax highlighting:

```
DETECT_LANGUAGE(path) →
    extension ← path.split('.').last().lowercase()
    RETURN LANGUAGE_MAP.get(extension, "")

LANGUAGE_MAP = {
    "cs"     → "csharp",
    "fs"     → "fsharp",
    "js"     → "javascript",
    "ts"     → "typescript",
    "py"     → "python",
    "rs"     → "rust",
    "go"     → "go",
    "java"   → "java",
    "json"   → "json",
    "xml"    → "xml",
    "yaml"   → "yaml",
    "yml"    → "yaml",
    "md"     → "markdown",
    "sh"     → "bash",
    "ps1"    → "powershell",
    "sql"    → "sql",
}
```

---

## Error Handling

### Error Categories

| Error | Severity | Behavior |
|-------|----------|----------|
| File not found | **Fatal** | Block expansion, exit non-zero |
| Line range out of bounds | **Fatal** | Block expansion, exit non-zero |
| Region not found | **Fatal** | Block expansion, exit non-zero |
| Symbol not found | **Fatal** | Block expansion, exit non-zero |
| Malformed reference syntax | **Fatal** | Block expansion, exit non-zero |
| Circular include detected | **Fatal** | Block expansion, exit non-zero |

### Error Message Format

```
coven-include: ERROR in {markdown_file}:{line}
  Reference: {reference_spec}
  Problem: {description}
  
  Suggestions:
    - {actionable_suggestion}
```

**Example:**
```
coven-include: ERROR in docs/getting-started.md:42
  Reference: src/Coven.Core/Daemon.cs#region:QuickStart
  Problem: Region 'QuickStart' not found
  
  Suggestions:
    - Available regions in this file: BasicSetup, AdvancedConfig
    - Did you mean: #region:BasicSetup
```

### Strict vs. Lenient Modes

**Strict (Default):** Any unresolvable reference is a fatal error. Use for CI validation.

**Lenient:** Unresolvable references emit warnings and preserve existing expanded content (or leave empty). Use during development.

```bash
coven-include expand --mode=strict   # CI
coven-include expand --mode=lenient  # local dev
```

---

## Edge Cases

### Nested Includes

**Not Supported.** If an included file itself contains `<!-- coven:include -->` tags, they are treated as literal text and not processed.

Rationale: Nested includes create complexity (circular references, ordering dependencies) without proportional benefit. Keep it simple.

### Empty Regions/Symbols

If a region or symbol exists but is empty, expand to an empty code block:

```markdown
<!-- coven:include src/example.cs#region:Empty -->
```csharp
```
<!-- /coven:include -->
```

### Whitespace Normalization

- Leading/trailing blank lines in extracted content are **preserved**
- Indentation is **preserved exactly** as in source
- Trailing whitespace on lines is **stripped**

### Binary Files

Attempting to include binary files produces an error:

```
coven-include: ERROR in docs/assets.md:10
  Reference: assets/logo.png
  Problem: Cannot include binary file
```

### Files Outside Repository

References must be relative paths within the repository. Absolute paths or `../` escaping the repo root are rejected:

```
coven-include: ERROR in docs/example.md:5
  Reference: /etc/passwd
  Problem: Absolute paths not allowed
```

```
coven-include: ERROR in docs/example.md:5
  Reference: ../../other-repo/file.cs
  Problem: Path escapes repository root
```

---

## Implementation Approach

### Option A: Git Hooks

**Pre-commit hook:**
```bash
#!/bin/bash
# .git/hooks/pre-commit
coven-include expand --staged
git add -u  # re-stage modified files
```

**Post-merge hook:**
```bash
#!/bin/bash
# .git/hooks/post-merge
coven-include contract
```

**Pros:**
- Automatic, transparent to developers
- Works locally without CI

**Cons:**
- Hooks must be installed per-clone
- Can be bypassed with `--no-verify`

### Option B: CI Validation

**GitHub Actions workflow:**
```yaml
name: Validate Code References
on: [push, pull_request]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Expand references
        run: coven-include expand --mode=strict
        
      - name: Check for drift
        run: |
          if ! git diff --quiet; then
            echo "::error::Expanded code references are out of sync"
            git diff
            exit 1
          fi
```

**Pros:**
- Enforced on all contributions
- Cannot be bypassed
- Clear failure in PR checks

**Cons:**
- Requires manual expansion before push
- Slightly higher friction

### Recommended: Hybrid Approach

1. **Install hooks via setup script** for convenience
2. **CI validation** as authoritative check
3. **VS Code task** for manual expand/contract

```json
// .vscode/tasks.json
{
  "label": "Expand Code References",
  "type": "shell",
  "command": "coven-include",
  "args": ["expand", "--mode=lenient"],
  "problemMatcher": []
}
```

---

## Tool Implementation

### CLI Interface

```
coven-include <command> [options]

Commands:
  expand      Expand all references in markdown files
  contract    Contract all references (remove expanded content)
  validate    Check that expanded content matches sources
  check       Parse references and report any errors without modifying

Options:
  --mode=<strict|lenient>   Error handling mode (default: strict)
  --staged                  Only process git-staged files
  --path=<glob>             Process files matching pattern (default: **/*.md)
  --verbose                 Show detailed processing info
  --dry-run                 Show what would change without modifying
```

### Implementation Language

Recommended: **C#** (consistent with project)

Could leverage:
- Roslyn for C# symbol extraction
- Existing project infrastructure

Alternative: **Rust** or **Go** for single-binary distribution without runtime dependencies.

### Project Structure

```
src/Coven.IncludeTool/
├── Coven.IncludeTool.csproj
├── Program.cs
├── Grammar/
│   ├── ReferenceParser.cs
│   ├── ReferenceSpec.cs
│   └── Fragment.cs
├── Resolution/
│   ├── FileResolver.cs
│   ├── LineRangeResolver.cs
│   ├── RegionResolver.cs
│   └── SymbolResolver.cs
├── Languages/
│   ├── ILanguageSupport.cs
│   ├── CSharpLanguageSupport.cs
│   └── GenericLanguageSupport.cs
└── Commands/
    ├── ExpandCommand.cs
    ├── ContractCommand.cs
    └── ValidateCommand.cs
```

---

## Examples

### README with API Example

**Source file (`src/Coven.Core/ITransmuter.cs`):**
```csharp
namespace Coven.Core;

/// <summary>
/// Transforms entries from one form to another.
/// </summary>
public interface ITransmuter<TIn, TOut>
{
    TOut Transmute(TIn input);
}
```

**Markdown (contracted):**
```markdown
## Core Abstractions

The transmuter interface defines the fundamental transformation contract:

<!-- coven:include src/Coven.Core/ITransmuter.cs#symbol:ITransmuter -->
<!-- /coven:include -->
```

**Markdown (expanded, as committed):**
```markdown
## Core Abstractions

The transmuter interface defines the fundamental transformation contract:

<!-- coven:include src/Coven.Core/ITransmuter.cs#symbol:ITransmuter -->
```csharp
/// <summary>
/// Transforms entries from one form to another.
/// </summary>
public interface ITransmuter<TIn, TOut>
{
    TOut Transmute(TIn input);
}
```
<!-- /coven:include -->
```

### Tutorial with Step-by-Step Regions

**Source file (`src/toys/GettingStarted/Tutorial.cs`):**
```csharp
public class Tutorial
{
    #region Step1_CreateDaemon
    var daemon = DaemonBuilder.Create()
        .WithModel("gpt-4")
        .Build();
    #endregion Step1_CreateDaemon
    
    #region Step2_SendMessage
    var response = await daemon.SendAsync("Hello, world!");
    Console.WriteLine(response.Content);
    #endregion Step2_SendMessage
}
```

**Markdown:**
```markdown
## Getting Started

### Step 1: Create a Daemon

<!-- coven:include src/toys/GettingStarted/Tutorial.cs#region:Step1_CreateDaemon -->
<!-- /coven:include -->

### Step 2: Send a Message

<!-- coven:include src/toys/GettingStarted/Tutorial.cs#region:Step2_SendMessage -->
<!-- /coven:include -->
```

### Sample Project Inclusion

**Markdown:**
```markdown
## Complete Example

See the full working example:

<!-- coven:include src/samples/BasicChat/Program.cs -->
<!-- /coven:include -->

Run it with:
```bash
dotnet run --project src/samples/BasicChat
```
```

---

## Migration Path

### Phase 1: Tool Development
- Implement `coven-include` CLI with expand/contract/validate
- Support file inclusion and line ranges
- Add C# region support

### Phase 2: Documentation Update
- Convert existing code examples to references
- Add regions to `src/toys/` for documentation excerpts
- Set up CI validation

### Phase 3: Extended Support
- Add symbol extraction (requires Roslyn integration)
- Support additional languages for regions
- VS Code extension for in-editor preview

### Phase 4: Developer Experience
- Git hooks installation via `dotnet tool`
- Pre-commit integration guide
- Editor snippets for reference syntax

---

## Open Questions

1. **Should expansion preserve manual formatting?**
   If someone manually adjusts indentation in expanded content, should that be preserved or overwritten?
   
   *Proposed answer:* Always overwrite. The source file is authoritative.

2. **Should we support query parameters for transformation?**
   Example: `#symbol:Method?strip-comments=true`
   
   *Proposed answer:* Not initially. Keep syntax simple. Add if clear need emerges.

3. **How to handle merge conflicts in expanded content?**
   
   *Proposed answer:* Contract before merge, expand after. The reference line is the merge unit, not the expanded content.

4. **Should the tool be a dotnet tool or standalone binary?**
   
   *Proposed answer:* Dotnet tool for consistency with project, with potential standalone release later.

---

## References

- [guidelines/documentation.md](../guidelines/documentation.md) — Documentation standards
- [Literate programming](https://en.wikipedia.org/wiki/Literate_programming) — Related concept
- [mdBook preprocessors](https://rust-lang.github.io/mdBook/format/configuration/preprocessors.html) — Similar approach in Rust ecosystem
- [Docusaurus code blocks](https://docusaurus.io/docs/markdown-features/code-blocks) — Prior art in JS ecosystem
