module FsWamp.Demo
open System
open System.Net
open System.Threading.Tasks
open System.Threading
open FsWamp.Messages
open FsWamp.Common
open FsWamp.Server
open FsWamp.Client

open StateManagement
[<EntryPoint>]
let main argv =
    let host = "localhost"
    async {
        let cts = new System.Threading.CancellationTokenSource()
        let rpcMappings = Map([("http://localhost/simple/calc#add",(List.map int >> List.sum >> string >> Some))])
        do! server host 16000 cts.Token rpcMappings
    } |> Async.Start
    printfn "Press enter to exit"
    System.Console.ReadLine() |> ignore
    0