module Impact

open Vec
open Domain
open Domain.Cfg

let private cross (a: V2) (b: V2) = a.X * b.Y - a.Y * b.X

/// The velocity a spinning hull carries at the tip of a lever arm. In 2D the
/// cross product of a scalar rate with a vector is a quarter turn and a scale.
let private spinAt (w: float) (r: V2) = v (-w * r.Y) (w * r.X)

/// Resistance to being spun, taken as a uniform disc of unit mass. Every ship
/// is the same hull, so this is a constant rather than a per-ship field.
let inertia () = 0.5 * shipRadius * shipRadius * spinInertia

let private tangential (rel: V2) (n: V2) (vn: float) =
    let tv = rel - n * vn
    let l = len tv
    if l < 1e-6 then zero, 0. else tv * (1. / l), l

/// The one contact solver the whole game routes through: asteroids, thrown
/// rocks, deployed walls and ship-on-ship rams all land here. The ship is a
/// unit-mass disc, `cp` the contact point, `n` the unit normal pointing out of
/// the other body toward the ship, and `otherInvMass` 0 for anything bolted to
/// the arena. Torque comes only from the tangential scrape at the contact, so
/// a square hit spins nothing and a glancing one spins hard. Returns the ship
/// and the normal impulse it took, which is what callers scale damage and
/// stun by.
let apply (cp: V2) (n: V2) (otherVel: V2) (otherInvMass: float) (sh: Ship) =
    let r = cp - sh.Pos
    let rel = sh.Vel + spinAt sh.Spin r - otherVel
    let vn = dot rel n
    if vn >= 0. then
        sh, 0.
    else
        let inv = inertia ()
        let rn = cross r n
        let jn = -(1. + restitution) * vn / (1. + otherInvMass + rn * rn / inv)
        let t, tl = tangential rel n vn
        let jt =
            if tl = 0. then
                0.
            else
                let rt = cross r t
                let raw = -tl / (1. + otherInvMass + rt * rt / inv)
                max (-impactFriction * jn) (min (impactFriction * jn) raw)
        let j = n * jn + t * jt
        { sh with Vel = sh.Vel + j; Spin = sh.Spin + cross r j / inv }, jn

/// The same contact seen from both hulls at once, so a ram solves one impulse
/// and applies it equal and opposite instead of inventing momentum by running
/// `apply` twice. `n` points from `a` toward `b`.
let pair (cp: V2) (n: V2) (a: Ship) (b: Ship) =
    let ra, rb = cp - a.Pos, cp - b.Pos
    let rel = (b.Vel + spinAt b.Spin rb) - (a.Vel + spinAt a.Spin ra)
    let vn = dot rel n
    if vn >= 0. then
        a, b, 0.
    else
        let inv = inertia ()
        let rna, rnb = cross ra n, cross rb n
        let jn = -(1. + restitution) * vn / (2. + (rna * rna + rnb * rnb) / inv)
        let t, tl = tangential rel n vn
        let jt =
            if tl = 0. then
                0.
            else
                let rta, rtb = cross ra t, cross rb t
                let raw = -tl / (2. + (rta * rta + rtb * rtb) / inv)
                max (-impactFriction * jn) (min (impactFriction * jn) raw)
        let j = n * jn + t * jt
        { a with Vel = a.Vel - j; Spin = a.Spin - cross ra j / inv },
        { b with Vel = b.Vel + j; Spin = b.Spin + cross rb j / inv },
        jn

/// Lifts a hull back out of the body it sank into, leaving its centre one
/// clearance away from the contact point along the normal.
let separate (cp: V2) (n: V2) (clearance: float) (sh: Ship) = { sh with Pos = cp + n * clearance }

/// A knock takes the controls away in proportion to how hard it landed, so a
/// graze costs a moment and a full-speed crash costs the whole stun.
let stun (jn: float) (sh: Ship) =
    let f = min 1. (jn / impactStunFull)
    if f < 0.05 then sh else { sh with Stun = max sh.Stun (asteroidStun * f); Thrusting = 0. }

/// Attitude thrusters bleed off a tumble over a second or so, and stop fighting
/// it once it is too small to see — the sim hash needs an exact zero, not an
/// exponential that only ever approaches one.
let damp dt (w: float) =
    let w = w * (1. - min 1. (spinDamp * dt))
    if abs w < spinRest then 0. else w
