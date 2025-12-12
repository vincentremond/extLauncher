module extLauncher.AppTests

open Swensen.Unquote
open Xunit

[<Fact>]
let ``should load a folder`` () =
    let folderPath = FolderPath.mk "/test"
    let pattern = "*.ext"

    let folder =
        let loadFiles _ _ _ = [|
            FilePath "/test/file2.ext", FileName "file2"
            FilePath "/test/file1.ext", FileName "file1"
        |]

        App.loadFolder loadFiles {
            Path = folderPath
            Pattern = Pattern.init pattern false
            FoldersToIgnore = [||]
            Launchers = Array.empty
        }

    folder
    =! Some {
        Path = folderPath
        Pattern = Pattern.init pattern false
        FoldersToIgnore = [||]
        Files = [|
            File.create (FilePath "/test/file1.ext") (FileName "file1")
            File.create (FilePath "/test/file2.ext") (FileName "file2")
        |]
        Launchers = Array.empty
    }

[<Fact>]
let ``should not load a folder if no result`` () =
    let folder =
        let loadFiles _ _ _ = Array.empty

        App.loadFolder loadFiles {
            Path = FolderPath.mk "/test"
            Pattern = Pattern.init "" false
            FoldersToIgnore = [||]
            Launchers = Array.empty
        }

    folder =! None

[<Fact>]
let ``refresh should synchronize files`` () =
    let newFolder =
        let loadFiles _ _ _ = [|
            FilePath "file1", FileName ""
            FilePath "file3", FileName ""
        |]

        let save = id

        {
            Path = FolderPath.mk "/test"
            Pattern = Pattern.init "" false
            FoldersToIgnore = [||]
            Files = [|
                File.create (FilePath "file1") (FileName "")
                File.create (FilePath "file2") (FileName "")
            |]
            Launchers = Array.empty
        }
        |> App.refresh loadFiles save

    newFolder.Files[0].Path.value =! "file1"
    newFolder.Files[1].Path.value =! "file3"

[<Fact>]
let ``refresh should keep triggers`` () =
    let newFolder =
        let loadFiles _ _ _ = [|
            FilePath "file1", FileName ""
            FilePath "file2", FileName ""
        |]

        let save = id

        {
            Path = FolderPath.mk "/test"
            Pattern = Pattern.init "" false
            FoldersToIgnore = [||]
            Files = [|
                File.create (FilePath "file1") (FileName "") |> File.triggered
                File.create (FilePath "file2") (FileName "")
            |]
            Launchers = Array.empty
        }
        |> App.refresh loadFiles save

    newFolder.Files[0].Triggered =! 1
    newFolder.Files[1].Triggered =! 0

[<Fact>]
let ``should filter out files in ignored folders when loading`` () =
    let folderPath = FolderPath.mk "/test"
    let ignoredFolder = FolderPath.mk "/test/ignored"
    let pattern = "*.ext"

    let folder =
        let loadFiles _ foldersToIgnore _ =
            // Simulate filtering: return files that are NOT in ignored folders
            let allFiles = [|
                FilePath "/test/file1.ext", FileName "file1"
                FilePath "/test/ignored/file2.ext", FileName "file2"
                FilePath "/test/other/file3.ext", FileName "file3"
            |]

            allFiles
            |> Array.filter (fun (filePath: FilePath, _) ->
                not (
                    foldersToIgnore
                    |> Array.exists (fun (folderToIgnore: FolderPath) ->
                        filePath.value.StartsWith(
                            folderToIgnore.value,
                            System.StringComparison.CurrentCultureIgnoreCase
                        )
                    )
                )
            )

        App.loadFolder loadFiles {
            Path = folderPath
            Pattern = Pattern.init pattern false
            FoldersToIgnore = [| ignoredFolder |]
            Launchers = Array.empty
        }

    folder
    =! Some {
        Path = folderPath
        Pattern = Pattern.init pattern false
        FoldersToIgnore = [| ignoredFolder |]
        Files = [|
            File.create (FilePath "/test/file1.ext") (FileName "file1")
            File.create (FilePath "/test/other/file3.ext") (FileName "file3")
        |]
        Launchers = Array.empty
    }

[<Fact>]
let ``should include all files when no folders are ignored`` () =
    let folderPath = FolderPath.mk "/test"
    let pattern = "*.ext"

    let folder =
        let loadFiles _ _ _ = [|
            FilePath "/test/file1.ext", FileName "file1"
            FilePath "/test/ignored/file2.ext", FileName "file2"
            FilePath "/test/other/file3.ext", FileName "file3"
        |]

        App.loadFolder loadFiles {
            Path = folderPath
            Pattern = Pattern.init pattern false
            FoldersToIgnore = [||]
            Launchers = Array.empty
        }

    folder
    =! Some {
        Path = folderPath
        Pattern = Pattern.init pattern false
        FoldersToIgnore = [||]
        Files = [|
            File.create (FilePath "/test/file1.ext") (FileName "file1")
            File.create (FilePath "/test/ignored/file2.ext") (FileName "file2")
            File.create (FilePath "/test/other/file3.ext") (FileName "file3")
        |]
        Launchers = Array.empty
    }

[<Fact>]
let ``should filter out files in multiple ignored folders`` () =
    let folderPath = FolderPath.mk "/test"
    let ignoredFolder1 = FolderPath.mk "/test/ignored1"
    let ignoredFolder2 = FolderPath.mk "/test/ignored2"
    let pattern = "*.ext"

    let folder =
        let loadFiles _ foldersToIgnore _ =
            let allFiles = [|
                FilePath "/test/file1.ext", FileName "file1"
                FilePath "/test/ignored1/file2.ext", FileName "file2"
                FilePath "/test/ignored2/file3.ext", FileName "file3"
                FilePath "/test/other/file4.ext", FileName "file4"
            |]

            allFiles
            |> Array.filter (fun (filePath: FilePath, _) ->
                not (
                    foldersToIgnore
                    |> Array.exists (fun (folderToIgnore: FolderPath) ->
                        filePath.value.StartsWith(
                            folderToIgnore.value,
                            System.StringComparison.CurrentCultureIgnoreCase
                        )
                    )
                )
            )

        App.loadFolder loadFiles {
            Path = folderPath
            Pattern = Pattern.init pattern false
            FoldersToIgnore = [|
                ignoredFolder1
                ignoredFolder2
            |]
            Launchers = Array.empty
        }

    folder
    =! Some {
        Path = folderPath
        Pattern = Pattern.init pattern false
        FoldersToIgnore = [|
            ignoredFolder1
            ignoredFolder2
        |]
        Files = [|
            File.create (FilePath "/test/file1.ext") (FileName "file1")
            File.create (FilePath "/test/other/file4.ext") (FileName "file4")
        |]
        Launchers = Array.empty
    }

[<Fact>]
let ``should filter out files in subdirectories of ignored folders`` () =
    let folderPath = FolderPath.mk "/test"
    let ignoredFolder = FolderPath.mk "/test/ignored"
    let pattern = "*.ext"

    let folder =
        let loadFiles _ foldersToIgnore _ =
            let allFiles = [|
                FilePath "/test/file1.ext", FileName "file1"
                FilePath "/test/ignored/sub/file2.ext", FileName "file2"
                FilePath "/test/ignored/sub/deep/file3.ext", FileName "file3"
            |]

            allFiles
            |> Array.filter (fun (filePath: FilePath, _) ->
                not (
                    foldersToIgnore
                    |> Array.exists (fun (folderToIgnore: FolderPath) ->
                        filePath.value.StartsWith(
                            folderToIgnore.value,
                            System.StringComparison.CurrentCultureIgnoreCase
                        )
                    )
                )
            )

        App.loadFolder loadFiles {
            Path = folderPath
            Pattern = Pattern.init pattern false
            FoldersToIgnore = [| ignoredFolder |]
            Launchers = Array.empty
        }

    folder
    =! Some {
        Path = folderPath
        Pattern = Pattern.init pattern false
        FoldersToIgnore = [| ignoredFolder |]
        Files = [| File.create (FilePath "/test/file1.ext") (FileName "file1") |]
        Launchers = Array.empty
    }

[<Fact>]
let ``refresh should filter out files in ignored folders`` () =
    let ignoredFolder = FolderPath.mk "/test/ignored"

    let newFolder =
        let loadFiles _ foldersToIgnore _ =
            let allFiles = [|
                FilePath "file1", FileName ""
                FilePath "/test/ignored/file2", FileName ""
                FilePath "file3", FileName ""
            |]

            allFiles
            |> Array.filter (fun (filePath: FilePath, _) ->
                not (
                    foldersToIgnore
                    |> Array.exists (fun (folderToIgnore: FolderPath) ->
                        filePath.value.StartsWith(
                            folderToIgnore.value,
                            System.StringComparison.CurrentCultureIgnoreCase
                        )
                    )
                )
            )

        let save = id

        {
            Path = FolderPath.mk "/test"
            Pattern = Pattern.init "" false
            FoldersToIgnore = [| ignoredFolder |]
            Files = [| File.create (FilePath "file1") (FileName "") |]
            Launchers = Array.empty
        }
        |> App.refresh loadFiles save

    newFolder.Files.Length =! 2
    newFolder.Files[0].Path.value =! "file1"
    newFolder.Files[1].Path.value =! "file3"
