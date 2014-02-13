module FsWamp.Client
open System
open System.Net.WebSockets
open System.Threading.Tasks
open System.Threading
open FsWamp.Messages
open FsWamp.Common

let client host port ct =
    async {
        use wsc = new ClientWebSocket();
        do! wsc.ConnectAsync(new Uri(sprintf "ws://%s:%i" host port), ct) |> awaitTask
        let rec reciveLoop() =
            async {
                if ct.IsCancellationRequested then return ()

                try
                    let! (data, mt) = recv wsc ct
                    
                    if mt = WebSocketMessageType.Text then
                        let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                        match s with 
                            | WELCOME (sessionId, serverIdent) ->
                                printfn "Got welcomemessage for session: %s from server %s" sessionId serverIdent
                                let call = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes((sprintf "[2,%s,%s,%s]" (Guid.NewGuid().ToString("n")) "addd" "8,6" )))
                                do! wsc.SendAsync(call, WebSocketMessageType.Text, true, ct) |> awaitTask
                                return! reciveLoop()
                            | CALLRESULT (callId, result) ->
                                printfn "Got result for callId: %s, it was: %s" callId result
                                return! reciveLoop()
                            | CALLERROR (callId, errorUri, errorDesc, errorDetails) ->
                                printfn "Got error: %s with description: %s for callId: %s" errorUri errorDesc callId
                                return! reciveLoop()
                            | _ -> printfn "Got unknown message"
                with
                    | :? OperationCanceledException -> printfn "Cancellation requested"; return()
            }
        return! reciveLoop()
    }