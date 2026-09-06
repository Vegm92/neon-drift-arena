module Spawn

open System
open Vec
open Domain
open Domain.Cfg
open State
open Track

let spawnPos i =
    if race && road.Length > 1 then
        let dir = norm (roadAhead 0 - road.[0])
        let side = v -dir.Y dir.X
        road.[0] + dir * (80. + 90. * float (i / 2)) + side * (if i % 2 = 0 then -60. else 60.)
    else
        let a = (diagLimit () - 320.) / 2.
        match i with
        | 0 -> v (-a) (-a)
        | 1 -> v a (-a)
        | 2 -> v (-a) a
        | _ -> v a a

let private spawnAngle (p: V2) =
    if race && road.Length > 1 then
        let d = roadAhead 0 - road.[0]
        atan2 d.Y d.X
    else
        atan2 (-p.Y) (-p.X)

let freshShip i =
    let p = spawnPos i
    { Id = i
      Team = 0
      Archetype = Standard
      Pos = p
      Vel = zero
      Angle = spawnAngle p
      Hp = hpMax
      Shield = 0.
      Boost = boostStart
      Stocks = stocks
      Alive = true
      Active = true
      Thrusting = 0.
      Reversing = false
      RespawnIn = 0.
      Invuln = invulnTime
      Cooldown = 0.
      Stun = 0.
      Spin = 0.
      Heat = 0.
      Locked = 0.
      Weapon = Blaster
      Ammo = 0
      Charge = 0.
      Held = false
      Tow = NoTether
      TowLeft = 0.
      LastHit = -1
      LastWeapon = Blaster
      LaunchCd = 0.
      LaunchAngle = atan2 p.Y p.X
      WarpCd = 0.
      Next = 1
      Laps = 0
      Finish = 0.
      Streak = 0
      Shots = 0
      Hits = 0
      Grabs = 0
      Rings = 0
      Kills = 0
      FirstBloodMedal = 0
      DoubleKillMedals = 0
      TripleKillMedals = 0
      RailKillMedals = 0
      RamKillMedals = 0
      LastKillTime = -999.
      MultiKillCount = 0 }

let respawn (s: Ship) =
    { freshShip s.Id with
        Team = s.Team
        Shots = s.Shots
        Hits = s.Hits
        Grabs = s.Grabs
        Rings = s.Rings
        Kills = s.Kills
        FirstBloodMedal = s.FirstBloodMedal
        DoubleKillMedals = s.DoubleKillMedals
        TripleKillMedals = s.TripleKillMedals
        RailKillMedals = s.RailKillMedals
        RamKillMedals = s.RamKillMedals }