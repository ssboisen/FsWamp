module FsWamp.Server
open System
open System.Net
open System.Threading.Tasks
open System.Threading
open FsWamp.Messages
open FsWamp.Common
open StateManagement

[<CustomEquality; CustomComparison>]
type Subscriber = { SessionId : string; Socket : WebSockets.WebSocket }
                    interface IComparable with
                       member this.CompareTo obj : int =
                        let other = obj :?> Subscriber
                        this.SessionId.CompareTo other.SessionId
                    override this.Equals(o) =
                        match o with
                            | :? Subscriber as other ->
                                this.SessionId.Equals(other.SessionId)
                            | _ -> false
                    override this.GetHashCode() =
                        this.SessionId.GetHashCode()


let private processContext (context : HttpListenerContext) subscribers rpcMappings ct =
    async {
        let! wsContext = context.AcceptWebSocketAsync(null) |> Async.AwaitTask
        let replyMessage = sendMessage wsContext.WebSocket ct

        let sessionId = (Guid.NewGuid().ToString("n"))
        let contextData =  { SessionId = sessionId; Socket = wsContext.WebSocket }
        let welcome = welcomeMessage sessionId "FsWamp/0.0.1"
        let prefixes = atom Map.empty<string,string>

        do! welcome |> replyMessage

        while not ct.IsCancellationRequested do
            let! msg = recv wsContext.WebSocket ct
            match msg with
                | Some(msg) ->
                    match msg with
                        | PREFIX (prefix, uri) ->
                            prefixes |> swap (fun m -> m |> Map.add prefix uri) |> ignore
                        | CALL (callId, procUri, args) ->
                            let uri =
                                if procUri.StartsWith "http://" || procUri.StartsWith "https://" then Some(procUri)
                                else
                                    match procUri.Split([|":"|], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
                                     | [ns; op] ->
                                        !prefixes |> Map.tryFind ns |> Option.map (fun u -> sprintf "%s%s" u op)
                                     | _ -> None
                            let dispatcher = uri |> Option.bind (fun u -> rpcMappings |> Map.tryFind u)
                            match dispatcher with
                                | Some(dispatchFunc) ->
                                    let callResult = dispatchFunc args |> callResultMessage callId
                                    do! callResult |> replyMessage
                                | _ ->
                                    let callError = callErrorMessage callId "error#generic" (sprintf "Unable to process uri: %s" procUri) (sprintf "Args: %A" args)
                                    do! callError |> replyMessage

                        | PUBLISH (topic, event, excludeMe, excludes, eligible) ->
                            let msg = eventMessage topic event
                            let subs = !subscribers |> Map.tryFind topic |> Option.getAndMapWithFallBack Set.toList []
                            do! subs |> List.map (fun ws -> msg |> sendMessage ws.Socket ct) |> Async.Parallel |> Async.Ignore
                        | SUBSCRIBE (topicId) ->
                            subscribers |> swapMapWithSet topicId (Add(contextData)) |> ignore

                        | UNSUBSCRIBE (topicId) ->
                            subscribers |> swapMapWithSet topicId (Remove(contextData)) |> ignore
                        | _ -> printfn "Got unknown message: %A" msg
                | None -> do! wsContext.WebSocket.CloseAsync( WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", ct) |> awaitTask
    }



let server host port ct =
    let listener = new HttpListener();
    let uri = sprintf "http://%s:%i/" host port
    listener.Prefixes.Add(uri);
    listener.Start();
    let subscribers = atom Map.empty<string, Subscriber Set>
    let rpcMappings = Map.empty<string, string list -> string option>
    let rpcMappings = Map.add "http://localhost/simple/calc#add" (List.map int >> List.sum >> string >> Some) rpcMappings
    let rec listen (ct : CancellationToken) =
        async {
            try
                printfn "Listening on %s" uri
                let! context = listener.GetContextAsync() |> Async.AwaitTask
                if context.Request.IsWebSocketRequest then
                    processContext context subscribers rpcMappings ct |> Async.Start
                else context.Response.Close()

                if not ct.IsCancellationRequested then
                    return! listen ct
                else (listener :> IDisposable).Dispose()
            with | :? OperationCanceledException -> printfn "Cancellation requested"; return()
        }

    listen ct

