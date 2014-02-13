module FsWamp.Messages
open System

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

