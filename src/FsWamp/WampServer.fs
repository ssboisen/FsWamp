namespace FsWamp
open FsWamp.Server
type WampServer(host : string, port : int) =
    let cts = new System.Threading.CancellationTokenSource()
    let rpcMappings = Map([("http://localhost/simple/calc#add",(List.map int >> List.sum >> string >> Some))])
    member this.Start() =
        server host port cts.Token rpcMappings |> Async.Start
    member this.Stop() =
        cts.Cancel()

    interface System.IDisposable with
        member this.Dispose() =
            cts.Cancel();
