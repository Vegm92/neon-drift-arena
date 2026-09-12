module Arena

open System
open Vec
open Domain
open Domain.Cfg
open State

let bounds t =
    if mode = Race || t <= matchTime then 1. else max shrinkMin (1. - (t - matchTime) / shrinkTime)

let sudden (w: World) = mode <> Race && w.Time > matchTime

let outOfBoundsAt k (p: V2) =
    abs p.X > arenaHalf * k + killMargin
    || abs p.Y > arenaHalf * k + killMargin
    || abs p.X + abs p.Y > diagLimit () * k + killMargin

let private inside k margin (p: V2) =
    abs p.X < arenaHalf * k - margin && abs p.Y < arenaHalf * k - margin && abs p.X + abs p.Y < diagLimit () * k - margin

let freeSpot k (ships: Ship[]) (avoid: V2 list) rng =
    let mutable r = rng
    let mutable pick = zero
    let mutable tries = 0
    let clear (p: V2) =
        inside k 120. p
        && asteroids |> Array.forall (fun a -> len (a.Pos - p) > a.Radius + 90.)
        && ships |> Array.forall (fun s -> not s.Alive || len (s.Pos - p) > 250.)
        && avoid |> List.forall (fun q -> len (q - p) > 500.)
    while tries < 40 && (tries = 0 || not (clear pick)) do
        r <- nextRng r
        let a = float (rngIndex 360 r) * Math.PI / 180.
        r <- nextRng r
        let d = 250. + float (rngIndex 900 r)
        pick <- ofAngle a * d
        tries <- tries + 1
    pick, r

let rim k angle =
    let c, s = abs (cos angle), abs (sin angle)
    let t = min (min (arenaHalf / max c 1e-9) (arenaHalf / max s 1e-9)) (diagLimit () / (c + s))
    ofAngle angle * (t * k)