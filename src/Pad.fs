module Pad

open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop

let private stickRadius = 70.
let private boostDrag = 60.
let private heartbeat = 500.

let private id =
    match window.sessionStorage.getItem "nda-pad-id" with
    | null ->
        let v = string (int (JS.Math.random () * 1e8))
        window.sessionStorage.setItem ("nda-pad-id", v)
        v
    | v -> v

let private state = createObj [ "id" ==> id ]
let mutable private dirty = true
let mutable private lastSent = 0.
let mutable private phase = ""
let mutable private card: obj = null
let private root = document.getElementById "pad"

let private set (k: string) (v: obj) =
    if state?(k) <> v then
        state?(k) <- v
        dirty <- true

let private clearInputs () =
    for k in [ "x"; "y"; "fire"; "boost"; "special"; "start"; "back"; "left"; "right"; "up"; "down" ] do
        set k (if k = "x" || k = "y" then box 0. else box false)

let private fullscreen () =
    try
        let p: JS.Promise<unit> = document.documentElement?requestFullscreen ()
        p?``then``((fun () -> try window?screen?orientation?lock "landscape" |> ignore with _ -> ()), fun _ -> ()) |> ignore
    with _ -> ()

let private hold (el: Element) (key: string) =
    let down (e: Event) =
        e.preventDefault ()
        set key true
        el.classList.add "on"
    let up (_: Event) =
        set key false
        el.classList.remove "on"
    el.addEventListener ("pointerdown", down)
    el.addEventListener ("pointerup", up)
    el.addEventListener ("pointercancel", up)
    el.addEventListener ("pointerleave", up)

let private button (cls: string) (key: string) (label: string) =
    sprintf "<div class=\"btn %s\" data-k=\"%s\"><span>%s</span></div>" cls key label

let private wire () =
    let list = root.querySelectorAll "[data-k]"
    for i in 0 .. list.length - 1 do
        let el = list.[i] :?> Element
        hold el (el.getAttribute "data-k")

let private place (el: Element) (x: float) (y: float) =
    el?style?left <- sprintf "%fpx" x
    el?style?top <- sprintf "%fpx" y

let private stick (zone: Element) =
    let ring = zone.querySelector ".ring"
    let knob = zone.querySelector ".knob"
    let mutable pid = -1.
    let mutable ox = 0.
    let mutable oy = 0.
    zone.addEventListener ("pointerdown", fun e ->
        if pid < 0. then
            pid <- e?pointerId
            ox <- e?clientX
            oy <- e?clientY
            try zone?setPointerCapture pid with _ -> ()
            place ring ox oy
            place knob ox oy
            ring.classList.remove "hidden"
            knob.classList.remove "hidden")
    zone.addEventListener ("pointermove", fun e ->
        if (e?pointerId: float) = pid then
            let dx, dy = (e?clientX: float) - ox, (e?clientY: float) - oy
            let mag = sqrt (dx * dx + dy * dy)
            let s = if mag > stickRadius then stickRadius / mag else 1.
            place knob (ox + dx * s) (oy + dy * s)
            set "x" (dx * s / stickRadius)
            set "y" (dy * s / stickRadius))
    let up (e: Event) =
        if (e?pointerId: float) = pid then
            pid <- -1.
            set "x" 0.
            set "y" 0.
            ring.classList.add "hidden"
            knob.classList.add "hidden"
    zone.addEventListener ("pointerup", up)
    zone.addEventListener ("pointercancel", up)

let private fireZone (zone: Element) =
    let mutable pid = -1.
    let mutable oy = 0.
    zone.addEventListener ("pointerdown", fun e ->
        if pid < 0. then
            pid <- e?pointerId
            oy <- e?clientY
            try zone?setPointerCapture pid with _ -> ()
            set "fire" true
            zone.classList.add "on")
    zone.addEventListener ("pointermove", fun e ->
        if (e?pointerId: float) = pid then
            let boost = oy - (e?clientY: float) > boostDrag
            set "boost" boost
            zone.classList.toggle ("boost", boost) |> ignore)
    let up (e: Event) =
        if (e?pointerId: float) = pid then
            pid <- -1.
            set "fire" false
            set "boost" false
            zone.classList.remove "on"
            zone.classList.remove "boost"
    zone.addEventListener ("pointerup", up)
    zone.addEventListener ("pointercancel", up)

let private render () =
    clearInputs ()
    match phase with
    | "play" ->
        root.innerHTML <-
            sprintf "<div class=\"zone\" id=\"stick\"><div class=\"ring hidden\"></div><div class=\"knob hidden\"></div></div><div class=\"zone fire\" id=\"fire\">%s<small>%s</small></div>%s%s"
                Strings.t.PadFire Strings.t.PadBoostHint
                (button "special" "special" Strings.t.PadSpecial)
                (button "menu" "start" "&#9776;")
        stick (document.getElementById "stick")
        fireZone (document.getElementById "fire")
    | "lobby" when isNullOrUndefined card ->
        root.innerHTML <-
            sprintf "<div class=\"card\"><div class=\"title\">%s %s</div>%s</div>" Strings.t.TitleMain Strings.t.TitleSub (button "big" "fire" Strings.t.Join)
    | "lobby" ->
        let ready: bool = card?ready
        root.innerHTML <-
            sprintf "<div class=\"card\" style=\"color:%s\"><b>%s</b><div class=\"art\">%s</div><div class=\"row\">%s%s%s</div><div class=\"row\">%s%s</div></div>"
                (card?color: string) (card?name: string) (card?ship: string)
                (button "" "left" "&#9664;")
                (button (if ready then "big ready" else "big") "fire" (if ready then Strings.t.Ready else (card?pick: string)))
                (button "" "right" "&#9654;")
                (button "" "back" Strings.t.PadBack) (button "" "start" Strings.t.Start)
    | "menu" ->
        root.innerHTML <-
            sprintf "<div class=\"card\"><div class=\"row\">%s%s%s%s</div><div class=\"row\">%s%s</div></div>"
                (button "" "left" "&#9664;") (button "" "up" "&#9650;") (button "" "down" "&#9660;") (button "" "right" "&#9654;")
                (button "big" "fire" Strings.t.PadOk) (button "" "back" Strings.t.PadBack)
    | _ -> root.innerHTML <- sprintf "<div class=\"card\"><div class=\"title\">%s</div></div>" Strings.t.PadConnecting
    wire ()

let private tick () =
    let t = JS.Constructors.Date.now ()
    if dirty || t - lastSent > heartbeat then
        Input.hotSend "nda:pad" state
        dirty <- false
        lastSent <- t

Input.hotOn "nda:host" (fun m ->
    let p: string = m?phase
    let c: obj = m?pads?(id)
    if p <> phase || JS.JSON.stringify c <> JS.JSON.stringify card then
        phase <- p
        card <- c
        render ())

document?addEventListener ("pointerdown", (fun (_: Event) -> fullscreen ()), createObj [ "once" ==> true ])
document?addEventListener ("touchmove", (fun (e: Event) -> e.preventDefault ()), createObj [ "passive" ==> false ])
render ()
window.setInterval (tick, 33) |> ignore
