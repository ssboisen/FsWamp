module FsWamp.Messages
open System

let getMessage (input : string) =
    input.Substring(1, input.LastIndexOf(']') - 1).Split([|','|], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

let private welcomeMessageId = "0"
let private prefixMessageId = "1"
let private callMessageId = "2"
let private callResultMessageId = "3"
let private callErrorMessageId = "4"
let private subscribeMessageId = "5"
let private unsubscribeMessageId = "5"
let private evenetMessageId = "8"

let (|WELCOME|_|) (input : string) =
    match input |> getMessage with
        | [welcomeMessageId; sessionId; _; serverIdent] -> Some((sessionId, serverIdent))
        | _ -> None

let (|PREFIX|_|) (input : string) =
    match input |> getMessage with
        | [prefixMessageId; prefix; uri] -> Some((prefix, uri))
        | _ -> None

let (|CALL|_|) (input : string) =
    let msg = input |> getMessage
    match msg with
        | callErrorMessageId :: callId :: procUri :: args ->
            Some((callId, procUri, args))
        | _ -> None

let (|CALLRESULT|_|) (input : string) =
    let msg = input |> getMessage
    match msg with
        | [callResultMessageId; callId; result] -> Some((callId, result))
        | _ -> None

let (|CALLERROR|_|) (input : string) =
    let msg = input |> getMessage
    match msg with
        | [callErrorMessageId; callId; errorUri; errorDesc] ->
            Some((callId, errorUri, errorDesc, ""))
        | [callErrorMessageId; callId; errorUri; errorDesc; errorDetails] ->
            Some((callId, errorUri, errorDesc, errorDetails))
        | _ -> None

let (|SUBSCRIBE|_|) (input : string) =
    let msg = input |> getMessage
    match msg with
        | [subscribeMessageId; topicUri] ->
            Some(topicUri)
        | _ -> None

let (|EVENT|_|) (input : string) =
    let msg = input |> getMessage
    match msg with
        | [evenetMessageId; topicUri; event] ->
            Some((topicUri, event))
        | _ -> None

let private makeMessage (s : string) =
    new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(s))

let welcomeMessage sessionId serverIdent =
    sprintf "[%s,%s,1,%s]" welcomeMessageId sessionId serverIdent |> makeMessage

let callMessage callId procUri (args : string array) =
    sprintf "[%s,%s,%s,%s]" callMessageId callId procUri (String.Join(",", args)) |> makeMessage

let callResultMessage callId result =
    sprintf "[%s,%s,%i]" callResultMessageId callId result |> makeMessage

let callErrorMessage callId errorUri errorDescription errorDetails =
    sprintf "[%s,%s,%s,%s%s]" callErrorMessageId callId errorUri errorDescription errorDetails |> makeMessage

let subscribeMessage topic =
    sprintf "[%s,%s]" subscribeMessageId topic |> makeMessage

let unSubscribeMessage topic =
    sprintf "[%s,%s]" unsubscribeMessageId topic |> makeMessage

let eventMessage topicId content =
    sprintf "[%s,%s,%s]" evenetMessageId topicId content |> makeMessage