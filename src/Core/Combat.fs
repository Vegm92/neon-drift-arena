module Combat

open Vec
open Domain
open Domain.Cfg
open State

let side (s: Ship) = if s.Team > 0 then -s.Team else s.Id

let sideOf (ships: Ship[]) i =
    if i >= 0 && i < ships.Length then side ships.[i] else i - ships.Length

let nearest (ships: Ship[]) owner (p: V2) =
    ships
    |> Array.filter (fun s -> s.Alive && side s <> side ships.[owner])
    |> Array.fold
        (fun best s ->
            match best with
            | Some (b: Ship) when len (b.Pos - p) <= len (s.Pos - p) -> best
            | _ -> Some s)
        None

let damage amt (s: Ship) =
    if s.Invuln > 0. then
        s
    elif mode = Race then
        if amt >= bulletDamage then
            { s with Stun = max s.Stun scatterStun; Spin = asteroidSpin; Thrusting = 0. }
        else
            s
    else
        let soaked = min s.Shield amt
        { s with Shield = s.Shield - soaked; Hp = s.Hp - (amt - soaked) }

let tag by wpn (s: Ship) = if s.Invuln > 0. then s else { s with LastHit = by; LastWeapon = wpn }

let hurting (s: Ship) = s.Alive && s.Hp < hurtBelow

let launcher (s: Ship) = s.Active && not s.Alive && s.Stocks <= 0