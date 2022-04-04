<br />

<p align="center">
    <img src="https://raw.githubusercontent.com/d-edge/extLauncher/main/assets/logo.png" alt="extLauncher logo" height="140">
</p>

<p align="center">
<a href="https://github.com/d-edge/extLauncher/actions" title="actions"><img src="https://github.com/d-edge/extLauncher/actions/workflows/build.yml/badge.svg?branch=main" alt="actions build" /></a>
<a href="https://github.com/d-edge/extLauncher/blob/main/LICENSE" title="license"><img src="https://img.shields.io/github/license/d-edge/extLauncher" alt="license" /></a>
</p>

<!--
    <a href="https://www.nuget.org/packages/extLauncher/" title="nuget"><img src="https://img.shields.io/nuget/vpre/extLauncher" alt="version" /></a>
    <a href="https://www.nuget.org/stats/packages/extLauncher?groupby=Version" title="stats"><img src="https://img.shields.io/nuget/dt/extLauncher" alt="download" /></a> -->

<p align="center">
    <img src="https://raw.githubusercontent.com/d-edge/extLauncher/main/assets/terminal.gif" alt="extLauncher terminal">
</p>

<br />

extLauncher is a dotnet tool to search and launch quickly projects in the user's preferred application. extLauncher is maintained by folks at [D-EDGE](https://www.d-edge.com/).

# Getting Started

Install extLauncher as a global dotnet tool

``` bash
dotnet tool install extLauncher -g
``` 

or as a dotnet local tool

``` bash
dotnet new tool-manifest
dotnet tool install extLauncher
```` 

# Usage

```
USAGE:
    extLauncher [OPTIONS]

EXAMPLES:
    extLauncher index *.sln
    extLauncher index "(.*)[.](fs|cs)proj$" --regex
    extLauncher launcher mylauncher set execpath
    extLauncher launcher mylauncher remove
    extLauncher launcher vscode set /usr/bin/code --choose file --args="-r %s"
    extLauncher launcher vscode set "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd" --choose directory
    extLauncher launcher explorer set explorer.exe --choose directory

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    prompt             (default command) Type to search. Arrows Up/Down to navigate. Enter to launch. Escape to quit
    index <pattern>    Indexes all files recursively with a specific pattern which can be a wildcard (default) or a regular expression
    launcher <name>    Add, update or remove a launcher (optional)
    deindex            Clears the current index
    info               Prints the current pattern and all the indexed files
    refresh            Updates the current index
```

# Build locally

- Clone the repository
- Open the repository
- Invoke the tool by running the `dotnet tool run` command: `dotnet tool run extLauncher` (with your arguments)

# Caches and data generated by extLauncher

This tool maintains a database to improve its performance. You should be able to find it in the most obvious place for your operating system:

- Windows: `%appdata%\Roaming\extLauncher\extLauncher.db`
- Linux: `~/.config/extLauncher/extLauncher.db`

# License

[MIT](https://github.com/d-edge/extLauncher/blob/main/LICENSE)
