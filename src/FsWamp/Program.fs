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
        do! server host 16000 cts.Token
    } |> Async.RunSynchronously
    System.Console.ReadLine() |> ignore
    0