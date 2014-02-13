module FsWamp.Client
open System
open System.Net.WebSockets
open System.Threading.Tasks
open System.Threading
open FsWamp.Messages
open FsWamp.Common
open StateManagement

type InflightRpcCalls = Map<string,TaskCompletionSource<string>> atom

let rec reciveLoop wsc (callIdMap : InflightRpcCalls) (ct : CancellationToken) =
            async {
                if ct.IsCancellationRequested then return ()
                try
                    let! (data, mt) = recv wsc ct
                    
                    if mt = WebSocketMessageType.Text then
                        let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                        match s with 
                            | WELCOME (sessionId, serverIdent) ->
                                return! reciveLoop wsc callIdMap ct
                            | CALLRESULT (callId, result) ->
                                !callIdMap |> Map.tryFind callId |> function Some(tcs) -> tcs.SetResult(result) | None -> ()
                                return! reciveLoop wsc callIdMap ct
                            | CALLERROR (callId, errorUri, errorDesc, errorDetails) ->
                                !callIdMap |> Map.tryFind callId |> function Some(tcs) -> tcs.SetException(new InvalidWampRpcCallException(callId, errorUri, errorDesc, errorDetails)) | None -> ()
                                return! reciveLoop wsc callIdMap ct
                            | _ -> printfn "Got unknown message"
                with
                    | :? OperationCanceledException -> printfn "Cancellation requested"; return()
            }