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
    let topicMap = atom Map.empty<string, Event<string> list>
    member this.Connect() =
        async {
            do! wsc.ConnectAsync(new Uri(sprintf "ws://%s:%i" host port), cts.Token) |> awaitTask
            reciveLoop wsc callIdMap topicMap cts.Token |> Async.Start
        } |> Async.StartAsTask :> Task

    member this.Disconnect() =
        async {
            do! wsc.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect invoked by user", CancellationToken.None) |> awaitTask
            callIdMap |> reset Map.empty<string,TaskCompletionSource<string>> |> ignore
        } |> Async.StartAsTask :> Task

    member this.Call(procURI : string, [<ParamArray>] arr : string array) =
        let tcs = new TaskCompletionSource<_>()
        async {
            let callId = Guid.NewGuid().ToString("n")
            let call = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes((sprintf "[2,%s,%s,%s]" callId procURI (String.Join(",", arr)) )))
            callIdMap |> swap (fun t -> t |> Map.add callId tcs) |> ignore
            do! wsc.SendAsync(call, WebSocketMessageType.Text, true, cts.Token) |> awaitTask
        } |> Async.Start
        tcs.Task

    member this.Subscribe(topic : string) =
        let event = new Event<string>()
        async {
            let call = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(sprintf "[5,%s]" topic))
            topicMap |> swap (fun m ->
                    m |> Map.tryFind topic
                      |> function
                            | Some(l) -> m |> Map.add topic (event :: l)
                            | None -> [(topic, [event])] |> Map.ofList
                    ) |> ignore
            do! wsc.SendAsync(call, WebSocketMessageType.Text, true, cts.Token) |> awaitTask
            printfn "Send subscribtion"
        } |> Async.Start
        event.Publish :> IObservable<string>

    member this.Unsubscribe(topic : string) =
        async {
            let call = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(sprintf "[6,%s]" topic))
            do! wsc.SendAsync(call, WebSocketMessageType.Text, true, cts.Token) |> awaitTask
        } |> Async.StartAsTask :> Task

    interface IDisposable with
        member this.Dispose() =
            cts.Cancel()
            wsc.Dispose()
            (callIdMap :> IDisposable).Dispose()