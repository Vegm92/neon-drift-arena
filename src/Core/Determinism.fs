module Determinism

open Vec
open Domain
open Domain.Cfg
open Sim

let steps = 600

let golden = 0xE6060A32L

let private qz (x: float) = int64 (x * 1000.)

let private fold (h: int64) (x: int64) = ((h ^^^ x) * 1099511628211L) &&& 0xFFFFFFFFL

let worldHash (w: World) =
    let mutable h = 2166136261L
    for s in w.Ships do
        for x in
            [ qz s.Pos.X; qz s.Pos.Y; qz s.Vel.X; qz s.Vel.Y; qz s.Angle; qz s.Spin
              qz s.Hp; qz s.Shield; qz s.Boost; qz s.Cooldown; qz s.Charge
              int64 s.Stocks; int64 s.Ammo; (if s.Alive then 1L else 0L) ] do
            h <- fold h x
    for x in
        [ int64 (List.length w.Bullets); int64 (List.length w.Mines); int64 (List.length w.Rocks)
          int64 (List.length w.Deploys); int64 w.Crates.Length; int64 w.Rng; qz w.Time ] do
        h <- fold h x
    h

let scripted frame id =
    { noInput with
        Present = true
        Turn = sin (float (frame + id * 37) * 0.031)
        Thrust = (frame + id) % 7 < 4
        Boost = (frame + id * 3) % 53 = 0
        Fire = (frame + id * 5) % 11 = 0
        Special = (frame + id * 11) % 97 = 0 }

let replay n w =
    Seq.fold (fun acc f -> step physicsDt (Array.init 4 (scripted f)) acc) w (seq { 0 .. n - 1 })

let goldenRun () =
    setLayout 0
    let joined = step physicsDt (Array.init 4 (fun _ -> { noInput with Present = true })) initial
    replay steps joined

let goldenHex () = sprintf "0x%08X" (worldHash (goldenRun ()))
