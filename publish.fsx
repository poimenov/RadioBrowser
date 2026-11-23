// Скрипт для сборки RadioBrowser
// Использование: dotnet fsi publish.fsx

#r "System.Formats.Tar"

open System
open System.IO
open System.IO.Compression
open System.Formats.Tar
open System.Xml.Linq

// ============ КОНФИГУРАЦИЯ ПРОЕКТА ============
let projectPath = "./src/RadioBrowser/RadioBrowser.fsproj"
let projectName = "RadioBrowser"

// Пути к плагинам
let pluginSourceDirs = [ "./src/RadioBrowser.Z3FmPlugin" ]

// Выходные директории
let outputPath = "./artifacts"
let publishDir = Path.Combine(outputPath, "publish")

// Платформы для сборки
let runtimeIdentifiers = [ "win-x64"; "linux-x64" ]

// ============ ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ============
let writeColor color message =
    let originalColor = Console.ForegroundColor
    Console.ForegroundColor <- color
    printfn $"{message}"
    Console.ForegroundColor <- originalColor

let writeInfo = writeColor ConsoleColor.Cyan
let writeSuccess = writeColor ConsoleColor.Green
let writeWarning = writeColor ConsoleColor.Yellow
let writeError = writeColor ConsoleColor.Red

let runDotnetCommand args =
    let psi = System.Diagnostics.ProcessStartInfo()
    psi.FileName <- "dotnet"
    psi.Arguments <- String.concat " " args
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true

    use proc = System.Diagnostics.Process.Start psi
    let output = proc.StandardOutput.ReadToEnd()
    let error = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        (false, error)
    else
        (true, output)

let rec deleteDirectorySafe dir =
    if Directory.Exists dir then
        try
            Directory.Delete(dir, true)
            true
        with ex ->
            writeWarning $"  Не удалось удалить {dir}: {ex.Message}"
            false
    else
        true

let rec copyDirectoryRecursive src dest =
    if not (Directory.Exists dest) then
        Directory.CreateDirectory dest |> ignore

    Directory.GetFiles(src)
    |> Array.iter (fun file ->
        let destFile = Path.Combine(dest, Path.GetFileName file)
        File.Copy(file, destFile, true))

    Directory.GetDirectories(src)
    |> Array.iter (fun dir ->
        let destDir = Path.Combine(dest, Path.GetFileName dir)
        copyDirectoryRecursive dir destDir)

let findPluginProjects (dirs: string list) =
    dirs
    |> List.collect (fun dir ->
        if Directory.Exists dir then
            Directory.GetFiles(dir, "*.*sproj", SearchOption.AllDirectories) |> Array.toList
        else
            [])

// ============ 1. ПРОВЕРКА ПРОЕКТА ============
writeInfo "Проверка наличия проекта..."

if not (File.Exists projectPath) then
    writeError $"Проект не найден: {projectPath}"
    writeError "Убедитесь, что вы находитесь в корневой директории решения"
    exit 1

// Проверяем наличие плагинов
let pluginProjects = findPluginProjects pluginSourceDirs

if pluginProjects.IsEmpty then
    writeWarning "Проекты плагинов не найдены!"
    writeWarning "Сборка будет продолжена без плагинов"
else
    writeInfo $"Найдено {pluginProjects.Length} проектов плагинов:"

    pluginProjects
    |> List.iter (fun p -> writeInfo $"  - {Path.GetFileName(Path.GetDirectoryName(p))}")

// ============ 2. ПОЛУЧЕНИЕ ВЕРСИИ ============
writeInfo "\nПолучение версии приложения..."

let getVersionFromProject () =
    try
        let doc = XDocument.Load projectPath

        let versionNode =
            doc.Descendants() |> Seq.tryFind (fun n -> n.Name.LocalName = "Version")

        match versionNode with
        | Some node ->
            let version = node.Value.Trim()
            writeSuccess $"Версия: {version}"
            version
        | None ->
            writeWarning "Версия не найдена, используется 1.0.0"
            "1.0.0"
    with ex ->
        writeWarning $"Ошибка чтения версии: {ex.Message}. Используется 1.0.0"
        "1.0.0"

let version = getVersionFromProject ()

// ============ 3. ОЧИСТКА ============
writeInfo "\nОчистка временных файлов..."

let deleteBuildArtifacts () =
    let currentDir = Directory.GetCurrentDirectory()

    let results =
        Directory.GetDirectories(currentDir, "*", SearchOption.AllDirectories)
        |> Array.choose (fun dir ->
            let dirName = Path.GetFileName dir

            if dirName = "bin" || dirName = "obj" then
                if deleteDirectorySafe dir then
                    None
                else
                    Some(sprintf "Не удалось удалить: %s" dir)
            else
                None)

    // Выводим результаты очистки
    if results.Length > 0 then
        results |> Array.iter writeInfo

deleteBuildArtifacts ()

// Очищаем папку с артефактами
writeInfo "\nОчистка папки артефактов..."

if Directory.Exists outputPath then
    if deleteDirectorySafe outputPath then
        writeSuccess "  Папка артефактов удалена"
    else
        writeError "  Не удалось удалить папку артефактов"
        writeError "  Возможно, файлы используются другим процессом"
        writeError "  Попробуйте закрыть программы, использующие файлы в artifacts/"
        exit 1

Directory.CreateDirectory outputPath |> ignore
Directory.CreateDirectory publishDir |> ignore

// ============ 4. СБОРКА ПЛАГИНОВ ============
let buildPluginsForRuntime (rid: string) (outputDir: string) =
    if pluginProjects.IsEmpty then
        writeWarning "Нет проектов плагинов для сборки"
        true
    else
        writeInfo $"\nСборка плагинов для {rid}..."

        let pluginsRuntimeDir = Path.Combine(outputDir, "Plugins")
        Directory.CreateDirectory pluginsRuntimeDir |> ignore

        let allSuccess =
            pluginProjects
            |> List.map (fun pluginProject ->
                let pluginDir = Path.GetDirectoryName(pluginProject)
                let pluginName = Path.GetFileName(pluginDir)
                writeInfo $"  Сборка {pluginName} для {rid}..."

                let args =
                    [ "build"
                      pluginProject
                      "-c"
                      "Release"
                      "-r"
                      rid
                      "-o"
                      pluginsRuntimeDir
                      "/p:DebugType=None"
                      "/p:DebugSymbols=false"
                      "/p:Platform=AnyCPU" ]

                let success, output = runDotnetCommand args

                if not success then
                    writeError $"  Ошибка сборки {pluginName}:"
                    writeError $"    {output}"
                    false
                else
                    writeSuccess $"  {pluginName} собран успешно"
                    true)

        // После сборки всех плагинов удаляем PluginContract файлы
        let filesToDelete =
            Directory.GetFiles(pluginsRuntimeDir, "RadioBrowser.PluginContract.*")
            |> Array.toList

        filesToDelete
        |> List.iter (fun file ->
            try
                File.Delete(file)
            with ex ->
                writeWarning $"    Не удалось удалить {Path.GetFileName(file)}: {ex.Message}")

        if List.forall id allSuccess then
            writeSuccess $"Все плагины для {rid} собраны"
            true
        else
            writeError $"Ошибка при сборке плагинов для {rid}"
            false

// ============ 5. ПУБЛИКАЦИЯ ============
let publishProject (rid: string) (isSelfContained: bool) =
    let isLinux = rid.StartsWith("linux-", StringComparison.Ordinal)
    let archiveExtension = if isLinux then ".tar.gz" else ".zip"

    let scText =
        if isSelfContained then
            "self-contained"
        else
            "framework-dependent"

    let platformPublishDir = Path.Combine(publishDir, rid)

    let archiveName =
        if isSelfContained then
            $"{projectName}-self-contained-{rid}-{version}{archiveExtension}"
        else
            $"{projectName}-{rid}-{version}{archiveExtension}"

    let archivePath = Path.Combine(outputPath, archiveName)

    writeInfo $"\n=== Публикация {scText} для {rid} ==="

    // Собираем плагины для этой платформы
    let pluginsBuilt = buildPluginsForRuntime rid platformPublishDir

    if not pluginsBuilt then
        writeError $"Пропускаем {rid} из-за ошибки сборки плагинов"
        false
    else
        // Публикуем основной проект
        writeInfo $"Публикация RadioBrowser для {rid}..."

        let args =
            [ "publish"
              projectPath
              "-c"
              "Release"
              "-r"
              rid
              "-o"
              platformPublishDir
              if isSelfContained then
                  "--self-contained"
              else
                  "--no-self-contained"
              "/p:DebugType=None"
              "/p:DebugSymbols=false"
              "/p:Platform=AnyCPU" ]

        let success, error = runDotnetCommand args

        if not success then
            writeError $"Ошибка публикации: {error}"
            false
        else
            // Проверяем наличие плагинов
            let pluginsDir = Path.Combine(platformPublishDir, "Plugins")

            if Directory.Exists pluginsDir then
                let dllCount = Directory.GetFiles(pluginsDir, "*.dll").Length
                let depsCount = Directory.GetFiles(pluginsDir, "*.deps.json").Length
                writeSuccess $"Плагины скопированы: {dllCount} DLL, {depsCount} deps.json"
            else
                writeWarning "Папка Plugins не найдена в выходной директории"

            // Создаем архив
            writeInfo $"Создание архива {archiveName}..."

            if Directory.Exists platformPublishDir then
                try
                    if isLinux then
                        let tarPath = archivePath + ".tmp.tar"

                        try
                            TarFile.CreateFromDirectory(platformPublishDir, tarPath, false)

                            use tarStream = File.OpenRead tarPath
                            use archiveStream = File.Create archivePath
                            use gzipStream = new GZipStream(archiveStream, CompressionLevel.Optimal)
                            tarStream.CopyTo gzipStream
                        finally
                            if File.Exists tarPath then
                                File.Delete tarPath
                    else
                        ZipFile.CreateFromDirectory(platformPublishDir, archivePath, CompressionLevel.Optimal, false)

                    let fileInfo = FileInfo(archivePath)
                    let sizeInMB = float fileInfo.Length / (1024.0 * 1024.0)
                    writeSuccess $"Архив создан: {archiveName} ({sizeInMB:F2} MB)"

                    // Удаляем временную папку
                    writeInfo "  Очистка временной папки..."

                    if deleteDirectorySafe platformPublishDir then
                        writeSuccess "  Временная папка удалена"
                    else
                        writeWarning "  Не удалось удалить временную папку"

                    true
                with ex ->
                    writeError $"Ошибка создания архива: {ex.Message}"
                    false
            else
                writeError $"Директория не найдена: {platformPublishDir}"
                false

// ============ 6. ЗАПУСК СБОРКИ ============
writeInfo "\n==================== НАЧАЛО СБОРКИ ===================="
writeInfo $"Проект: {projectName}"
writeInfo $"Версия: {version}"
let sep = ", "
writeInfo $"Платформы: {String.concat sep runtimeIdentifiers}"
writeInfo $"Плагины: {pluginProjects.Length}"
writeInfo "========================================================"

let results =
    runtimeIdentifiers
    |> List.collect (fun rid ->
        [ (rid, false) // framework-dependent
          (rid, true) ] // self-contained
    )
    |> List.map (fun (rid, isSelfContained) -> publishProject rid isSelfContained)

// ============ 7. ИТОГОВЫЙ ОТЧЕТ ============
writeInfo "\n==================== РЕЗУЛЬТАТЫ СБОРКИ ===================="

let successCount = results |> List.filter id |> List.length
let totalCount = results |> List.length

if successCount = totalCount then
    writeSuccess "ВСЕ СБОРКИ УСПЕШНЫ"

    let archives =
        Array.concat
            [| Directory.GetFiles(outputPath, "*.zip")
               Directory.GetFiles(outputPath, "*.tar.gz") |]
        |> Array.sort

    if archives.Length > 0 then
        writeInfo "\nСозданные архивы:"

        archives
        |> Array.iteri (fun i archive ->
            let fileName = Path.GetFileName archive
            let fileInfo = FileInfo(archive)
            let sizeInMB = float fileInfo.Length / (1024.0 * 1024.0)
            writeInfo $"  {i + 1}. {fileName} ({sizeInMB:F2} MB)")

    writeInfo $"\nАртефакты находятся в: {Path.GetFullPath outputPath}"

    if not pluginProjects.IsEmpty then
        writeInfo "\nВключенные плагины:"

        pluginProjects
        |> List.iter (fun p ->
            let pluginName = Path.GetFileName(Path.GetDirectoryName(p))
            writeInfo $"  - {pluginName}")
else
    let failedCount = totalCount - successCount
    writeError $"СБОРКА ЗАВЕРШЕНА С ОШИБКАМИ"
    writeError $"  Успешно: {successCount} из {totalCount}"
    writeError $"  Ошибок: {failedCount}"
    exit 1

writeInfo "========================================================"
