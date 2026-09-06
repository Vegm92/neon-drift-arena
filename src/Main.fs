module Main

open Browser
open Fable.Core
open Fable.Core.JsInterop
open Domain

Input.init ()
Settings.init ()
Sfx.init ()

async {
    do! CrazyGames.init(Sfx.setCrazyGamesMuted) |> Async.AwaitPromise
    Input.initNetwork ()
    Menu.initUser ()
    CrazyGames.addJoinRoomListener(fun roomId ->
        if roomId <> "" then
            window.sessionStorage.setItem ("nda-room", roomId)
            window.location.reload ()
    )
} |> Async.StartImmediate

let view = Render.create ()
let banner = document.getElementById "banner"
document.getElementById("loader").className <- "done"

let mutable private adPlaying = false

let requestMidgameAd (onDone: unit -> unit) =
    adPlaying <- true
    CrazyGames.gameplayStop ()
    let finish () =
        adPlaying <- false
        Sfx.setAdMuted false
        onDone ()
    CrazyGames.requestAd("midgame", (fun () -> Sfx.setAdMuted true), finish, ignore >> finish)

let tutKey = "nda-tut"
let tutEl = document.getElementById "tut"
let mutable tutShown = false

let showTutorial () =
    if window.localStorage.getItem tutKey = null then
        let rows =
            Strings.tutRows ()
            |> List.map (fun (caps, label) ->
                let keys = caps |> List.map (sprintf "<i>%s</i>") |> String.concat ""
                sprintf "<div class=\"row\"><div class=\"keys\">%s</div><div class=\"lbl\">%s</div></div>" keys label)
            |> String.concat ""
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
            elif i = State.target then { noInput with Present = true }
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
    CrazyGames.gameplayStart ()

let private launch (w: World) =
    State.practice <- Menu.practice ()
    State.race <- Menu.race ()
    if State.practice then
        if State.target < 0 || not (Menu.isBot State.target) then State.target <- Menu.addTarget ()
        world <- w
        go false
        world <- Sim.step Cfg.physicsDt (masked ()) world |> Sim.stage
        countdown <- 1.
    else
        State.target <- -1
        world <- w
        go true

let private name i = if Menu.names.[i] = "" then Strings.t.Player i else Menu.names.[i]

let private title (w: World) =
    match w.Phase with
    | Over(Some i) ->
        let s = w.Ships.[i]
        Strings.t.Wins(if s.Team > 0 then [| ""; Strings.t.Blue; Strings.t.Red |].[s.Team] else name i)
    | _ -> Strings.t.Draw

let private accuracy (s: Ship) =
    if s.Shots = 0 then 0 else int (100. * float s.Hits / float s.Shots)

let private statRow winner pos (s: Ship) =
    let pips =
        if State.race then
            sprintf "<b>%s</b>" (if s.Finish > 0. then sprintf "%d:%02d.%d" (int s.Finish / 60) (int s.Finish % 60) (int (s.Finish * 10.) % 10) else Strings.t.Dnf)
        else
            String.concat "" [ for k in 1 .. Cfg.stocks -> if k <= s.Stocks then "<i></i>" else "<i class=\"gone\"></i>" ]
    sprintf
        "<div class=\"line%s\" style=\"color:#%06x\"><div class=\"pos\">%d</div><div class=\"who\"><svg viewBox=\"0 0 48 48\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"3\" stroke-linejoin=\"round\"><path d=\"M24 4 41 40 24 32 7 40Z\"/></svg><b>%s</b></div><div class=\"num big\">%d</div><div class=\"acc\"><div class=\"bar\"><i style=\"width:%d%%\"></i></div><span>%d%%</span></div><div class=\"num\">%d</div><div class=\"num\">%d</div><div class=\"stocks\">%s</div></div>"
        (if s.Id = winner then " lead" elif s.Stocks = 0 then " out" else "")
        (RenderTypes.shipColor s)
        pos
        (name s.Id)
        s.Kills
        (accuracy s)
        (accuracy s)
        s.Grabs
        s.Rings
        pips

let private awardRow slot label text =
    sprintf "<div class=\"award\" style=\"color:#%06x\"><b>%s</b><span>%s</span></div>" RenderTypes.colors.[slot] label text

let private stats (w: World) =
    let act = w.Ships |> Array.filter (fun s -> s.Active)
    let winner =
        match w.Phase with
        | Over(Some i) -> i
        | _ -> -1
    let rows =
        (if State.race then Sim.rank act |> Array.map (fun i -> w.Ships.[i]) else act |> Array.sortByDescending (fun s -> s.Id = winner, s.Stocks, s.Kills))
        |> Array.mapi (fun i s -> statRow winner (i + 1) s)
        |> String.concat ""
    let awards = ResizeArray()
    match act |> Array.sortByDescending (fun s -> s.Kills) |> Array.tryHead with
    | Some s when s.Kills > 0 -> awards.Add(awardRow 0 Strings.t.AwardKills (Strings.t.MostKills (name s.Id) s.Kills))
    | _ -> ()
    let aim = act |> Array.filter (fun s -> s.Shots >= 5) |> Array.sortByDescending accuracy |> Array.tryHead
    match aim with
    | Some s -> awards.Add(awardRow 1 Strings.t.AwardAim (Strings.t.BestAim (name s.Id) (accuracy s)))
    | None -> ()
    match act |> Array.sortByDescending (fun s -> s.Rings) |> Array.tryHead with
    | Some s when s.Rings > 0 -> awards.Add(awardRow 2 Strings.t.AwardRings (Strings.t.MostRings (name s.Id) s.Rings))
    | _ -> ()
    match act |> Array.tryFind (fun s -> s.Grabs = 0) with
    | Some s -> awards.Add(awardRow 3 Strings.t.AwardCrates (Strings.t.NoCrates(name s.Id)))
    | None -> ()
    let head =
        sprintf
            "<div class=\"hdr\"><span></span><span></span><span>%s</span><span>%s</span><span>%s</span><span>%s</span><span>%s</span></div>"
            Strings.t.ColKills
            Strings.t.ColAccuracy
            Strings.t.ColCrates
            Strings.t.ColRings
            (if State.race then Strings.t.ColTime else Strings.t.ColStocks)
    sprintf "<div class=\"table\">%s%s</div><div class=\"awards\">%s</div>" head rows (String.concat "" awards)

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
    let finishes = events |> List.choose (function Finished(i, place) -> Some(i, place) | _ -> None)
    if Sim.sudden w && not suddenSaid then
        suddenSaid <- true
        say Strings.t.SuddenDeath
    elif not finishes.IsEmpty then
        let i, place = finishes.Head
        say (Strings.t.Finish (name i) Strings.t.Places.[place - 1])
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

let private menuEl = document.getElementById "menu"
let mutable private frameEvents: Event list = []
let mutable private lastMenu = ""
let mutable private lastMenuAt = 0.
let mutable private remote: obj = null
let mutable private remoteAt = 0.
let mutable private wasClient = false
Input.hotOn "nda:state" (fun m -> remote <- m; remoteAt <- JS.Constructors.Date.now ())
let private client () = not (isNull remote) && JS.Constructors.Date.now () - remoteAt < 1000.

let private sendState () =
    let html = menuEl.innerHTML
    let now = JS.Constructors.Date.now ()
    let menu = if html = lastMenu && now - lastMenuAt < 1000. then null else html
    if not (isNull menu) then lastMenuAt <- now
    lastMenu <- html
    Input.hotSend "nda:state" (createObj [ "world" ==> { world with Events = [] }; "events" ==> List.toArray frameEvents; "layout" ==> State.layout; "race" ==> State.race; "colors" ==> playerColor; "intro" ==> view.Intro; "banner" ==> banner.textContent; "bannerClass" ==> banner.className; "menuClass" ==> menuEl.className; "menu" ==> menu ])

let private weaponOf (s: string) =
    match s with
    | "Rail" -> Rail | "Mines" -> Mines | "Swarm" -> Swarm | "Pulse" -> Pulse | "Scatter" -> Scatter
    | "Tractor" -> Tractor | "Collision" -> Collision | "Rock" -> Rock | "Singularity" -> Singularity | _ -> Blaster

let private towOf (t: obj) =
    if not (JS.Constructors.Array.isArray t) then NoTether
    elif string t?(0) = "TowShip" then TowShip(unbox t?(1))
    else TowRock(unbox t?(1))

let private eventOf (e: obj) : Event =
    let a (i: int) : 'a = unbox e?(i)
    match string e?(0) with
    | "Hit" -> Hit(a 1, a 2, a 3)
    | "Explode" -> Explode(a 1, a 2, a 3)
    | "Downed" -> Downed(a 1, a 2, weaponOf (a 3), a 4)
    | "Shot" -> Shot(a 1)
    | "Ram" -> Ram(a 1)
    | "Bump" -> Bump(a 1)
    | "Pickup" -> Pickup(a 1, a 2)
    | "Mend" -> Mend(a 1)
    | "Grab" -> Grab(a 1)
    | "Charging" -> Charging(a 1)
    | "Beam" -> Beam(a 1, a 2, a 3)
    | "MineSet" -> MineSet(a 1)
    | "MineLive" -> MineLive(a 1)
    | "Blast" -> Blast(a 1)
    | "Wave" -> Wave(a 1, a 2, a 3)
    | "Cooked" -> Cooked(a 1)
    | "Zap" -> Zap(a 1, a 2, a 3)
    | "Latch" -> Latch(a 1, a 2)
    | "Launch" -> Launch(a 1)
    | "PortalOpen" -> PortalOpen(a 1, a 2)
    | "Warp" -> Warp(a 1)
    | "Finished" -> Finished(a 1, a 2)
    | _ -> HoleOpen(a 1)

let private worldOf (w: obj) : World =
    { Ships = (w?Ships: obj[]) |> Array.map (fun s -> { unbox<Ship> s with Weapon = weaponOf s?Weapon; LastWeapon = weaponOf s?LastWeapon; Tow = towOf s?Tow })
      Bullets = List.ofArray w?Bullets
      Mines = List.ofArray w?Mines
      Rocks = List.ofArray w?Rocks
      Portals = List.ofArray w?Portals
      PortalIn = w?PortalIn
      Hole = unbox w?Hole
      HoleIn = w?HoleIn
      RaceEnd = w?RaceEnd
      Pads = w?Pads
      Crates = w?Crates
      Rng = w?Rng
      Phase = (if JS.Constructors.Array.isArray w?Phase then Over(unbox w?Phase?(1)) else Playing)
      Time = w?Time
      Events = [] }

let private clientFrame dt =
    Input.sendRemote ()
    let m = remote
    let layout: int = m?layout
    State.race <- m?race
    if State.layout <> layout then Sim.setLayout layout
    Array.blit (m?colors: int[]) 0 playerColor 0 4
    Render.syncArena view
    view.Intro <- m?intro
    banner.textContent <- m?banner
    banner.className <- m?bannerClass
    menuEl.className <- m?menuClass
    let html: string = m?menu
    if not (isNull html) then
        menuEl.innerHTML <- html
        m?menu <- null
    let events = (m?events: obj[]) |> Array.map eventOf |> List.ofArray
    m?events <- [||]
    world <- worldOf m?world
    Sfx.track (menuEl.className <> "hidden")
    Sfx.play events
    Sfx.thrust world
    Render.draw view world events dt

let private localFrame (t: float) dt =
    frameEvents <- []
    if t - lastHost > 100. then
        lastHost <- t
        broadcast ()
    Sfx.track (Menu.visible ())
    if adPlaying then Render.draw view world [] dt
    elif Menu.visible () then
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
            State.target <- -1
            Menu.show ()
        | None -> ()
        if view.Layout <> State.layout then world <- Sim.initial
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
        frameEvents <- events
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
                let kills0 = world.Ships.[0].Kills
                if world.Ships.[0].Active && kills0 > Menu.highScore then
                    Menu.highScore <- kills0
                    Menu.saveProgress ()
                requestMidgameAd (fun () -> Menu.result endTitle)
        | Playing when pause -> Menu.pause ()
        | Playing -> ()
        Render.draw view world events dt

let rec frame (t: float) =
    let dt = if last = 0. then 0. else (t - last) / 1000. |> max 0. |> min 0.1
    last <- t
    for i in 0..3 do view.Names.[i] <- name i
    if client () then
        wasClient <- true
        clientFrame dt
    else
        if wasClient then
            wasClient <- false
            world <- Sim.initial
            State.target <- -1
            Menu.show ()
        localFrame t dt
        sendState ()
    window.requestAnimationFrame frame |> ignore
window.addEventListener ("keydown", fun _ -> hideTutorial ())
window.addEventListener ("pointerdown", fun _ -> hideTutorial ())
window.addEventListener (
    "visibilitychange",
    fun _ ->
        if document.hidden && not (client ()) && not (Menu.visible ()) && world.Phase = Playing then
            Menu.pause ()
)
window.requestAnimationFrame frame |> ignore

window.addEventListener (
    "keydown",
    fun e ->
        if State.practice && not (client ()) && not (Menu.visible ()) then
            match (e :?> Browser.Types.KeyboardEvent).code with
            | "KeyK" -> world <- { world with Ships = world.Ships |> Array.map (fun s -> if s.Id = State.target then { s with Hp = 0.; Invuln = 0. } else s) }
            | "KeyT" -> world <- { world with Time = Cfg.matchTime }
            | "KeyG" -> world <- { world with Ships = world.Ships |> Array.map (fun s -> if s.Id = Input.keyboardSlot then { s with Alive = false; Stocks = 0 } else s) }
            | "KeyR" -> world <- Sim.stage world
            | code when code.StartsWith "Digit" && Input.keyboardSlot >= 0 ->
                let k = int (code.Substring 5) - 1
                if k = -1 then world <- Sim.arm Input.keyboardSlot Blaster world
                elif k >= 0 && k < State.arsenal.Length then world <- Sim.arm Input.keyboardSlot State.arsenal.[k] world
            | _ -> ()
)
