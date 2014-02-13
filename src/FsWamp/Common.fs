module FsWamp.Common
open System
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets

let awaitTask (t : Task) = t |> Async.AwaitIAsyncResult |> Async.Ignore

type InvalidWampRpcCallException(callId : string, errorUri : string, errorDescription : string, errorDetails : string) =
  inherit System.Exception(errorDescription)
  member this.CallId = callId
  member this.ErrorUri = errorUri
  member this.ErrorDetails = errorDetails

let recv (ws : WebSocket) (ct : CancellationToken) = 
    let rec recv' (acc : byte array) (ws : WebSocket) =
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

