module Progress

type Task =
    | WinMatches
    | TotalKills
    | KillsInMatch
    | RaceUnder
    | FlawlessWins

type Outcome =
    { Won: bool
      Kills: int
      Deaths: int
      RaceTime: float
      Clean: bool }

let pool =
    [| WinMatches, 3, 60
       WinMatches, 5, 100
       TotalKills, 10, 60
       TotalKills, 20, 100
       KillsInMatch, 5, 80
       KillsInMatch, 8, 120
       RaceUnder, 90, 100
       FlawlessWins, 1, 80 |]

let task i = let t, _, _ = pool.[i] in t
let goal i = let _, g, _ = pool.[i] in g
let reward i = let _, _, x = pool.[i] in x

let level xp = 1 + int (sqrt (float (max 0 xp) / 50.))
let levelFloor lv = 50 * (lv - 1) * (lv - 1)
let toNext xp = levelFloor (level xp + 1) - max 0 xp

let matchXp (o: Outcome) =
    10 + (if o.Won then 5 else 0) + 2 * max 0 o.Kills

let private hash (s: string) =
    let mutable h = 7
    for c in s do
        h <- (h * 31 + int c) % 1000003
    h

let daily (date: string) =
    Array.init pool.Length id
    |> Array.sortBy (fun i -> ((hash date + i * 7919) * 104729) % 1000003, i)
    |> Array.fold (fun acc i -> if List.length acc < 3 && not (acc |> List.exists (fun j -> task j = task i)) then acc @ [ i ] else acc) []
    |> List.toArray

let advance (id: int) (progress: int) (o: Outcome) =
    let g = goal id
    match task id with
    | WinMatches -> progress + (if o.Won then 1 else 0)
    | TotalKills -> progress + max 0 o.Kills
    | KillsInMatch -> max progress o.Kills
    | RaceUnder -> if o.RaceTime > 0. && o.RaceTime <= float g then max progress 1 else progress
    | FlawlessWins -> progress + (if o.Won && o.Deaths = 0 then 1 else 0)

let complete (id: int) (progress: int) = progress >= goal id

let earned (ids: int[]) (before: int[]) (after: int[]) =
    Seq.init ids.Length id
    |> Seq.sumBy (fun k -> if complete ids.[k] after.[k] && not (complete ids.[k] before.[k]) then reward ids.[k] else 0)

let encode (date: string) (ids: int[]) (progress: int[]) =
    sprintf "%s|%s|%s" date (ids |> Array.map string |> String.concat ",") (progress |> Array.map string |> String.concat ",")

let decode (today: string) (s: string) =
    let fresh () = today, daily today, Array.create 3 0
    if System.String.IsNullOrEmpty s then fresh ()
    else
        match s.Split '|' with
        | [| d; i; p |] when d = today ->
            let nums (x: string) = x.Split ',' |> Array.map (fun n -> match System.Int32.TryParse n with | true, v -> v | _ -> -1)
            let ids = nums i
            let prog = nums p
            if ids.Length = 3 && prog.Length = 3
               && ids |> Array.forall (fun k -> k >= 0 && k < pool.Length)
               && prog |> Array.forall (fun v -> v >= 0)
            then d, ids, prog
            else fresh ()
        | _ -> fresh ()
