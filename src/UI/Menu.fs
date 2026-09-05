module Menu

open System.Collections.Generic
open Browser
open Fable.Core
open Fable.Core.JsInterop
open Domain

[<Import("qrToSvg", "./qr.js")>]
let private qrToSvg (text: string) : string = jsNative

type Screen =
    | Lobby
    | Options
    | Pause
    | Result of string

type Action =
    | Resume
    | Rematch
    | Restart
    | Quit
    | Configure

let joined = HashSet<int>()
let teams: int[] = Array.zeroCreate 4
let wins: int[] = Array.zeroCreate 4
let names: string[] = Array.create 4 ""
let mutable screen = Lobby
let mutable note = ""
let mutable private shown = true
let mutable private cursor = 0
let mutable private lobbyPick = 5
let private ready = Array.create 4 false
let private onRow = Array.create 4 false
let private owner = Array.create 4 ""
let mutable private teamMode = false
let mutable private practiceMode = false
let practice () = practiceMode
let mutable private optRows: Settings.Row list = []
let mutable private optCursor = 0
let mutable private optTop = 0
let mutable private optBack = Lobby
let mutable private repeatAt = 0.
let private held = HashSet<string>()
let private el = document.getElementById "menu"
let private colors = [| "#00f6ff"; "#ff2bd6"; "#b6ff3b"; "#ffb347" |]
let private teamColors = [| ""; "#3b7bff"; "#ff3b5c" |]
let private window' = 11
let mutable private padUrl = ""
window?fetch("/__pad-url")?``then``(fun r -> r?text())?``then``(fun (t: string) -> padUrl <- t) |> ignore

let visible () = shown

let rising (key: string) (name: string) (down: bool) =
    let k = key + name
    if down then held.Add k else (held.Remove k |> ignore; false)

let private pulse (key: string) (name: string) (down: bool) =
    let k = key + name
    let now = JS.Constructors.Date.now ()
    if not down then
        held.Remove k |> ignore
        false
    elif held.Add k then
        repeatAt <- now + 340.
        true
    elif now >= repeatAt then
        repeatAt <- now + 90.
        true
    else
        false

let private swallow () =
    let keys = [ for d in Input.devices () -> d.Key ] @ [ for s in 0..3 -> sprintf "s%d" s ]
    for k in keys do
        for name in [ "fire"; "start"; "back"; "up"; "down"; "left"; "right" ] do
            held.Add(k + name) |> ignore
    repeatAt <- JS.Constructors.Date.now () + 340.

let private open' s =
    screen <- s
    cursor <- 0
    shown <- true
    swallow ()
    el.className <- (match s with Lobby | Options -> "" | _ -> "play")

let show () = open' Lobby
let pause () = open' Pause
let result (title: string) = open' (Result title)

let private hide () =
    shown <- false
    swallow ()
    el.className <- "hidden"

let private items () =
    match screen with
    | Pause -> [ Strings.t.Resume, Resume; Strings.t.Settings, Configure; Strings.t.Restart, Restart; Strings.t.Quit, Quit ]
    | _ -> [ Strings.t.Rematch, Rematch; Strings.t.Quit, Quit ]

let private teamName t = [| ""; Strings.t.Blue; Strings.t.Red |].[t]

let private colorTaken slot c =
    joined |> Seq.exists (fun s -> s <> slot && playerColor.[s] = c)

let private cycleColor slot dir =
    let free = [ 0..3 ] |> List.filter (fun c -> c = playerColor.[slot] || not (colorTaken slot c))
    let i = List.findIndex ((=) playerColor.[slot]) free
    playerColor.[slot] <- free.[(i + dir + free.Length) % free.Length]

let private applyMode () =
    Array.fill ready 0 4 false
    Array.fill wins 0 4 0
    let order = joined |> Seq.sort |> Seq.toList
    if teamMode then
        order |> List.iteri (fun i s -> teams.[s] <- 1 + i % 2)
    else
        Array.fill teams 0 4 0
        order |> List.iteri (fun i s -> playerColor.[s] <- i)

let private botKey = "bot"
let isBot slot = joined.Contains slot && owner.[slot] = botKey

let private claim key slot =
    if key <> botKey then Input.assign key slot
    Array.fill wins 0 4 0
    owner.[slot] <- key
    joined.Add slot |> ignore
    ready.[slot] <- false
    onRow.[slot] <- false
    teams.[slot] <- 0
    if teamMode then
        let side t = joined |> Seq.filter (fun s -> teams.[s] = t) |> Seq.length
        teams.[slot] <- if side 1 <= side 2 then 1 else 2
    else
        playerColor.[slot] <- [ 0..3 ] |> List.find (fun c -> not (colorTaken slot c))
    if key = botKey then ready.[slot] <- true

let private toggleBots () =
    match [ 0..3 ] |> List.tryFind (fun i -> not (joined.Contains i)) with
    | Some slot -> claim botKey slot
    | None ->
        for s in Seq.toArray joined do
            if isBot s then
                joined.Remove s |> ignore
                ready.[s] <- false
                teams.[s] <- 0

let dropIn () =
    let mutable arrived = None
    for d in Input.devices () do
        let mine = d.Slot >= 0 && joined.Contains d.Slot && owner.[d.Slot] = d.Key
        let wants = rising d.Key "start" d.Input.Start || rising d.Key "fire" d.Input.Fire
        if not mine && wants && arrived.IsNone then
            let free = [ 0..3 ] |> List.tryFind (fun i -> not (joined.Contains i))
            let seat = free |> Option.orElse ([ 0..3 ] |> List.tryFind (fun i -> isBot i && i <> Sim.target))
            match seat with
            | Some slot ->
                let fromBot = isBot slot
                claim d.Key slot
                ready.[slot] <- true
                held.Add(sprintf "s%dstart" slot) |> ignore
                held.Add(sprintf "s%dfire" slot) |> ignore
                arrived <- Some(slot, fromBot)
            | None -> ()
    arrived

let addTarget () =
    match [ 0..3 ] |> List.tryFind (fun i -> not (joined.Contains i)) with
    | Some slot ->
        claim botKey slot
        slot
    | None -> -1

let private leave slot =
    joined.Remove slot |> ignore
    Array.fill wins 0 4 0
    ready.[slot] <- false
    onRow.[slot] <- false
    teams.[slot] <- 0

let private allReady () = joined |> Seq.forall (fun s -> ready.[s])

let private opposed () =
    (joined |> Seq.exists (fun s -> teams.[s] = 1)) && (joined |> Seq.exists (fun s -> teams.[s] = 2))

let private canStart () =
    if practiceMode then joined.Count >= 1
    else joined.Count >= 2 && allReady () && (not teamMode || opposed ())

let private modeName () =
    if practiceMode then Strings.t.Practice elif teamMode then Strings.t.Teams else Strings.t.Ffa

let private pickName i =
    if teamMode then Strings.t.Team(teamName teams.[i]) else Strings.t.Colors.[playerColor.[i]]

let private ship i =
    let k = if teams.[i] > 0 then teams.[i] - 1 else [| 0; 1; 3; 4 |].[playerColor.[i]]
    sprintf "<i class=\"ship k%d t%d\"></i>" k teams.[i]

let private cardColor i = if teams.[i] > 0 then teamColors.[teams.[i]] else colors.[playerColor.[i]]

let private displayName i = if names.[i] = "" then Strings.t.Player i else names.[i]

let private rename slot =
    let s: string = window?prompt (Strings.t.RenamePrompt, displayName slot)
    if not (isNull s) then names.[slot] <- s.Trim()

let phase () =
    if not shown then "play"
    else match screen with Lobby -> "lobby" | _ -> "menu"

let padCard (key: string) : obj =
    match [ 0..3 ] |> List.tryFind (fun s -> joined.Contains s && owner.[s] = key) with
    | Some i ->
        createObj [ "slot" ==> i; "name" ==> displayName i; "color" ==> cardColor i; "ship" ==> ship i; "pick" ==> pickName i; "ready" ==> ready.[i] ]
    | None -> null

let private qr () =
    if padUrl = "" then ""
    else
        let wasOpen = el.querySelector "details.qr[open]" |> isNull |> not
        sprintf
            "<details class=\"legend qr\"%s onpointerdown=\"this.open=!this.open\"><summary><div class=\"lt\">%s</div><div class=\"big\">%s<div class=\"url\">%s</div></div></summary></details>"
            (if wasOpen then " open" else "") Strings.t.ScanToJoin (qrToSvg padUrl) padUrl

let private plus =
    "<svg viewBox=\"0 0 100 100\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linejoin=\"round\"><path d=\"M50 8 L86 29 L86 71 L50 92 L14 71 L14 29 Z\"/><path d=\"M50 34 L50 66 M34 50 L66 50\" stroke-width=\"4\"/></svg>"

let private legend (title: string) (rows: (string list * string) list) =
    let cells =
        rows
        |> List.map (fun (keys, label) ->
            let caps = keys |> List.map (fun k -> sprintf "<i>%s</i>" k) |> String.concat ""
            sprintf "<div class=\"key\"><div class=\"caps\">%s</div><span>%s</span></div>" caps label)
        |> String.concat ""
    sprintf "<div class=\"legend\"><div class=\"lt\">%s</div><div class=\"keys\">%s</div></div>" title cells

let private hint (keys: string) (label: string) =
    let caps = keys.Split([| " / " |], System.StringSplitOptions.None) |> Array.map (sprintf "<i>%s</i>") |> String.concat ""
    sprintf "<div class=\"key\"><div class=\"caps\">%s</div><span>%s</span></div>" caps label

let private renderLobby (devices: Input.Device[]) =
    let slots =
        [ for i in 0..3 ->
              let inGame = joined.Contains i
              let dev = devices |> Array.tryFind (fun d -> inGame && d.Key = owner.[i])
              let name =
                  if isBot i then Strings.t.Bot
                  else
                      dev
                      |> Option.map (fun d -> if d.Key = "kb" then Strings.t.Keyboard else d.Name)
                      |> Option.defaultValue Strings.t.PressToJoin
              let color = cardColor i
              let foot =
                  if not inGame then ""
                  elif ready.[i] then sprintf "<div class=\"foot\">%s %s</div>" Strings.t.Ready (String.replicate wins.[i] "★")
                  else sprintf "<div class=\"foot pick\"><span data-dir=\"-1\">&#9664;</span> %s <span data-dir=\"1\">&#9654;</span></div>" (pickName i)
              let cls = (if inGame then " in" else "") + (if inGame && onRow.[i] then " away" else "")
              sprintf
                  "<div class=\"slot%s\" data-slot=\"%d\" style=\"color:%s\"><b>%s</b><div class=\"art\">%s</div><div class=\"dev\">%s</div>%s</div>"
                  cls i color (if inGame then displayName i else Strings.t.Player i) (if inGame then ship i else plus) name foot ]
        |> String.concat ""
    let mode = Strings.t.Mode |> List.map (sprintf "<span>%s</span>") |> String.concat ""
    let go = canStart ()
    let who =
        joined
        |> Seq.filter (fun s -> onRow.[s])
        |> Seq.sort
        |> Seq.map (fun s ->
            let c = cardColor s
            sprintf "<i style=\"color:%s\">%s</i>" c (displayName s))
        |> String.concat ""
    let picks =
        [ Strings.t.ModeLabel, modeName (), true
          Strings.t.Arena, Settings.arenaName (), true
          Strings.t.Mutator, Strings.t.Mutators.[Sim.mutator], true
          "", (if joined.Count < 4 then Strings.t.AddBot else Strings.t.ClearBots), true
          "", Strings.t.Settings, true
          Strings.t.KeysLaunch, Strings.t.Start, go ]
        |> List.mapi (fun i (top, t, ok) ->
            let sel = i = lobbyPick && who <> ""
            let cls = (if sel then " sel" else "") + (if ok then "" else " dim") + (if i = 5 && go then " go" else "")
            sprintf "<div class=\"item%s\" data-pick=\"%d\"><em>%s</em><b>%s</b><div class=\"who\">%s</div></div>" cls i top t (if sel then who else ""))
        |> String.concat ""
    let hints =
        [ Strings.t.KeysJoin, Strings.t.Join + " / " + Strings.t.Ready
          "&#9664; / &#9654;", (if teamMode then Strings.t.TeamLabel else Strings.t.ColorLabel)
          "&#9650;", Strings.t.RenameLabel
          "&#9660;", Strings.t.RowLabel
          Strings.t.KeysLeave, Strings.t.Leave ]
        |> List.map (fun (k, l) -> hint k l)
        |> String.concat ""
    let note =
        if Sfx.asleep () then Strings.t.SoundHint
        elif go then ""
        elif joined.Count < 1 then Strings.t.NeedOne
        elif joined.Count < 2 && not practiceMode then Strings.t.NeedPlayers
        elif teamMode && not (opposed ()) then Strings.t.NeedTwo
        else Strings.t.NeedReady
    el.innerHTML <-
        sprintf
            "<div class=\"lobby\"><div class=\"title\"><h1>%s</h1><div class=\"sub\">%s</div></div><div class=\"modebar\">%s</div><div class=\"slots\">%s</div><div class=\"hints\">%s</div><div class=\"buttons\">%s</div><div class=\"note\">%s</div><div class=\"legends\">%s%s%s%s</div></div>"
            Strings.t.TitleMain Strings.t.TitleSub mode slots hints picks note
            (legend Strings.t.Keyboard (Strings.kbLegend ()))
            (legend Strings.t.Gamepad Strings.t.PadLegend)
            (legend Strings.t.Phone Strings.t.PhoneLegend)
            (qr ())

let recordWin (winner: int) (team: int) =
    for s in joined do
        if s = winner || (team > 0 && teams.[s] = team) then wins.[s] <- wins.[s] + 1
    let series = wins.[winner] >= Cfg.seriesTo
    if series then Array.fill wins 0 4 0
    series

let private tally () =
    joined
    |> Seq.sort
    |> Seq.map (fun s -> sprintf "<i style=\"color:%s\">%s %s</i>" (cardColor s) (displayName s) (String.replicate wins.[s] "★"))
    |> String.concat ""

let private renderList (title: string) =
    let list =
        items ()
        |> List.mapi (fun i (label, _) -> sprintf "<div class=\"item%s\" data-i=\"%d\"><b>%s</b></div>" (if i = cursor then " sel" else "") i label)
        |> String.concat ""
    let hints =
        (match screen with
         | Pause -> Strings.t.NavKeys
         | _ -> Strings.t.NavKeys @ [ Strings.t.KeysJoin, Strings.t.Join; Strings.t.KeysLeave, Strings.t.Leave ])
        |> List.map (fun (k, l) -> hint k l)
        |> String.concat ""
    el.innerHTML <-
        sprintf "<div class=\"lobby\"><div class=\"title\"><h1>%s</h1></div><div class=\"tally\">%s</div><div class=\"stats\">%s</div><div class=\"buttons col\">%s</div><div class=\"hints\">%s</div>%s</div>"
            title (tally ()) note list hints (if screen = Pause then qr () else "")

let private renderOptions () =
    let n = optRows.Length
    if optCursor < optTop then optTop <- optCursor
    elif optCursor >= optTop + window' then optTop <- optCursor - window' + 1
    let top = optTop
    let rows =
        optRows
        |> List.indexed
        |> List.filter (fun (i, _) -> i >= top && i < top + window')
        |> List.map (fun (i, r) ->
            let cls =
                match r with
                | Settings.Header _ -> "row hdr"
                | Settings.Note _ -> "row note"
                | _ -> if i = optCursor then "row sel" else "row"
            sprintf "<div class=\"%s\" data-i=\"%d\"><span>%s</span><b data-dir=\"1\">%s</b></div>" cls i (Settings.label r) (Settings.value r))
        |> String.concat ""
    el.innerHTML <-
        sprintf "<h1>%s</h1><div class=\"rows\">%s</div><div class=\"hint\">%s%s</div>"
            Strings.t.Settings rows
            (if top + window' < n then Strings.t.More + "\n" else "")
            (Strings.optHint ())

let private openOptions () =
    optBack <- screen
    optRows <- Settings.rows ()
    optCursor <- optRows |> List.findIndex Settings.selectable
    optTop <- 0
    open' Options

let private dropMissing (devices: Input.Device[]) =
    for s in Seq.toArray joined do
        if not (isBot s) && not (devices |> Array.exists (fun d -> d.Key = owner.[s] && d.Slot = s)) then leave s

let private updateLobby () =
    let devices = Input.devices ()
    dropMissing devices
    let mutable launch = false
    let mutable options = false
    for d in devices do
        let mine = d.Slot >= 0 && joined.Contains d.Slot && owner.[d.Slot] = d.Key
        let fire = rising d.Key "fire" (d.Input.Fire || d.Input.Boost)
        let start = rising d.Key "start" d.Input.Start
        let back = rising d.Key "back" d.Input.Back
        let up = rising d.Key "up" d.Input.Thrust
        let down = rising d.Key "down" d.Input.Reverse
        let h = d.Input.Turn + d.Input.Strafe
        let right = rising d.Key "right" (h > 0.5)
        let left = rising d.Key "left" (h < -0.5)
        if not mine then
            if fire || start then
                let slot =
                    if d.Slot >= 0 && not (joined.Contains d.Slot) then Some d.Slot
                    else [ 0..3 ] |> List.tryFind (fun i -> not (joined.Contains i))
                slot |> Option.iter (claim d.Key)
        elif onRow.[d.Slot] then
            if left then lobbyPick <- (lobbyPick + 5) % 6
            if right then lobbyPick <- (lobbyPick + 1) % 6
            if up || back then onRow.[d.Slot] <- false
            elif start && canStart () then launch <- true
            elif fire || start then
                match lobbyPick with
                | 0 ->
                    if practiceMode then practiceMode <- false
                    elif teamMode then (teamMode <- false; practiceMode <- true)
                    else teamMode <- true
                    applyMode ()
                | 1 -> Settings.adjust Settings.Arena 1
                | 2 -> Sim.mutator <- (Sim.mutator + 1) % Sim.mutators
                | 3 -> toggleBots ()
                | 4 -> options <- true
                | _ -> if canStart () then launch <- true
        else
            if (left || right) && not ready.[d.Slot] then
                if teamMode then teams.[d.Slot] <- 3 - teams.[d.Slot]
                else cycleColor d.Slot (if right then 1 else -1)
            if down then onRow.[d.Slot] <- true
            elif up then rename d.Slot
            elif back then (if ready.[d.Slot] then ready.[d.Slot] <- false else leave d.Slot)
            elif start && canStart () then launch <- true
            elif fire || start then ready.[d.Slot] <- true
    if options then
        openOptions ()
        false
    else
        renderLobby (Input.devices ())
        if launch then hide ()
        launch

let private updateOptions () =
    let n = optRows.Length
    let move dir =
        let mutable i = optCursor
        let mutable steps = 0
        let mutable go = true
        while go && steps < n do
            i <- (i + dir + n) % n
            steps <- steps + 1
            if Settings.selectable optRows.[i] then go <- false
        optCursor <- i
    let mutable back = false
    for d in Input.devices () do
        let inp = d.Input
        let k = d.Key
        if pulse k "up" inp.Thrust then move -1
        if pulse k "down" inp.Reverse then move 1
        let h = inp.Turn + inp.Strafe
        if pulse k "left" (h < -0.5) then Settings.adjust optRows.[optCursor] -1
        if pulse k "right" (h > 0.5) then Settings.adjust optRows.[optCursor] 1
        if rising k "fire" (inp.Fire || inp.Boost) then Settings.activate optRows.[optCursor]
        let esc = rising k "back" inp.Back
        let start = rising k "start" inp.Start
        if esc || start then back <- true
    renderOptions ()
    if back then
        if optBack = Lobby then dropMissing (Input.devices ())
        open' optBack

let private updateList (title: string) (inputs: Input[]) =
    let n = (items ()).Length
    let mutable action = None
    inputs
    |> Array.iteri (fun i inp ->
        let k = sprintf "s%d" i
        if rising k "up" inp.Thrust then cursor <- (cursor + n - 1) % n
        if rising k "down" inp.Reverse then cursor <- (cursor + 1) % n
        let fire = rising k "fire" (inp.Fire || inp.Boost)
        let start = rising k "start" inp.Start
        if fire || start then action <- Some(snd (items ()).[cursor])
        if rising k "back" inp.Back then
            if screen = Pause then action <- Some Resume
            else
                leave i
                if joined.Count < 2 then action <- Some Quit)
    if screen <> Pause then
        for d in Input.devices () do
            let mine = d.Slot >= 0 && joined.Contains d.Slot && owner.[d.Slot] = d.Key
            if not mine && rising d.Key "fire" (d.Input.Fire || d.Input.Boost) then
                [ 0..3 ] |> List.tryFind (fun i -> not (joined.Contains i)) |> Option.iter (claim d.Key)
    renderList title
    match action with
    | Some Configure ->
        openOptions ()
        None
    | Some _ ->
        hide ()
        action
    | None -> None

let update (inputs: Input[]) =
    match screen with
    | Lobby -> if updateLobby () then Some Rematch else None
    | Options ->
        updateOptions ()
        None
    | Pause -> updateList Strings.t.Paused inputs
    | Result title -> updateList title inputs

let private hit (e: Browser.Types.Event) (attr: string) =
    (e.target :?> Browser.Types.Element).closest (sprintf "[data-%s]" attr)
    |> Option.map (fun t -> int (t.getAttribute ("data-" + attr)))

let private kb () = Input.keyboardSlot
let private kbIn () = kb () >= 0 && joined.Contains(kb ()) && owner.[kb ()] = "kb"
let private arrow d = Input.press (if d < 0 then "ArrowLeft" else "ArrowRight")

let private pick i =
    match screen with
    | Options -> if Settings.selectable optRows.[i] then optCursor <- i
    | _ -> cursor <- i

el.addEventListener ("contextmenu", fun e -> e.preventDefault ())
el.addEventListener ("pointerup", fun _ -> Input.release ())

el.addEventListener (
    "wheel",
    fun e ->
        let down = (e :?> Browser.Types.WheelEvent).deltaY > 0.
        match hit e "dir", hit e "slot", hit e "i" with
        | Some _, Some s, _ when kbIn () && s = kb () -> arrow (if down then -1 else 1)
        | Some _, None, Some i ->
            pick i
            arrow (if down then -1 else 1)
        | _ -> Input.press (if down then "ArrowDown" else "ArrowUp")
        Input.release ()
)

el.addEventListener (
    "pointermove",
    fun e ->
        match hit e "i", hit e "pick" with
        | Some i, _ -> pick i
        | _, Some p when kbIn () && onRow.[kb ()] -> lobbyPick <- p
        | _ -> ()
)

el.addEventListener (
    "pointerdown",
    fun e ->
        if (e :?> Browser.Types.MouseEvent).button = 2. then Input.press "Escape"
        else
            match hit e "dir", hit e "slot", hit e "pick", hit e "i" with
            | Some d, Some s, _, _ -> if kbIn () && s = kb () then arrow d
            | Some d, None, _, Some i ->
                pick i
                arrow d
            | _, Some s, _, _ when not (kbIn ()) ->
                if not (joined.Contains s) then Input.keyboardSlot <- s
                Input.press "Space"
            | _, Some s, _, _ when s = kb () -> if onRow.[s] then onRow.[s] <- false else Input.press "Space"
            | _, _, Some p, _ when kbIn () ->
                onRow.[kb ()] <- true
                lobbyPick <- p
                Input.press "Space"
            | _, _, _, Some i ->
                pick i
                Input.press "Space"
            | _ -> ()
)
