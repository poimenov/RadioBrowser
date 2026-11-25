[<AutoOpen>]
module RadioBrowser.ProcessLauncherService

open System
open System.Diagnostics

type ProcessLauncherService() =
    interface IProcessLauncher with
        member _.Start(startInfo) =
            use proc = Process.Start(startInfo)
            ()
