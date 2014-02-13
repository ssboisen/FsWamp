namespace FsWamp.CSharpFacade
open FsWamp.Client
open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open FsWamp.Common
open FsWamp.Messages
open StateManagement

type Client(host : string, port : int) =
    let cts = new CancellationTokenSource()
    let wsc = new ClientWebSocket();
    let callIdMap = atom Map.empty<string,TaskCompletionSource<string>>

    member this.Connect() =
        async {
            do! wsc.ConnectAsync(new Uri(sprintf "ws://%s:%i" host port), cts.Token) |> awaitTask
            reciveLoop wsc callIdMap cts.Token |> Async.Start
        } |> Async.StartAsTask :> Task

    member this.Disconnect() =
        cts.Cancel()

    member this.Call(procURI : string, [<ParamArray>] arr : string array) =
        let tcs = new TaskCompletionSource<_>()
        async {
            let callId = Guid.NewGuid().ToString("n")
            let call = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes((sprintf "[2,%s,%s,%s]" callId procURI (String.Join(",", arr)) )))
            callIdMap |> swap (fun t -> t |> Map.add callId tcs) |> ignore
            do! wsc.SendAsync(call, WebSocketMessageType.Text, true, cts.Token) |> awaitTask
        } |> Async.Start
        tcs.Task