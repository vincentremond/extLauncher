namespace extLauncher

open System
open System.ComponentModel
open Spectre.Console
open Spectre.Console.Cli

[<AutoOpen>]
module private Implementations =
    open System.Diagnostics
    type Path = System.IO.Path

    let markup value =
        AnsiConsole.MarkupLineInterpolated value

    let notInitialized () =
        markup $"Folder not yet indexed: [yellow]{IO.AppName}[/] index [gray]--help[/]"
        1

    let run (file: File) launcher =
        markup $"""Launching [green]{file.Name.value}[/] [gray]{file.Path.value}[/]..."""
        let file = file |> File.triggered |> Db.updateFile

        let timeout = TimeSpan.FromSeconds(2.0)

        let psi =
            match launcher with
            | None ->
                let containingFolder = file.Path.folder

                let psi = ProcessStartInfo file.Path.value
                psi.UseShellExecute <- true
                psi.WorkingDirectory <- containingFolder.value
                psi
            | Some launcher ->
                let path, workingDirectory =
                    match launcher.Choose with
                    | Choose.File -> file.Path.value, file.Path.folder.value
                    | Choose.Directory ->
                        let dir = file.Path.folder.value
                        dir, dir

                let psi = ProcessStartInfo launcher.Path.value
                psi.Arguments <- Launcher.buildArgs launcher path
                psi.WorkingDirectory <- workingDirectory
                psi

        let p = Process.Start psi
        p.WaitForExit(timeout) |> ignore

    let chooseLauncher folder file =
        match folder.Launchers with
        | [||] -> run file None
        | [| launcher |] -> run file (Some launcher)
        | launchers ->
            Helpers.searchByName launchers _.Name
            |> Console.prompt Console.Terminal "With which launcher?" Launcher.name 10
            |> function
                | Some launcher -> run file (Some launcher)
                | None -> ()

    let filePrompt (file: File) : string =
        $"""[white]%s{file.Name.value}[/]  [gray](%s{file.Path.folder.value})[/]"""

    let prompt folder =
        folder
        |> App.makeSearcher
        |> Console.prompt Console.Terminal "Search and launch:" filePrompt 10
        |> Option.iter (chooseLauncher folder)

    let withLoader<'T> (worker: StatusContext -> 'T) =
        AnsiConsole.Status().Start("Indexing...", worker)

    let currentPath = FolderPath Environment.CurrentDirectory

    let findFolder () =
        let rec find path =
            match Db.findFolder path with
            | Some f -> Some f
            | None ->
                let parentFolder =
                    (path.value |> Path.GetDirectoryName |> Option.ofObj |> (Option.map FolderPath))

                match parentFolder with
                | None -> None
                | Some p -> find p

        find currentPath

    let toCount str num =
        if num > 1 then $"%i{num} %s{str}s" else $"%i{num} %s{str}"

    let renderArgs args =
        match args with
        | None -> "-"
        | Some s -> s |> Markup.Escape |> _.Replace("%s", "[white bold]%s[/]")

    let printLaunchers (folder: Folder) =
        let launchers =
            Table()
                .AddColumns(
                    [|
                        "Name"
                        "Choose"
                        "Path"
                        "Arguments"
                    |]
                )

        launchers.Border <- TableBorder.Minimal

        for l in folder.Launchers do
            launchers.AddRow(
                [|
                    l.Name.EscapeMarkup()
                    string l.Choose
                    l.Path.value.EscapeMarkup()
                    renderArgs l.Arguments
                |]
            )
            |> ignore

        AnsiConsole.Write launchers

type PromptCommand() =
    inherit Command()

    override _.Execute(_context, _cancellationToken) =
        findFolder ()
        |> Option.map (prompt >> fun () -> 0)
        |> Option.defaultWith notInitialized

type IndexSettings() =
    inherit CommandSettings()

    [<CommandArgument(0, "<pattern>")>]
    [<Description "The search string to match against the file names.">]
    member val Pattern = "" with get, set

    [<CommandOption "-r|--regex">]
    [<Description "If set then the pattern is a regular expression, otherwise it's a combination of valid literal path and wildcard (* and ?) characters.">]
    member val IsRegex = false with get, set

    [<CommandOption "-i|--ignore-folder">]
    [<Description "Folders to ignore during indexing.">]
    member val FoldersToIgnore: string array = [||] with get, set

type IndexCommand() =
    inherit Command<IndexSettings>()

    override _.Execute(_context, settings, _cancellationToken) =
        (fun _ ->
            App.index IO.getFiles Db.upsertFolder {
                Path = currentPath
                Pattern = Pattern.init settings.Pattern settings.IsRegex
                FoldersToIgnore = settings.FoldersToIgnore |> Array.map FolderPath.mk
                Launchers = Array.empty
            }
        )
        |> withLoader
        |> function
            | Some folder ->
                markup $"""{toCount "file" folder.Files.Length} indexed."""
                markup $"Start to search and launch: [yellow]{IO.AppName}[/]"
                markup $"Add a specific launcher: [yellow]{IO.AppName}[/] launcher [gray]--help[/]"
                0
            | None ->
                markup $"{Console.NoMatch}"
                -1

type LauncherSettings() =
    inherit CommandSettings()

    [<CommandArgument(0, "<name>")>]
    [<Description "Name of the launcher.">]
    member val Name = "" with get, set

type SetLauncherSettings() =
    inherit LauncherSettings()

    [<CommandArgument(0, "<path>")>]
    [<Description "Launcher full path or launcher filename in the env path.">]
    member val Path = "" with get, set

    [<CommandOption "-a|--args">]
    [<Description "Launcher command line arguments. Default is '%s' where the launched file or directory path will be inserted.">]
    [<DefaultValue("%s")>]
    member val Arguments = "%s" with get, set

    [<CommandOption "-c|--choose">]
    [<Description "Which should be launched, the 'file' [italic](default)[/] or the 'directory'?">]
    member val Choose = Choose.File with get, set

type RemoveLauncherSettings() =
    inherit LauncherSettings()

type SetLauncherCommand() =
    inherit Command<SetLauncherSettings>()

    override _.Execute(_, settings, _cancellationToken) =
        match findFolder () with
        | None -> notInitialized ()
        | Some folder ->
            markup $"[teal]{settings.Name}[/] launcher updated."

            {
                Name = settings.Name
                Path = FilePath settings.Path
                Arguments =
                    match settings.Arguments with
                    | x when String.IsNullOrWhiteSpace(x) -> None
                    | args -> Some args
                Choose = settings.Choose
            }
            |> fun launcher ->
                match folder.Launchers |> Array.tryFindIndex (fun l -> l.Name = launcher.Name) with
                | Some index ->
                    folder.Launchers[index] <- launcher
                    folder
                | None -> { folder with Launchers = Array.insertAt 0 launcher folder.Launchers }
                |> Db.upsertFolder
                |> printLaunchers

            0

    interface ICommandLimiter<LauncherSettings>

type RemoveLauncherCommand() =
    inherit Command<RemoveLauncherSettings>()

    override _.Execute(_, settings, _cancellationToken) =
        match findFolder () with
        | None -> notInitialized ()
        | Some folder ->
            match folder.Launchers |> Array.tryFindIndex (fun l -> l.Name = settings.Name) with
            | Some index ->
                markup $"[green]{settings.Name}[/] launcher removed."

                { folder with Launchers = Array.removeAt index folder.Launchers }
                |> Db.upsertFolder
                |> printLaunchers

                0
            | None ->
                markup $"[green]{settings.Name}[/] launcher not found."
                printLaunchers folder
                0

    interface ICommandLimiter<LauncherSettings>

type DeindexCommand() =
    inherit Command()

    override _.Execute(_context, _cancellationToken) =
        match Db.findFolder currentPath with
        | None -> notInitialized ()
        | Some folder ->
            Db.deleteFolder folder.Path
            markup $"Deindexed"
            0

type InfoCommand() =
    inherit Command()

    override _.Execute(_context, _cancellationToken) =
        match findFolder () with
        | None -> notInitialized ()
        | Some folder ->
            markup $"[teal]Path:[/]"
            markup $"  {folder.Path.value}"
            markup $""
            markup $"[teal]Pattern:[/]"
            markup $"  {folder.Pattern.value}"
            markup $""
            markup $"[teal]Folders to ignore:[/]"

            if Array.isEmpty folder.FoldersToIgnore then
                markup $"  -"
            else
                for folderToIgnore in folder.FoldersToIgnore do
                    markup $"  {folderToIgnore.value}"

            markup $""
            markup $"[teal]Launchers:[/]"

            if Array.isEmpty folder.Launchers then
                markup $"  -"
                markup $""
            else
                printLaunchers folder

            markup $"[teal]Indexed files:[/]"

            let files =
                Table()
                    .AddColumns(
                        [|
                            "Name"
                            "Triggered"
                            "Path"
                        |]
                    )

            files.Border <- TableBorder.Minimal

            for f in folder.Files do
                let path = f.Path.value.Remove(0, folder.Path.value.Length)

                files.AddRow(
                    [|
                        f.Name.value
                        string f.Triggered
                        path
                    |]
                )
                |> ignore

            AnsiConsole.Write files

            0

type RefreshCommand() =
    inherit Command()

    override _.Execute(_context, _cancellationToken) =
        match findFolder () with
        | None -> notInitialized ()
        | Some folder ->
            fun _ -> folder |> App.refresh IO.getFiles Db.upsertFolder
            |> withLoader
            |> Option.iter prompt

            0

module Program =

    [<EntryPoint>]
    let main args =
        let app = CommandApp<PromptCommand>()

        app.Configure(fun conf ->
            conf.SetApplicationName(IO.AppName) |> ignore

            conf
                .AddCommand<PromptCommand>("prompt")
                .WithDescription(
                    "[italic](default command)[/] Type to search. Arrows Up/Down to navigate. Enter to launch. Escape to quit."
                )
            |> ignore

            conf
                .AddCommand<IndexCommand>("index")
                .WithDescription(
                    "Indexes all files recursively with a specific pattern which can be a wildcard [italic](default)[/] or a regular expression."
                )
            |> ignore

            conf.AddBranch<LauncherSettings>(
                "launcher",
                fun launcher ->
                    launcher.SetDescription("Add, update or remove a launcher [italic](optional)[/].")

                    launcher.AddCommand<SetLauncherCommand>("set").WithDescription("Add or update a launcher.")
                    |> ignore

                    launcher.AddCommand<RemoveLauncherCommand>("remove").WithDescription("Remove a launcher.")
                    |> ignore
            )
            |> ignore

            conf.AddCommand<DeindexCommand>("deindex").WithDescription("Clears the current index.")
            |> ignore

            conf
                .AddCommand<InfoCommand>("info")
                .WithDescription("Prints the current pattern and all the indexed files.")
            |> ignore

            conf.AddCommand<RefreshCommand>("refresh").WithDescription("Updates the current index.")
            |> ignore

            conf.AddExample(
                [|
                    "index"
                    "*.sln"
                |]
            )
            |> ignore

            conf.AddExample(
                [|
                    "index"
                    "\"(.*)[.](fs|cs)proj$\""
                    "--regex"
                |]
            )
            |> ignore

            conf.AddExample(
                [|
                    "launcher"
                    "mylauncher"
                    "set"
                    "execpath"
                |]
            )
            |> ignore

            conf.AddExample(
                [|
                    "launcher"
                    "mylauncher"
                    "remove"
                |]
            )
            |> ignore

            conf.AddExample(
                [|
                    "launcher"
                    "vscode"
                    "set"
                    "/usr/bin/code"
                    "--choose"
                    "file"
                    "--args=\"-r %s\""
                |]
            )
            |> ignore

            conf.AddExample(
                [|
                    "launcher"
                    "vscode"
                    "set"
                    @"""$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd"""
                    "--choose"
                    "directory"
                |]
            )
            |> ignore

            conf.AddExample(
                [|
                    "launcher"
                    "explorer"
                    "set"
                    "explorer.exe"
                    "--choose"
                    "directory"
                |]
            )
            |> ignore

#if DEBUG
            conf.ValidateExamples() |> ignore
#endif
        )

        app.Run args
