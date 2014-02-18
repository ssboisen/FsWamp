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
        do! welcome |> replyMessage

        let rec loop prefixes subs =
            async {
              if not ct.IsCancellationRequested then
                  let! msg = recv wsContext.WebSocket ct
                  match msg with
                      | Some(msg) ->
                          match msg with
                              | PREFIX (prefix, uri) ->
                                return! loop (prefixes |> Map.add prefix uri) subs

                              | CALL (callId, procUri, args) ->
                                  let uri = processPrefix prefixes procUri
                                  let dispatcher = uri |> Option.bind (fun u -> rpcMappings |> Map.tryFind u)
                                  match dispatcher with
                                      | Some(dispatchFunc) ->
                                          try
                                              let res = dispatchFunc args
                                              let callResult = res |> callResultMessage callId
                                              do! callResult |> replyMessage
                                          with
                                              | _ ->
                                                  let callError = callErrorMessage callId "error#exception" (sprintf "Exception while processing uri: %s" procUri) (sprintf "Args: %A" args)
                                                  do! callError |> replyMessage
                                          return! loop prefixes subs
                                      | _ ->
                                          let callError = callErrorMessage callId "error#generic" (sprintf "Unable to process uri: %s" procUri) (sprintf "Args: %A" args)
                                          do! callError |> replyMessage
                                          return! loop prefixes subs

                              | PUBLISH (topicUri, event, excludeMe, excludes, eligible) ->
                                  let topic = processPrefix prefixes topicUri
                                  match topic with
                                      | Some(topic) ->
                                          let msg = eventMessage topic event
                                          do! !subscribers |> Map.tryFind topic |> Option.getAndMapWithFallBack Set.toList [] 
                                                |> List.map (fun sub -> msg |> sendMessage sub.Socket ct) |> Async.Parallel |> Async.Ignore
                                          return! loop prefixes subs
                                      | None -> 
                                          return! loop prefixes subs

                              | SUBSCRIBE (topicUri) ->
                                  let topic = processPrefix prefixes topicUri
                                  match topic with
                                      | Some(topic) ->
                                          subscribers |> swapMapWithSet topic (Add(contextData)) |> ignore
                                          return! loop prefixes (subs |> Set.add topic)
                                      | None ->
                                          return! loop prefixes subs

                              | UNSUBSCRIBE (topicUri) ->
                                  let topic = processPrefix prefixes topicUri
                                  match topic with
                                      | Some(topic) -> 
                                          subscribers |> swapMapWithSet topic (Remove(contextData)) |> ignore
                                          return! loop prefixes (subs |> Set.remove topic)
                                      | None -> ()
                              | _ -> printfn "Got unknown message: %A" msg
                      | None ->
                          subscribers |> swap (fun m ->
                              subs |> Set.fold (fun m t -> 
                                      m |> Map.tryFind t
                                        |> function
                                              | Some (s) -> m |> Map.add t (s |> Set.remove contextData)
                                              | None -> m) m) |> ignore
                          do! wsContext.WebSocket.CloseAsync( WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", ct) |> awaitTask
        }
        do! loop Map.empty Set.empty
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

