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

    let rec reciveLoop() =
            async {
                if cts.Token.IsCancellationRequested then return ()
                try
                    let! (data, mt) = recv wsc cts.Token
                    
                    if mt = WebSocketMessageType.Text then
                        let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                        match s with 
                            | WELCOME (sessionId, serverIdent) ->
                                return! reciveLoop()
                            | CALLRESULT (callId, result) ->
                                !callIdMap |> Map.tryFind callId |> function Some(tcs) -> tcs.SetResult(result) | None -> ()
                                return! reciveLoop()
                            | CALLERROR (callId, errorUri, errorDesc, errorDetails) ->
                                !callIdMap |> Map.tryFind callId |> function Some(tcs) -> tcs.SetException(new InvalidWampRpcCallException(callId, errorUri, errorDesc, errorDetails)) | None -> ()
                                return! reciveLoop()
                            | _ -> printfn "Got unknown message"
                with
                    | :? OperationCanceledException -> printfn "Cancellation requested"; return()
            }

    member this.Connect() =
        async {
            do! wsc.ConnectAsync(new Uri(sprintf "ws://%s:%i" host port), cts.Token) |> awaitTask
            reciveLoop() |> Async.Start
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