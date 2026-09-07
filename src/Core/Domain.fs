module Domain

open Vec

type Layout = Qwerty | Azerty
let mutable layout = Qwerty

/// Keyboard action -> physical `KeyboardEvent.code` list. Several actions carry
/// two codes by default (a letter and an arrow), so the map is a list per action,
/// never a single code. Pure data: `Settings.fs` persists it, `Input.fs` reads it
/// and `Strings.fs` renders the labels.
module Binds =
    type Act =
        | TurnLeft
        | TurnRight
        | StrafeLeft
        | StrafeRight
        | Thrust
        | Reverse
        | Boost
        | Fire
        | Special
        | Start
        | Back
        | Swap

    let all =
        [| TurnLeft; TurnRight; StrafeLeft; StrafeRight; Thrust; Reverse; Boost; Fire; Special; Start; Back; Swap |]

    let ord a =
        match a with
        | TurnLeft -> 0
        | TurnRight -> 1
        | StrafeLeft -> 2
        | StrafeRight -> 3
        | Thrust -> 4
        | Reverse -> 5
        | Boost -> 6
        | Fire -> 7
        | Special -> 8
        | Start -> 9
        | Back -> 10
        | Swap -> 11

    /// Stable storage key, independent of the display label.
    let name a =
        match a with
        | TurnLeft -> "turnLeft"
        | TurnRight -> "turnRight"
        | StrafeLeft -> "strafeLeft"
        | StrafeRight -> "strafeRight"
        | Thrust -> "thrust"
        | Reverse -> "reverse"
        | Boost -> "boost"
        | Fire -> "fire"
        | Special -> "special"
        | Start -> "start"
        | Back -> "back"
        | Swap -> "swap"

    /// The letter comes first so the legends read W/S/A/D as they always have.
    let defaults a =
        match a with
        | TurnLeft -> [| "KeyA"; "ArrowLeft" |]
        | TurnRight -> [| "KeyD"; "ArrowRight" |]
        | StrafeLeft -> [| "KeyQ" |]
        | StrafeRight -> [| "KeyE" |]
        | Thrust -> [| "KeyW"; "ArrowUp" |]
        | Reverse -> [| "KeyS"; "ArrowDown" |]
        | Boost -> [| "ShiftLeft"; "ShiftRight" |]
        | Fire -> [| "Space" |]
        | Special -> [| "KeyF" |]
        | Start -> [| "Enter" |]
        | Back -> [| "Escape" |]
        | Swap -> [| "KeyB" |]

    let private codes = all |> Array.map defaults

    let get a = codes.[ord a]

    /// An action never ends up with an empty list: an empty write is ignored.
    let set a (cs: string[]) = if cs.Length > 0 then codes.[ord a] <- cs

    let reset () =
        for a in all do
            codes.[ord a] <- defaults a

    let isBound (code: string) = codes |> Array.exists (fun cs -> Array.contains code cs)

    /// True when `code` is the last binding another action has left, which makes
    /// it unsafe to steal for `except`.
    let isSoleBindingOf (code: string) (except: Act) =
        all
        |> Array.exists (fun b -> ord b <> ord except && (get b).Length = 1 && Array.contains code (get b))

module Cfg =
    let mutable arenaHalf = 1350.
    let arenaDefault = 1350.
    let raceHalf = 1500.
    let diagLimit () = arenaHalf * 1.62
    let killMargin = 60.
    let shipRadius = 18.
    let mutable turnRate = 4.2
    let mutable thrustAccel = 110.
    let mutable reverseFactor = 0.25
    let mutable boostAccel = 320.
    let mutable maxSpeed = 270.
    let mutable strafeAccel = 90.
    let mutable drag = 0.078
    let boostMax = 100.
    let mutable boostDrain = 20.9
    let boostStart = 60.
    let mutable padRefill = 25.9
    let padRespawn = 7.
    let padRadius = 28.
    let mutable fireCooldown = 0.11
    let mutable bulletSpeed = 560.
    let bulletLife = 1.3
    let mutable bulletDamage = 20.
    let mutable bulletKnockback = 40.
    let mutable recoil = 4.08
    let hpMax = 100.
    let stocks = 3
    let respawnDelay = 3.
    let invulnTime = 1.5
    let restitution = 0.85
    let mutable ramDamageFactor = 0.09
    let mutable ramSeparate = 6.
    let mutable ramEventSpeed = 40.
    let physicsDt = 1. / 120.
    let mutable asteroidStun = 0.666
    let mutable asteroidSpin = 6.3

    let heatMax = 100.
    let mutable heatPerShot = 15.
    let mutable heatCool = 50.

    let mutable hurtBelow = 25.
    let mutable hurtFactor = 0.75

    let mutable healAmount = 40.
    let healRespawn = 30.

    let mutable shieldAmount = hpMax * 0.2
    let shieldRespawn = 30.

    let crateRespawn = 10.
    let crateRadius = 26.

    let mutable railCharge = 1.5
    let mutable railDamage = 100.
    let mutable railRecoil = 46.
    let railAmmo = 2

    let mineRadius = 11.
    let mutable mineMagnet = 130.
    let mutable mineFuse = 1.5
    let mutable mineChase = 200.
    let mutable mineBlast = 115.
    let mutable mineDamage = 45.
    let mineAmmo = 4

    let mutable seekerSpeed = 290.
    let mutable seekerTurn = 3.4
    let mutable seekerDamage = 34.
    let seekerLife = 12.
    let swarmAmmo = 3

    let mutable pulseRange = 340.
    let mutable pulseCone = 0.62
    let mutable pulseForce = 430.
    let pulseAmmo = 3

    let mutable scatterRange = bulletSpeed * bulletLife * 0.5
    let mutable scatterCone = 0.7
    let mutable scatterDamage = 12.
    let mutable scatterStun = 1.
    let scatterAmmo = 2

    let mutable tractorRange = bulletSpeed * bulletLife * 0.5
    let mutable tractorCone = 0.9
    let mutable tractorPull = 900.
    let mutable tractorTime = 1.4
    let tractorAmmo = 2

    let mutable matchTime = 150.
    let mutable shrinkTime = 40.
    let shrinkMin = 0.45
    let introTime = 5.
    let victoryTime = 5.
    let victoryCamH = 220.
    // Ships still in the match at or below which the camera may frame tighter.
    let duelShips = 2
    // Fraction of the normal minimum camera height used once duelShips remain.
    let duelCamFactor = 0.6
    // Fraction of the normal minimum camera height used in RACE.
    let raceCamFactor = 0.6
    let seriesTo = 5
    let mutable ghostSpeed = 240.
    let mutable ghostCooldown = 8.
    let mutable rockSpeed = 300.
    let mutable rockRadius = 20.
    let mutable rockCooldown = 6.
    let mutable rockLife = 14.
    let mutable rockDamage = 0.09
    let inviteButton = true

    let mutable portalEvery = 20.
    let mutable portalLife = 12.
    let mutable portalRadius = 45.

    let mutable holeEvery = 35.
    let mutable holeLife = 15.
    let mutable holeG = 9e6
    let mutable holeCore = 30.

    let mutable wallLife = 6.
    let mutable wallLen = 140.
    let mutable wallThick = 8.
    let barrierAmmo = 2

    let sentryRadius = 16.
    let mutable sentryRange = 420.
    let mutable sentryLife = 15.
    let mutable sentryHp = 60.
    let mutable sentryCooldown = 0.25
    let mutable sentryDamage = 12.
    let sentryAmmo = 1

    let mutable bubbleRadius = 220.
    let mutable bubbleLife = 4.
    let mutable bubbleFactor = 0.4
    let bubbleAmmo = 1

    let mutable laps = 3.
    let mutable gateRadius = 150.
    let mutable raceGrace = 20.
    let mutable raceDrag = 0.10
    let mutable offroadFactor = 0.55
    let mutable raceGrip = 2.5
    let mutable racePulseRange = 190.
    let mutable raceBubbleLife = 2.

    let mutable padAimOn = 0.15
    let mutable padThrustOn = 0.75

    let netStateMs = 100.

    let tunables: (string * (unit -> float) * (float -> unit))[] =
        [| "turnRate", (fun () -> turnRate), (fun x -> turnRate <- x)
           "thrustAccel", (fun () -> thrustAccel), (fun x -> thrustAccel <- x)
           "reverseFactor", (fun () -> reverseFactor), (fun x -> reverseFactor <- x)
           "boostAccel", (fun () -> boostAccel), (fun x -> boostAccel <- x)
           "maxSpeed", (fun () -> maxSpeed), (fun x -> maxSpeed <- x)
           "strafeAccel", (fun () -> strafeAccel), (fun x -> strafeAccel <- x)
           "drag", (fun () -> drag), (fun x -> drag <- x)
           "boostDrain", (fun () -> boostDrain), (fun x -> boostDrain <- x)
           "padRefill", (fun () -> padRefill), (fun x -> padRefill <- x)
           "fireCooldown", (fun () -> fireCooldown), (fun x -> fireCooldown <- x)
           "bulletSpeed", (fun () -> bulletSpeed), (fun x -> bulletSpeed <- x)
           "bulletDamage", (fun () -> bulletDamage), (fun x -> bulletDamage <- x)
           "bulletKnockback", (fun () -> bulletKnockback), (fun x -> bulletKnockback <- x)
           "recoil", (fun () -> recoil), (fun x -> recoil <- x)
           "ramDamageFactor", (fun () -> ramDamageFactor), (fun x -> ramDamageFactor <- x)
           "ramSeparate", (fun () -> ramSeparate), (fun x -> ramSeparate <- x)
           "ramEventSpeed", (fun () -> ramEventSpeed), (fun x -> ramEventSpeed <- x)
           "asteroidStun", (fun () -> asteroidStun), (fun x -> asteroidStun <- x)
           "asteroidSpin", (fun () -> asteroidSpin), (fun x -> asteroidSpin <- x)
           "heatPerShot", (fun () -> heatPerShot), (fun x -> heatPerShot <- x)
           "heatCool", (fun () -> heatCool), (fun x -> heatCool <- x)
           "hurtBelow", (fun () -> hurtBelow), (fun x -> hurtBelow <- x)
           "hurtFactor", (fun () -> hurtFactor), (fun x -> hurtFactor <- x)
           "healAmount", (fun () -> healAmount), (fun x -> healAmount <- x)
           "shieldAmount", (fun () -> shieldAmount), (fun x -> shieldAmount <- x)
           "railCharge", (fun () -> railCharge), (fun x -> railCharge <- x)
           "railDamage", (fun () -> railDamage), (fun x -> railDamage <- x)
           "railRecoil", (fun () -> railRecoil), (fun x -> railRecoil <- x)
           "mineMagnet", (fun () -> mineMagnet), (fun x -> mineMagnet <- x)
           "mineFuse", (fun () -> mineFuse), (fun x -> mineFuse <- x)
           "mineChase", (fun () -> mineChase), (fun x -> mineChase <- x)
           "mineBlast", (fun () -> mineBlast), (fun x -> mineBlast <- x)
           "mineDamage", (fun () -> mineDamage), (fun x -> mineDamage <- x)
           "seekerSpeed", (fun () -> seekerSpeed), (fun x -> seekerSpeed <- x)
           "seekerTurn", (fun () -> seekerTurn), (fun x -> seekerTurn <- x)
           "seekerDamage", (fun () -> seekerDamage), (fun x -> seekerDamage <- x)
           "pulseRange", (fun () -> pulseRange), (fun x -> pulseRange <- x)
           "pulseCone", (fun () -> pulseCone), (fun x -> pulseCone <- x)
           "pulseForce", (fun () -> pulseForce), (fun x -> pulseForce <- x)
           "scatterRange", (fun () -> scatterRange), (fun x -> scatterRange <- x)
           "scatterCone", (fun () -> scatterCone), (fun x -> scatterCone <- x)
           "scatterDamage", (fun () -> scatterDamage), (fun x -> scatterDamage <- x)
           "scatterStun", (fun () -> scatterStun), (fun x -> scatterStun <- x)
           "tractorRange", (fun () -> tractorRange), (fun x -> tractorRange <- x)
           "tractorCone", (fun () -> tractorCone), (fun x -> tractorCone <- x)
           "tractorPull", (fun () -> tractorPull), (fun x -> tractorPull <- x)
           "tractorTime", (fun () -> tractorTime), (fun x -> tractorTime <- x)
           "matchTime", (fun () -> matchTime), (fun x -> matchTime <- x)
           "ghostSpeed", (fun () -> ghostSpeed), (fun x -> ghostSpeed <- x)
           "ghostCooldown", (fun () -> ghostCooldown), (fun x -> ghostCooldown <- x)
           "rockSpeed", (fun () -> rockSpeed), (fun x -> rockSpeed <- x)
           "rockRadius", (fun () -> rockRadius), (fun x -> rockRadius <- x)
           "rockCooldown", (fun () -> rockCooldown), (fun x -> rockCooldown <- x)
           "rockLife", (fun () -> rockLife), (fun x -> rockLife <- x)
           "rockDamage", (fun () -> rockDamage), (fun x -> rockDamage <- x)
           "portalEvery", (fun () -> portalEvery), (fun x -> portalEvery <- x)
           "portalLife", (fun () -> portalLife), (fun x -> portalLife <- x)
           "portalRadius", (fun () -> portalRadius), (fun x -> portalRadius <- x)
           "holeEvery", (fun () -> holeEvery), (fun x -> holeEvery <- x)
           "holeLife", (fun () -> holeLife), (fun x -> holeLife <- x)
           "holeG", (fun () -> holeG), (fun x -> holeG <- x)
           "holeCore", (fun () -> holeCore), (fun x -> holeCore <- x)
           "wallLife", (fun () -> wallLife), (fun x -> wallLife <- x)
           "wallLen", (fun () -> wallLen), (fun x -> wallLen <- x)
           "wallThick", (fun () -> wallThick), (fun x -> wallThick <- x)
           "sentryRange", (fun () -> sentryRange), (fun x -> sentryRange <- x)
           "sentryLife", (fun () -> sentryLife), (fun x -> sentryLife <- x)
           "sentryHp", (fun () -> sentryHp), (fun x -> sentryHp <- x)
           "sentryCooldown", (fun () -> sentryCooldown), (fun x -> sentryCooldown <- x)
           "sentryDamage", (fun () -> sentryDamage), (fun x -> sentryDamage <- x)
           "bubbleRadius", (fun () -> bubbleRadius), (fun x -> bubbleRadius <- x)
           "bubbleLife", (fun () -> bubbleLife), (fun x -> bubbleLife <- x)
           "bubbleFactor", (fun () -> bubbleFactor), (fun x -> bubbleFactor <- x)
           "shrinkTime", (fun () -> shrinkTime), (fun x -> shrinkTime <- x)
           "laps", (fun () -> laps), (fun x -> laps <- x)
           "gateRadius", (fun () -> gateRadius), (fun x -> gateRadius <- x)
           "raceGrace", (fun () -> raceGrace), (fun x -> raceGrace <- x)
           "raceDrag", (fun () -> raceDrag), (fun x -> raceDrag <- x)
           "offroadFactor", (fun () -> offroadFactor), (fun x -> offroadFactor <- x)
           "raceGrip", (fun () -> raceGrip), (fun x -> raceGrip <- x)
           "racePulseRange", (fun () -> racePulseRange), (fun x -> racePulseRange <- x)
           "raceBubbleLife", (fun () -> raceBubbleLife), (fun x -> raceBubbleLife <- x)
           "padAimOn", (fun () -> padAimOn), (fun x -> padAimOn <- x)
           "padThrustOn", (fun () -> padThrustOn), (fun x -> padThrustOn <- x) |]

type Input =
    { Turn: float
      Aim: float option
      Absolute: bool
      Steer: bool
      Strafe: float
      Thrust: bool
      Reverse: bool
      Boost: bool
      Fire: bool
      Special: bool
      Start: bool
      Back: bool
      Swap: bool
      Present: bool }

let noInput =
    { Turn = 0.; Aim = None; Absolute = false; Steer = false; Strafe = 0.; Thrust = false; Reverse = false; Boost = false; Fire = false; Special = false; Start = false; Back = false; Swap = false; Present = false }

type Weapon =
    | Blaster
    | Rail
    | Mines
    | Swarm
    | Pulse
    | Scatter
    | Tractor
    | Collision
    | Rock
    | Singularity
    | Barrier
    | Sentry
    | Bubble

type ShipArchetype =
    | Standard
    | Interceptor
    | Juggernaut
    | Engineer
    | Scout

let crateTiers = [| Rail, 1; Bubble, 1; Pulse, 2; Scatter, 2; Tractor, 2; Barrier, 2; Sentry, 2; Mines, 3; Swarm, 3 |]
let crateWeapons = crateTiers |> Array.collect (fun (w, n) -> Array.create n w)

let weaponAmmo w =
    match w with
    | Blaster -> 0
    | Rail -> Cfg.railAmmo
    | Mines -> Cfg.mineAmmo
    | Swarm -> Cfg.swarmAmmo
    | Pulse -> Cfg.pulseAmmo
    | Scatter -> Cfg.scatterAmmo
    | Tractor -> Cfg.tractorAmmo
    | Collision -> 0
    | Rock -> 0
    | Singularity -> 0
    | Barrier -> Cfg.barrierAmmo
    | Sentry -> Cfg.sentryAmmo
    | Bubble -> Cfg.bubbleAmmo

type Tether =
    | NoTether
    | TowShip of int
    | TowRock of int

let playerColor = [| 0; 1; 2; 3 |]

type Ship =
    { Id: int
      Team: int
      Archetype: ShipArchetype
      Pos: V2
      Vel: V2
      Angle: float
      Hp: float
      Shield: float
      Boost: float
      Stocks: int
      Alive: bool
      Active: bool
      Thrusting: float
      Reversing: bool
      RespawnIn: float
      Invuln: float
      Cooldown: float
      Stun: float
      Spin: float
      Heat: float
      Locked: float
      Weapon: Weapon
      Ammo: int
      Charge: float
      Held: bool
      Tow: Tether
      TowLeft: float
      LastHit: int
      LastWeapon: Weapon
      LaunchCd: float
      LaunchAngle: float
      Ghosting: bool
      WarpCd: float
      Next: int
      Laps: int
      Finish: float
      Streak: int
      Shots: int
      Hits: int
      Grabs: int
      Rings: int
      Kills: int
      FirstBloodMedal: int
      DoubleKillMedals: int
      TripleKillMedals: int
      RailKillMedals: int
      RamKillMedals: int
      OnFireMedals: int
      VoidMedals: int
      HoleMedals: int
      AbductMedals: int
      GraveMedals: int
      LastKillTime: float
      MultiKillCount: int }

type Bullet =
    { Owner: int
      Pos: V2
      Vel: V2
      Life: float
      Kind: int
      Damage: float }

type Mine = { Owner: int; Pos: V2; Vel: V2; Fuse: float }

type Rock = { Owner: int; Pos: V2; Vel: V2; Radius: float; Life: float }

type Portal = { A: V2; B: V2; Life: float }

type Hole = { Pos: V2; Life: float }

type Deployable =
    { Owner: int
      Kind: int
      Pos: V2
      Angle: float
      Hp: float
      Life: float
      Cooldown: float }

let wallKind = 0
let turretKind = 1
let bubbleKind = 2

type Pad = { Pos: V2; Amount: float; RespawnIn: float; Kind: int }

type Crate = { Pos: V2; RespawnIn: float }

type Asteroid = { Pos: V2; Radius: float }

type Phase =
    | Playing
    | Over of winner: int option

type Event =
    | Hit of V2 * string * float * int
    | Explode of V2 * int * bool
    | Downed of int * int * Weapon * bool
    | Medal of int * string
    | Shot of V2
    | Ram of V2
    | Bump of V2
    | Pickup of V2 * bool
    | Mend of V2
    | Grab of V2
    | Charging of V2
    | Beam of V2 * V2 * int
    | MineSet of V2
    | MineLive of V2
    | Blast of V2
    | Wave of V2 * float * int
    | Cooked of V2
    | Zap of V2 * float * int
    | Latch of V2 * int
    | Launch of V2
    | PortalOpen of V2 * V2
    | Warp of V2
    | HoleOpen of V2
    | Deployed of Deployable
    | Finished of int * int

type World =
    { Ships: Ship[]
      Bullets: Bullet list
      Mines: Mine list
      Rocks: Rock list
      Portals: Portal list
      PortalIn: float
      Hole: Hole option
      HoleIn: float
      Deploys: Deployable list
      RaceEnd: float
      Pads: Pad[]
      Crates: Crate[]
      Rng: int
      Phase: Phase
      Time: float
      Events: Event list }
