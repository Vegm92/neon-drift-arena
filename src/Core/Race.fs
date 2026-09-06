module Race

open System
open Vec
open Domain
open Domain.Cfg
open State
open Combat

let private progress (s: Ship) =
    let n = gates.Length
    float (s.Laps * n + (s.Next + n - 1) % n) - len (s.Pos - gates.[s.Next]) / (4. * arenaHalf)

let rank (ships: Ship[]) =
    ships
    |> Array.filter (fun s -> s.Active)
    |> Array.sortBy (fun s -> (if s.Finish > 0. then s.Finish else infinity), -progress s)
    |> Array.map (fun s -> s.Id)

let place (ships: Ship[]) i =
    rank ships |> Array.tryFindIndex ((=) i) |> Option.defaultValue 0 |> (+) 1

let stepGates time (ships: Ship[]) =
    let mutable place = ships |> Array.filter (fun s -> s.Finish > 0.) |> Array.length
    let events = ResizeArray()
    let ships =
        ships
        |> Array.map (fun s ->
            if not (race && s.Alive) || len (s.Pos - gates.[s.Next]) > gateRadius then
                s
            else
                let next = (s.Next + 1) % gates.Length
                let n = if next = 1 then s.Laps + 1 else s.Laps
                if float n >= laps then
                    place <- place + 1
                    events.Add(Finished(s.Id, place))
                    { s with Next = next; Laps = n; Finish = time; Alive = false; Vel = zero; Thrusting = 0. }
                else
                    { s with Next = next; Laps = n })
    ships, List.ofSeq events

let phase (ships: Ship[]) raceEnd =
    let active = ships |> Array.filter (fun s -> s.Active)
    let contenders = active |> Array.filter (fun s -> s.Stocks > 0)
    if race then
        let finished = active |> Array.filter (fun s -> s.Finish > 0.)
        if active.Length >= 2 && finished.Length > 0 && (finished.Length = active.Length || raceEnd >= raceGrace) then
            Over(Some (finished |> Array.minBy (fun s -> s.Finish)).Id)
        else
            Playing
    elif active.Length >= 2 && (contenders |> Array.distinctBy side).Length <= 1 then
        Over(contenders |> Array.tryHead |> Option.map (fun s -> s.Id))
    else
        Playing