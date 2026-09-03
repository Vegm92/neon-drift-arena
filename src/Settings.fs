module Settings

open Browser
open Fable.Core
open Fable.Core.JsInterop
open Domain

let private tweaksKey = "nda-tweaks"
let private padsKey = "nda-pads"
let private arenaKey = "nda-arena"

let private defaults = Cfg.tunables |> Array.map (fun (_, get, _) -> get ())

type Row =
    | Header of string
    | Slot of string * (unit -> int) * (int -> unit)
    | Swap of string * (unit -> bool) * (bool -> unit)
    | Level of string * int
    | Tune of int
    | Action of string * (unit -> unit)
    | Arena
    | Note of string

let private saveTweaks () =
    let o = obj ()
    for name, get, _ in Cfg.tunables do
        o?(name) <- get ()
    window.localStorage.setItem (tweaksKey, JS.JSON.stringify o)

let private loadTweaks () =
    match window.localStorage.getItem tweaksKey with
    | null -> ()
    | json ->
        let o = JS.JSON.parse json
        for name, _, set in Cfg.tunables do
            let x: float = o?(name)
            if not (isNullOrUndefined x) then set x

let private savePads () =
    let o = obj ()
    o?keyboard <- Input.keyboardSlot
    for KeyValue(i, p) in Input.prefs do
        o?(string i) <- p
    window.localStorage.setItem (padsKey, JS.JSON.stringify o)

let private loadPads () =
    match window.localStorage.getItem padsKey with
    | null -> ()
    | json ->
        let o = JS.JSON.parse json
        if not (isNullOrUndefined o?keyboard) then Input.keyboardSlot <- o?keyboard
        for i in 0..3 do
            let p = o?(string i)
            if not (isNullOrUndefined p) then
                Input.prefs.[i] <- { Slot = p?Slot; Swap = p?Swap; Absolute = (p?Absolute: bool) <> false }

let private padPref i f =
    Input.prefs.[i] <- f (Input.pref i)
    savePads ()

let rows () =
    let pads = Input.connected ()
    [ yield Header Strings.t.Controllers
      yield Slot(Strings.t.Keyboard, (fun () -> Input.keyboardSlot), (fun s -> Input.keyboardSlot <- s; savePads ()))
      if pads.Length = 0 then
          yield Note Strings.t.NoPads
      for p in pads do
          let i: int = p?index
          let name = sprintf "%d · %s" i ((p?id: string).Split('(').[0].Trim())
          yield Slot(name, (fun () -> (Input.pref i).Slot), (fun s -> padPref i (fun pr -> { pr with Slot = s })))
          yield Swap(Strings.t.SwapSticks, (fun () -> (Input.pref i).Swap), (fun b -> padPref i (fun pr -> { pr with Swap = b })))
      yield Header Strings.t.Arena
      yield Arena
      yield Header Strings.t.Audio
      yield Level(Strings.t.Music, 1)
      yield Level(Strings.t.Sounds, 0)
      yield Header Strings.t.Tuning
      for k in 0 .. Cfg.tunables.Length - 1 do
          yield Tune k
      yield
          Action(
              Strings.t.Save,
              fun () ->
                  let o = obj ()
                  for name, get, _ in Cfg.tunables do
                      o?(name) <- get ()
                  window?fetch ("/__tweaks", createObj [ "method" ==> "POST"; "body" ==> JS.JSON.stringify o ]) |> ignore
          )
      yield
          Action(
              Strings.t.Reset,
              fun () ->
                  window.localStorage.removeItem tweaksKey
                  window.location.reload ()
          ) ]

let selectable r =
    match r with
    | Header _
    | Note _ -> false
    | _ -> true

let label r =
    match r with
    | Header t
    | Note t
    | Action(t, _) -> t
    | Slot(t, _, _)
    | Swap(t, _, _)
    | Level(t, _) -> t
    | Arena -> Strings.t.Arena
    | Tune k ->
        let name, _, _ = Cfg.tunables.[k]
        name

let value r =
    match r with
    | Slot(_, get, _) -> if get () = Input.autoSlot then Strings.t.Auto else Strings.t.Player(get ())
    | Swap(_, get, _) -> if get () then Strings.t.On else Strings.t.Off
    | Level(_, k) -> if Sfx.level k = 0. then Strings.t.Off else sprintf "%.0f%%" (Sfx.level k * 100.)
    | Arena -> Strings.t.Arenas.[Sim.layout]
    | Tune k ->
        let _, get, _ = Cfg.tunables.[k]
        sprintf "%.3g" (get ())
    | _ -> ""

let adjust r dir =
    match r with
    | Slot(_, get, set) -> set ((get () + 1 + dir + 5) % 5 - 1)
    | Swap(_, get, set) -> set (not (get ()))
    | Level(_, k) -> Sfx.setLevel k (Sfx.level k + float dir * 0.1)
    | Arena ->
        Sim.setLayout (Sim.layout + dir)
        window.localStorage.setItem (arenaKey, string Sim.layout)
    | Tune k ->
        let _, get, set = Cfg.tunables.[k]
        let step = defaults.[k] / 20.
        set (max 0. (min (defaults.[k] * 3.) (get () + float dir * step)))
        saveTweaks ()
    | _ -> ()

let activate r =
    match r with
    | Action(_, run) -> run ()
    | Swap _
    | Arena -> adjust r 1
    | Level(_, k) -> Sfx.setLevel k (if Sfx.level k = 0. then 1. else 0.)
    | _ -> ()

let init () =
    loadTweaks ()
    loadPads ()
    match window.localStorage.getItem arenaKey with
    | null -> ()
    | v ->
        match System.Int32.TryParse v with
        | true, i when i >= 0 && i < Sim.layouts.Length -> Sim.setLayout i
        | _ -> ()
    Input.changed <- savePads
