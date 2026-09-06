module Sim

open System
open Vec
open Domain
open Domain.Cfg
open Maps
open State
open Track
open Spawn
open Arena
open Combat
open Kinematics
open Weapons
open Entities
open Collisions
open Rebirth
open Race
open Bot

let withTeams (teams: int[]) (w: World) =
    { w with Ships = w.Ships |> Array.map (fun s -> { s with Team = teams.[s.Id] }) }

let spawnPos i = Spawn.spawnPos i
let onTrack (p: V2) = Track.onTrack p
let side (s: Ship) = Combat.side s
let hurting (s: Ship) = Combat.hurting s
let launcher (s: Ship) = Combat.launcher s
let bounds t = Arena.bounds t
let sudden (w: World) = Arena.sudden w
let rank (ships: Ship[]) = Race.rank ships
let place (ships: Ship[]) i = Race.place ships i
let bot (w: World) i = Bot.bot w i

let private build i =
    let l = layouts.[i]
    asteroids <- l.Rocks |> List.map (fun (p, r) -> { Pos = p; Radius = r }) |> List.toArray
    cratePositions <- l.Crates |> List.toArray
    arenaHalf <- l.Size
    corners <- l.Track |> Option.map (fun (r, _, _) -> List.toArray r) |> Option.defaultValue [||]
    road <- if corners.Length > 0 then smoothLoop corners else [||]
    gateEvery <- l.Track |> Option.map (fun (_, _, k) -> k * 4) |> Option.defaultValue 1
    gates <- [| for i in 0 .. gateEvery .. road.Length - 1 -> road.[i] |]
    trackWidth <- l.Track |> Option.map (fun (_, w, _) -> w) |> Option.defaultValue 0.
    mapHole <- l.Hole
    { Ships = Array.init 4 (fun i -> { freshShip i with Active = false; Alive = false; Stocks = 0 })
      Bullets = []
      Mines = []
      Rocks = []
      Portals = []
      PortalIn = portalEvery
      Hole = l.Hole |> Option.map (fun (p, _, _) -> { Pos = p; Life = infinity })
      HoleIn = holeEvery
      RaceEnd = 0.
      Pads = l.Pads |> List.map (fun (p, a, k) -> { Pos = p; Amount = a; RespawnIn = 0.; Kind = k }) |> List.toArray
      Crates = [| 0; 2; 4; 6 |] |> Array.map (fun i -> { Pos = cratePositions.[i % cratePositions.Length]; RespawnIn = 0. })
      Rng = 7
      Phase = Playing
      Time = 0.
      Events = [] }

let mutable initial = build 0

let setLayout i =
    layout <- (i + layouts.Length) % layouts.Length
    initial <- build layout

let stage (w: World) =
    let shooters = w.Ships |> Array.filter (fun s -> s.Active && s.Id <> target) |> Array.map (fun s -> s.Id)
    let ships =
        w.Ships
        |> Array.map (fun s ->
            if s.Id = target then
                { freshShip s.Id with Team = s.Team; Pos = v 300. 0.; Angle = Math.PI; Invuln = 0. }
            elif s.Active then
                let k = Array.findIndex ((=) s.Id) shooters
                let y = (float k - float (shooters.Length - 1) / 2.) * 120.
                { freshShip s.Id with Team = s.Team; Pos = v -300. y; Angle = 0.; Invuln = 0. }
            else s)
    let crates = w.Crates |> Array.mapi (fun i c -> if i = 0 then { c with Pos = v 0. -220.; RespawnIn = 0. } else c)
    let mines = if target >= 0 then [ { Owner = target; Pos = v 0. 220.; Vel = zero; Fuse = -1. } ] else []
    { w with Ships = ships; Crates = crates; Mines = mines; Bullets = [] }

let arm i wpn (w: World) =
    { w with Ships = w.Ships |> Array.map (fun s -> if s.Id = i then { s with Weapon = wpn; Ammo = weaponAmmo wpn; Charge = 0. } else s) }

let reset (active: int -> bool) (w: World) =
    { initial with
        Ships =
            w.Ships
            |> Array.map (fun s ->
                if active s.Id then { freshShip s.Id with Team = s.Team }
                else { freshShip s.Id with Team = s.Team; Active = false; Alive = false; Stocks = 0 })
        Rng = w.Rng }

let step dt (inputs: Input[]) (w: World) =
    match w.Phase with
    | Over _ when inputs |> Array.exists (fun i -> i.Start) -> reset (fun i -> w.Ships.[i].Active) w
    | _ ->
        let k = bounds w.Time
        let live = (match w.Phase with Playing -> true | Over _ -> false)
        let fired =
            w.Ships
            |> Array.map (fun s -> join inputs.[s.Id] s |> stepShip k dt inputs.[s.Id] |> fire live dt inputs.[s.Id])
        let ships = fired |> Array.map (fun (s, _, _, _, _) -> s)
        let newBullets = fired |> Array.toList |> List.collect (fun (_, b, _, _, _) -> b)
        let newMines = fired |> Array.toList |> List.collect (fun (_, _, m, _, _) -> m)
        let newRocks = fired |> Array.toList |> List.collect (fun (_, _, _, r, _) -> r)
        let shotEvents = fired |> Array.toList |> List.collect (fun (_, _, _, _, e) -> e)
        let beams = shotEvents |> List.choose (function Beam(a, b, o) -> Some(a, b, o) | _ -> None)
        let waves = shotEvents |> List.choose (function Wave(p, a, o) -> Some(p, a, o) | _ -> None)
        let zaps = shotEvents |> List.choose (function Zap(p, a, o) -> Some(p, a, o) | _ -> None)
        let latches = shotEvents |> List.choose (function Latch(_, o) -> Some o | _ -> None)
        let ships = resolveWaves ships waves
        let ships, zapHits = resolveZaps ships zaps
        let ships = resolveTows dt ships latches
        let ships, beamHits = resolveBeams ships beams
        let bullets =
            newBullets @ w.Bullets
            |> List.map (steer dt ships)
            |> List.map (fun b -> { b with Vel = pull w.Hole dt b.Pos b.Vel })
            |> List.choose (stepBullet k w.Rocks dt)
            |> List.map (fun b -> match warpAt w.Portals b.Pos b.Vel with Some p -> { b with Pos = p } | None -> b)
        let ships, bullets, hits = resolveBullets ships bullets
        let mines, blasts, mineEvents = stepMines k dt ships (newMines @ w.Mines)
        let mines = mines |> List.map (fun m -> match warpAt w.Portals m.Pos m.Vel with Some p -> { m with Pos = p } | None -> { m with Vel = pull w.Hole dt m.Pos m.Vel })
        let ships, mineBlasts = resolveBlasts ships blasts
        let ships, rams = resolveRams ships
        let ships, bumps = resolveAsteroids ships
        let rocks, rockEvents = stepRocks k dt (newRocks @ w.Rocks)
        let rocks = rocks |> List.map (fun r -> match warpAt w.Portals r.Pos r.Vel with Some p -> { r with Pos = p } | None -> { r with Vel = pull w.Hole dt r.Pos r.Vel })
        let ships, rockHits = resolveRocks ships rocks
        let ships, warps = resolveWarps dt w.Portals ships
        let ships = resolveHole w.Hole dt ships
        let ships, pads, picks = resolvePads (sudden w) dt ships w.Pads
        let ships, crates, rng, grabs = stepCrates dt w.Rng ships w.Crates
        let portals, portalIn, rng, portalEvents = stepPortals k dt (sudden w || race) ships rng w
        let hole, holeIn, rng, holeEvents = stepHole k dt (sudden w || race) ships rng w
        let ships, gateEvents = stepGates (w.Time + dt) ships
        let raceEnd = if ships |> Array.exists (fun s -> s.Finish > 0.) then w.RaceEnd + dt else 0.
        let settled, deaths = ships |> Array.map (settle ships rng k dt) |> Array.unzip
        let ships = Array.copy settled
        let kills = deaths |> Array.toList |> List.choose id
        let mutable medalEvents = []
        let mutable hasFirstBlood = ships |> Array.exists (fun s -> s.FirstBloodMedal > 0)
        for (_, victim, ring, by, wpn) in kills do
            if by >= 0 && by <> victim then
                let s = ships.[by]
                let isRail = (wpn = Rail)
                let isRam = (wpn = Collision)
                
                // First Blood check
                let fbAwarded, newHasFB =
                    if not hasFirstBlood then
                        medalEvents <- Medal(by, "firstblood") :: medalEvents
                        1, true
                    else
                        0, hasFirstBlood
                hasFirstBlood <- newHasFB
                
                // Multi-kill tracking
                let nextMulti, dkIncrement, tkIncrement =
                    if w.Time - s.LastKillTime <= 4.0 then
                        let m = s.MultiKillCount + 1
                        let dk = if m = 2 then 1 else 0
                        let tk = if m >= 3 then 1 else 0
                        m, dk, tk
                    else
                        1, 0, 0
                
                if dkIncrement > 0 then medalEvents <- Medal(by, "doublekill") :: medalEvents
                if tkIncrement > 0 then medalEvents <- Medal(by, "triplekill") :: medalEvents
                
                // Rail kill tracking
                let rkIncrement = if isRail then 1 else 0
                if rkIncrement > 0 then medalEvents <- Medal(by, "railkill") :: medalEvents

                let rmIncrement = if isRam then 1 else 0
                if rmIncrement > 0 then medalEvents <- Medal(by, "ramkill") :: medalEvents

                let vdIncrement = if ring then 1 else 0
                if vdIncrement > 0 then medalEvents <- Medal(by, "voidkill") :: medalEvents

                let ehIncrement = if wpn = Singularity then 1 else 0
                if ehIncrement > 0 then medalEvents <- Medal(by, "holekill") :: medalEvents

                let abIncrement = if wpn = Tractor then 1 else 0
                if abIncrement > 0 then medalEvents <- Medal(by, "abductkill") :: medalEvents

                let grIncrement = if wpn = Rock then 1 else 0
                if grIncrement > 0 then medalEvents <- Medal(by, "gravekill") :: medalEvents

                let ofIncrement = if s.Streak + 1 = 3 then 1 else 0
                if ofIncrement > 0 then medalEvents <- Medal(by, "onfire") :: medalEvents
                
                ships.[by] <-
                    { s with
                        Kills = s.Kills + 1
                        Streak = s.Streak + 1
                        FirstBloodMedal = s.FirstBloodMedal + fbAwarded
                        DoubleKillMedals = s.DoubleKillMedals + dkIncrement
                        TripleKillMedals = s.TripleKillMedals + tkIncrement
                        RailKillMedals = s.RailKillMedals + rkIncrement
                        RamKillMedals = s.RamKillMedals + rmIncrement
                        OnFireMedals = s.OnFireMedals + ofIncrement
                        VoidMedals = s.VoidMedals + vdIncrement
                        HoleMedals = s.HoleMedals + ehIncrement
                        AbductMedals = s.AbductMedals + abIncrement
                        GraveMedals = s.GraveMedals + grIncrement
                        LastKillTime = w.Time
                        MultiKillCount = nextMulti }
        { Ships = ships
          Bullets = bullets
          Mines = mines
          Rocks = rocks
          Portals = portals
          PortalIn = portalIn
          Hole = hole
          HoleIn = holeIn
          RaceEnd = raceEnd
          Pads = pads
          Crates = crates
          Rng = rng
          Phase = phase ships raceEnd
          Time = w.Time + dt
          Events =
            shotEvents
            @ beamHits
            @ zapHits
            @ hits
            @ mineEvents
            @ mineBlasts
            @ medalEvents
            @ rams
            @ bumps
            @ rockEvents
            @ rockHits
            @ warps
            @ portalEvents
            @ holeEvents
            @ gateEvents
            @ picks
            @ grabs
            @ (kills |> List.map (fun (p, i, ring, _, _) -> Explode(p, i, ring)))
            @ (kills |> List.map (fun (_, i, ring, by, wpn) -> Downed(i, by, wpn, ring))) }