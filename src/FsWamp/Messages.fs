module FsWamp.Messages
open System
open FsWamp.Common

let join s (ss : string seq) =
    String.Join(s, ss)

let formatWampData (s : string) =
    if s.StartsWith("{") || s.StartsWith("\"") || s.StartsWith("[") || Char.IsNumber(s.[0]) || s = "null"
        then s
    else
        sprintf "\"%s\"" s

let escapeJsonArray = getMessage
let private welcomeMessageId = "0"
let private prefixMessageId = "1"
let private callMessageId = "2"
let private callResultMessageId = "3"
let private callErrorMessageId = "4"
let private subscribeMessageId = "5"
let private unsubscribeMessageId = "6"
let private publishMessageId = "7"
let private eventMessageId = "8"

let (|WELCOME|_|) (input : string list) =
    match input with
        | ["0"; sessionId; _; serverIdent] -> Some((sessionId, serverIdent))
        | _ -> None

let (|PREFIX|_|) (input : string list) =
    match input with
        | ["1"; prefix; uri] -> Some((prefix.Trim('"'), uri.Trim('"')))
        | _ -> None

let (|CALL|_|) (input : string list) =
    let msg = input
    match msg with
        | "2" :: callId :: procUri :: args ->
            Some((callId, procUri.Trim('"'), args))
        | _ -> None

let (|CALLRESULT|_|) (input : string list) =
    let msg = input
    match msg with
        | ["3"; callId; result] ->
            Some((callId.Trim('"'), result))
        | _ -> None

let (|CALLERROR|_|) (input : string list) =
    let msg = input
    match msg with
        | ["4"; callId; errorUri; errorDesc] ->
            Some((callId.Trim('"'), errorUri, errorDesc, ""))
        | ["4"; callId; errorUri; errorDesc; errorDetails] ->
            Some((callId.Trim('"'), errorUri, errorDesc, errorDetails))
        | _ -> None

let (|SUBSCRIBE|_|) (input : string list) =
    let msg = input
    match msg with
        | ["5"; topicUri] ->
            Some(topicUri.Trim('"'))
        | _ -> None

let (|UNSUBSCRIBE|_|) (msg : string list) =
    match msg with
        | ["6"; topicUri] ->
            Some(topicUri.Trim('"'))
        | _ -> None

let (|PUBLISH|_|) (input : string list) =
    let msg = input
    match msg with
        | ["7"; topicUri; event] ->
            Some((topicUri.Trim('"'), event, false, [], []))
        | ["7"; topicUri; event; excludeMe] ->
            Some((topicUri.Trim('"'), event, excludeMe |> Convert.ToBoolean, [], []))
        | ["7"; topicUri; event; exclude; eligible] ->
            Some((topicUri.Trim('"'),event, false, exclude |> escapeJsonArray, eligible |> escapeJsonArray))
        | _ -> None

let (|EVENT|_|) (input : string list) =
    let msg = input
    match msg with
        | ["8"; topicUri; event] ->
            Some((topicUri.Replace("\"",""), event))
        | _ -> None

let private makeMessage (s : string) =
    new ArraySegment<_>(System.Text.UTF8Encoding.UTF8.GetBytes(s))

let welcomeMessage sessionId serverIdent =
    let sessionId = sessionId |> formatWampData
    let serverIdent = serverIdent |> formatWampData
    sprintf "[%s,%s,1,%s]" welcomeMessageId sessionId serverIdent |> makeMessage

let prefixMessage prefix uri =
    let prefix = prefix |> formatWampData
    let uri = uri |> formatWampData
    sprintf "[%s,%s,%s]" prefixMessageId prefix uri |> makeMessage

let callMessage callId procUri (args : string array) =
    let callId = callId |> formatWampData
    let procUri = procUri |> formatWampData
    let args = args |> Array.map formatWampData
    match args.Length with
        | 0 -> sprintf "[%s,%s,%s]" callMessageId callId procUri |> makeMessage
        | _ -> sprintf "[%s,%s,%s,%s]" callMessageId callId procUri (String.Join(",", args)) |> makeMessage

let callResultMessage callId result =
    let callId = callId |> formatWampData
    let result = result |> Option.getWithfallBack "null" |> formatWampData
    sprintf "[%s,%s,%s]" callResultMessageId callId result |> makeMessage

let callErrorMessage callId errorUri errorDescription errorDetails =
    let callId = callId |> formatWampData
    let errorUri = errorUri |> formatWampData
    let errorDescription = errorDescription |> formatWampData
    let errorDetails = errorDetails |> formatWampData
    sprintf "[%s,%s,%s,%s,%s]" callErrorMessageId callId errorUri errorDescription errorDetails |> makeMessage

let subscribeMessage topicUri =
    let topicUri = topicUri |> formatWampData
    sprintf "[%s,%s]" subscribeMessageId topicUri |> makeMessage

let unSubscribeMessage topicUri =
    let topicUri = topicUri |> formatWampData
    sprintf "[%s,%s]" unsubscribeMessageId topicUri |> makeMessage

let publishMessage topicUri (event : string option) (excludeMe : string option) (exclude : string seq option) (eligible : string seq option) =
    let topicUri = topicUri |> formatWampData
    let event = event |> Option.getWithfallBack "null" |> formatWampData
    match excludeMe, exclude, eligible with
        | None, None, None ->
            sprintf "[%s,%s,%s]" publishMessageId topicUri event |> makeMessage
        | Some(s), None, None ->
            sprintf "[%s,%s,%s,%s]" publishMessageId topicUri event "true" |> makeMessage
        | Some(me), Some(ex), Some(el) ->
            let exclude = sprintf "[%s]" (join "," (me :: (ex |> List.ofSeq)))
            let eligible = sprintf "[%s]" (join "," el)
            sprintf "[%s,%s,%s,%s,%s]" publishMessageId topicUri event exclude eligible |> makeMessage
        | _ -> raise (new Exception())

let eventMessage topicUri content =
    let topicUri = topicUri |> formatWampData
    let content = content |> formatWampData
    sprintf "[%s,%s,%s]" eventMessageId topicUri content |> makeMessage