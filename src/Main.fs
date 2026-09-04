module Main

open Browser
open Fable.Core.JsInterop
open Domain

Input.init ()
Settings.init ()
Sfx.init ()
let view = Render.create ()
let banner = document.getElementById "banner"
let mutable world = Sim.initial
let mutable last = 0.
let mutable lastHost = 0.
let mutable acc = 0.
let mutable countdown = 0.
let mutable slowmo = 0.
let mutable hitstop = 0.
let mutable finish = 0.
let mutable ending = false
let mutable shout = 0.
let mutable shoutText = ""
let mutable bled = false
let private lowStock = Array.create 4 false
let private hyped = Array.create 4 false
let mutable endTitle = ""
let mutable endNote = ""

let private masked () =
    Input.read () |> Array.mapi (fun i x -> if Menu.joined.Contains i then x else noInput)

let private go () =
    countdown <- 3.
    acc <- 0.
    slowmo <- 0.
    hitstop <- 0.
    finish <- 0.
    ending <- false
    shout <- 0.
    bled <- false
    Array.fill lowStock 0 4 false
    Array.fill hyped 0 4 false
    Menu.note <- ""

let private name i = Strings.t.Player i

let private title (w: World) =
    match w.Phase with
    | Over(Some i) ->
        let s = w.Ships.[i]
        Strings.t.Wins(if s.Team > 0 then [| ""; Strings.t.Blue; Strings.t.Red |].[s.Team] else name i)
    | _ -> Strings.t.Draw

let private stats (w: World) =
    let act = w.Ships |> Array.filter (fun s -> s.Active)
    let lines = ResizeArray()
    match act |> Array.sortByDescending (fun s -> s.Kills) |> Array.tryHead with
    | Some s when s.Kills > 0 -> lines.Add(Strings.t.MostKills (name s.Id) s.Kills)
    | _ -> ()
    let aim =
        act
        |> Array.filter (fun s -> s.Shots >= 5)
        |> Array.sortByDescending (fun s -> float s.Hits / float s.Shots)
        |> Array.tryHead
    match aim with
    | Some s -> lines.Add(Strings.t.BestAim (name s.Id) (int (100. * float s.Hits / float s.Shots)))
    | None -> ()
    match act |> Array.sortByDescending (fun s -> s.Rings) |> Array.tryHead with
    | Some s when s.Rings > 0 -> lines.Add(Strings.t.MostRings (name s.Id) s.Rings)
    | _ -> ()
    match act |> Array.tryFind (fun s -> s.Grabs = 0) with
    | Some s -> lines.Add(Strings.t.NoCrates(name s.Id))
    | None -> ()
    lines |> String.concat "\n"

let private say text =
    shoutText <- text
    shout <- 1.4
    Sfx.cue Sfx.Alert

let private announce (w: World) (events: Event list) =
    let deaths = events |> List.choose (function Explode(_, i, ring) -> Some(i, ring) | _ -> None)
    if deaths.Length >= 2 then say Strings.t.DoubleKill
    elif not deaths.IsEmpty then
        let i, ring = deaths.Head
        if not bled then
            bled <- true
            say Strings.t.FirstBlood
        elif ring then
            say Strings.t.RingOut
    w.Ships
    |> Array.iter (fun s ->
        if s.Active && s.Stocks = 1 && not lowStock.[s.Id] then
            lowStock.[s.Id] <- true
            if shout <= 0. then say (Strings.t.LastStock(name s.Id))
        if s.Streak = 0 then hyped.[s.Id] <- false
        elif s.Streak >= 3 && not hyped.[s.Id] then
            hyped.[s.Id] <- true
            if shout <= 0. then say (Strings.t.OnFire(name s.Id)))

let private padStats (card: obj) =
    if isNull card then null
    else
        let s = world.Ships.[card?slot]
        createObj [ "hp" ==> s.Hp / Cfg.hpMax; "sh" ==> s.Shield / Cfg.hpMax; "boost" ==> s.Boost / Cfg.boostMax; "stocks" ==> s.Stocks; "kills" ==> s.Kills ]

let private broadcast () =
    let phones = [ for d in Input.devices () do if d.Key.StartsWith "ph:" then d.Key.Substring 3, Menu.padCard d.Key ]
    Input.hotSend "nda:host" (createObj [ "phase" ==> Menu.phase (); "pads" ==> createObj [ for id, c in phones -> id ==> c ]; "stats" ==> createObj [ for id, c in phones -> id ==> padStats c ] ])

let rec frame (t: float) =
    let dt = if last = 0. then 0. else (t - last) / 1000. |> max 0. |> min 0.1
    last <- t
    if t - lastHost > 100. then
        lastHost <- t
        broadcast ()
    Sfx.track (Menu.visible ())
    if Menu.visible () then
        Sfx.silence ()
        match Menu.update (masked ()) with
        | Some Menu.Rematch when Menu.screen = Menu.Lobby ->
            world <- { Sim.initial with Rng = System.Random().Next 1000003 } |> Sim.withTeams Menu.teams
            go ()
        | Some Menu.Rematch
        | Some Menu.Restart ->
            world <- Sim.reset world
            go ()
        | Some Menu.Resume -> go ()
        | Some Menu.Quit ->
            world <- Sim.initial
            Menu.show ()
        | None -> ()
        Render.syncArena view
        Render.draw view world [] dt
    elif countdown > 0. then
        let prev = ceil countdown |> int
        countdown <- countdown - dt
        let cur = ceil countdown |> int
        if cur <> prev then Sfx.cue (if cur = 0 then Sfx.Go else Sfx.Tick)
        banner.textContent <- string (ceil countdown |> int)
        banner.className <- if countdown > 0. then "" else "hidden"
        Render.draw view world [] dt
    else
        slowmo <- max 0. (slowmo - dt)
        hitstop <- max 0. (hitstop - dt)
        let scale = if hitstop > 0. then 0. elif slowmo > 0. then 0.28 else 1.
        acc <- acc + dt * scale
        let inputs = masked ()
        let mutable events = []
        let mutable steps = 0
        while acc >= Cfg.physicsDt && steps < 8 do
            world <- Sim.step Cfg.physicsDt inputs world
            events <- world.Events @ events
            acc <- acc - Cfg.physicsDt
            steps <- steps + 1
        if steps = 8 then acc <- 0.
        let mutable pause = false
        inputs
        |> Array.iteri (fun i inp ->
            if Menu.rising (sprintf "s%d" i) "start" inp.Start then pause <- true)
        if events |> List.exists (function Explode _ -> true | _ -> false) then hitstop <- 0.055
        announce world events
        shout <- max 0. (shout - dt)
        if shout > 0. then
            banner.textContent <- shoutText
            banner.className <- ""
        else
            banner.className <- "hidden"
        Sfx.play events
        Sfx.thrust world
        match world.Phase with
        | Over _ when not ending ->
            ending <- true
            finish <- 1.5
            slowmo <- 1.5
            endTitle <- title world
            endNote <- stats world
        | Over _ ->
            finish <- finish - dt
            if finish <= 0. then
                banner.className <- "hidden"
                Menu.note <- endNote
                Menu.result endTitle
        | Playing when pause -> Menu.pause ()
        | Playing -> ()
        Render.draw view world events dt
    window.requestAnimationFrame frame |> ignore

window.requestAnimationFrame frame |> ignore
