module Sfx

open Fable.Core
open Fable.Core.JsInterop
open Browser
open Browser.Types
open Vec
open Domain

type Param =
    abstract value: float with get, set
    abstract setValueAtTime: float * float -> unit
    abstract linearRampToValueAtTime: float * float -> unit
    abstract exponentialRampToValueAtTime: float * float -> unit

type Node =
    abstract connect: Node -> Node

type Osc =
    inherit Node
    abstract ``type``: string with get, set
    abstract frequency: Param
    abstract detune: Param
    abstract start: float -> unit
    abstract stop: float -> unit

type Gain =
    inherit Node
    abstract gain: Param

type Panner =
    inherit Node
    abstract pan: Param

type Filter =
    inherit Node
    abstract ``type``: string with get, set
    abstract frequency: Param
    abstract Q: Param

type Delay =
    inherit Node
    abstract delayTime: Param

type Buffer =
    abstract getChannelData: int -> float[]

type Source =
    inherit Node
    abstract buffer: Buffer with get, set
    abstract start: float -> unit
    abstract stop: float -> unit

type Ctx =
    abstract currentTime: float
    abstract sampleRate: float
    abstract destination: Node
    abstract state: string
    abstract resume: unit -> unit
    abstract createOscillator: unit -> Osc
    abstract createGain: unit -> Gain
    abstract createStereoPanner: unit -> Panner
    abstract createBiquadFilter: unit -> Filter
    abstract createDelay: float -> Delay
    abstract createBufferSource: unit -> Source
    abstract createBuffer: int * int * float -> Buffer

[<Emit("new (window.AudioContext || window.webkitAudioContext)()")>]
let private newCtx () : Ctx = jsNative

let mutable private ctx: Ctx = Unchecked.defaultof<Ctx>
let mutable private master: Gain = Unchecked.defaultof<Gain>
let mutable private send: Gain = Unchecked.defaultof<Gain>
let mutable private noiseBuf: Buffer = Unchecked.defaultof<Buffer>
let mutable private engine: Gain = Unchecked.defaultof<Gain>
let mutable private engineCut: Filter = Unchecked.defaultof<Filter>
let mutable private crazyGamesMuted = false
let mutable private localMuted = false
let mutable private adMuted = false
let isMuted () = crazyGamesMuted || localMuted || adMuted

let private volume = 0.32
let private rnd = System.Random()
let private audioKey = "nda-audio"

let mutable private levels = ResizeArray [ 1.; 1. ]
let private music = document.createElement "audio" :?> HTMLAudioElement

let private apply () =
    let m = isMuted ()
    music.volume <- 0.35 * levels.[1]
    music.muted <- m
    if not (isNullOrUndefined (box ctx)) then
        master.gain.value <- (if m then 0. else volume * levels.[0])

let setCrazyGamesMuted (v: bool) =
    crazyGamesMuted <- v
    apply ()

let setAdMuted (v: bool) =
    adMuted <- v
    apply ()

let level k = levels.[k]

let setLevel k v =
    levels.[k] <- System.Math.Round (10. * max 0. (min 1. v)) / 10.
    window.localStorage.setItem (audioKey, JS.JSON.stringify levels)
    apply ()

let private battle = [| "battle1"; "battle2"; "battle3" |]
let mutable private inMenu = true

let private pick () =
    let name = if inMenu then "menu" else battle.[rnd.Next battle.Length]
    music.src <- sprintf "/music/%s.mp3" name
    music.loop <- inMenu
    music.play () |> ignore

let private build () =
    ctx <- newCtx ()
    master <- ctx.createGain ()
    apply ()
    master.connect ctx.destination |> ignore

    let delay = ctx.createDelay 1.
    delay.delayTime.value <- 0.135
    let fb = ctx.createGain ()
    fb.gain.value <- 0.36
    let shimmer = ctx.createBiquadFilter ()
    shimmer.``type`` <- "lowpass"
    shimmer.frequency.value <- 2400.
    delay.connect (shimmer :> Node) |> ignore
    shimmer.connect (fb :> Node) |> ignore
    fb.connect (delay :> Node) |> ignore
    delay.connect (master :> Node) |> ignore
    send <- ctx.createGain ()
    send.gain.value <- 0.22
    send.connect (delay :> Node) |> ignore

    let n = int (ctx.sampleRate * 0.5)
    noiseBuf <- ctx.createBuffer (1, n, ctx.sampleRate)
    let data = noiseBuf.getChannelData 0
    for i in 0 .. n - 1 do
        data.[i] <- rnd.NextDouble () * 2. - 1.

    engineCut <- ctx.createBiquadFilter ()
    engineCut.``type`` <- "lowpass"
    engineCut.frequency.value <- 300.
    engine <- ctx.createGain ()
    engine.gain.value <- 0.
    engineCut.connect (engine :> Node) |> ignore
    engine.connect (master :> Node) |> ignore
    for wave, hz in [ "sawtooth", 55.; "square", 82.5 ] do
        let o = ctx.createOscillator ()
        o.``type`` <- wave
        o.frequency.value <- hz
        o.connect (engineCut :> Node) |> ignore
        o.start ctx.currentTime

let asleep () = isNullOrUndefined (box ctx) || ctx.state = "suspended"

let private ready () =
    if isNullOrUndefined (box ctx) then
        build ()
        music.onended <- fun _ -> pick ()
        pick ()
    elif ctx.state = "suspended" then ctx.resume ()
    not (isMuted ())

let track menu =
    if menu <> inMenu then
        inMenu <- menu
        if not (isNullOrUndefined (box ctx)) then pick ()

let private now () = ctx.currentTime

let private env pan peak dur =
    let g = ctx.createGain ()
    let t = now ()
    g.gain.setValueAtTime (0.0001, t)
    g.gain.exponentialRampToValueAtTime (max 0.0002 peak, t + 0.005)
    g.gain.exponentialRampToValueAtTime (0.0001, t + dur)
    let p = ctx.createStereoPanner ()
    p.pan.value <- max -0.9 (min 0.9 pan)
    g.connect (p :> Node) |> ignore
    p.connect (master :> Node) |> ignore
    p.connect (send :> Node) |> ignore
    g

let private tone wave pan f0 f1 dur peak =
    let o = ctx.createOscillator ()
    o.``type`` <- wave
    let t = now ()
    o.frequency.setValueAtTime (f0, t)
    o.frequency.exponentialRampToValueAtTime (max 20. f1, t + dur)
    o.connect (env pan peak dur :> Node) |> ignore
    o.start t
    o.stop (t + dur + 0.03)

let private noise kind pan cutoff dur peak q =
    let s = ctx.createBufferSource ()
    s.buffer <- noiseBuf
    let f = ctx.createBiquadFilter ()
    f.``type`` <- kind
    let t = now ()
    f.frequency.setValueAtTime (cutoff, t)
    f.frequency.exponentialRampToValueAtTime (max 60. (cutoff * 0.3), t + dur)
    f.Q.value <- q
    s.connect (f :> Node) |> ignore
    f.connect (env pan peak dur :> Node) |> ignore
    s.start t
    s.stop (t + dur + 0.03)

let private panOf (p: V2) = p.X / Cfg.arenaHalf

let private jitter () = 0.94 + rnd.NextDouble () * 0.12

type Cue =
    | Tick
    | Go
    | Win
    | Alert

let cue c =
    if ready () then
        match c with
        | Tick -> tone "square" 0. 880. 880. 0.09 0.5
        | Go ->
            tone "square" 0. 660. 660. 0.1 0.55
            tone "square" 0. 1320. 1320. 0.3 0.4
        | Win ->
            tone "square" -0.2 523. 523. 0.12 0.4
            tone "square" 0.2 784. 784. 0.14 0.4
            tone "sawtooth" 0. 1046. 1046. 0.6 0.35
        | Alert ->
            tone "square" 0. 1174. 1174. 0.08 0.34
            tone "square" 0. 1568. 1568. 0.18 0.3

let private shot p =
    let x = panOf p
    tone "square" x (940. * jitter ()) 130. 0.085 0.24
    noise "bandpass" x 2600. 0.05 0.12 1.

let private hit p =
    let x = panOf p
    noise "bandpass" x 1900. 0.13 0.4 1.4
    tone "square" x 320. 90. 0.11 0.3

let private ram p =
    let x = panOf p
    noise "lowpass" x 700. 0.3 0.55 0.8
    tone "sawtooth" x 150. 45. 0.28 0.35

let private bump p =
    let x = panOf p
    tone "sine" x 196. 88. 0.55 0.3
    tone "sine" x 203. 91. 0.55 0.22
    noise "highpass" x 3200. 0.09 0.2 1.

let private pickup p big =
    let x = panOf p
    tone "square" x 660. 660. 0.07 0.28
    tone "square" x 990. 990. 0.12 0.26
    if big then tone "square" x 1320. 1320. 0.22 0.26

let private mend p =
    let x = panOf p
    tone "sine" x 440. 440. 0.1 0.3
    tone "sine" x 660. 660. 0.22 0.28
    tone "sine" x 880. 880. 0.34 0.2

let private grab p =
    let x = panOf p
    tone "square" x 523. 523. 0.07 0.26
    tone "square" x 784. 784. 0.12 0.26
    tone "square" x 1046. 1046. 0.24 0.24
    noise "highpass" x 4200. 0.18 0.14 1.

let private charging p =
    let x = panOf p
    tone "sawtooth" x 180. 1500. (max 0.2 Cfg.railCharge) 0.2

let private beam p =
    let x = panOf p
    tone "sawtooth" x 2400. 150. 0.4 0.5
    tone "square" x 1200. 90. 0.3 0.3
    noise "bandpass" x 3000. 0.3 0.4 0.7

let private mineSet p =
    let x = panOf p
    tone "square" x 150. 110. 0.06 0.22
    noise "highpass" x 2600. 0.05 0.12 1.

let private mineLive p =
    let x = panOf p
    tone "square" x 1046. 1046. 0.07 0.3
    tone "square" x 1318. 1318. 0.14 0.28

let private blast p =
    let x = panOf p
    noise "lowpass" x 1400. 0.5 0.6 0.9
    tone "sawtooth" x 320. 40. 0.42 0.4

let private wave p =
    let x = panOf p
    noise "bandpass" x 500. 0.34 0.45 2.2
    tone "sine" x 90. 300. 0.3 0.3

let private zap p =
    let x = panOf p
    noise "highpass" x 2500. 0.22 0.4 1.
    tone "square" x 1800. 200. 0.18 0.25

let private latch p =
    let x = panOf p
    tone "sine" x 220. 660. 0.3 0.3
    tone "triangle" x 110. 330. 0.3 0.2

let private cooked p =
    let x = panOf p
    tone "square" x 420. 70. 0.42 0.32
    noise "lowpass" x 1800. 0.35 0.3 0.8

let private explode p =
    let x = panOf p
    noise "lowpass" x 1100. 0.75 0.7 0.9
    tone "sawtooth" x 220. 32. 0.6 0.45
    tone "square" x 110. 24. 0.45 0.3

let play (events: Event list) =
    if ready () then
        let mutable shots = 0
        let mutable bumps = 0
        for e in events do
            match e with
            | Shot p ->
                if shots < 2 then
                    shots <- shots + 1
                    shot p
            | Hit(p, _, _, _) -> hit p
            | Medal _ -> ()
            | Ram p -> ram p
            | Bump p ->
                if bumps < 2 then
                    bumps <- bumps + 1
                    bump p
            | Pickup(p, big) -> pickup p big
            | Mend p -> mend p
            | Grab p -> grab p
            | Charging p -> charging p
            | Beam(p, _, _) -> beam p
            | MineSet p -> mineSet p
            | MineLive p -> mineLive p
            | Blast p -> blast p
            | Wave(p, _, _) -> wave p
            | Cooked p -> cooked p
            | Zap(p, _, _) -> zap p
            | Latch(p, _) -> latch p
            | Launch p -> bump p
            | PortalOpen(p, _) -> mineLive p
            | Warp p -> pickup p false
            | HoleOpen p -> blast p
            | Explode(p, _, _) -> explode p
            | Downed _ -> ()
            | Finished _ -> ()

let thrust (w: World) =
    if ready () then
        let ships = w.Ships |> Array.filter (fun s -> s.Alive && s.Thrusting > 0.)
        let level = ships |> Array.sumBy (fun s -> if s.Thrusting > 1. then 0.09 else 0.035)
        let speed = ships |> Array.fold (fun acc s -> max acc (Vec.len s.Vel)) 0.
        let t = now () + 0.05
        engine.gain.linearRampToValueAtTime (min 0.16 level, t)
        engineCut.frequency.linearRampToValueAtTime (180. + speed / Cfg.maxSpeed * 620., t)

let silence () =
    if not (isNullOrUndefined (box ctx)) then
        engine.gain.linearRampToValueAtTime (0., now () + 0.08)

let init () =
    for ev in [ "keydown"; "pointerdown"; "gamepadconnected" ] do
        window.addEventListener (ev, fun _ -> ready () |> ignore)
    window.addEventListener (
        "keydown",
        fun e ->
            if (e :?> KeyboardEvent).key = "m" then
                localMuted <- not localMuted
                apply ()
    )
    window.addEventListener (
        "visibilitychange",
        fun _ ->
            if not document.hidden then ready () |> ignore
    )
    match window.localStorage.getItem audioKey with
    | null -> ()
    | json -> levels <- JS.JSON.parse json :?> ResizeArray<float>
