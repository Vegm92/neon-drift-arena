module Strings

type Locale =
    { Player: int -> string
      Wins: string -> string
      Draw: string
      Rematch: string
      TitleMain: string
      TitleSub: string
      Mode: string list
      KbLegend: (string list * string) list
      PadLegend: (string list * string) list
      Gamepad: string
      Phone: string
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
      KeysJoin: string
      KeysLeave: string
      KeysLaunch: string
      RowLabel: string
      TeamLabel: string
      ColorLabel: string
      Colors: string[]
      ModeLabel: string
      Ffa: string
      Teams: string
      Practice: string
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
      NavKeys: (string * string) list
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
      WSwarm: string
      WPulse: string
      WScatter: string
      WTractor: string
      Loaded: string -> int -> string
      FirstBlood: string
      DoubleKill: string
      RingOut: string
      LastStock: string -> string
      OnFire: string -> string
      BestAim: string -> int -> string
      MostKills: string -> int -> string
      MostRings: string -> int -> string
      NoCrates: string -> string
      SuddenDeath: string
      SoundHint: string
      CatchUp: string
      SeriesWin: string -> string
      GhostReady: string
      GhostWait: string
      Bot: string
      AddBot: string
      ClearBots: string
      Tuning: string
      Audio: string
      Music: string
      Sounds: string
      On: string
      Off: string
      More: string
      OptHint: string }

let en =
    { Player = fun i -> sprintf "P%d" (i + 1)
      Wins = sprintf "%s WINS"
      Draw = "DRAW"
      Rematch = "REMATCH"
      TitleMain = "NEON DRIFT"
      TitleSub = "ARENA"
      Mode = [ "STOCK BATTLE"; "3 LIVES"; "LAST SHIP FLYING WINS" ]
      KbLegend =
        [ [ "W"; "S" ], "THRUST / REVERSE"
          [ "A"; "D" ], "TURN"
          [ "Q"; "E" ], "STRAFE"
          [ "SHIFT" ], "BOOST"
          [ "SPACE" ], "FIRE"
          [ "F" ], "SPECIAL"
          [ "ENTER" ], "PAUSE"
          [ "M" ], "MUTE" ]
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
      KeysJoin = "SPACE / A"
      KeysLeave = "ESC / B"
      KeysLaunch = "ENTER / START"
      RowLabel = "MENU"
      TeamLabel = "TEAM"
      ColorLabel = "COLOUR"
      Colors = [| "CYAN"; "MAGENTA"; "LIME"; "AMBER" |]
      ModeLabel = "MODE"
      Ffa = "FREE FOR ALL"
      Teams = "TEAMS"
      Practice = "PRACTICE"
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
      NavKeys = [ "W / S / STICK", "MOVE"; "SPACE / A", "SELECT"; "ESC / B", "BACK" ]
      Tweaks = "TWEAKS"
      Reset = "RESET"
      Save = "SAVE TO CODE"
      Settings = "SETTINGS"
      Arena = "ARENA"
      Arenas = [| "CORE RING"; "CROSS BASTIONS"; "PINWHEEL"; "TWIN GAUNTLET"; "OPEN BELT" |]
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
      WSwarm = "SEEKERS"
      WPulse = "REPULSOR"
      WScatter = "SCATTER GUN"
      WTractor = "TRACTOR"
      Loaded = fun n a -> sprintf "%s x%d" n a
      FirstBlood = "FIRST BLOOD"
      DoubleKill = "DOUBLE KILL"
      RingOut = "RING OUT"
      LastStock = sprintf "%s ON LAST STOCK"
      OnFire = sprintf "%s IS ON FIRE"
      BestAim = fun n p -> sprintf "%s LANDED %d%% OF SHOTS" n p
      MostKills = fun n k -> sprintf "%s TOOK DOWN %d SHIPS" n k
      MostRings = fun n r -> sprintf "%s FLEW OUT %d TIMES" n r
      NoCrates = sprintf "%s NEVER OPENED A CRATE"
      SuddenDeath = "SUDDEN DEATH"
      SoundHint = "TAP ANY KEY OR CLICK THE SCREEN FOR SOUND"
      CatchUp = "CATCH-UP"
      SeriesWin = sprintf "%s TAKES THE SERIES"
      GhostReady = "GHOST · FIRE DROPS A MINE"
      GhostWait = "GHOST · RECHARGING"
      Bot = "BOT"
      AddBot = "ADD BOT"
      ClearBots = "CLEAR BOTS"
      Tuning = "TUNING"
      Audio = "AUDIO"
      Music = "MUSIC"
      Sounds = "SOUNDS"
      On = "ON"
      Off = "OFF"
      More = "▼ MORE BELOW"
      OptHint = "W S / STICK  ·  MOVE        A D / STICK  ·  ADJUST        SPACE / A  ·  TOGGLE        ESC / B  ·  BACK" }

let t = en
