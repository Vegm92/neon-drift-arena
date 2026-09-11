module Bot

open System
open Vec
open Domain
open Domain.Cfg
open State
open Track
open Arena
open Combat

let private lead (me: Ship) (t: Ship) =
    if me.Weapon = Rail then
        t.Pos
    else
        let rel = t.Vel - me.Vel * 0.5
        let speed = if me.Weapon = Swarm then seekerSpeed else bulletSpeed
        Seq.fold (fun (p: V2) _ -> t.Pos + rel * (len (p - me.Pos) / speed)) t.Pos (seq { 1..3 })

let private rockBetween (from: V2) (target: V2) =
    let d = target - from
    let l = len d
    let dir = norm d
    asteroids
    |> Array.exists (fun a ->
        let rel = a.Pos - from
        let along = max 0. (min l (dot rel dir))
        len (rel - dir * along) < a.Radius)

let private dodge (me: Ship) =
    let dir = if len me.Vel > 40. then norm me.Vel else ofAngle me.Angle
    let look = 180. + len me.Vel * 0.9
    asteroids
    |> Array.choose (fun a ->
        let rel = a.Pos - me.Pos
        let along = dot rel dir
        let side = dir.X * rel.Y - dir.Y * rel.X
        if along > 0. && along < look + a.Radius && abs side < a.Radius + 2.5 * shipRadius then Some(along, side) else None)
    |> Array.sortBy fst
    |> Array.tryHead
    |> Option.map (fun (_, side) -> atan2 dir.Y dir.X - (if side >= 0. then 1. else -1.) * 0.9)

let private ramDmg (hitter: Ship) (victim: Ship) =
    let n = norm (victim.Pos - hitter.Pos)
    let face = max 0. (dot (ofAngle victim.Angle) (n * -1.))
    max 0. (dot hitter.Vel n) * ramDamageFactor * (1. - ramFaceGuard * face)

let private veer (me: Ship) (t: Ship) =
    let n = norm (t.Pos - me.Pos)
    let closing = dot (me.Vel - t.Vel) n
    let contact = (len (t.Pos - me.Pos) - 2. * shipRadius) / max 1. closing
    if closing > 0. && contact < 1. && ramDmg t me >= me.Hp + me.Shield then
        let side = n.X * t.Vel.Y - n.Y * t.Vel.X
        Some(atan2 n.Y n.X - (if side >= 0. then 1. else -1.) * Math.PI / 2.)
    else
        None

let private fleeHole (me: Ship) (w: World) =
    w.Hole
    |> Option.filter (fun h -> len (h.Pos - me.Pos) < 1.5 * sqrt (holeGNow () / thrustAccel))
    |> Option.map (fun h -> let d = me.Pos - h.Pos in atan2 d.Y d.X)

let private seekPad (me: Ship) (w: World) =
    let need = if hurting me && not (sudden w) then Some 1 elif me.Boost < 20. then Some 0 else None
    need
    |> Option.bind (fun k ->
        w.Pads
        |> Array.filter (fun p -> p.Kind = k && p.RespawnIn <= 0.)
        |> Array.sortBy (fun p -> len (p.Pos - me.Pos))
        |> Array.tryHead)
    |> Option.map (fun p -> let d = p.Pos - me.Pos in atan2 d.Y d.X)

let bot (w: World) i =
    let me = w.Ships.[i]
    let idle = { noInput with Present = true }
    if launcher me then
        match nearest w.Ships i me.Pos with
        | Some t -> { idle with Aim = Some(atan2 t.Pos.Y t.Pos.X); Fire = me.LaunchCd <= 0. }
        | None -> idle
    elif not me.Alive then
        idle
    elif race then
        let lo = (me.Next + gates.Length - 1) % gates.Length * gateEvery
        let seg = [| lo .. lo + gateEvery - 1 |] |> Array.minBy (fun i -> segDist road.[i % road.Length] (roadAhead i) me.Pos)
        let goal = if len (me.Pos - roadAhead seg) < 160. then roadAhead (seg + 1) else roadAhead seg
        let d = goal - me.Pos
        let aim = defaultArg (dodge me) (atan2 d.Y d.X)
        let off = abs (atan2 (sin (aim - me.Angle)) (cos (aim - me.Angle)))
        let ahead =
            nearest w.Ships i me.Pos
            |> Option.filter (fun t ->
                let r = t.Pos - me.Pos
                len r < 520. && abs (atan2 (sin (atan2 r.Y r.X - me.Angle)) (cos (atan2 r.Y r.X - me.Angle))) < 0.25)
        { idle with
            Aim = Some aim
            Steer = true
            Thrust = off < 1.2
            Boost = off < 0.15 && me.Boost > 30.
            Special = ahead.IsSome && me.Weapon <> Blaster && int (w.Time * 2.) % 2 = 0 }
    else
        match nearest w.Ships i me.Pos with
        | None -> idle
        | Some t ->
            let dist = len (t.Pos - me.Pos)
            let k = bounds w.Time
            let shot = lead me t
            let d = shot - me.Pos
            let dodging = dodge me
            let escaping = fleeHole me w |> Option.orElse (veer me t)
            let seeking = seekPad me w
            let aim =
                if abs me.Pos.X > arenaHalf * k - 220. || abs me.Pos.Y > arenaHalf * k - 220. then
                    atan2 -me.Pos.Y -me.Pos.X
                else
                    escaping |> Option.orElse dodging |> Option.orElse seeking |> Option.defaultValue (atan2 d.Y d.X)
            let off = abs (atan2 (sin (aim - me.Angle)) (cos (aim - me.Angle)))
            let toShot = atan2 d.Y d.X
            let facing = abs (atan2 (sin (toShot - me.Angle)) (cos (toShot - me.Angle))) < 0.25 && dodging.IsNone && not (rockBetween me.Pos shot)
            let hold = me.Weapon = Rail || me.Weapon = Tractor
            let charged = me.Weapon = Rail && me.Charge >= railCharge
            { idle with
                Aim = Some aim
                Steer = true
                Thrust = escaping.IsSome || seeking.IsSome || dodging.IsSome || dist > 320. || off > 0.6
                Boost = (escaping.IsSome && me.Boost > 0.) || (dist > 900. && me.Boost > 40.)
                Fire = facing && dist < 650.
                Special = not charged && facing && dist < 520. && me.Weapon <> Blaster && (hold || int (w.Time * 2.) % 2 = 0) }