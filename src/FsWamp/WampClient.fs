namespace FsWamp
open FsWamp.Client
open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open FsWamp.Common
open FsWamp.Messages
open StateManagement

type WampClient(host : string, port : int) =
    let cts = new CancellationTokenSource()
    let wsc = new ClientWebSocket();
    let callIdMap = atom (new InflightRpcCalls([]))
    let topicMap = atom (new TopicListeners([]))
    let sessionId = atom SessionId.None
    let sendMessage = sendMessage wsc cts.Token
    let prefixes = atom Map.empty<string,string>

    let publish (topic : string) (event : string option) (excludeMe : bool option) (exclude : string seq option) (eligible : string seq option) =
        async {
            match (processPrefix !prefixes topic) with //Check if topic is valid with respect to already registered prefixes
                | None -> raise (InvalidTopicException(topic))
                | _ ->
                    let excludeMe = excludeMe |> Option.bind (fun b -> if b then (!sessionId) else None)
                    let msg = publishMessage topic event excludeMe exclude eligible
                    do! msg |> sendMessage
        } |> Async.StartAsTask :> Task

    member this.Connect() =
        async {
            do! wsc.ConnectAsync(new Uri(sprintf "ws://%s:%i" host port), cts.Token) |> awaitTask
            reciveLoop wsc callIdMap topicMap sessionId cts.Token |> Async.Start
        } |> Async.StartAsTask :> Task

    member this.Disconnect() =
        async {
            do! wsc.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect invoked by user", CancellationToken.None) |> awaitTask
            callIdMap |> reset (new InflightRpcCalls([])) |> ignore
            topicMap |> reset (new TopicListeners([])) |> ignore
            sessionId |> reset None |> ignore
        } |> Async.StartAsTask :> Task

    member this.Prefix(prefix : string, uri : string) =
        async {
            let msg = prefixMessage prefix uri
            do! msg |> sendMessage
            prefixes |> swap (fun m -> m |> Map.add prefix uri) |> ignore
        } |> Async.StartAsTask :> Task

    member this.Call(procURI : string, [<ParamArray>] arr : string array) =
        let tcs = new TaskCompletionSource<_>()
        async {
            match (processPrefix !prefixes procURI) with //Check if rpcUri is valid with respect to already registered prefixes
                | None -> raise (InvalidRpcUriException(procURI))
                | _ ->
                    let callId = Guid.NewGuid().ToString("n")
                    let msg = callMessage callId procURI arr
                    callIdMap |> swap (fun t -> t |> Map.add callId tcs) |> ignore
                    do! msg |> sendMessage
        } |> Async.Start
        tcs.Task

    member this.Subscribe(topic : string) =
        let event = new Event<string>()
        async {
            match (processPrefix !prefixes topic) with //Check if topic is valid with respect to already registered prefixes
                | None -> raise (InvalidTopicException(topic))
                | Some(uri) ->
                    let msg = subscribeMessage topic
                    topicMap |> swapMapWithList uri event |> ignore
                    do! msg |> sendMessage
        } |> Async.Start
        event.Publish :> IObservable<string>

    member this.Unsubscribe(topic : string) =
        async {
            match (processPrefix !prefixes topic) with //Check if topic is valid with respect to already registered prefixes
                | None -> raise (InvalidTopicException(topic))
                | Some(uri) ->
                    let msg = unSubscribeMessage topic
                    topicMap |> swap (fun m -> m|> Map.remove uri) |> ignore
                    do! msg |> sendMessage
        } |> Async.StartAsTask :> Task

    member this.Publish(topic) =
        publish topic None None None None
    member this.Publish(topic, event) =
        publish topic (Some(event)) None None None

    member this.Publish(topic, event, excludeMe) =
        publish topic (Some(event)) (Some(excludeMe)) None None

    member this.Publish(topic, event, excludeMe, excludes) =
        publish topic (Some(event)) (Some(excludeMe)) (Some(excludes)) None

    member this.Publish(topic, event, excludeMe, excludes, eligible) =
        publish topic (Some(event)) (Some(excludeMe)) (Some(excludes)) (Some(eligible))


    interface IDisposable with
        member this.Dispose() =
            cts.Cancel()
            wsc.Dispose()
            (callIdMap :> IDisposable).Dispose()
            (topicMap :> IDisposable).Dispose()
            (sessionId :> IDisposable).Dispose()