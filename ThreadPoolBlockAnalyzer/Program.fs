open System.Diagnostics
open Microsoft.Diagnostics.Runtime
open System

// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.



let detectBlockedThreadPoolThreads (pid : int) =
    let mutable lastReport = -1
    let mutable lastReportBlocked = Map.empty

    let target = Microsoft.Diagnostics.Runtime.DataTarget.AttachToProcess(pid, 10000u, AttachFlag.Passive)
    fun () ->
        try
          let proc = Process.GetProcessById pid
          let v = target.ClrVersions |> Seq.head
          let runtime = v.CreateRuntime()
          let mutable cnt = 0
          let mutable suspended = Map.empty

          let table =
              Map.ofList [
                  for t in proc.Threads do
                      yield uint32 t.Id, t
              ]


          for t in runtime.Threads do
              if t.IsThreadpoolWorker && t.IsAlive then
                  match Map.tryFind t.OSThreadId table with
                      | Some thread ->
                          let state = thread.ThreadState
                          if state = ThreadState.Wait && t.StackTrace.Count > 0 then
                            let reason = 
                                try t.StackTrace.[0].Method.ToString()
                                with _ -> "other"
                            let oldCount = Map.tryFind reason suspended |> Option.defaultValue 0
                            suspended <- Map.add reason (oldCount + 1) suspended

                          else
                            ()
                          //if thread.ThreadState <> System.Diagnostics.ThreadState.Running then
                          //    suspended <- suspended + 1
                      | None -> 
                          ()
                  cnt <- cnt + 1
          if cnt <> lastReport || suspended <> lastReportBlocked then
              printfn "threads: %A (%A)" cnt suspended
              lastReport <- cnt
              lastReportBlocked <- suspended
          ()
        with _ ->
          ()
    


let (|Int|_|) (str : string) =
    match Int32.TryParse str with
        | (true, v) when v > 0 -> Some v
        | _ -> None

[<EntryPoint>]
let main argv =
    //let pid = Process.GetCurrentProcess().Id
    //let run = detectBlockedThreadPoolThreads pid
    //while true do
    //        run()

    match argv with
        | [| Int pid |] ->
            let run = detectBlockedThreadPoolThreads pid
            while true do
                run()
        | _ ->
            printfn "usage analyzer.exe <pid>"
    0 // return an integer exit code
