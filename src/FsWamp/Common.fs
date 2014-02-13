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

let split (s : string) =
    s.Split([|","|], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

let getMessage (input : string) =
    input.Substring(1, input.LastIndexOf(']') - 1) |> split

let recv (ws : WebSocket) (ct : CancellationToken) =
    let rec recv' (acc : byte array) (ws : WebSocket) =
                async {
                    let buffer = new ArraySegment<_>(Array.create 1024 (new byte()))
                    let! result = ws.ReceiveAsync(buffer, ct) |> Async.AwaitTask
                    let data = if result.Count < buffer.Count then buffer.Array.[.. result.Count] else buffer.Array
                    let acc = Array.append acc data
                    match result.CloseStatus.HasValue, result.EndOfMessage, result.MessageType with
                        | false, true, WebSocketMessageType.Text ->
                            return Some(acc |> System.Text.UTF8Encoding.UTF8.GetString |> getMessage)
                        | false, false, WebSocketMessageType.Text ->
                            return! recv' acc ws
                        | true, true, _
                        | _ -> return None
                }
    recv' [||] ws

let sendMessage (ws: WebSocket) (ct : CancellationToken) (msg : ArraySegment<_>) =
    async {
        do! ws.SendAsync(msg, WebSocketMessageType.Text, true, ct) |> awaitTask
    }
