module Kinematics

open System
open Vec
open Domain
open Domain.Cfg
open State
open Track
open Spawn
open Arena
open Combat
open Impact

let private turbo () = if mutator = 2 then 1.5 else 1.
let private slick () = if mutator = 3 then 0.25 else 1.

/// Flies just like a live ship — same turn/accel/drag/strafe feel — but boost
/// never drains (a ghost has nothing left to conserve it for) and it passes
/// through everything, clamped only to the arena bounds.
let private stepGhost dt (inp: Input) (s: Ship) =
    let k = turbo ()
    let angle =
        match inp.Aim with
        | Some a when not inp.Steer -> a
        | Some a ->
            let d = atan2 (sin (a - s.Angle)) (cos (a - s.Angle))
            let lim = turnRate * k * dt
            s.Angle + max -lim (min lim d)
        | None -> s.Angle + inp.Turn * turnRate * k * dt
    let boosting = inp.Boost
    let thrusting = if boosting then 2. elif inp.Thrust || inp.Boost then 1. else 0.
    let accel =
        if boosting then boostAccel * k
        elif thrusting > 0. then thrustAccel * k
        elif inp.Reverse then -thrustAccel * reverseFactor * k
        else 0.
    let push = ofAngle angle * accel + ofAngle (angle + System.Math.PI / 2.) * (inp.Strafe * strafeAccel * k)
    let vel = (s.Vel + push * dt) * (1. - drag * slick () * dt) |> clampLen (maxSpeed * k)
    let p = s.Pos + vel * dt
    let clamp x = max -arenaHalf (min arenaHalf x)
    { s with
        Angle = angle
        LaunchAngle = angle
        Vel = vel
        Pos = v (clamp p.X) (clamp p.Y)
        Boost = boostMax
        Thrusting = thrusting
        Reversing = accel < 0.
        LaunchCd = max 0. (s.LaunchCd - dt) }

let private stepLauncher k dt (inp: Input) (s: Ship) =
    let angle =
        match inp.Aim with
        | Some a -> a
        | None -> s.LaunchAngle + inp.Turn * turnRate * dt
    { s with
        LaunchAngle = angle
        Angle = angle + Math.PI
        Vel = zero
        Pos = rim k angle
        LaunchCd = max 0. (s.LaunchCd - dt) }

let private slow (s: Ship) =
    (if hurting s then hurtFactor else 1.) * (if onTrack s.Pos then 1. else offroadFactor)

let join (inp: Input) (s: Ship) =
    if not s.Active && inp.Present then respawn s else s

let stepShip k dt (inp: Input) (s: Ship) =
    if launcher s then
        if s.Ghosting then stepGhost dt inp s else stepLauncher k dt inp s
    elif not s.Alive then
        s
    else
        let inp = if s.Stun > 0. then noInput else inp
        let k = slow s * turbo ()
        let angle =
            match inp.Aim with
            | Some a when not inp.Steer -> a
            | Some a ->
                let d = atan2 (sin (a - s.Angle)) (cos (a - s.Angle))
                let lim = turnRate * k * dt
                s.Angle + max -lim (min lim d) + s.Spin * dt
            | None -> s.Angle + (inp.Turn * turnRate * k + s.Spin) * dt
        let boosting = inp.Boost && s.Boost > 0.
        let thrusting = if boosting then 2. elif inp.Thrust || inp.Boost then 1. else 0.
        let accel =
            if boosting then boostAccel * k
            elif thrusting > 0. then thrustAccel * k
            elif inp.Reverse then -thrustAccel * reverseFactor * k
            else 0.
        let push = ofAngle angle * accel + ofAngle (angle + System.Math.PI / 2.) * (inp.Strafe * strafeAccel * k)
        let vel = (s.Vel + push * dt) * (1. - (if race then raceDrag else drag) * slick () * dt) |> clampLen (maxSpeed * k)
        let vel =
            if race then
                let f = ofAngle angle
                let fwd = dot vel f
                f * fwd + (vel - f * fwd) * max 0. (1. - raceGrip * dt)
            else vel
        { s with
            Angle = angle
            Vel = vel
            Pos = s.Pos + vel * dt
            Boost = if boosting then max 0. (s.Boost - boostDrain * dt) else s.Boost
            Thrusting = thrusting
            Reversing = accel < 0.
            Invuln = max 0. (s.Invuln - dt)
            Cooldown = max 0. (s.Cooldown - dt)
            Heat = max 0. (s.Heat - heatCool * dt)
            Locked = max 0. (s.Locked - dt)
            Stun = max 0. (s.Stun - dt)
            Spin = Impact.damp dt s.Spin }