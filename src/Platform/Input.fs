module Input

open System.Collections.Generic
open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop
open Domain

type PadPref = { Slot: int; Swap: bool; Absolute: bool }

let autoSlot = -1
let prefs = Dictionary<int, PadPref>()
let mutable keyboardSlot = autoSlot

let pref i =
    match prefs.TryGetValue i with
    | true, p -> p
    | _ -> { Slot = autoSlot; Swap = false; Absolute = true }

[<Emit("import.meta.hot")>]
let hot: obj = jsNative

[<Emit("Math.random().toString(36).slice(2, 6).toUpperCase()")>]
let private newRoom () : string = jsNative

let private isPad = window.location.pathname.EndsWith "pad.html"

let private room =
    if not (isNullOrUndefined hot) then ""
    elif isPad then window.location.hash.TrimStart '#'
    else
        match window.sessionStorage.getItem "nda-room" with
        | null ->
            let code = newRoom ()
            window.sessionStorage.setItem ("nda-room", code)
            code
        | code -> code

let padUrl = if isPad || room = "" then "" else window.location.origin + "/pad.html#" + room

let private handlers = Dictionary<string, obj -> unit>()
let mutable private sock: obj = null

let rec private connect () =
    let proto = if window.location.protocol = "https:" then "wss://" else "ws://"
    let s = createNew window?WebSocket (proto + window.location.host + "/relay?room=" + room + (if isPad then "&role=pad" else "&role=host"))
    sock <- s
    s?onmessage <- fun (e: obj) ->
        let msg = JS.JSON.parse (e?data: string)
        match handlers.TryGetValue(msg?``event``: string) with
        | true, f -> f msg?data
        | _ -> ()
    s?onclose <- fun (_: obj) -> window.setTimeout (connect, 1000) |> ignore

if room <> "" then connect ()

let hotOn (ev: string) (f: obj -> unit) =
    if not (isNullOrUndefined hot) then hot?on (ev, f) else handlers.[ev] <- f

let hotSend (ev: string) (data: obj) =
    if not (isNullOrUndefined hot) then
        try hot?send (ev, data) with _ -> ()
    elif not (isNull sock) && (sock?readyState: int) = 1 then
        sock?send (JS.JSON.stringify (createObj [ "event" ==> ev; "data" ==> data ]))

let private phones = Dictionary<string, obj * float>()
let private phoneSlots = Dictionary<string, int>()
let private phoneSlot id = match phoneSlots.TryGetValue id with | true, s -> s | _ -> autoSlot
let private phoneTimeout = 2000.

let private keys = HashSet<string>()
let mutable private keyboardSeen = false
let private captured = set [ "Space"; "ArrowUp"; "ArrowDown"; "ArrowLeft"; "ArrowRight"; "Escape"; "Enter"; "Tab" ]

let private detectLayout () =
    let kb: obj = window?navigator?keyboard
    if isNullOrUndefined kb then ()
    else
        (kb?getLayoutMap ())?``then``(fun (m: obj) ->
            let w: string = m?get ("KeyW")
            if not (isNullOrUndefined w) && w = "z" then Domain.layout <- Azerty
        ) |> ignore

let init () =
    detectLayout ()
    window.addEventListener (
        "keydown",
        fun e ->
            let ke = e :?> KeyboardEvent
            keys.Add ke.code |> ignore
            keyboardSeen <- true
            if captured.Contains ke.code then e.preventDefault ()
            if ke.ctrlKey && ke.code = "KeyW" then e.preventDefault ()
    )
    window.addEventListener ("keyup", fun e -> keys.Remove (e :?> KeyboardEvent).code |> ignore)
    window.addEventListener ("blur", fun _ -> keys.Clear())
    hotOn "nda:pad" (fun m -> phones.[string m?id] <- (m, JS.Constructors.Date.now ()))

let private key k = keys.Contains k

let private taps = HashSet<string>()

let press (code: string) =
    keys.Add code |> ignore
    taps.Add code |> ignore
    keyboardSeen <- true

let release () =
    window.requestAnimationFrame (fun _ ->
        for c in taps do keys.Remove c |> ignore
        taps.Clear ())
    |> ignore

let private keyboard () =
    { Aim = None
      Absolute = false
      Steer = false
      Turn =
        (if key "ArrowRight" || key "KeyD" then 1. else 0.)
        - (if key "ArrowLeft" || key "KeyA" then 1. else 0.)
      Strafe = (if key "KeyE" then 1. else 0.) - (if key "KeyQ" then 1. else 0.)
      Thrust = key "ArrowUp" || key "KeyW"
      Reverse = key "ArrowDown" || key "KeyS"
      Boost = key "ShiftLeft" || key "ShiftRight"
      Fire = key "Space"
      Special = key "KeyF"
      Start = key "Enter"
      Back = key "Escape"
      Present = keyboardSeen }

let private deadzone x = if abs x < 0.18 then 0. else x

let private lbDown = Dictionary<int, bool>()

let private gamepad (gp: obj) =
    let idx: int = gp?index
    let axes: float[] = gp?axes
    let buttons: obj[] = gp?buttons
    let pressed i = i < buttons.Length && (buttons.[i]?pressed: bool)
    let lb = pressed 3
    let wasLb = match lbDown.TryGetValue idx with | true, b -> b | _ -> false
    lbDown.[idx] <- lb
    if lb && not wasLb then prefs.[idx] <- { pref idx with Absolute = not (pref idx).Absolute }
    let pr = pref idx
    let swap = pr.Swap
    let value i = if i < buttons.Length then (buttons.[i]?value: float) else 0.
    let axis i = if i < axes.Length then deadzone axes.[i] else 0.
    let move, turn = if swap then 2, 0 else 0, 2
    let raw i = if i < axes.Length then axes.[i] else 0.
    let ax, ay = raw turn, raw (turn + 1)
    { Turn = (if pressed 15 then 1. elif pressed 14 then -1. else axis turn)
      Aim = if pr.Absolute && ax * ax + ay * ay > 0.25 then Some (atan2 ay ax) else None
      Absolute = pr.Absolute
      Steer = false
      Strafe = axis move
      Thrust = axis (move + 1) < -0.3 || pressed 12
      Reverse = axis (move + 1) > 0.3 || pressed 13
      Boost = pressed 0 || value 6 > 0.3
      Fire = value 7 > 0.3
      Special = pressed 5
      Start = pressed 9
      Back = pressed 1
      Present = true }

let private phoneRaw (m: obj) =
    let flag k = (m?(k): bool) = true
    let num k = let v: float = m?(k) in if isNullOrUndefined v then 0. else v
    let x, y = num "x", num "y"
    let mag = sqrt (x * x + y * y)
    { Turn = (if flag "right" then 1. elif flag "left" then -1. else 0.)
      Aim = if mag > Cfg.padAimOn then Some (atan2 y x) else None
      Absolute = true
      Steer = true
      Strafe = num "s"
      Thrust = mag > Cfg.padThrustOn || flag "up"
      Reverse = flag "down"
      Boost = flag "boost"
      Fire = flag "fire"
      Special = flag "special"
      Start = flag "start"
      Back = flag "back"
      Present = true }

let private phone (m: obj) =
    if isNullOrUndefined m?input then phoneRaw m else unbox<Input> m?input

let private livePhones () =
    let now = JS.Constructors.Date.now ()
    [ for KeyValue(id, (m, seen)) in phones do if now - seen < phoneTimeout then yield id, m ]

let connected () : obj[] =
    let raw: obj[] = window?navigator?getGamepads ()
    if isNullOrUndefined raw then [||]
    else raw |> Array.filter (fun p -> not (isNullOrUndefined p) && (p?connected: bool))

type Device = { Key: string; Name: string; Slot: int; Input: Input }

let devices () : Device[] =
    [| yield { Key = "kb"; Name = ""; Slot = keyboardSlot; Input = keyboard () }
       for p in connected () do
           let i: int = p?index
           let pr = pref i
           yield { Key = string i; Name = (p?id: string).Split('(').[0].Trim(); Slot = pr.Slot; Input = gamepad p }
       for id, m in livePhones () do
           yield { Key = "ph:" + id; Name = (if isNullOrUndefined m?input then Strings.t.Phone else Strings.t.Remote); Slot = phoneSlot id; Input = phone m } |]

let private remoteId = string (int (JS.Math.random () * 1e8))

let sendRemote () =
    for d in devices () do
        if d.Input.Present && not (d.Key.StartsWith "ph:") then
            hotSend "nda:pad" (createObj [ "id" ==> remoteId + d.Key; "input" ==> d.Input ])

let mutable changed = fun () -> ()

let assign key slot =
    if slot <> autoSlot then
        if keyboardSlot = slot then keyboardSlot <- autoSlot
        for id in Seq.toArray phoneSlots.Keys do
            if phoneSlots.[id] = slot then phoneSlots.[id] <- autoSlot
        for i in Seq.toArray prefs.Keys do
            if prefs.[i].Slot = slot then prefs.[i] <- { prefs.[i] with Slot = autoSlot }
    if key = "kb" then keyboardSlot <- slot
    elif key.StartsWith "ph:" then phoneSlots.[key.Substring 3] <- slot
    else prefs.[int key] <- { pref (int key) with Slot = slot }
    changed ()

let read () : Input[] =
    let out = Array.create 4 noInput
    let free = HashSet [ 0..3 ]
    let place slot input = if free.Remove slot then out.[slot] <- input
    place keyboardSlot (keyboard ())
    let auto = ResizeArray()
    for p in connected () do
        let pref = pref (p?index: int)
        if pref.Slot = autoSlot then auto.Add(gamepad p)
        else place pref.Slot (gamepad p)
    for id, m in livePhones () do
        if phoneSlot id = autoSlot then auto.Add(phone m) else place (phoneSlot id) (phone m)
    for input in auto do
        if free.Count > 0 then place (Seq.min free) input
    out
