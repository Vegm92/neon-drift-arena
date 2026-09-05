module Main

open Browser
open Fable.Core.JsInterop
open Domain

Input.init ()
Settings.init ()
Sfx.init ()
let view = Render.create ()
let banner = document.getElementById "banner"
document.getElementById("loader").className <- "done"

let tutKey = "nda-tut"
let tutEl = document.getElementById "tut"
let mutable tutShown = false

let showTutorial () =
    if window.localStorage.getItem tutKey = null then
        let keys move turn fire boost spec pause =
            sprintf "<div class=\"row\"><div class=\"keys\"><i>%s</i><i>%s</i></div><div class=\"lbl\">%s</div></div>" move turn Strings.t.TutTurn
            + sprintf "<div class=\"row\"><div class=\"keys\"><i>%s</i></div><div class=\"lbl\">%s</div></div>" fire Strings.t.TutFire
            + sprintf "<div class=\"row\"><div class=\"keys\"><i>%s</i></div><div class=\"lbl\">%s</div></div>" boost Strings.t.TutBoost
            + sprintf "<div class=\"row\"><div class=\"keys\"><i>%s</i></div><div class=\"lbl\">%s</div></div>" spec Strings.t.TutSpecial
            + sprintf "<div class=\"row\"><div class=\"keys\"><i>%s</i></div><div class=\"lbl\">%s</div></div>" pause Strings.t.TutPause
        let movement =
            if Domain.layout = Azerty then "Z", "S"
            else "W", "S"
        let rows =
            sprintf "<div class=\"row\"><div class=\"keys\"><i>%s</i><i>%s</i></div><div class=\"lbl\">%s</div></div>" (fst movement) (snd movement) Strings.t.TutThrust
            + keys "" "" "SPACE" "SHIFT" "F" "ENTER"
        tutEl.innerHTML <- sprintf "<h2>%s</h2><div class=\"rows\">%s</div><div class=\"skip\">%s</div>" Strings.t.TutTitle rows Strings.t.TutSkip
        tutEl.className <- ""
        tutShown <- true

let hideTutorial () =
    if tutShown then
        window.localStorage.setItem (tutKey, "1")
        tutEl.className <- "hidden"
        tutShown <- false

showTutorial ()

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
let mutable suddenSaid = false
let private lowStock = Array.create 4 false
let private hyped = Array.create 4 false
let mutable endTitle = ""
let mutable endNote = ""

let private masked () =
    Input.read ()
    |> Array.mapi (fun i x ->
        if Menu.isBot i then
            if Menu.visible () then noInput
            elif i = Sim.target then { noInput with Present = true }
            else Sim.bot world i
        elif Menu.joined.Contains i then x
        else noInput)

let private go intro =
    countdown <- if intro then Cfg.introTime else 3.
    view.Intro <- if intro then Cfg.introTime else 0.
    Render.syncArena view
    if intro then world <- Sim.step Cfg.physicsDt (masked ()) world
    acc <- 0.
    slowmo <- 0.
    hitstop <- 0.
    finish <- 0.
    ending <- false
    shout <- 0.
    bled <- false
    suddenSaid <- false
    Array.fill lowStock 0 4 false
    Array.fill hyped 0 4 false
    Menu.note <- ""

let private launch (w: World) =
    Sim.practice <- Menu.practice ()
    if Sim.practice then
        if Sim.target < 0 || not (Menu.isBot Sim.target) then Sim.target <- Menu.addTarget ()
        world <- w
        go false
        world <- Sim.step Cfg.physicsDt (masked ()) world |> Sim.stage
        countdown <- 1.
    else
        Sim.target <- -1
        world <- w
        go true

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

let private flash (cls: string) =
    banner.className <- ""
    banner?offsetWidth |> ignore
    banner.className <- cls

let private say text =
    shoutText <- text
    shout <- 1.4
    flash "shout"
    Sfx.cue Sfx.Alert

let private announce (w: World) (events: Event list) =
    let deaths = events |> List.choose (function Explode(_, i, ring) -> Some(i, ring) | _ -> None)
    if Sim.sudden w && not suddenSaid then
        suddenSaid <- true
        say Strings.t.SuddenDeath
    elif deaths.Length >= 2 then say Strings.t.DoubleKill
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
            Settings.rollArena ()
            launch ({ Sim.initial with Rng = System.Random().Next 1000003 } |> Sim.withTeams Menu.teams)
        | Some Menu.Rematch
        | Some Menu.Restart ->
            if Menu.screen <> Menu.Pause then Settings.rollArena ()
            launch (Sim.reset (fun i -> Menu.joined.Contains i) world |> Sim.withTeams Menu.teams)
        | Some Menu.Resume -> go false
        | Some Menu.Configure -> ()
        | Some Menu.Quit ->
            world <- Sim.initial
            Sim.target <- -1
            Menu.show ()
        | None -> ()
        Render.syncArena view
        Render.draw view world [] dt
    elif countdown > 0. then
        let prev = ceil countdown |> int
        countdown <- countdown - dt
        let cur = ceil countdown |> int
        if cur <> prev then
            Sfx.cue (if cur = 0 then Sfx.Go else Sfx.Tick)
            if cur = 0 then
                shoutText <- Strings.t.Go
                shout <- 0.7
                view.Tint <- 0.9
                view.TintHex <- "#ffffff"
                view.Spike <- 1.2
                flash "shout go"
            else
                flash "count"
        if countdown > 0. then banner.textContent <- string cur
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
        match Menu.dropIn () with
        | Some(slot, fromBot) ->
            if fromBot then
                world <- { world with Ships = world.Ships |> Array.map (fun s -> if s.Id = slot then { s with Active = false; Alive = false } else s) }
            world <- Sim.withTeams Menu.teams world
            say (Strings.t.Joins(name slot))
        | None -> ()
        let mutable pause = false
        inputs
        |> Array.iteri (fun i inp ->
            if Menu.rising (sprintf "s%d" i) "start" inp.Start then pause <- true)
        if events |> List.exists (function Explode _ -> true | _ -> false) then hitstop <- 0.09
        announce world events
        shout <- max 0. (shout - dt)
        if shout > 0. then
            banner.textContent <- shoutText
        else
            banner.className <- "hidden"
        Sfx.play events
        Sfx.thrust world
        match world.Phase with
        | Over _ when not ending ->
            ending <- true
            finish <- Cfg.victoryTime
            slowmo <- 0.6
            endTitle <- title world
            endNote <- stats world
            match world.Phase with
            | Over(Some i) when Menu.recordWin i world.Ships.[i].Team ->
                endTitle <- Strings.t.SeriesWin(if world.Ships.[i].Team > 0 then [| ""; Strings.t.Blue; Strings.t.Red |].[world.Ships.[i].Team] else name i)
            | _ -> ()
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
window.addEventListener ("keydown", fun _ -> hideTutorial ())
window.addEventListener ("pointerdown", fun _ -> hideTutorial ())
window.addEventListener (
    "visibilitychange",
    fun _ ->
        if document.hidden && not (Menu.visible ()) && world.Phase = Playing then
            Menu.pause ()
)
window.requestAnimationFrame frame |> ignore

window.addEventListener (
    "keydown",
    fun e ->
        if Sim.practice && not (Menu.visible ()) then
            match (e :?> Browser.Types.KeyboardEvent).code with
            | "KeyK" -> world <- { world with Ships = world.Ships |> Array.map (fun s -> if s.Id = Sim.target then { s with Hp = 0.; Invuln = 0. } else s) }
            | "KeyT" -> world <- { world with Time = Cfg.matchTime }
            | "KeyG" -> world <- { world with Ships = world.Ships |> Array.map (fun s -> if s.Id = Input.keyboardSlot then { s with Alive = false; Stocks = 0 } else s) }
            | "KeyR" -> world <- Sim.stage world
            | code when code.StartsWith "Digit" && Input.keyboardSlot >= 0 ->
                let k = int (code.Substring 5) - 1
                if k = -1 then world <- Sim.arm Input.keyboardSlot Blaster world
                elif k >= 0 && k < Sim.arsenal.Length then world <- Sim.arm Input.keyboardSlot Sim.arsenal.[k] world
            | _ -> ()
)
