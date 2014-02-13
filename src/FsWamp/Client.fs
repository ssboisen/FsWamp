module FsWamp.Client
open System
open System.Net.WebSockets
open System.Threading.Tasks
open System.Threading
open FsWamp.Messages
open FsWamp.Common
open StateManagement

type InflightRpcCalls = Map<string,TaskCompletionSource<string>> atom
type TopicListeners = Map<string, Event<string> list> atom

let rec reciveLoop wsc (callIdMap : InflightRpcCalls) (topicMap : TopicListeners) (ct : CancellationToken) =
            async {
                if ct.IsCancellationRequested then return ()
                try
                    let! (data, mt) = recv wsc ct

                    if mt = WebSocketMessageType.Text then
                        let s = System.Text.UTF8Encoding.UTF8.GetString(data);
                        match s with
                            | WELCOME (sessionId, serverIdent) ->
                                return! reciveLoop wsc callIdMap topicMap ct
                            | CALLRESULT (callId, result) ->
                                !callIdMap
                                    |> Map.tryFind callId
                                    |> function
                                        | Some(tcs) -> tcs.SetResult(result)
                                        | None -> ()
                                return! reciveLoop wsc callIdMap topicMap ct
                            | CALLERROR (callId, errorUri, errorDesc, errorDetails) ->
                                !callIdMap
                                    |> Map.tryFind callId
                                    |> function
                                        | Some(tcs) -> tcs.SetException(new InvalidWampRpcCallException(callId, errorUri, errorDesc, errorDetails))
                                        | None -> ()
                                return! reciveLoop wsc callIdMap topicMap ct
                            | EVENT (topicUri, event) ->
                                !topicMap
                                    |> Map.tryFind topicUri
                                    |> function
                                        | Some(subscribers) -> subscribers |> List.iter (fun e -> e.Trigger(event))
                                        | None -> ()
                                return! reciveLoop wsc callIdMap topicMap ct
                            | _ -> printfn "Got unknown message"
                with
                    | :? OperationCanceledException -> printfn "Cancellation requested"; return()
            }