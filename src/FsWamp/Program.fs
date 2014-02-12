module FsWamp =
    open System
    open System.Net
    open System.Threading.Tasks
    open System.Threading
    let awaitTask = Async.AwaitIAsyncResult >> Async.Ignore

    let getMessage (input : string) = 
        input.Substring(1, input.LastIndexOf(']') - 1).Split([|','|], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

    let (|WELCOME|_|) (input : string) =
        match input |> getMessage with
            | ["0"; sessionId; _; serverIdent] -> Some((sessionId, serverIdent))
            | _ -> None

    let (|PREFIX|_|) (input : string) =
        match input |> getMessage with
            | ["1"; prefix; uri] -> Some((prefix, uri))
            | _ -> None

    let (|CALL|_|) (input : string) =
        let msg = input |> getMessage
        match msg with
            | "2" :: callId :: procUri :: args ->
                Some((callId, procUri, args))
            | _ -> None

    let (|CALLRESULT|_|) (input : string) =
        let msg = input |> getMessage
        match msg with
            | ["3"; callId; result] -> Some((callId, result))
            | _ -> None

    let (|CALLERROR|_|) (input : string) =
        let msg = input |> getMessage
        match msg with
            | ["4"; callId; errorUri; errorDesc] ->
                Some((callId, errorUri, errorDesc, ""))
            | ["4"; callId; errorUri; errorDesc; errorDetails] ->
                Some((callId, errorUri, errorDesc, errorDetails))
            | _ -> None
                

    let recv (ws : WebSockets.WebSocket) (ct : CancellationToken) = 
        let rec recv' (acc : byte array) (ws : WebSockets.WebSocket) =
                    async {
                        let buffer = new ArraySegment<_>(Array.create 1024 (new byte()))
                        let! result = ws.ReceiveAsync(buffer, ct) |> Async.AwaitTask
                        let data = if result.Count < buffer.Count then buffer.Array.[.. result.Count] else buffer.Array
                        let acc = Array.append acc data
                        if result.EndOfMessage then
                            return acc, result.MessageType
                            else return! recv' acc ws
                    }
        recv' [||] ws

    let processContext (context : HttpListenerContext) ct =
        async {
            let! wsContext = context.AcceptWebSocketAsync(null) |> Async.AwaitTask
            
            let welcome = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(sprintf "[0,%s,1,FsWamp/0.0.1]" (Guid.NewGuid().ToString("n") )))

            do! wsContext.WebSocket.SendAsync(welcome, WebSockets.WebSocketMessageType.Text, true, ct) |> awaitTask

            while not ct.IsCancellationRequested do
                let! (data, mt) = recv wsContext.WebSocket ct

                if mt = WebSockets.WebSocketMessageType.Text then
                    let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                    match s with
                        | PREFIX (prefix, uri) ->
                            printfn "Got prefixmessage with prefix: %s for uri: %s" prefix uri
                        | CALL (callId, procUri, args) ->
                            printfn "Trying to call '%s' with args: %A under callId: %s" procUri args callId
                            match procUri with
                                | "add" -> 
                                    let res = args |> Seq.map int |> Seq.sum
                                    let callResult = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(sprintf "[3,%s,%i]" callId res))
                                    do! wsContext.WebSocket.SendAsync(callResult, WebSockets.WebSocketMessageType.Text, true, ct) |> awaitTask
                                | _ ->
                                    let callError = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(sprintf "[4,%s,%s,%s%s]" callId "error#unknown_function" "Unknown function: " procUri))
                                    do! wsContext.WebSocket.SendAsync(callError, WebSockets.WebSocketMessageType.Text, true, ct) |> awaitTask
                        | _ -> printfn "Got unknown message"
        }

    let server port ct =
        let listener = new HttpListener();
        listener.Prefixes.Add(sprintf "http://localhost:%i/" port);
        listener.Start();
        let rec doListen (ct : CancellationToken) =
            async {
                let! context = listener.GetContextAsync() |> Async.AwaitTask
                processContext context ct |> Async.Start
                if not ct.IsCancellationRequested then
                    return! doListen ct
                  else 
                    printfn "Cancellation requested!"
                    (listener :> IDisposable).Dispose()
            }
        
        doListen ct
        

    let client port (text : string) ct =
        async {
            use wsc = new WebSockets.ClientWebSocket();
            do! wsc.ConnectAsync(new Uri(sprintf "ws://localhost:%i" port), ct) |> awaitTask
            //let buffer = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(text))
            //do! wsc.SendAsync(buffer, WebSockets.WebSocketMessageType.Text, true, ct) |> awaitTask
            let rec reciveLoop() =
                async {
                    let! (data, mt) = recv wsc ct
                    
                    if mt = WebSockets.WebSocketMessageType.Text then
                        let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                        match s with 
                            | WELCOME (sessionId, serverIdent) ->
                                printfn "Got welcomemessage for session: %s from server %s" sessionId serverIdent
                                let call = new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes((sprintf "[2,%s,%s,%s]" (Guid.NewGuid().ToString("n")) "addd" "8,6" )))
                                do! wsc.SendAsync(call, WebSockets.WebSocketMessageType.Text, true, ct) |> awaitTask
                                return! reciveLoop()
                            | CALLRESULT (callId, result) ->
                                printfn "Got result for callId: %s, it was: %s" callId result
                            | CALLERROR (callId, errorUri, errorDesc, errorDetails) ->
                                printfn "Got error: %s with description: %s for callId: %s" errorUri errorDesc callId
                            | _ -> printfn "Got unknown message"
                }
            return! reciveLoop()
        }

[<EntryPoint>]
let main argv = 
    async {
        let large = new string('?', 512*1024);
        let cts = new System.Threading.CancellationTokenSource()
        FsWamp.server 16000 cts.Token |> Async.Start
        do! FsWamp.client 16000 large cts.Token
    } |> Async.RunSynchronously
    System.Console.ReadLine() |> ignore
    0