module Settings

open Browser
open Fable.Core
open Fable.Core.JsInterop
open Domain

let private tweaksKey = "nda-tweaks"
let private padsKey = "nda-pads"
let private bindsKey = "nda-binds"
let private arenaKey = "nda-arena"
let private catchKey = "nda-catchup"
/// The old FLASH & SHAKE key, now the shake half of that row. A player who
/// turned it off carries over as shake off and flashing reduced.
let private shakeKey = "nda-screenfx"
let private flashKey = "nda-reduceflash"

let private defaults = Cfg.tunables |> Array.map (fun (_, get, _) -> get ())

/// TUNING is a developer tool: it only joins the settings list behind `?dev=1`,
/// the same way `?desktop=1` overrides the mobile gate.
let private devMode =
    window.location.search.Split([| '?'; '&' |])
    |> Array.exists (fun p -> p = "dev" || p.StartsWith "dev=")

let mutable arenaPick = 0
let mutable isGuest = true
let mutable onSignInCompleted : string -> unit = fun _ -> ()

let arenaName () =
    if arenaPick = Maps.layouts.Length then Strings.t.Random else Strings.t.Arenas.[State.layout]

let private pool () = [| 0 .. Maps.layouts.Length - 1 |] |> Array.filter (fun i -> Maps.isTrack i = State.race)

let rollArena () =
    if arenaPick = Maps.layouts.Length then
        let p = pool ()
        Sim.setLayout p.[System.Random().Next p.Length]

let fixArena () =
    if arenaPick < Maps.layouts.Length && Maps.isTrack arenaPick <> State.race then
        arenaPick <- (pool ()).[0]
        Sim.setLayout arenaPick

type Row =
    | Header of string
    | Slot of string * (unit -> int) * (int -> unit)
    | Swap of string * (unit -> bool) * (bool -> unit)
    | Level of string * int
    | Tune of int
    | Action of string * (unit -> unit)
    | Arena
    | Note of string
    | Bind of Binds.Act

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
        for i in 0..3 do
            let p = o?(string i)
            if not (isNullOrUndefined p) then
                Input.prefs.[i] <- { Slot = Input.autoSlot; Swap = p?Swap; Absolute = (p?Absolute: bool) <> false }

let private padPref i f =
    Input.prefs.[i] <- f (Input.pref i)
    savePads ()

let private saveBinds () =
    let o = obj ()
    for a in Binds.all do
        o?(Binds.name a) <- Binds.get a
    window.localStorage.setItem (bindsKey, JS.JSON.stringify o)

/// A stored map written by an older build can be missing actions, or carry junk
/// for one: anything that does not read back as a non-empty array of codes leaves
/// that action on its default.
let private loadBinds () =
    match window.localStorage.getItem bindsKey with
    | null -> ()
    | json ->
        let o = JS.JSON.parse json
        if not (isNullOrUndefined o) then
            for a in Binds.all do
                let v = o?(Binds.name a)
                if not (isNullOrUndefined v) && JS.Constructors.Array.isArray v then
                    let codes =
                        unbox<string[]> v
                        |> Array.filter (fun c -> not (isNullOrUndefined c) && c <> "")
                        |> Array.distinct
                    if codes.Length > 0 then Binds.set a codes

/// The action waiting for a keypress, and whether the captured code is added to
/// its list (true) or replaces it (false).
let mutable private grabbing : (Binds.Act * bool) option = None

let capturing () = grabbing.IsSome

let cancelCapture () = grabbing <- None

let bindingOf (a: Binds.Act) =
    match grabbing with
    | Some(b, _) when Binds.ord b = Binds.ord a -> Strings.t.BindPress
    | _ -> Strings.bindKeys a

/// Takes the captured code. Escape always cancels, which is what keeps Escape
/// itself bindable-proof and the menus reachable. A code that is another action's
/// last remaining binding is refused outright rather than leaving that action
/// unusable; anything else is stolen from the actions that also hold it.
let private takeBind (code: string) =
    match grabbing with
    | None -> ()
    | Some(a, add) ->
        grabbing <- None
        if code <> "Escape" && not (Binds.isSoleBindingOf code a) then
            let cur = Binds.get a
            let next =
                if add then (if Array.contains code cur then cur else Array.append cur [| code |])
                else [| code |]
            Binds.set a next
            for b in Binds.all do
                if Binds.ord b <> Binds.ord a then
                    let cs = Binds.get b
                    let kept = cs |> Array.filter (fun c -> c <> code)
                    if kept.Length > 0 && kept.Length <> cs.Length then Binds.set b kept
            saveBinds ()

let private dropBind (a: Binds.Act) =
    let cs = Binds.get a
    if cs.Length > 1 then
        Binds.set a (Array.sub cs 0 (cs.Length - 1))
        saveBinds ()

let private resetBinds () =
    grabbing <- None
    Binds.reset ()
    window.localStorage.removeItem bindsKey

let rows (machine: bool) =
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
      yield Header Strings.t.Bindings
      yield Note (Strings.bindHint ())
      yield Note Strings.t.BindTaken
      for a in Binds.all do
          yield Bind a
      yield Action(Strings.t.ResetKeys, resetBinds)
      if not machine then
          yield Header Strings.t.Arena
          yield Arena
          yield Swap(Strings.t.CatchUp, (fun () -> State.catchUp), (fun b -> State.catchUp <- b; window.localStorage.setItem (catchKey, string b)))
      yield Header Strings.t.Video
      yield
          Swap(
              Strings.t.ReduceFlash,
              (fun () -> RenderTypes.reduceFlash),
              fun b ->
                  RenderTypes.reduceFlash <- b
                  window.localStorage.setItem (flashKey, string b)
          )
      yield
          Swap(
              Strings.t.ScreenShake,
              (fun () -> RenderTypes.screenShake),
              fun b ->
                  RenderTypes.screenShake <- b
                  window.localStorage.setItem (shakeKey, string b)
          )
      yield Header Strings.t.Audio
      yield Level(Strings.t.Music, 1)
      yield Level(Strings.t.Sounds, 0)
      if devMode && not machine then
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
              )
      if isGuest then
          yield Header Strings.t.CrazyGames
          yield Action(Strings.t.SignIn, fun () ->
              async {
                  let! user = CrazyGames.showAuthPrompt() |> Async.AwaitPromise
                  if not (isNullOrUndefined user) && not (isNullOrUndefined user.username) then
                      onSignInCompleted user.username
              } |> Async.StartImmediate
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
    | Bind a -> Strings.actionName a
    | Tune k ->
        let name, _, _ = Cfg.tunables.[k]
        name

let value r =
    match r with
    | Slot(_, get, _) -> if get () = Input.autoSlot then Strings.t.Auto else Strings.t.Player(get ())
    | Swap(_, get, _) -> if get () then Strings.t.On else Strings.t.Off
    | Level(_, k) -> if Sfx.level k = 0. then Strings.t.Off else sprintf "%.0f%%" (Sfx.level k * 100.)
    | Arena -> arenaName ()
    | Bind a -> bindingOf a
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
        let ring = Array.append (pool ()) [| Maps.layouts.Length |]
        let i = ring |> Array.tryFindIndex ((=) arenaPick) |> Option.defaultValue 0
        arenaPick <- ring.[(i + dir + ring.Length) % ring.Length]
        if arenaPick < Maps.layouts.Length then Sim.setLayout arenaPick
        window.localStorage.setItem (arenaKey, string arenaPick)
    | Bind a -> if dir > 0 then grabbing <- Some(a, true) else dropBind a
    | Tune k ->
        let _, get, set = Cfg.tunables.[k]
        let step = defaults.[k] / 20.
        set (max 0. (min (defaults.[k] * 3.) (get () + float dir * step)))
        saveTweaks ()
    | _ -> ()

let activate r =
    match r with
    | Action(_, run) -> run ()
    | Bind a -> grabbing <- Some(a, false)
    | Swap _
    | Arena -> adjust r 1
    | Level(_, k) -> Sfx.setLevel k (if Sfx.level k = 0. then 1. else 0.)
    | _ -> ()

let init () =
    loadTweaks ()
    loadPads ()
    loadBinds ()
    Input.capturing <- capturing
    Input.capture <- takeBind
    match window.localStorage.getItem arenaKey with
    | null -> ()
    | v ->
        match System.Int32.TryParse v with
        | true, i when i >= 0 && i < Maps.layouts.Length ->
            arenaPick <- i
            Sim.setLayout i
        | true, i when i = Maps.layouts.Length -> arenaPick <- i
        | _ -> ()
    fixArena ()
    State.catchUp <- window.localStorage.getItem catchKey <> "false"
    let shakeOff = window.localStorage.getItem shakeKey = "false"
    RenderTypes.screenShake <- not shakeOff
    RenderTypes.reduceFlash <-
        match window.localStorage.getItem flashKey with
        | null -> shakeOff
        | v -> v = "true"
    Input.changed <- savePads
