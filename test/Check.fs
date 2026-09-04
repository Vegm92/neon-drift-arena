module Check

open Vec
open Domain
open Domain.Cfg
open Sim

let dt = physicsDt
let present = { noInput with Present = true }
let all i = Array.init 4 (fun _ -> i)
let run n (inputs: Input[]) w = Seq.fold (fun w _ -> step dt inputs w) w (seq { 1..n })

let place i p a (w: World) =
    { w with
        Ships =
            w.Ships
            |> Array.map (fun s -> if s.Id = i then { s with Pos = p; Angle = a; Vel = zero; Invuln = 0. } else s) }

let edit i f (w: World) = { w with Ships = w.Ships |> Array.map (fun s -> if s.Id = i then f s else s) }

let damage' amt (s: Ship) =
    let soaked = min s.Shield amt
    { s with Shield = s.Shield - soaked; Hp = s.Hp - (amt - soaked) }

let check name cond =
    if not cond then failwithf "FAIL: %s" name else printfn "ok  %s" name

[<EntryPoint>]
let main _ =
    let traverse = 2. * arenaHalf / maxSpeed
    check "end-to-end at max speed takes 8-10s" (traverse >= 8. && traverse <= 10.)

    let w0 = step dt (all present) initial
    check "all four join" (w0.Ships |> Array.forall (fun s -> s.Active && s.Alive))
    let aimed = w0 |> place 0 (spawnPos 0) 0. |> step dt (all { present with Aim = Some 3.; Steer = true })
    check "aim turns toward the stick at turn rate" (abs (aimed.Ships.[0].Angle - turnRate * dt) < 1e-6)
    let snapped = w0 |> place 0 (spawnPos 0) 0. |> step dt (all { present with Aim = Some 3. })
    check "gamepad aim snaps" (snapped.Ships.[0].Angle = 3.)

    let w1 = step dt (all { present with Fire = true }) w0
    let s0 = w1.Ships.[0]
    check "recoil pushes shooter backwards" (dot s0.Vel (ofAngle s0.Angle) < 0.)
    check "one bullet per ship" (w1.Bullets.Length = 4)

    let w2 = w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0.
    let shooter = Array.init 4 (fun i -> if i = 0 then { present with Fire = true } else present)
    let w3 = run 60 shooter w2
    check "bullet damages target" (w3.Ships.[1].Hp < hpMax)
    check "hit knocks target along bullet path" (w3.Ships.[1].Vel.X > 0.)

    let w4 = w0 |> edit 0 (fun s -> { s with Pos = zero }) |> step dt (all present)
    check "core grants a shield" (w4.Ships.[0].Shield = shieldAmount)
    check "shield is a fifth of a hull" (shieldAmount = hpMax * 0.2)
    check "core respawns in 30s" (w4.Pads.[0].RespawnIn > padRespawn)
    let w4b = w4 |> step dt (all present)
    check "core ignores an already shielded ship" (w4b.Ships.[0].Shield = shieldAmount)
    let shielded = w4 |> edit 0 (fun s -> { s with Invuln = 0. })
    let soaked = shielded |> edit 0 (damage' 12.) 
    check "shield soaks damage before the hull" (soaked.Ships.[0].Shield = shieldAmount - 12. && soaked.Ships.[0].Hp = hpMax)
    let through = shielded |> edit 0 (damage' (shieldAmount + 15.))
    check "damage past the shield hits the hull" (through.Ships.[0].Shield = 0. && through.Ships.[0].Hp = hpMax - 15.)

    let w5 = w0 |> place 0 (v (arenaHalf + killMargin + 1.) 0.) 0. |> step dt (all present)
    check "out of bounds kills and costs a stock" (not w5.Ships.[0].Alive && w5.Ships.[0].Stocks = stocks - 1)
    check "explosion event emitted" (w5.Events |> List.exists (function Explode _ -> true | _ -> false))
    let w6 = run (int (respawnDelay / dt) + 2) (all present) w5
    check "respawns at own corner" (w6.Ships.[0].Alive && w6.Ships.[0].Pos = spawnPos 0)

    let w7 =
        w0 |> place 0 zero 0. |> place 1 (v 30. 0.) 0. |> edit 1 (fun s -> { s with Vel = v (-150.) 0. })
        |> step dt (all present)
    check "ram exchanges momentum" (w7.Ships.[0].Vel.X < -50. && w7.Ships.[1].Vel.X > -50.)

    let two = Array.init 4 (fun i -> if i < 2 then present else noInput)
    let w8 = step dt two initial |> edit 1 (fun s -> { s with Hp = 0.; Stocks = 1 }) |> step dt two
    check "last ship standing wins" (w8.Phase = Over(Some 0))
    let w9 = step dt (Array.init 4 (fun i -> if i = 0 then { present with Start = true } else noInput)) w8
    check "rematch resets stocks" (w9.Phase = Playing && w9.Ships.[1].Stocks = stocks)

    let three = Array.init 4 (fun i -> if i < 3 then present else noInput)
    let teamed = initial |> withTeams [| 1; 1; 2; 0 |] |> step dt three
    let w11 = teamed |> edit 2 (fun s -> { s with Hp = 0.; Stocks = 1 }) |> step dt three
    check "team wins when the other side is out" (match w11.Phase with Over(Some i) -> w11.Ships.[i].Team = 1 | _ -> false)
    let w12 = teamed |> edit 0 (fun s -> { s with Hp = 0.; Stocks = 1 }) |> step dt three
    check "match continues while a teammate lives" (w12.Phase = Playing)
    let w13 =
        teamed |> place 0 zero 0. |> place 1 (v 30. 0.) 0. |> edit 1 (fun s -> { s with Vel = v (-150.) 0. })
        |> step dt three
    check "teammates ram without damage" (w13.Ships.[0].Hp = hpMax && w13.Ships.[1].Hp = hpMax)
    check "teammates keep their team on respawn" (w13.Ships.[0].Team = 1 && (reset w13).Ships.[1].Team = 1)

    let clear (p: V2) margin = asteroids |> Array.forall (fun a -> len (a.Pos - p) > a.Radius + margin)
    let ray (dir: V2) = [ 180. .. 40. .. 900. ] |> List.forall (fun t -> clear (dir * t) shipRadius)
    for i in 0 .. layouts.Length - 1 do
        setLayout i
        let tag name = sprintf "arena %d: %s" i name
        check (tag "pads sit clear of rock") (initial.Pads |> Array.forall (fun p -> clear p.Pos (padRadius + shipRadius)))
        check (tag "spawns are clear") ([ 0..3 ] |> List.forall (fun k -> clear (spawnPos k) (3. * shipRadius)))
        check (tag "crate spots are clear") (cratePositions |> Array.forall (fun p -> clear p (crateRadius + shipRadius)))
        check (tag "rocks do not overlap") (
            asteroids
            |> Array.forall (fun a ->
                asteroids |> Array.forall (fun b -> System.Object.ReferenceEquals(a, b) || len (a.Pos - b.Pos) > a.Radius + b.Radius)))
        check (tag "every rock is inside the arena") (
            asteroids
            |> Array.forall (fun a ->
                abs a.Pos.X + a.Radius < arenaHalf
                && abs a.Pos.Y + a.Radius < arenaHalf
                && abs a.Pos.X + abs a.Pos.Y + a.Radius < diagLimit))
        check (tag "the core has an open approach") (
            [ 0. .. 45. .. 315. ]
            |> List.exists (fun d ->
                let a = d * System.Math.PI / 180.
                ray (v (cos a) (sin a))))
        check (tag "one shield core") ((initial.Pads |> Array.filter (fun p -> p.Kind = 2)).Length = 1)
        check (tag "four heal pads") ((initial.Pads |> Array.filter (fun p -> p.Kind = 1)).Length = 4)
        check (tag "eight crate spots") (cratePositions.Length = 8)
    setLayout 0

    let strafer = Array.init 4 (fun i -> if i = 0 then { present with Strafe = 1. } else present)
    let w10 = w0 |> place 0 zero 0. |> run 30 strafer
    check "E strafes to the ship's right" (w10.Ships.[0].Vel.Y > 1. && abs w10.Ships.[0].Vel.X < 1e-6 && w10.Ships.[0].Angle = 0.)

    let reverser = Array.init 4 (fun i -> if i = 0 then { present with Reverse = true } else present)
    let thruster = Array.init 4 (fun i -> if i = 0 then { present with Thrust = true } else present)
    let rev = (w0 |> place 0 zero 0. |> run 30 reverser).Ships.[0]
    let back = rev.Vel.X
    check "reversing flag drives the retro flames" rev.Reversing
    let fwd = (w0 |> place 0 zero 0. |> run 30 thruster).Ships.[0].Vel.X
    check "S reverses at a quarter of thrust" (back < 0. && abs (abs back / fwd - reverseFactor) < 0.02)

    let rock = asteroids.[0]
    let w11 =
        w0 |> place 0 (rock.Pos + v (-(rock.Radius + shipRadius + 2.)) 0.) 0.
        |> edit 0 (fun s -> { s with Vel = v 200. 0. }) |> run 3 (all present)
    let s11 = w11.Ships.[0]
    check "asteroid bounces ship back" (s11.Vel.X < 0.)
    check "asteroid stuns and spins" (s11.Stun > 0. && s11.Spin <> 0.)
    let w12 = run 10 (Array.init 4 (fun i -> if i = 0 then { present with Turn = 1.; Thrust = true } else present)) w11
    check "stunned ship ignores input" (w12.Ships.[0].Thrusting = 0.)
    let w13 = run (int (asteroidStun / dt) + 2) (all present) w12
    check "stun wears off" (w13.Ships.[0].Stun = 0. && w13.Ships.[0].Spin = 0.)

    let w14 =
        w0 |> place 0 (rock.Pos + v (-(rock.Radius + 60.)) 0.) 0.
        |> run 30 (Array.init 4 (fun i -> if i = 0 then { present with Fire = true } else present))
    check "asteroids block bullets" (w14.Bullets |> List.forall (fun b -> b.Pos.X < rock.Pos.X))

    let firing = Array.init 4 (fun i -> if i = 0 then { present with Fire = true } else present)
    let using = Array.init 4 (fun i -> if i = 0 then { present with Special = true } else present)

    let healPad = initial.Pads |> Array.find (fun p -> p.Kind = 1)
    check "four heal pads" ((initial.Pads |> Array.filter (fun p -> p.Kind = 1)).Length = 4)
    let wh = w0 |> place 0 healPad.Pos 0. |> edit 0 (fun s -> { s with Hp = 30. }) |> step dt (all present)
    check "heal pad restores hp" (wh.Ships.[0].Hp = 30. + healAmount)
    check "heal pad respawns in 30s" (wh.Pads |> Array.exists (fun p -> p.Kind = 1 && p.RespawnIn > padRespawn))
    let whole = w0 |> place 0 healPad.Pos 0. |> step dt (all present)
    check "heal pad ignores a healthy ship" (whole.Pads |> Array.forall (fun p -> p.Kind = 0 || p.RespawnIn = 0.))

    let sick = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Hp = 10. }) |> run 30 thruster
    let well = w0 |> place 0 zero 0. |> run 30 thruster
    check "damaged ships fly a quarter slower" (abs (sick.Ships.[0].Vel.X / well.Ships.[0].Vel.X - hurtFactor) < 0.02)
    check "hurting reports the smoke threshold" (hurting sick.Ships.[0] && not (hurting well.Ships.[0]))

    let cook = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Heat = heatMax - 1. }) |> step dt firing
    check "overheat locks the blaster" (cook.Ships.[0].Locked > 0.)
    check "overheat emits a cue" (cook.Events |> List.exists (function Cooked _ -> true | _ -> false))
    check "locked blaster does not fire" ((run 5 firing cook).Bullets.Length = cook.Bullets.Length)

    check "crate spots are clear" (cratePositions |> Array.forall (fun p -> clear p (crateRadius + shipRadius)))
    check "four crates at all times" (initial.Crates.Length = 4)
    check "crates start apart" ((initial.Crates |> Array.distinctBy (fun c -> c.Pos)).Length = 4)
    let grab = w0 |> place 0 initial.Crates.[0].Pos 0. |> step dt (all present)
    check "crate grants a loaded weapon" (grab.Ships.[0].Weapon <> Blaster && grab.Ships.[0].Ammo > 0)
    check "crate respawns in 10s on a free spot"
        (grab.Crates.[0].RespawnIn > 0.
         && cratePositions |> Array.contains grab.Crates.[0].Pos
         && (grab.Crates |> Array.distinctBy (fun c -> c.Pos)).Length = 4)

    let railed =
        w0 |> place 0 zero 0. |> place 1 (v 1000. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Rail; Ammo = 2 })
        |> run (int (railCharge / dt) + 2) using
    check "rail is a one hit kill" (not railed.Ships.[1].Alive && railed.Ships.[1].Stocks = stocks - 1)
    check "rail spends a charge" (railed.Ships.[0].Ammo = 1)
    check "rail kicks the shooter back" (railed.Ships.[0].Vel.X < -railRecoil * 0.5)
    let early =
        w0 |> place 0 zero 0. |> place 1 (v 1000. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Rail; Ammo = 2 })
        |> run 30 using |> run 2 (all present)
    check "releasing early drops the charge" (early.Ships.[0].Charge = 0. && early.Ships.[1].Hp = hpMax)

    let mined =
        w0 |> place 0 zero 0. |> place 1 (v 400. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Mines; Ammo = 4 })
        |> step dt using
    check "mine drops dormant behind the ship" (mined.Mines.Length = 1 && mined.Mines.Head.Fuse < 0.)
    let idle = run 10 (all present) mined
    check "mine stays dormant out of range" (idle.Mines |> List.forall (fun m -> m.Fuse < 0.))
    let lured = idle |> place 1 (v 60. 0.) 0. |> step dt (all present)
    check "mine arms when an enemy closes in" (lured.Mines |> List.forall (fun m -> m.Fuse > 0.))
    let boom = run (int (mineFuse / dt) + 2) (all present) lured
    check "mine blows 1.5s after arming" (boom.Mines.IsEmpty && boom.Ships.[1].Hp < hpMax)

    let swarmed =
        w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Weapon = Swarm; Ammo = 3 })
        |> run 30 using
    check "swarm fires one seeker per press" (swarmed.Bullets.Length = 1 && swarmed.Ships.[0].Ammo = 2)
    let both =
        w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Weapon = Swarm; Ammo = 3 })
        |> step dt (Array.init 4 (fun i -> if i = 0 then { present with Fire = true; Special = true } else present))
    check "blaster and special fire together" (both.Bullets.Length = 2 && both.Ships.[0].Ammo = 2)
    check "seekers run at seeker speed" (swarmed.Bullets |> List.forall (fun b -> abs (len b.Vel - seekerSpeed) < 1.))

    let pulsed =
        w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0. |> place 2 (v (-200.) 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Pulse; Ammo = 3 })
        |> step dt using
    check "pulse shoves the ship in front" (pulsed.Ships.[1].Vel.X > 100.)
    check "pulse spares the ship behind" (abs pulsed.Ships.[2].Vel.X < 1.)
    check "pulse deals no damage" (pulsed.Ships.[1].Hp = hpMax)
    check "pulse kicks the user back" (pulsed.Ships.[0].Vel.X < 0.)

    let zapped =
        w0 |> place 0 zero 0. |> place 1 (v 100. 0.) 0. |> place 2 (v (-100.) 0.) 0. |> place 3 (v 600. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Scatter; Ammo = 2 })
        |> step dt using
    check "scatter stuns the ship in front for a second" (zapped.Ships.[1].Stun >= scatterStun - dt)
    check "scatter hurts a little" (zapped.Ships.[1].Hp = hpMax - scatterDamage)
    check "scatter spares ships behind or far" (zapped.Ships.[2].Stun = 0. && zapped.Ships.[3].Stun = 0.)
    check "scatter has two shots" (zapped.Ships.[0].Ammo = 1 && zapped.Ships.[0].Weapon = Scatter)

    let towed =
        w0 |> place 0 zero 0. |> place 1 (v 300. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2 })
        |> run 30 using
    check "tractor latches the nearest enemy" (towed.Ships.[0].Tow = TowShip 1 && towed.Ships.[0].Ammo = 1)
    check "tractor pulls the enemy toward us" (towed.Ships.[1].Vel.X < -50. && abs towed.Ships.[0].Vel.X < 1e-6)
    let letGo = run (int (tractorTime / dt) + 2) (all present) towed
    check "tractor lets go after its time" (letGo.Ships.[0].Tow = NoTether)
    let hooked =
        w0 |> place 0 (rock.Pos + v (-(rock.Radius + 300.)) 0.) 0. |> place 1 (v 1200. 1200.) 0.
        |> place 2 (v (-1200.) 1200.) 0. |> place 3 (v 1200. (-1200.)) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2 })
        |> run 30 using
    check "tractor pulls us to a rock when no ship is near"
        ((match hooked.Ships.[0].Tow with TowRock _ -> true | _ -> false) && hooked.Ships.[0].Vel.X > 50.)
    let ringed = w0 |> place 0 (v (arenaHalf + killMargin + 1.) 0.) 0. |> step dt (all present)
    check "ring outs are flagged" (ringed.Events |> List.exists (function Explode(_, _, r) -> r | _ -> false))
    check "ring outs are counted" (ringed.Ships.[0].Rings = 1)
    let shotDown =
        w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0.
        |> edit 1 (fun s -> { s with Hp = 1. }) |> run 60 firing
    check "kills are credited to the shooter" (shotDown.Ships.[0].Kills = 1 && shotDown.Ships.[0].Streak = 1)
    check "hits are counted for accuracy" (shotDown.Ships.[0].Hits > 0 && shotDown.Ships.[0].Shots > 0)

    0
