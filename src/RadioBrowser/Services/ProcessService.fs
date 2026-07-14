[<AutoOpen>]
module RadioBrowser.ProcessService

open System.Diagnostics
open Microsoft.Extensions.Logging

type ProcessService(logger: ILogger<IProcessService>) =
    interface IProcessService with
        member _.Run(command, arguments) =
            try
                let psi = new ProcessStartInfo(command)
                psi.RedirectStandardOutput <- false
                psi.UseShellExecute <- true
                psi.CreateNoWindow <- true
                psi.Arguments <- arguments

                use p = new Process()
                p.StartInfo <- psi
                p.Start() |> ignore
                p.Dispose()
            with ex ->
                logger.LogError(ex, $"Error running process: {command} {arguments}")
