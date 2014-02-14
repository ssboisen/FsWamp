﻿namespace FsWamp

module Client =
    open System
    open System.Net.WebSockets
    open System.Threading.Tasks
    open System.Threading
    open FsWamp.Messages
    open FsWamp.Common
    open StateManagement

    type InflightRpcCalls = Map<string,TaskCompletionSource<string>>
    type TopicListeners = Map<string, Event<string> list>
    type SessionId = string option

    let rec reciveLoop wsc (callIdMap : InflightRpcCalls atom) (topicMap : TopicListeners atom) (sessionId : SessionId atom) (ct : CancellationToken) =
                async {
                    if ct.IsCancellationRequested then return ()
                    try
                        let! msg = recv wsc ct
                        match msg with
                            | Some(msg) ->
                                match msg with
                                    | WELCOME (sId, serverIdent) ->
                                        printfn "got welcome"
                                        sessionId |> reset (Some(sId)) |> ignore
                                        return! reciveLoop wsc callIdMap topicMap sessionId ct
                                    | CALLRESULT (callId, result) ->
                                        printfn "got callres: %s %s" callId result
                                        !callIdMap
                                            |> Map.tryFind callId
                                            |> function
                                                | Some(tcs) ->
                                                    tcs.SetResult(result)
                                                    callIdMap |> swap (fun m -> m |> Map.remove callId) |> ignore
                                                | None -> ()
                                        return! reciveLoop wsc callIdMap topicMap sessionId ct
                                    | CALLERROR (callId, errorUri, errorDesc, errorDetails) ->
                                        printfn "got callerror"
                                        !callIdMap
                                            |> Map.tryFind callId
                                            |> function
                                                | Some(tcs) ->
                                                    tcs.SetException(new InvalidWampRpcCallException(callId, errorUri, errorDesc, errorDetails))
                                                    callIdMap |> swap (fun m -> m |> Map.remove callId) |> ignore
                                                | None -> ()
                                        return! reciveLoop wsc callIdMap topicMap sessionId ct
                                    | EVENT (topicUri, event) ->
                                        let map = !topicMap
                                        map
                                            |> Map.tryFind topicUri
                                            |> function
                                                | Some(subscribers) ->
                                                    subscribers |> List.iter (fun e ->
                                                    e.Trigger(event)
                                                    )
                                                | None -> ()
                                        return! reciveLoop wsc callIdMap topicMap sessionId ct
                                    | _ -> printfn "Got unknown message"
                            | None -> return! reciveLoop wsc callIdMap topicMap sessionId ct
                    with
                        | :? OperationCanceledException -> printfn "Cancellation requested"; return()
                }