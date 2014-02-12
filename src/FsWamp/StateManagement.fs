module StateManagement
open System

type WatcherCallback<'T> = Guid -> 'T -> 'T -> unit

and Messages<'T> =
        private 
        | Swap of ('T -> 'T) * AsyncReplyChannel<'T>
        | CompareAndSet of oldValue : 'T * newValue : 'T * AsyncReplyChannel<bool>
        | Reset of value : 'T
        | Deref of AsyncReplyChannel<'T>
        | AddWatcher of key : Guid * WatcherCallback<'T>
        | RemoveWatcher of key : Guid
and
    atom<'T> = private { container : MailboxProcessor<Messages<'T>> }

/// Creates a new atom with the value v
let atom<'T when 'T : equality> (v : 'T) : atom<'T> = 
    let mbp = MailboxProcessor<Messages<'T>>.Start(fun inbox ->
                let rec loop (state : 'T) (watchers : Map<Guid, WatcherCallback<'T>>) =
                    async {
                        let! msg = inbox.Receive()
                        match msg with
                            | Swap(f, c) -> 
                                let newValue = f state
                                c.Reply(newValue)
                                watchers |> Map.iter (fun k f -> f k state newValue)
                                return! loop newValue watchers
                            | CompareAndSet(o,n,c) ->
                                if state = o then
                                    c.Reply(true)
                                    watchers |> Map.iter (fun k f -> f k state n)
                                    return! loop n watchers
                                else
                                    c.Reply(false)
                                    return! loop state watchers
                            | Reset(v) ->
                                watchers |> Map.iter (fun k f -> f k state v)
                                return! loop v watchers
                            | Deref(c) ->
                                c.Reply(state)
                                return! loop state watchers
                            | AddWatcher(key, cb) ->
                                return! loop state (watchers |> Map.add key cb)
                            | RemoveWatcher(key) ->
                                return! loop state (watchers |> Map.remove key)

                    } 
                loop v Map.empty
                )
    { container = mbp }

/// Swaps the value of the atom by setting it to the application of f to the current value
let swap<'T> f (a : 'T atom) =
    a.container.PostAndReply(fun c -> Swap(f,c))

/// Compares the oldValue with the current state of the atom, if equal sets the new state to the newValue otherwise no change
let compareAndSet oldValue newValue (a : 'T atom) =
    a.container.PostAndReply(fun c -> CompareAndSet(oldValue, newValue, c))

/// Sets the new value of the atom to v disregarding the previous value
let set v (a : 'T atom) =
    a.container.Post(Reset(v))
    v

/// Dereferences the value of the atom
let (!) (a : atom<_>) =
    a.container.PostAndReply(fun c -> Deref(c))

/// Adds a watcher to the atom that will be notified whenever the state changes
let addWatch k f (a : atom<_>) =
    a.container.Post(AddWatcher(k, f))

/// Removes a previously added watcher from the atom
let removeWatch k (a : atom<_>) =
    a.container.Post(RemoveWatcher(k))

