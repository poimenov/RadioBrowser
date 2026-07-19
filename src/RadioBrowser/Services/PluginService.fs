[<AutoOpen>]
module RadioBrowser.PluginService

open System
open System.IO
open System.Reflection
open System.Linq
open System.Composition.Convention
open System.Composition.Hosting
open System.Composition.Hosting.Core
open System.Net.Http
open System.Collections.Generic
open RadioBrowser.PluginContract

type HttpClientFactoryExportProvider(httpClientFactory: IHttpClientFactory) =
    inherit ExportDescriptorProvider()
    
    override _.GetExportDescriptors(contract, descriptorAccessor) =
        seq {
            if contract.ContractType = typeof<IHttpClientFactory> then
                let dependencies = Seq.empty<CompositionDependency>
                
                let createDescriptor =
                    ExportDescriptor.Create(
                        (fun context operation -> box httpClientFactory),
                        Dictionary<string, obj>()
                    )
                
                let promise = 
                    ExportDescriptorPromise(
                        contract,
                        "HttpClientFactory",
                        true,
                        (fun _ -> dependencies),
                        (fun _ -> createDescriptor)
                    )
                
                yield promise
        }

type PluginService(httpClientFactory: IHttpClientFactory) =
    let mutable plugins: ISongDownloaderPlugin list = []
    
    let loadPlugins() =
        let pluginPath = 
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Plugins"
            )
        
        if not (Directory.Exists pluginPath) then
            Directory.CreateDirectory pluginPath |> ignore
            []
        else
            // Загружаем сборки плагинов
            let assemblies =
                Directory.GetFiles(pluginPath, "*.dll", SearchOption.TopDirectoryOnly)
                |> Seq.map Assembly.LoadFrom
                |> Seq.toList
            
            // Настраиваем конвенции MEF
            let conventions = ConventionBuilder()
            conventions
                .ForTypesDerivedFrom<ISongDownloaderPlugin>()
                .Export<ISongDownloaderPlugin>()
                .Shared()
            |> ignore
            
            // Создаем конфигурацию с провайдером
            let configuration = 
                ContainerConfiguration()
                    .WithAssemblies(assemblies, conventions)
                    .WithProvider(HttpClientFactoryExportProvider(httpClientFactory)) // 👈 Используем провайдер
            
            // Создаем контейнер и получаем плагины
            use container = configuration.CreateContainer()
            let loadedPlugins = container.GetExports<ISongDownloaderPlugin>() |> Seq.toList
            
            loadedPlugins
    
    do
        plugins <- loadPlugins()
    
    member _.GetPlugins() = plugins
    
    member _.GetPluginByName(name: string) =
        plugins |> List.tryFind (fun p -> p.PluginName = name)
    
    interface IPluginService with
        member _.GetPlugins() = plugins :> IReadOnlyList<ISongDownloaderPlugin>
        member _.GetPluginByName(name: string) = 
            plugins |> List.tryFind (fun p -> p.PluginName = name)