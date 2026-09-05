module Pad

open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop

let private stickRadius = 70.
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
let mutable private stats: obj = null
let private root = document.getElementById "pad"

Input.initNetwork ()

let private flush () =
    Input.hotSend "nda:pad" state
    dirty <- false
    lastSent <- JS.Constructors.Date.now ()

let private set (k: string) (v: obj) =
    if state?(k) <> v then
        state?(k) <- v
        if k = "x" || k = "y" || k = "s" then dirty <- true else flush ()

let private clearInputs () =
    for k in [ "x"; "y"; "s"; "fire"; "boost"; "special"; "start"; "back"; "left"; "right"; "up"; "down" ] do
        set k (if k = "x" || k = "y" || k = "s" then box 0. else box false)

let private fullscreen () =
    try
        let p: JS.Promise<unit> = document.documentElement?requestFullscreen ()
        p?``then``((fun () -> window?screen?orientation?lock("landscape")?``catch``(fun _ -> ()) |> ignore), fun _ -> ()) |> ignore
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
            ox <- e?offsetX
            oy <- e?offsetY
            try zone?setPointerCapture pid with _ -> ()
            place ring ox oy
            place knob ox oy
            zone.classList.add "on")
    zone.addEventListener ("pointermove", fun e ->
        if (e?pointerId: float) = pid then
            let dx, dy = (e?offsetX: float) - ox, (e?offsetY: float) - oy
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
            zone.classList.remove "on"
            for el in [ ring; knob ] do
                el?style?left <- ""
                el?style?top <- ""
    zone.addEventListener ("pointerup", up)
    zone.addEventListener ("pointercancel", up)

let private slider (zone: Element) =
    let thumb = zone.querySelector ".thumb"
    let fill = zone.querySelector ".fill"
    let mutable pid = -1.
    let move (e: Event) =
        if (e?pointerId: float) = pid then
            let w: float = zone?clientWidth
            let v = max -1. (min 1. (((e?offsetX: float) - w / 2.) / (w / 2. - 24.)))
            set "s" v
            thumb?style?left <- sprintf "%f%%" (50. + v * 42.)
            fill?style?left <- sprintf "%f%%" (if v < 0. then 50. + v * 42. else 50.)
            fill?style?width <- sprintf "%f%%" (abs v * 42.)
    zone.addEventListener ("pointerdown", fun e ->
        if pid < 0. then
            pid <- e?pointerId
            try zone?setPointerCapture pid with _ -> ()
            zone.classList.add "on"
            move e)
    zone.addEventListener ("pointermove", move)
    let up (e: Event) =
        if (e?pointerId: float) = pid then
            pid <- -1.
            set "s" 0.
            zone.classList.remove "on"
            thumb?style?left <- ""
            fill?style?width <- ""
    zone.addEventListener ("pointerup", up)
    zone.addEventListener ("pointercancel", up)

let private corners = "<div class=\"corner tl\"></div><div class=\"corner tr\"></div><div class=\"corner bl\"></div><div class=\"corner br\"></div>"

let private header =
    sprintf "<div class=\"bar\"><span><b>%s</b> %s</span><span class=\"tag\">%s</span>%s</div>" Strings.t.TitleMain Strings.t.TitleSub Strings.t.PadBadge (button "menu" "start" "&#9776;")

let private footer =
    Strings.t.PhoneLegend
    |> List.map (fun (keys, what) -> sprintf "<span>%s%s</span>" (keys |> List.map (sprintf "<i>%s</i>") |> String.concat "") what)
    |> String.concat ""
    |> sprintf "<div class=\"bar foot\">%s</div>"

let private showStats () =
    match document.getElementById "stats" with
    | null -> ()
    | el when isNullOrUndefined stats || isNullOrUndefined card -> el.innerHTML <- ""
    | el ->
        let meter cls (label: string) (v: float) (sh: float) =
            sprintf "<small>%s</small><div class=\"meter %s\"><i style=\"width:%d%%\"></i><u style=\"width:%d%%\"></u></div>" label cls (int (100. * max 0. (min 1. v))) (int (100. * max 0. (min 1. sh)))
        el.innerHTML <-
            sprintf "<b style=\"color:%s\">%s</b>%s%s<div class=\"nums\"><span>%s <em>%d</em></span><span>%s <em>%d</em></span></div>"
                (card?color: string) (card?name: string)
                (meter "hp" Strings.t.PadHull (stats?hp: float) (stats?sh: float))
                (meter "boost" Strings.t.PadBoost (stats?boost: float) 0.)
                Strings.t.PadStocks (stats?stocks: int) Strings.t.PadKills (stats?kills: int)

let private render () =
    clearInputs ()
    match phase with
    | "play" ->
        root.innerHTML <-
            sprintf "%s%s<div class=\"row2\"><div class=\"panel cyan zone\" id=\"stick\"><div class=\"ring\"></div><div class=\"knob\"></div><div class=\"lbl\">%s<small>%s</small></div></div><div class=\"mid\" id=\"stats\"></div><div class=\"panel mag acts\"><div class=\"hexes\">%s%s%s</div><div class=\"slide\" id=\"strafe\"><div class=\"lbl\">&#9664; %s &#9654;</div><div class=\"track\"><div class=\"fill\"></div><div class=\"thumb\"></div></div></div></div></div>%s"
                corners header Strings.t.PadMove Strings.t.PadMoveHint
                (button "hex special" "special" Strings.t.PadSpecial)
                (button "hex boost" "boost" Strings.t.PadBoost)
                (button "hex fire" "fire" Strings.t.PadFire)
                Strings.t.PadStrafe
                footer
        stick (document.getElementById "stick")
        slider (document.getElementById "strafe")
        showStats ()
    | "lobby" when isNullOrUndefined card ->
        root.innerHTML <-
            sprintf "%s<div class=\"card panel cyan\"><div class=\"title\">%s %s</div>%s</div>" corners Strings.t.TitleMain Strings.t.TitleSub (button "big" "fire" Strings.t.Join)
    | "lobby" ->
        let ready: bool = card?ready
        root.innerHTML <-
            sprintf "%s<div class=\"card panel\" style=\"color:%s;--c:%s\"><b>%s</b><div class=\"art\">%s</div><div class=\"row\">%s%s%s</div><div class=\"row\">%s%s</div></div>"
                corners (card?color: string) (card?color: string) (card?name: string) (card?ship: string)
                (button "" "left" "&#9664;")
                (button (if ready then "big ready" else "big") "fire" (if ready then Strings.t.Ready else (card?pick: string)))
                (button "" "right" "&#9654;")
                (button "" "back" Strings.t.PadBack) (button "" "start" Strings.t.Start)
    | "menu" ->
        root.innerHTML <-
            sprintf "%s<div class=\"card panel mag\"><div class=\"row\">%s%s%s%s</div><div class=\"row\">%s%s</div></div>"
                corners
                (button "" "left" "&#9664;") (button "" "up" "&#9650;") (button "" "down" "&#9660;") (button "" "right" "&#9654;")
                (button "big" "fire" Strings.t.PadOk) (button "" "back" Strings.t.PadBack)
    | _ -> root.innerHTML <- sprintf "%s<div class=\"card\"><div class=\"title\">%s</div></div>" corners Strings.t.PadConnecting
    wire ()

let private tick () =
    if dirty || JS.Constructors.Date.now () - lastSent > heartbeat then flush ()

Input.hotOn "nda:host" (fun m ->
    let p: string = m?phase
    let c: obj = m?pads?(id)
    let s: obj = m?stats?(id)
    if p <> phase || JS.JSON.stringify c <> JS.JSON.stringify card then
        phase <- p
        card <- c
        stats <- s
        render ()
    elif JS.JSON.stringify s <> JS.JSON.stringify stats then
        stats <- s
        showStats ())

document?addEventListener ("pointerdown", (fun (_: Event) -> fullscreen ()), createObj [ "once" ==> true ])
document?addEventListener ("touchmove", (fun (e: Event) -> e.preventDefault ()), createObj [ "passive" ==> false ])
render ()
window.setInterval (tick, 33) |> ignore
