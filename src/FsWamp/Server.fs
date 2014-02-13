module FsWamp.Server
open System
open System.Net
open System.Threading.Tasks
open System.Threading
open FsWamp.Messages
open FsWamp.Common

let private processContext (context : HttpListenerContext) ct =
    async {
        let! wsContext = context.AcceptWebSocketAsync(null) |> Async.AwaitTask
        let sendMessage = sendMessage wsContext.WebSocket ct
        let welcome = welcomeMessage (Guid.NewGuid().ToString("n")) "FsWamp/0.0.1"

        do! welcome |> sendMessage

        while not ct.IsCancellationRequested do
            let! (data, mt) = recv wsContext.WebSocket ct

            if mt = WebSockets.WebSocketMessageType.Text then
                let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                match s with
                    | PREFIX (prefix, uri) ->
                        printfn "Got prefixmessage with prefix: %s for uri: %s" prefix uri
                    | CALL (callId, procUri, args) ->
                        match procUri with
                            | "add" ->
                                let res = args |> Seq.map int |> Seq.sum
                                let callResult = callResultMessage callId res
                                do! callResult |> sendMessage
                            | _ ->
                                let callError = callErrorMessage callId "error#unknown_function" "Unknown function: " procUri
                                do! callError |> sendMessage
                    | SUBSCRIBE (topicId) ->
                        match topicId with
                            | "seTopic" ->
                                let event = eventMessage topicId "hello through event"
                                do! event |> sendMessage
                                printfn "send back an event"
                            | _ -> ()
                    | _ -> printfn "Got unknown message"
    }

let server host port ct =
    let listener = new HttpListener();
    listener.Prefixes.Add(sprintf "http://%s:%i/" host port);
    listener.Start();
    let rec listen (ct : CancellationToken) =
        async {
            try
                printfn "Listening"
                let! context = listener.GetContextAsync() |> Async.AwaitTask
                processContext context ct |> Async.Start
                if not ct.IsCancellationRequested then
                    return! listen ct
                  else
                    printfn "Cancellation requested!"
                    (listener :> IDisposable).Dispose()
            with | :? OperationCanceledException -> printfn "Cancellation requested"; return()
        }

    listen ct

