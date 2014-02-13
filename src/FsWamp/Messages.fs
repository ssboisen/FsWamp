﻿module FsWamp.Messages
open System

let join s (ss : string seq) =
    String.Join(s, ss)

let split (s : string) =
    s.Split([|","|], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

let getMessage (input : string) =
    input.Substring(1, input.LastIndexOf(']') - 1) |> split

let escapeJsonArray = getMessage
let private welcomeMessageId = "0"
let private prefixMessageId = "1"
let private callMessageId = "2"
let private callResultMessageId = "3"
let private callErrorMessageId = "4"
let private subscribeMessageId = "5"
let private unsubscribeMessageId = "6"
let private publishMessageId = "7"
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

let (|PUBLISH|_|) (input : string) =
    let msg = input |> getMessage
    match msg with
        | [publishMessageId; topicUri; event] ->
            Some((topicUri, event, false, [], []))
        | [publishMessageId; topicUri; event; excludeMe] ->
            Some((topicUri, event, excludeMe |> Convert.ToBoolean, [], []))
        | [publishMessageId; topicUri; event; exclude; eligible] ->
            Some((topicUri,event, false, exclude |> escapeJsonArray, eligible |> escapeJsonArray))
        | _ -> None

let private makeMessage (s : string) =
    new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(s))

let welcomeMessage sessionId serverIdent =
    sprintf "[%s,%s,1,%s]" welcomeMessageId sessionId serverIdent |> makeMessage

let callMessage callId procUri (args : string array) =
    sprintf "[%s,%s,%s,%s]" callMessageId callId procUri (String.Join(",", args)) |> makeMessage

let callResultMessage callId result =
    sprintf "[%s,%s,%s]" callResultMessageId callId result |> makeMessage

let callErrorMessage callId errorUri errorDescription errorDetails =
    sprintf "[%s,%s,%s,%s%s]" callErrorMessageId callId errorUri errorDescription errorDetails |> makeMessage

let subscribeMessage topic =
    sprintf "[%s,%s]" subscribeMessageId topic |> makeMessage

let unSubscribeMessage topic =
    sprintf "[%s,%s]" unsubscribeMessageId topic |> makeMessage

let eventMessage topicId content =
    sprintf "[%s,%s,%s]" evenetMessageId topicId content |> makeMessage

let publishMessage topic event (excludeMe : string option) (exclude : string seq option) (eligible : string seq option) =
    match excludeMe, exclude, eligible with
        | None, None, None ->
            sprintf "[%s, %s, %s]" publishMessageId topic event |> makeMessage
        | Some(s), None, None ->
            sprintf "[%s, %s, %s, %s]" publishMessageId topic event "true" |> makeMessage
        | Some(me), Some(ex), Some(el) ->
            let exclude = sprintf "[%s]" (join "," (me :: (ex |> List.ofSeq)))
            let eligible = sprintf "[%s]" (join "," el)
            sprintf "[%s, %s, %s, %s, %s]" publishMessageId topic event exclude eligible |> makeMessage
        | _ -> raise (new Exception())