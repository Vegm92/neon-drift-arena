module Weapons

open Vec
open Domain
open Domain.Cfg
open State
open Combat

type TriggerType =
    | ContinuousPrimary
    | InstantSpecialPress
    | ChargeAndRelease of getThreshold: (unit -> float)
    | ChargeAndFireOnFull of getThreshold: (unit -> float)

type CostType =
    | Free
    | ConsumesAmmo of amount: int
    | GeneratesHeat of getPerShot: (unit -> float) * getMaxHeat: (unit -> float) * getLockDuration: (unit -> float)

type PayloadType =
    | BulletPayload of getSpeed: (unit -> float) * getLife: (unit -> float) * getDamage: (unit -> float) * kind: int
    | MinePayload of getOffset: (unit -> float) * getSpeedFactor: (unit -> float) * getFuse: (unit -> float)
    | BeamPayload of getRangeMultiplier: (unit -> float)
    | WavePayload of getForce: (unit -> float)
    | ZapPayload
    | LatchPayload

type WeaponDef =
    { Name: string
      Trigger: TriggerType
      Payload: PayloadType
      Recoil: unit -> float
      Cost: CostType
      Cooldown: unit -> float }

let spendAmt (s: Ship) amt =
    let a = s.Ammo - amt
    if a <= 0 then { s with Weapon = Blaster; Ammo = 0; Charge = 0. } else { s with Ammo = a }

let spend (s: Ship) = spendAmt s 1

// Modular registry
let weaponRegistry =
    dict [
        Blaster, { Name = "Blaster"
                   Trigger = ContinuousPrimary
                   Payload = BulletPayload((fun () -> bulletSpeed), (fun () -> bulletLife), (fun () -> bulletDamage), 0)
                   Recoil = (fun () -> recoil)
                   Cost = GeneratesHeat((fun () -> heatPerShot), (fun () -> heatMax), (fun () -> overheatLock))
                   Cooldown = (fun () -> fireCooldown) }
        Rail, { Name = "Rail"
                Trigger = ChargeAndRelease(fun () -> railCharge)
                Payload = BeamPayload(fun () -> 4.0)
                Recoil = (fun () -> railRecoil)
                Cost = ConsumesAmmo 1
                Cooldown = (fun () -> 0.0) }
        Mines, { Name = "Mines"
                 Trigger = InstantSpecialPress
                 Payload = MinePayload((fun () -> -(shipRadius + 10.0)), (fun () -> 0.4), (fun () -> -1.0))
                 Recoil = (fun () -> 0.0)
                 Cost = ConsumesAmmo 1
                 Cooldown = (fun () -> 0.0) }
        Swarm, { Name = "Swarm"
                 Trigger = InstantSpecialPress
                 Payload = BulletPayload((fun () -> seekerSpeed), (fun () -> seekerLife), (fun () -> seekerDamage), 2)
                 Recoil = (fun () -> recoil * 0.6)
                 Cost = ConsumesAmmo 1
                 Cooldown = (fun () -> 0.0) }
        Pulse, { Name = "Pulse"
                 Trigger = InstantSpecialPress
                 Payload = WavePayload(fun () -> pulseForce)
                 Recoil = (fun () -> pulseForce * 0.08)
                 Cost = ConsumesAmmo 1
                 Cooldown = (fun () -> 0.0) }
        Scatter, { Name = "Scatter"
                   Trigger = InstantSpecialPress
                   Payload = ZapPayload
                   Recoil = (fun () -> recoil)
                   Cost = ConsumesAmmo 1
                   Cooldown = (fun () -> 0.0) }
        Tractor, { Name = "Tractor"
                   Trigger = ChargeAndFireOnFull(fun () -> railCharge)
                   Payload = LatchPayload
                   Recoil = (fun () -> 0.0)
                   Cost = Free
                   Cooldown = (fun () -> 0.0) }
    ]

let executeWeapon live dt (inp: Input) (s0: Ship) (def: WeaponDef) =
    let isPrimary = match def.Trigger with ContinuousPrimary -> true | _ -> false
    let press = (not isPrimary) && inp.Special && not s0.Held
    let s = if isPrimary then s0 else { s0 with Held = inp.Special }
    
    if not s.Alive then
        { s with Charge = 0. }, [], [], []
    else
        let dir = ofAngle s.Angle
        let nose = s.Pos + dir * (shipRadius + 6.)
        
        let triggered, nextCharge, extraEvents =
            match def.Trigger with
            | ContinuousPrimary ->
                let canFire = inp.Fire && not race && s.Cooldown <= 0. && s.Locked <= 0.
                canFire, s.Charge, []
            | InstantSpecialPress ->
                press, s.Charge, []
            | ChargeAndRelease getThreshold ->
                let threshold = getThreshold()
                if inp.Special then
                    false, min threshold (s.Charge + dt), (if press then [ Charging s.Pos ] else [])
                elif live && s.Charge >= threshold then
                    true, 0., []
                else
                    false, 0., []
            | ChargeAndFireOnFull getThreshold ->
                let threshold = getThreshold()
                if inp.Special && s.Tow = NoTether then
                    let c = s.Charge + dt
                    if c >= threshold then
                        true, 0., [ Latch(s.Pos, s.Id) ]
                    else
                        false, c, (if press then [ Charging s.Pos ] else [])
                else
                    false, 0., []

        let s' = { s with Charge = nextCharge }
        if not triggered then
            s', [], [], extraEvents
        else
            let recForce = def.Recoil()
            let s'' = { s' with Vel = s'.Vel - dir * recForce; Shots = s'.Shots + 1 }
            
            let s''' =
                match def.Cost with
                | Free -> s''
                | ConsumesAmmo amt -> spendAmt s'' amt
                | GeneratesHeat(getPerShot, getMaxHeat, getLockDuration) ->
                    let maxHeat = getMaxHeat()
                    let heat = s''.Heat + getPerShot()
                    let cooked = heat >= maxHeat
                    { s'' with
                        Cooldown = def.Cooldown()
                        Heat = (if cooked then maxHeat else heat)
                        Locked = (if cooked then getLockDuration() else 0.) }

            let bullets, mines, events =
                match def.Payload with
                | BulletPayload(getSpeed, getLife, getDamage, kind) ->
                    let b =
                        { Owner = s.Id
                          Pos = nose
                          Vel = dir * getSpeed() + s'''.Vel * 0.5
                          Life = getLife()
                          Kind = kind
                          Damage = getDamage() }
                    [ b ], [], [ Shot nose ]
                | MinePayload(getOffset, getSpeedFactor, getFuse) ->
                    let m =
                        { Owner = s.Id
                          Pos = s'''.Pos + dir * getOffset()
                          Vel = (if race then zero else s'''.Vel * getSpeedFactor())
                          Fuse = getFuse() }
                    [], [ m ], [ MineSet m.Pos ]
                | BeamPayload(getRangeMultiplier) ->
                    let far = s'''.Pos + dir * (getRangeMultiplier() * arenaHalf)
                    [], [], [ Beam(nose, far, s.Id) ]
                | WavePayload(getForce) ->
                    [], [], [ Wave(s'''.Pos, s'''.Angle, s.Id) ]
                | ZapPayload ->
                    [], [], [ Zap(s'''.Pos, s'''.Angle, s.Id) ]
                | LatchPayload ->
                    [], [], []

            let finalEvents =
                match def.Cost with
                | GeneratesHeat(_, getMaxHeat, _) when s'''.Heat >= getMaxHeat() ->
                    extraEvents @ events @ [ Cooked s.Pos ]
                | _ ->
                    extraEvents @ events

            s''', bullets, mines, finalEvents

let fire live dt (inp: Input) (s: Ship) =
    if launcher s then
        if inp.Fire && s.LaunchCd <= 0. then
            let r = { Owner = s.Id; Pos = s.Pos; Vel = norm (zero - s.Pos) * rockSpeed; Radius = rockRadius; Life = rockLife }
            { s with LaunchCd = rockCooldown }, [], [], [ r ], [ Launch s.Pos ]
        else
            s, [], [], [], []
    else
        // 1. Process Blaster
        let s1, b1, m1, e1 = executeWeapon live dt inp s weaponRegistry.[Blaster]
        
        // 2. Process Special Weapon
        let s2, b2, m2, e2 =
            match s1.Weapon with
            | Blaster
            | Collision
            | Rock
            | Singularity -> { s1 with Held = inp.Special; Charge = 0. }, [], [], []
            | wpn -> executeWeapon live dt inp s1 weaponRegistry.[wpn]
        
        s2, b1 @ b2, m1 @ m2, [], e1 @ e2
