# extLauncher – Copilot Instructions

## Build, Test & Run

```bash
# Build (restores tools first)
./build.ps1
# or
dotnet build

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter ClassName=DomainTests
dotnet test --filter ClassName=AppTests
dotnet test --filter ClassName=ConsoleTests

# Run a single test by name
dotnet test --filter "Name=<TestMethodName>"

# Run the app locally
dotnet run --project extLauncher -- index *.sln
```

## Architecture

The project is a layered hexagonal architecture. File compilation order in the `.fsproj` reflects the dependency direction — each layer depends only on what's above it:

```
Domain.fs   → core types only, no logic
App.fs      → pure business logic, no I/O
Console.fs  → terminal UI abstraction (ITerminal interface + interactive prompt)
Infra.fs    → file system I/O (module IO) and LiteDB persistence (module Db)
Program.fs  → Spectre.Console.Cli command definitions and entry point
```

Tests mirror this structure: `DomainTests.fs`, `AppTests.fs`, `ConsoleTests.fs`.

## Key Conventions

### Single-case discriminated unions for type safety
Primitive values are wrapped to prevent accidental misuse:
```fsharp
type FileName = FileName of string
type FilePath = FilePath of string
type FolderPath = private FolderPath of string  // private constructor, use FolderPath.mk()
```

### Smart constructors as static members
When construction requires normalization or validation, use a static `mk` or `from`:
```fsharp
static member mk(path: string) = FolderPath <| if Path.IsPathRooted path then path else Path.GetFullPath path
static member from value isRegex = if isRegex then RegexPattern value else WildcardPattern value
```

### Dependency injection via higher-order functions
Pure functions receive I/O dependencies as function arguments (not interfaces, not DI containers):
```fsharp
type LoadFiles = FolderPath -> FolderPath array -> Pattern -> LoadFilesResult array
let index (loadFiles: LoadFiles) save (conf: FolderConf) : Folder option = ...
```

### ITerminal interface for testable console I/O
All console operations go through `ITerminal`. Tests inject a mock that feeds a `Queue<TerminalKey>` and captures output — never call `System.Console` directly in logic code.

### Custom equality and comparison on `File`
`File` uses `[<CustomEquality; CustomComparison>]`: equality is by `Path` only; sort order is trigger count descending, then name ascending. Don't derive structural equality on this type.

### LiteDB persistence layer
DB types (`FolderDb`, `FileDb`, etc.) in `Infra.fs` require `[<CLIMutable>]` for BSON serialization. Each has `fromDomain` / `toDomain` static conversions. Domain types never leak into DB types and vice versa.

### Compiler strictness
`FS0025` (incomplete pattern matches) is configured as an error. All `match` expressions must be exhaustive.

### Formatting (fantomas via EditorConfig)
- `fsharp_multiline_bracket_style = stroustrup`
- `fsharp_bar_before_discriminated_union_declaration = true`
- `fsharp_max_record_number_of_items = 1` (one field per line)
- `fsharp_max_array_or_list_number_of_items = 1` (one item per line)

## Dependencies

- **Spectre.Console / Spectre.Console.Cli** — rich terminal output and CLI command parsing
- **LiteDB** — embedded file-based NoSQL database (stored in `%appdata%/extLauncher/`)
- **FsCheck.Xunit + Unquote** — property-based tests and F#-idiomatic assertions
- Managed with **Paket** (`paket.dependencies` / `paket.lock`)
