module Rebirth

open System
open Vec
open Domain
open Domain.Cfg
open State
open Spawn
open Arena
open Combat

let private pickSpawn (ships: Ship[]) rng (s: Ship) =
    let foes = ships |> Array.filter (fun t -> t.Alive && side t <> side s)
    let safety p =
        if foes.Length = 0 then 0. else foes |> Array.map (fun t -> len (t.Pos - p)) |> Array.min
    let ranked = [| 0..3 |] |> Array.sortByDescending (fun i -> safety (spawnPos i))
    ranked.[rngIndex 2 (rng + s.Id * 7919)]

let private underdog (ships: Ship[]) (s: Ship) =
    let rivals = ships |> Array.filter (fun t -> t.Active && t.Stocks > 0 && side t <> side s)
    catchUp && rivals.Length > 0 && s.Stocks < (rivals |> Array.map (fun t -> t.Stocks) |> Array.min)

let private reborn (ships: Ship[]) rng (s: Ship) =
    let p, a =
        if race then
            let p = gates.[(s.Next + gates.Length - 1) % gates.Length]
            let d = gates.[s.Next] - p
            p, atan2 d.Y d.X
        else
            let p = spawnPos (pickSpawn ships rng s)
            p, atan2 (-p.Y) (-p.X)
    let back = { respawn s with Stocks = s.Stocks; Pos = p; Angle = a; Next = s.Next; Laps = s.Laps }
    if underdog ships s then { back with Boost = boostMax; Shield = shieldAmount } else back

let settle (ships: Ship[]) rng k dt (s: Ship) =
    if s.Alive && (s.Hp <= 0. || outOfBoundsAt k s.Pos) then
        let ring = outOfBoundsAt k s.Pos
        { s with
            Alive = false
            Stocks = (if practice || race then s.Stocks else s.Stocks - 1)
            RespawnIn = respawnDelay
            Vel = zero
            Thrusting = 0.
            Streak = 0
            Rings = s.Rings + (if ring then 1 else 0) },
        Some(s.Pos, s.Id, ring, s.LastHit, s.LastWeapon)
    elif not s.Alive && s.Active && s.Stocks > 0 && s.Finish = 0. then
        (if s.RespawnIn <= dt then reborn ships rng s else { s with RespawnIn = s.RespawnIn - dt }),
        None
    else
        s, None