module Domain

open Vec

module Cfg =
    let arenaHalf = 1350.
    let diagLimit = arenaHalf * 1.62
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
    let physicsDt = 1. / 120.
    let mutable asteroidStun = 0.666
    let mutable asteroidSpin = 6.3

    let heatMax = 100.
    let mutable heatPerShot = 9.6
    let mutable heatCool = 33.
    let mutable overheatLock = 1.5

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
    let mutable seekerDamage = 14.
    let seekerLife = 4.
    let swarmAmmo = 3

    let mutable pulseRange = 340.
    let mutable pulseCone = 0.62
    let mutable pulseForce = 430.
    let pulseAmmo = 3

    let mutable padAimOn = 0.15
    let mutable padThrustOn = 0.75

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
           "asteroidStun", (fun () -> asteroidStun), (fun x -> asteroidStun <- x)
           "asteroidSpin", (fun () -> asteroidSpin), (fun x -> asteroidSpin <- x)
           "heatPerShot", (fun () -> heatPerShot), (fun x -> heatPerShot <- x)
           "heatCool", (fun () -> heatCool), (fun x -> heatCool <- x)
           "overheatLock", (fun () -> overheatLock), (fun x -> overheatLock <- x)
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
      Present: bool }

let noInput =
    { Turn = 0.; Aim = None; Absolute = false; Steer = false; Strafe = 0.; Thrust = false; Reverse = false; Boost = false; Fire = false; Special = false; Start = false; Back = false; Present = false }

type Weapon =
    | Blaster
    | Rail
    | Mines
    | Swarm
    | Pulse

let crateWeapons = [| Rail; Mines; Swarm; Pulse |]

let weaponAmmo w =
    match w with
    | Blaster -> 0
    | Rail -> Cfg.railAmmo
    | Mines -> Cfg.mineAmmo
    | Swarm -> Cfg.swarmAmmo
    | Pulse -> Cfg.pulseAmmo

let playerColor = [| 0; 1; 2; 3 |]

type Ship =
    { Id: int
      Team: int
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
      LastHit: int
      Streak: int
      Shots: int
      Hits: int
      Grabs: int
      Rings: int
      Kills: int }

type Bullet =
    { Owner: int
      Pos: V2
      Vel: V2
      Life: float
      Kind: int
      Damage: float }

type Mine = { Owner: int; Pos: V2; Vel: V2; Fuse: float }

type Pad = { Pos: V2; Amount: float; RespawnIn: float; Kind: int }

type Crate = { Pos: V2; RespawnIn: float }

type Asteroid = { Pos: V2; Radius: float }

type Phase =
    | Playing
    | Over of winner: int option

type Event =
    | Hit of V2
    | Explode of V2 * int * bool
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

type World =
    { Ships: Ship[]
      Bullets: Bullet list
      Mines: Mine list
      Pads: Pad[]
      Crates: Crate[]
      Rng: int
      Phase: Phase
      Time: float
      Events: Event list }
