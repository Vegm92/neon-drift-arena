/// What a joined desktop draws between the host's `nda:state` snapshots. Drawing
/// each snapshot as it lands moved every ship in steps several frames wide at
/// the send rate, whatever the frame rate; `sample` blends the two snapshots
/// either side of a moment a fixed `Cfg.netDelayMs` behind the host instead, so
/// the mirror moves every frame.
module Mirror

open Vec
open Domain

/// One `nda:state` as it arrived: the host's clock when it was sent, its world
/// and the events the host raised since the previous one.
type Snap = { At: float; World: World; Events: Event list }

let private mix (a: float) (b: float) k = a + (b - a) * k
let private mixV (a: V2) (b: V2) k = a + (b - a) * k
let private mixAngle a b k = a + atan2 (sin (b - a)) (cos (b - a)) * k

/// Farther than this over `dt` seconds of sim time is a portal, a respawn or a
/// reset, never flight, so the ship holds and snaps rather than sliding across.
let private warped (a: V2) (b: V2) dt = len (b - a) > Cfg.shipRadius * 4. + Cfg.maxSpeed * 4. * dt

let private ship dt k (a: Ship) (b: Ship) =
    if a.Alive <> b.Alive || a.Active <> b.Active || warped a.Pos b.Pos dt then a
    else
        { a with
            Pos = mixV a.Pos b.Pos k
            Vel = mixV a.Vel b.Vel k
            Angle = mixAngle a.Angle b.Angle k
            LaunchAngle = mixAngle a.LaunchAngle b.LaunchAngle k
            Thrusting = mix a.Thrusting b.Thrusting k
            Boost = mix a.Boost b.Boost k }

/// Bullets, mines and rocks carry no id, so each one in `a` is paired with the
/// nearest of its owner's in `b` to where its velocity puts it. One without a
/// partner is gone by `b`; it flies on along its velocity until the pair moves.
let private flight dt k (pos: 'T -> V2) (vel: 'T -> V2) (same: 'T -> 'T -> bool) (b: 'T list) (x: 'T) =
    let guess = pos x + vel x * dt
    let near = b |> List.filter (same x) |> List.sortBy (fun y -> len (pos y - guess)) |> List.tryHead
    match near with
    | Some y when len (pos y - guess) < Cfg.shipRadius * 2. -> mixV (pos x) (pos y) k
    | _ -> pos x + vel x * (dt * k)

/// `a` blended `k` of the way to `b`. Everything that steps rather than flows
/// - hull, stocks, weapons, timers - stays at `a` until the pair moves on.
let lerp (a: World) (b: World) (k: float) : World =
    let dt = b.Time - a.Time
    if k <= 0. || dt <= 0. || dt > 1. || a.Ships.Length <> b.Ships.Length then a
    else
        let bullet = flight dt k (fun (x: Bullet) -> x.Pos) (fun x -> x.Vel) (fun x y -> x.Owner = y.Owner && x.Kind = y.Kind) b.Bullets
        let mine = flight dt k (fun (x: Mine) -> x.Pos) (fun x -> x.Vel) (fun x y -> x.Owner = y.Owner) b.Mines
        let rock = flight dt k (fun (x: Rock) -> x.Pos) (fun x -> x.Vel) (fun x y -> x.Owner = y.Owner) b.Rocks
        { a with
            Ships = Array.map2 (ship dt k) a.Ships b.Ships
            Bullets = a.Bullets |> List.map (fun x -> { x with Pos = bullet x })
            Mines = a.Mines |> List.map (fun x -> { x with Pos = mine x })
            Rocks = a.Rocks |> List.map (fun x -> { x with Pos = rock x })
            Hole =
                match a.Hole, b.Hole with
                | Some h, Some g -> Some { h with Pos = mixV h.Pos g.Pos k }
                | h, _ -> h
            Time = mix a.Time b.Time k
            Events = [] }

/// The world at host time `at`, from snapshots oldest first: blended between
/// the pair either side of it, held at the oldest before the buffer starts and
/// at the newest once `at` runs past it, never extrapolated.
let sample (snaps: ResizeArray<Snap>) (at: float) : World =
    let mutable i = snaps.Count - 1
    while i > 0 && snaps.[i].At > at do
        i <- i - 1
    if i = snaps.Count - 1 || snaps.[i].At > at then snaps.[i].World
    else
        let a, b = snaps.[i], snaps.[i + 1]
        lerp a.World b.World ((at - a.At) / (b.At - a.At))
