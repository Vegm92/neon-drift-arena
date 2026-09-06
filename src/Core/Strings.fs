module Strings

type Locale =
    { Player: int -> string
      Wins: string -> string
      Draw: string
      Rematch: string
      TitleMain: string
      TitleSub: string
      ModeFfa: string list
      ModeTeams: string list
      ModeRace: int -> string list
      ModePractice: string list
      PadLegend: (string list * string) list
      Gamepad: string
      Phone: string
      Remote: string
      ScanToJoin: string
      PadConnecting: string
      PadFire: string
      PadBoost: string
      PadMove: string
      PadMoveHint: string
      PadBadge: string
      PadStrafe: string
      PadHull: string
      PadStocks: string
      PadKills: string
      PadSpecial: string
      PadOk: string
      PadBack: string
      PhoneLegend: (string list * string) list
      Join: string
      Leave: string
      RowLabel: string
      RenameLabel: string
      RenamePrompt: string
      TeamLabel: string
      ColorLabel: string
      Colors: string[]
      ModeLabel: string
      Ffa: string
      Teams: string
      Practice: string
      Race: string
      Lap: string -> int -> int -> string
      ColTime: string
      Dnf: string
      Finish: string -> string -> string
      Places: string[]
      NeedOne: string
      Start: string
      NeedPlayers: string
      NeedReady: string
      PressToJoin: string
      Ready: string
      NeedTwo: string
      Paused: string
      Resume: string
      Quit: string
      Restart: string
      Blue: string
      Red: string
      Team: string -> string
      Tweaks: string
      Reset: string
      Save: string
      Settings: string
      Arena: string
      Arenas: string[]
      Random: string
      Mutator: string
      Mutators: string[]
      Controllers: string
      Keyboard: string
      Auto: string
      SwapSticks: string
      NoPads: string
      WBlaster: string
      WRail: string
      WMines: string
      WContact: string
      WMissile: string
      WSwarm: string
      WPulse: string
      WScatter: string
      WTractor: string
      Loaded: string -> int -> string
      Go: string
      FirstBlood: string
      DoubleKill: string
      RingOut: string
      LastStock: string -> string
      OnFire: string -> string
      BestAim: string -> int -> string
      MostKills: string -> int -> string
      MostRings: string -> int -> string
      NoCrates: string -> string
      HudHull: string
      HudShield: string
      HudBoost: string
      HudHeat: string
      HudOver: string
      ColKills: string
      ColAccuracy: string
      ColCrates: string
      ColRings: string
      ColStocks: string
      AwardKills: string
      AwardAim: string
      AwardRings: string
      AwardCrates: string
      SuddenDeath: string
      SoundHint: string
      Joins: string -> string
      CatchUp: string
      SeriesWin: string -> string
      LaunchReady: string
      LaunchWait: string
      Bot: string
      CrazyGames: string
      SignIn: string
      AddBot: string
      ClearBots: string
      Tuning: string
      Audio: string
      Music: string
      Sounds: string
      On: string
      Off: string
      MoreUp: string
      More: string
      Stick: string
      HintMove: string
      HintAdjust: string
      HintToggle: string
      HintSelect: string
      HintBack: string
      Mute: string
      MuteKey: string
      Bindings: string
      BindPress: string
      ResetKeys: string
      BindSet: string
      BindAdd: string
      BindDrop: string
      BindCancel: string
      BindTaken: string
      BindTurnL: string
      BindTurnR: string
      BindStrafeL: string
      BindStrafeR: string
      BindThrust: string
      BindReverse: string
      BindBoost: string
      BindFire: string
      BindSpecial: string
      BindStart: string
      BindBack: string
      TutTitle: string
      TutThrust: string
      TutTurn: string
      TutFire: string
      TutBoost: string
      TutSpecial: string
      TutPause: string
      TutSkip: string }

let en =
    { Player = fun i -> sprintf "P%d" (i + 1)
      Wins = sprintf "%s WINS"
      Draw = "DRAW"
      Rematch = "REMATCH"
      TitleMain = "NEON DRIFT"
      TitleSub = "ARENA"
      ModeFfa = [ "STOCK BATTLE"; "3 LIVES"; "LAST SHIP FLYING WINS" ]
      ModeTeams = [ "TEAM BATTLE"; "3 LIVES"; "LAST TEAM FLYING WINS" ]
      ModeRace = fun laps -> [ "CIRCUIT RACE"; sprintf "%d LAPS" laps; "FIRST ACROSS THE LINE WINS" ]
      ModePractice = [ "TARGET RANGE"; "NO STOCKS"; "EVERY WEAPON IN ORDER" ]
      PadLegend =
        [ [ "L" ], "THRUST / STRAFE"
          [ "R" ], "TURN"
          [ "A"; "LT" ], "BOOST"
          [ "RT" ], "FIRE"
          [ "RB" ], "SPECIAL"
          [ "START" ], "PAUSE"
          [ "B" ], "BACK" ]
      Gamepad = "GAMEPAD"
      Phone = "PHONE"
      Remote = "LAN PC"
      ScanToJoin = "SCAN TO PLAY FROM YOUR PHONE"
      PadConnecting = "CONNECTING"
      PadFire = "FIRE"
      PadBoost = "BOOST"
      PadMove = "MOVE"
      PadMoveHint = "TURN &#8226; PUSH OUT = THRUST"
      PadBadge = "MOBILE"
      PadStrafe = "STRAFE"
      PadHull = "HULL"
      PadStocks = "STOCKS"
      PadKills = "KILLS"
      PadSpecial = "SPECIAL"
      PadOk = "OK"
      PadBack = "BACK"
      PhoneLegend =
        [ [ "L" ], "TURN + THRUST"
          [ "RT" ], "FIRE"
          [ "B" ], "BOOST"
          [ "SP" ], "SPECIAL"
          [ "◀▶" ], "STRAFE"
          [ "☰" ], "PAUSE" ]
      Join = "JOIN"
      Leave = "LEAVE"
      RowLabel = "MENU"
      RenameLabel = "RENAME"
      RenamePrompt = "ENTER NAME"
      TeamLabel = "TEAM"
      ColorLabel = "COLOUR"
      Colors = [| "CYAN"; "MAGENTA"; "LIME"; "AMBER" |]
      ModeLabel = "MODE"
      Ffa = "FREE FOR ALL"
      Teams = "TEAMS"
      Practice = "PRACTICE"
      Race = "RACE"
      Lap = sprintf "%s · LAP %d/%d"
      ColTime = "TIME"
      Dnf = "DNF"
      Finish = sprintf "%s FINISHES %s"
      Places = [| "1ST"; "2ND"; "3RD"; "4TH" |]
      NeedOne = "NEED 1 PLAYER"
      Start = "START"
      NeedPlayers = "NEED 2 PLAYERS"
      NeedReady = "ALL PLAYERS MUST READY UP"
      PressToJoin = "PRESS FIRE TO JOIN"
      Ready = "READY"
      NeedTwo = "NEED 2 OPPOSING SIDES"
      Paused = "PAUSED"
      Resume = "RESUME"
      Quit = "QUIT TO LOBBY"
      Restart = "RESTART MATCH"
      Blue = "BLUE"
      Red = "RED"
      Team = sprintf "%s TEAM"
      Tweaks = "TWEAKS"
      Reset = "RESET"
      Save = "SAVE TO CODE"
      Settings = "SETTINGS"
      Arena = "ARENA"
      Arenas = [| "CORE RING"; "CROSS BASTIONS"; "PINWHEEL"; "TWIN GAUNTLET"; "OPEN BELT"; "GRAND PRIX"; "HAIRPIN"; "INFINITY" |]
      Random = "RANDOM"
      Mutator = "MUTATOR"
      Mutators = [| "NONE"; "RAILS ONLY"; "TURBO"; "ICE" |]
      Controllers = "CONTROLLERS"
      Keyboard = "KEYBOARD"
      Auto = "AUTO"
      SwapSticks = "SWAP STICKS"
      NoPads = "NO GAMEPADS DETECTED - PRESS A BUTTON"
      WBlaster = "BLASTER"
      WRail = "RAILGUN"
      WMines = "MAG MINES"
      WContact = "CONTACT MINES"
      WMissile = "MISSILE"
      WSwarm = "SEEKERS"
      WPulse = "REPULSOR"
      WScatter = "SCATTER GUN"
      WTractor = "TRACTOR"
      Loaded = fun n a -> sprintf "%s x%d" n a
      Go = "GO"
      FirstBlood = "FIRST BLOOD"
      DoubleKill = "DOUBLE KILL"
      RingOut = "RING OUT"
      LastStock = sprintf "%s ON LAST STOCK"
      OnFire = sprintf "%s IS ON FIRE"
      BestAim = fun n p -> sprintf "%s LANDED %d%% OF SHOTS" n p
      MostKills = fun n k -> sprintf "%s TOOK DOWN %d SHIPS" n k
      MostRings = fun n r -> sprintf "%s FLEW OUT %d TIMES" n r
      NoCrates = sprintf "%s NEVER OPENED A CRATE"
      HudHull = "HULL"
      HudShield = "SHIELD"
      HudBoost = "BOOST"
      HudHeat = "HEAT"
      HudOver = "OVER"
      ColKills = "KILLS"
      ColAccuracy = "ACCURACY"
      ColCrates = "CRATES"
      ColRings = "RINGS"
      ColStocks = "STOCKS"
      AwardKills = "MOST KILLS"
      AwardAim = "BEST AIM"
      AwardRings = "RING-OUTS"
      AwardCrates = "EMPTY HANDED"
      SuddenDeath = "SUDDEN DEATH"
      SoundHint = "TAP ANY KEY OR CLICK THE SCREEN FOR SOUND"
      Joins = sprintf "%s JOINS THE FIGHT"
      CatchUp = "CATCH-UP"
      SeriesWin = sprintf "%s TAKES THE SERIES"
      LaunchReady = "LAUNCHER · FIRE HURLS A ROCK"
      LaunchWait = "LAUNCHER · RELOADING"
      Bot = "BOT"
      CrazyGames = "CRAZYGAMES"
      SignIn = "SIGN IN"
      AddBot = "ADD BOT"
      ClearBots = "CLEAR BOTS"
      Tuning = "TUNING"
      Audio = "AUDIO"
      Music = "MUSIC"
      Sounds = "SOUNDS"
      On = "ON"
      Off = "OFF"
      MoreUp = "▲ MORE ABOVE"
      More = "▼ MORE BELOW"
      Stick = "STICK"
      HintMove = "MOVE"
      HintAdjust = "ADJUST"
      HintToggle = "TOGGLE"
      HintSelect = "SELECT"
      HintBack = "BACK"
      Mute = "MUTE"
      MuteKey = "M"
      Bindings = "KEY BINDINGS"
      BindPress = "PRESS A KEY"
      ResetKeys = "RESET KEYS"
      BindSet = "FIRE REBINDS"
      BindAdd = "ADD KEY"
      BindDrop = "DROP KEY"
      BindCancel = "CANCELS"
      BindTaken = "A KEY THAT IS ANOTHER ACTION'S ONLY BINDING IS REFUSED"
      BindTurnL = "TURN LEFT"
      BindTurnR = "TURN RIGHT"
      BindStrafeL = "STRAFE LEFT"
      BindStrafeR = "STRAFE RIGHT"
      BindThrust = "THRUST"
      BindReverse = "REVERSE"
      BindBoost = "BOOST"
      BindFire = "FIRE"
      BindSpecial = "SPECIAL"
      BindStart = "PAUSE / START"
      BindBack = "BACK / CANCEL"
      TutTitle = "HOW TO PLAY"
      TutThrust = "THRUST / REVERSE"
      TutTurn = "TURN"
      TutFire = "FIRE"
      TutBoost = "BOOST"
      TutSpecial = "SPECIAL"
      TutPause = "PAUSE"
      TutSkip = "PRESS ANY KEY OR CLICK TO START" }

let t = en

module Bind = Domain.Binds

/// Physical `KeyboardEvent.code` -> the cap printed on the player's keyboard.
/// The AZERTY branch lives here and nowhere else, so no legend needs a second
/// hand-written variant.
let keyName (code: string) =
    let letter (c: string) =
        if Domain.layout = Domain.Azerty then
            match c with
            | "A" -> "Q"
            | "Q" -> "A"
            | "W" -> "Z"
            | "Z" -> "W"
            | x -> x
        else c
    match code with
    | "" -> "-"
    | "Space" -> "SPACE"
    | "Enter"
    | "NumpadEnter" -> "ENTER"
    | "Escape" -> "ESC"
    | "Tab" -> "TAB"
    | "Backspace" -> "BKSP"
    | "CapsLock" -> "CAPS"
    | "ShiftLeft"
    | "ShiftRight" -> "SHIFT"
    | "ControlLeft"
    | "ControlRight" -> "CTRL"
    | "AltLeft"
    | "AltRight" -> "ALT"
    | "ArrowUp" -> "\u2191"
    | "ArrowDown" -> "\u2193"
    | "ArrowLeft" -> "\u2190"
    | "ArrowRight" -> "\u2192"
    | "Minus" -> "-"
    | "Equal" -> "="
    | "Comma" -> ","
    | "Period" -> "."
    | "Slash" -> "/"
    | "Backslash" -> "\\"
    | "Semicolon" -> ";"
    | "Quote" -> "'"
    | "BracketLeft" -> "["
    | "BracketRight" -> "]"
    | "Backquote" -> "`"
    | c when c.StartsWith "Key" -> letter (c.Substring 3)
    | c when c.StartsWith "Digit" -> c.Substring 5
    | c when c.StartsWith "Numpad" -> "NUM " + c.Substring 6
    | c -> c.ToUpper()

/// Every cap currently bound to an action, deduped (LSHIFT and RSHIFT both read SHIFT).
let bindCaps (a: Bind.Act) =
    Bind.get a |> Array.map keyName |> Array.distinct |> List.ofArray

/// The same caps as one " / " separated string.
let bindKeys (a: Bind.Act) = bindCaps a |> String.concat " / "

/// The primary cap only — what the compact legends and hints show.
let private first (a: Bind.Act) =
    let cs = Bind.get a
    if cs.Length = 0 then "" else keyName cs.[0]

let private pair a b = sprintf "%s %s" (first a) (first b)

let actionName (a: Bind.Act) =
    match a with
    | Bind.TurnLeft -> t.BindTurnL
    | Bind.TurnRight -> t.BindTurnR
    | Bind.StrafeLeft -> t.BindStrafeL
    | Bind.StrafeRight -> t.BindStrafeR
    | Bind.Thrust -> t.BindThrust
    | Bind.Reverse -> t.BindReverse
    | Bind.Boost -> t.BindBoost
    | Bind.Fire -> t.BindFire
    | Bind.Special -> t.BindSpecial
    | Bind.Start -> t.BindStart
    | Bind.Back -> t.BindBack

let bindHint () =
    sprintf "%s  ·  \u25b6 %s  ·  \u25c0 %s  ·  %s %s"
        t.BindSet t.BindAdd t.BindDrop (keyName "Escape") t.BindCancel

let optHint () =
    sprintf "%s / %s  ·  %s        %s / %s  ·  %s        %s / A  ·  %s        %s / B  ·  %s"
        (pair Bind.Thrust Bind.Reverse) t.Stick t.HintMove
        (pair Bind.TurnLeft Bind.TurnRight) t.Stick t.HintAdjust
        (bindKeys Bind.Fire) t.HintToggle
        (bindKeys Bind.Back) t.HintBack

let navKeys () =
    [ sprintf "%s / %s / %s" (first Bind.Thrust) (first Bind.Reverse) t.Stick, t.HintMove
      sprintf "%s / A" (bindKeys Bind.Fire), t.HintSelect
      sprintf "%s / B" (bindKeys Bind.Back), t.HintBack ]

let keysJoin () = sprintf "%s / A" (bindKeys Bind.Fire)
let keysLeave () = sprintf "%s / B" (bindKeys Bind.Back)
let keysLaunch () = sprintf "%s / %s" (bindKeys Bind.Start) t.Start

let kbLegend () =
    [ [ first Bind.Thrust; first Bind.Reverse ], t.TutThrust
      [ first Bind.TurnLeft; first Bind.TurnRight ], t.TutTurn
      [ first Bind.StrafeLeft; first Bind.StrafeRight ], t.PadStrafe
      bindCaps Bind.Boost, t.TutBoost
      bindCaps Bind.Fire, t.TutFire
      bindCaps Bind.Special, t.TutSpecial
      bindCaps Bind.Start, t.TutPause
      [ t.MuteKey ], t.Mute ]

/// The tutorial card: the same rows as the keyboard legend without STRAFE and MUTE.
let tutRows () =
    [ [ first Bind.Thrust; first Bind.Reverse ], t.TutThrust
      [ first Bind.TurnLeft; first Bind.TurnRight ], t.TutTurn
      bindCaps Bind.Fire, t.TutFire
      bindCaps Bind.Boost, t.TutBoost
      bindCaps Bind.Special, t.TutSpecial
      bindCaps Bind.Start, t.TutPause ]

let kbHint () = sprintf "%s / %s" (pair Bind.Thrust Bind.Reverse) t.Stick, t.HintMove
