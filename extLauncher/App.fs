module extLauncher.App

type FolderConf = {
    Path: FolderPath
    Pattern: Pattern
    FoldersToIgnore: FolderPath array
    Launchers: Launcher array
}

let loadFolder (loadFiles: LoadFiles) conf : Folder option =
    loadFiles conf.Path conf.FoldersToIgnore conf.Pattern
    |> Array.map ((<||) File.create)
    |> Array.sort
    |> function
        | [||] -> None
        | files ->
            {
                Path = conf.Path
                Pattern = conf.Pattern
                FoldersToIgnore = conf.FoldersToIgnore
                Files = files
                Launchers = conf.Launchers
            }
            |> Some

let index (loadFiles: LoadFiles) save (conf: FolderConf) : Folder option =
    loadFolder loadFiles conf |> Option.map save

let refresh (loadFiles: LoadFiles) save (folder: Folder) : Folder option =

    let newFiles =
         loadFiles folder.Path folder.FoldersToIgnore folder.Pattern |> Array.map ((<||) File.create)

    let currentFiles = folder.Files |> Array.map (fun f -> f.Path, f) |> Map

    newFiles
    |> Array.map (fun newFile ->
        match currentFiles.TryFind newFile.Path with
        | Some current -> { newFile with Triggered = current.Triggered }
        | None -> newFile
    )
    |> fun files -> { folder with Files = files }
    |> save
    |> Some

let makeSearcher folder str =
    Helpers.searchByName folder.Files (fun f -> f.Name.value) str |> Array.sort
