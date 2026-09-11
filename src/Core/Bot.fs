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

/// Breaking off is judged on the whole exchange, not on whether this single
/// touch is lethal. A hit that leaves both hulls on a sliver kills both on the
/// next contact, so the bot compares what the trade costs each side and only
/// holds the charge while it is the one winning it.
let private veer (me: Ship) (t: Ship) =
    let n = norm (t.Pos - me.Pos)
    let closing = dot (me.Vel - t.Vel) n
    let contact = (len (t.Pos - me.Pos) - 2. * shipRadius) / max 1. closing
    let costMe = ramDmg t me / max 1. (me.Hp + me.Shield)
    let costThem = ramDmg me t / max 1. (t.Hp + t.Shield)
    if closing > 0. && contact < 1. && costMe >= ramVeerRisk && costMe >= costThem then
        let side = n.X * t.Vel.Y - n.Y * t.Vel.X
        Some(atan2 n.Y n.X - (if side >= 0. then 1. else -1.) * Math.PI / 2.)
    else
        None

let private fleeHole (me: Ship) (w: World) =
    w.Hole
    |> Option.filter (fun h -> len (h.Pos - me.Pos) < 1.5 * sqrt (holeGNow () / thrustAccel))
    |> Option.map (fun h -> let d = me.Pos - h.Pos in atan2 d.Y d.X)

/// In a lull there is nothing to spend the time on but topping up, so the bar
/// for being worth a detour drops to "not already full".
let private seekPad lull (me: Ship) (w: World) =
    let need =
        if me.Hp < (if lull then hpMax else hurtBelow) && not (sudden w) then Some 1
        elif me.Boost < (if lull then boostMax else 20.) then Some 0
        else None
    need
    |> Option.bind (fun k ->
        w.Pads
        |> Array.filter (fun p -> p.Kind = k && p.RespawnIn <= 0.)
        |> Array.sortBy (fun p -> len (p.Pos - me.Pos))
        |> Array.tryHead)
    |> Option.map (fun p -> let d = p.Pos - me.Pos in atan2 d.Y d.X)

/// Idle time is still worth something: a loose crate is a weapon for the next
/// fight, so a bot with nothing to shoot goes shopping.
let private seekCrate (me: Ship) (w: World) =
    w.Crates
    |> Array.filter (fun c -> c.RespawnIn <= 0.)
    |> Array.sortBy (fun c -> len (c.Pos - me.Pos))
    |> Array.tryHead
    |> Option.map (fun c -> let d = c.Pos - me.Pos in atan2 d.Y d.X)

/// The last resort, and the reason there is no longer a branch that hands back
/// nothing: a pilot with no errand left flies a slow inward circuit instead of
/// coasting to a halt in open space like a prop.
let private patrol (me: Ship) =
    let r = if len me.Pos < 1. then ofAngle me.Angle else norm me.Pos
    atan2 r.Y r.X + Math.PI * 0.6

/// A duelling stance that never moves is both an easy target and an obvious
/// machine, so bots weave across the line of fire while they hold one. The
/// swap is driven off the clock and the slot so it stays deterministic and the
/// two sides of a fight do not weave in lockstep.
let private weave (w: World) i (me: Ship) dist =
    if dist < 320. && me.Stun <= 0. then
        (if (int (w.Time * 1.7) + i) % 2 = 0 then 1. else -1.)
    else
        0.

/// Two ships nosed at each other and closing is a trade, not a fight: both
/// hulls come out of it worse, and an opening full of them is a mutual
/// funeral. Offsetting the bearing turns the joust into a pass, and the shot
/// comes later from an angle the target is not already pointing at. Both
/// sides offset the same way around their own bearing, and those bearings
/// point opposite ways, so they slide past each other rather than converging.
/// A hull that is already well ahead on health is the one that wins a trade,
/// so it presses instead - surviving beats killing only while the exchange is
/// not already in your favour.
let private pass (me: Ship) (t: Ship) =
    let d = t.Pos - me.Pos
    let n = norm d
    if (me.Hp + me.Shield) <= (t.Hp + t.Shield) * passEdge
       && len d < passRange
       && dot (me.Vel - t.Vel) n > passSpeed
       && dot (ofAngle t.Angle) (n * -1.) > passNose then
        Some(atan2 n.Y n.X + passArc)
    else
        None

/// The rim is lethal and the arena shrinks, so wanting back inside is not a
/// combat decision - it applies whether or not there is anyone left to fight.
let private inbound (w: World) (me: Ship) =
    let k = bounds w.Time
    if abs me.Pos.X > arenaHalf * k - 220. || abs me.Pos.Y > arenaHalf * k - 220. then
        Some(atan2 -me.Pos.Y -me.Pos.X)
    else
        None

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
        | None ->
            // Nobody to chase used to mean no input at all, so a bot coasted on
            // whatever heading it last had and sailed off the edge while the
            // enemy was still respawning. A lull is for getting off the rim and
            // filling the tanks.
            let aim =
                fleeHole me w
                |> Option.orElse (inbound w me)
                |> Option.orElse (dodge me)
                |> Option.orElse (seekPad true me w)
                |> Option.orElse (seekCrate me w)
                |> Option.defaultValue (patrol me)
            let off = abs (atan2 (sin (aim - me.Angle)) (cos (aim - me.Angle)))
            { idle with Aim = Some aim; Steer = true; Thrust = off < 1.2 }
        | Some t ->
            let dist = len (t.Pos - me.Pos)
            let shot = lead me t
            let d = shot - me.Pos
            let dodging = dodge me
            let escaping = fleeHole me w |> Option.orElse (inbound w me) |> Option.orElse (veer me t)
            let seeking = seekPad false me w
            let aim =
                escaping
                |> Option.orElse dodging
                |> Option.orElse seeking
                |> Option.orElse (pass me t)
                |> Option.defaultValue (atan2 d.Y d.X)
            let off = abs (atan2 (sin (aim - me.Angle)) (cos (aim - me.Angle)))
            let toShot = atan2 d.Y d.X
            let facing = abs (atan2 (sin (toShot - me.Angle)) (cos (toShot - me.Angle))) < 0.25 && dodging.IsNone && not (rockBetween me.Pos shot)
            let hold = me.Weapon = Rail || me.Weapon = Tractor
            let charged = me.Weapon = Rail && me.Charge >= railCharge
            { idle with
                Aim = Some aim
                Steer = true
                Strafe = weave w i me dist
                Thrust = escaping.IsSome || seeking.IsSome || dodging.IsSome || dist > 320. || off > 0.6
                Boost = (escaping.IsSome && me.Boost > 0.) || (dist > 900. && me.Boost > 40.)
                Fire = facing && dist < 650.
                Special = not charged && facing && dist < 520. && me.Weapon <> Blaster && (hold || int (w.Time * 2.) % 2 = 0) }