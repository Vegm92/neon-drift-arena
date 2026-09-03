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

let private keys = HashSet<string>()
let mutable private keyboardSeen = false
let private captured = set [ "Space"; "ArrowUp"; "ArrowDown"; "ArrowLeft"; "ArrowRight" ]

let init () =
    window.addEventListener (
        "keydown",
        fun e ->
            let code = (e :?> KeyboardEvent).code
            keys.Add code |> ignore
            keyboardSeen <- true
            if captured.Contains code then e.preventDefault ()
    )
    window.addEventListener ("keyup", fun e -> keys.Remove (e :?> KeyboardEvent).code |> ignore)
    window.addEventListener ("blur", fun _ -> keys.Clear())

let private key k = keys.Contains k

let private keyboard () =
    { Aim = None
      Absolute = false
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
      Strafe = axis move
      Thrust = axis (move + 1) < -0.3 || pressed 12
      Reverse = axis (move + 1) > 0.3 || pressed 13
      Boost = pressed 0 || value 6 > 0.3
      Fire = value 7 > 0.3
      Special = pressed 5
      Start = pressed 9
      Back = pressed 1
      Present = true }

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
           yield { Key = string i; Name = (p?id: string).Split('(').[0].Trim(); Slot = pr.Slot; Input = gamepad p } |]

let mutable changed = fun () -> ()

let assign key slot =
    if key = "kb" then keyboardSlot <- slot
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
    for input in auto do
        if free.Count > 0 then place (Seq.min free) input
    out
