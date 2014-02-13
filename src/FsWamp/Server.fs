﻿module FsWamp.Server
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
            let! msg = recv wsContext.WebSocket ct
            printfn "got message on server: %A" msg
            match msg with
                | Some(msg) ->
                    match msg with
                        | PREFIX (prefix, uri) ->
                            printfn "Got prefixmessage with prefix: %s for uri: %s" prefix uri
                        | CALL (callId, procUri, args) ->
                            match procUri with
                                | "add" ->
                                    let res = args |> Seq.map int |> Seq.sum |> string
                                    let callResult = callResultMessage callId res
                                    do! callResult |> sendMessage
                                | _ ->
                                    let callError = callErrorMessage callId "error#unknown_function" "Unknown function: " procUri
                                    do! callError |> sendMessage
                        | PUBLISH (topic, event, excludeMe, excludes, eligible) ->
                            let event = eventMessage topic event
                            printfn "got publish on server: '%s'" topic
                            do! event |> sendMessage
                            printfn "replied"
                        | SUBSCRIBE (topicId) ->
                            printfn "got subscribe for %s on server" topicId
                            match topicId with
                                | "seTopic" ->
                                    let event = eventMessage topicId "hello through event"
                                    do! event |> sendMessage
                                    printfn "send message back"
                                | _ -> ()
                        | _ -> printfn "Got unknown message"
                | None -> do! wsContext.WebSocket.CloseAsync( WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", ct) |> awaitTask
    }

let server host port ct =
    let listener = new HttpListener();
    let uri = sprintf "http://%s:%i/" host port
    listener.Prefixes.Add(uri);
    listener.Start();
    let rec listen (ct : CancellationToken) =
        async {
            try
                printfn "Listening on %s" uri
                let! context = listener.GetContextAsync() |> Async.AwaitTask
                if context.Request.IsWebSocketRequest then
                    processContext context ct |> Async.Start
                else context.Response.Close()

                if not ct.IsCancellationRequested then
                    return! listen ct
                else (listener :> IDisposable).Dispose()
            with | :? OperationCanceledException -> printfn "Cancellation requested"; return()
        }

    listen ct

