module Weapons

open Vec
open Domain
open Domain.Cfg
open State
open Combat

let spend (s: Ship) =
    let a = s.Ammo - 1
    if a <= 0 then { s with Weapon = Blaster; Ammo = 0; Charge = 0. } else { s with Ammo = a }

let private blaster (inp: Input) (s: Ship) =
    if s.Alive && inp.Fire && not race && s.Cooldown <= 0. && s.Locked <= 0. then
        let dir = ofAngle s.Angle
        let nose = s.Pos + dir * (shipRadius + 6.)
        let heat = s.Heat + heatPerShot
        let cooked = heat >= heatMax
        let b =
            { Owner = s.Id
              Pos = nose
              Vel = dir * bulletSpeed + s.Vel * 0.5
              Life = bulletLife
              Kind = 0
              Damage = bulletDamage }
        { s with
            Cooldown = fireCooldown
            Heat = (if cooked then heatMax else heat)
            Locked = (if cooked then overheatLock else 0.)
            Vel = s.Vel - dir * recoil
            Shots = s.Shots + 1 },
        [ b ],
        (Shot nose :: (if cooked then [ Cooked s.Pos ] else []))
    else
        s, [], []

let private special live dt (inp: Input) (s: Ship) =
    let press = inp.Special && not s.Held
    let s = { s with Held = inp.Special }
    if not s.Alive then
        { s with Charge = 0. }, [], [], []
    else
        let dir = ofAngle s.Angle
        let nose = s.Pos + dir * (shipRadius + 6.)
        match s.Weapon with
        | Blaster
        | Collision
        | Rock
        | Singularity -> s, [], [], []
        | Rail ->
            if inp.Special then
                { s with Charge = min railCharge (s.Charge + dt) }, [], [], (if press then [ Charging s.Pos ] else [])
            elif live && s.Charge >= railCharge then
                let far = s.Pos + dir * (4. * arenaHalf)
                { spend s with
                    Charge = 0.
                    Vel = s.Vel - dir * railRecoil
                    Shots = s.Shots + 1 },
                [],
                [],
                [ Beam(nose, far, s.Id) ]
            else
                { s with Charge = 0. }, [], [], []
        | Mines ->
            if press then
                let m =
                    { Owner = s.Id
                      Pos = s.Pos - dir * (shipRadius + 10.)
                      Vel = (if race then zero else s.Vel * 0.4)
                      Fuse = -1. }
                spend s, [], [ m ], [ MineSet m.Pos ]
            else
                s, [], [], []
        | Swarm ->
            if press then
                let b =
                    { Owner = s.Id
                      Pos = nose
                      Vel = dir * seekerSpeed + s.Vel * 0.5
                      Life = seekerLife
                      Kind = 2
                      Damage = seekerDamage }
                { spend s with
                    Vel = s.Vel - dir * (recoil * 0.6)
                    Shots = s.Shots + 1 },
                [ b ],
                [],
                [ Shot nose ]
            else
                s, [], [], []
        | Pulse ->
            if press then
                { spend s with Vel = s.Vel - dir * (pulseForce * 0.08) }, [], [], [ Wave(s.Pos, s.Angle, s.Id) ]
            else
                s, [], [], []
        | Scatter ->
            if press then
                { spend s with Vel = s.Vel - dir * recoil; Shots = s.Shots + 1 }, [], [], [ Zap(s.Pos, s.Angle, s.Id) ]
            else
                s, [], [], []
        | Tractor ->
            if inp.Special && s.Tow = NoTether then
                let c = s.Charge + dt
                if c >= railCharge then
                    { s with Charge = 0. }, [], [], [ Latch(s.Pos, s.Id) ]
                else
                    { s with Charge = c }, [], [], (if press then [ Charging s.Pos ] else [])
            else
                { s with Charge = 0. }, [], [], []

let fire live dt (inp: Input) (s: Ship) =
    if launcher s then
        if inp.Fire && s.LaunchCd <= 0. then
            let r = { Owner = s.Id; Pos = s.Pos; Vel = norm (zero - s.Pos) * rockSpeed; Radius = rockRadius; Life = rockLife }
            { s with LaunchCd = rockCooldown }, [], [], [ r ], [ Launch s.Pos ]
        else
            s, [], [], [], []
    else
    let s, shots, e1 = blaster inp s
    let s, more, mines, e2 = special live dt inp s
    s, shots @ more, mines, [], e1 @ e2