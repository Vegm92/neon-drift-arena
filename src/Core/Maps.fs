module Maps

open System
open Vec
open Domain
open Domain.Cfg

type Layout =
    { Rocks: (V2 * float) list
      Pads: (V2 * float * int) list
      Crates: V2 list
      Track: (V2 list * float * int) option
      Hole: (V2 * float * float) option
      Size: float }

let private polar r deg =
    let a = deg * Math.PI / 180.
    v (cos a * r) (sin a * r)

let private turn (p: V2) deg =
    let a = deg * Math.PI / 180.
    v (p.X * cos a - p.Y * sin a) (p.X * sin a + p.Y * cos a)

let private quad f =
    [ for sx in [ 1.; -1. ] do
          for sy in [ 1.; -1. ] do
              yield! f sx sy ]

let private spin f = [ for q in 0..3 -> f (float q * 90.) ] |> List.concat

let private jitter i =
    let x = sin (float i * 12.9898) * 43758.5453
    x - floor x

let private core = v 0. 0., shieldAmount, 2

let private axisPads m =
    [ core
      v m 0., padRefill, 0
      v (-m) 0., padRefill, 0
      v 0. m, padRefill, 0
      v 0. (-m), padRefill, 0
      v (m / 2.) 0., padRefill, 0
      v (-m / 2.) 0., padRefill, 0
      v 0. (m / 2.), padRefill, 0
      v 0. (-m / 2.), padRefill, 0 ]

let private heals h =
    [ v h 0., healAmount, 1
      v (-h) 0., healAmount, 1
      v 0. h, healAmount, 1
      v 0. (-h), healAmount, 1 ]

let private arenas =
    [| { Rocks =
           [ for k in 0..11 do
                 if k % 3 <> 0 then
                     yield polar 430. (float k * 30.), 44. + jitter k * 10.
             yield! quad (fun sx sy ->
                 let c = arenaHalf * 0.52
                 [ v (sx * c) (sy * c), 58.
                   v (sx * (c + 230.)) (sy * (c - 180.)), 40.
                   v (sx * (c - 180.)) (sy * (c + 230.)), 40. ])
             yield! quad (fun sx sy ->
                 let e = arenaHalf * 0.86
                 [ v (sx * e) (sy * 250.), 42.
                   v (sx * 250.) (sy * e), 42. ]) ]
         Pads = axisPads (arenaHalf * 0.55) @ heals (arenaHalf * 0.874)
         Crates =
           quad (fun sx sy ->
               [ v (sx * arenaHalf * 0.35) (sy * arenaHalf * 0.35)
                 v (sx * arenaHalf * 0.75) (sy * arenaHalf * 0.75) ])
         Track = None
         Hole = None
         Size = arenaDefault }

       { Rocks =
           [ yield! spin (fun a ->
                 [ turn (v 620. 0.) a, 58.
                   turn (polar 640. 15.) a, 42.
                   turn (polar 640. -15.) a, 42.
                   turn (v 790. 0.) a, 36. ])
             yield! quad (fun sx sy ->
                 let c = arenaHalf * 0.62
                 [ v (sx * c) (sy * c), 48.
                   v (sx * (c + 210.)) (sy * (c - 260.)), 38. ]) ]
         Pads =
           core
           :: quad (fun sx sy ->
               let d = arenaHalf * 0.78 / sqrt 2.
               [ v (sx * d) (sy * d), padRefill, 0
                 v (sx * d * 0.45) (sy * d * 0.45), padRefill, 0 ])
           @ heals (arenaHalf * 0.86)
         Crates = spin (fun a -> [ turn (v (arenaHalf * 0.32) 0.) a; turn (v (arenaHalf * 0.85) 0.) a ])
         Track = None
         Hole = None
         Size = arenaDefault }

       { Rocks =
           spin (fun a ->
               [ for i in 0..4 ->
                     turn (polar (320. + float i * 160.) (26. + float i * 13.)) a, 50. - float i * 3. ])
         Pads =
           core
           :: spin (fun a -> [ turn (polar 560. -24.) a, padRefill, 0; turn (polar 900. -30.) a, padRefill, 0 ])
           @ spin (fun a -> [ turn (polar (arenaHalf * 0.86) -8.) a, healAmount, 1 ])
         Crates = spin (fun a -> [ turn (polar 700. 68.) a; turn (polar 1080. 50.) a ])
         Track = None
         Hole = None
         Size = arenaDefault }

       { Rocks =
           [ for sy in [ 1.; -1. ] do
                 for i in 0..6 do
                     let x = -900. + float i * 300.
                     if abs x > 200. then yield v x (sy * 340.), 46.
             yield! quad (fun sx sy ->
                 [ v (sx * arenaHalf * 0.82) (sy * arenaHalf * 0.62), 50.
                   v (sx * arenaHalf * 0.5) (sy * arenaHalf * 0.86), 40. ])
             for sy in [ 1.; -1. ] -> v 0. (sy * 760.), 54. ]
         Pads =
           [ core
             v 420. 0., padRefill, 0
             v (-420.) 0., padRefill, 0
             v 760. 0., padRefill, 0
             v (-760.) 0., padRefill, 0
             v 0. 560., padRefill, 0
             v 0. (-560.), padRefill, 0
             v 420. 560., padRefill, 0
             v (-420.) (-560.), padRefill, 0
             v (arenaHalf * 0.9) 0., healAmount, 1
             v (-(arenaHalf * 0.9)) 0., healAmount, 1
             v 0. (arenaHalf * 0.88), healAmount, 1
             v 0. (-(arenaHalf * 0.88)), healAmount, 1 ]
         Crates = quad (fun sx sy -> [ v (sx * 640.) (sy * 640.); v (sx * 1060.) (sy * 260.) ])
         Track = None
         Hole = None
         Size = arenaDefault }

       { Rocks =
           [ for k in 0..19 do
                 if k % 5 <> 0 then
                     yield polar 780. (float k * 18.), 52. + jitter k * 16. ]
         Pads =
           core
           :: quad (fun sx sy -> [ v (sx * 300.) (sy * 300.), padRefill, 0; v (sx * 520.) (sy * 520.), padRefill, 0 ])
           @ heals (arenaHalf * 0.9)
         Crates = spin (fun a -> [ turn (v 1080. 0.) a; turn (v 560. 0.) a ])
         Track = None
         Hole = None
         Size = arenaDefault } |]

let private grandPrix =
    [ v -900. -1000.; v -400. -1000.; v 100. -1000.; v 600. -1000.; v 900. -920.; v 1050. -700.; v 1050. -400.
      v 950. -200.; v 700. -100.; v 600. 150.; v 750. 400.; v 1000. 500.; v 1100. 750.; v 950. 950.; v 650. 1000.
      v 300. 900.; v 100. 650.; v -200. 550.; v -550. 650.; v -750. 900.; v -1050. 850.; v -1150. 550.
      v -1050. 250.; v -750. 150.; v -500. -50.; v -550. -350.; v -850. -450.; v -1100. -650.; v -1050. -900. ]

let private hairpin =
    let arc cx cy =
        [ for a in [ -90.; -45.; 0.; 45.; 90. ] -> v (cx + cos (a * Math.PI / 180.) * 250.) (cy + sin (a * Math.PI / 180.) * 250.) ]
    [ v -500. -850.; v 100. -850.; v 600. -850. ]
    @ arc 950. -600.
    @ [ v 500. -350.; v 150. -350.; v -150. -260.; v -380. -140.; v -380. 140.; v -150. 260.; v 250. 250.; v 600. 250. ]
    @ arc 950. 500.
    @ [ v 400. 750.; v -200. 750.; v -700. 750.; v -1000. 650.; v -1150. 300.; v -1180. -50.; v -1150. -400.; v -1000. -750. ]

let private figureEight =
    [ v 350. -350.; v -350. 350.; v -750. 480.; v -1050. 300.; v -1150. 0.; v -1050. -300.; v -750. -480.
      v -350. -350.; v 350. 350.; v 750. 480.; v 1050. 300.; v 1150. 0.; v 1050. -300.; v 750. -480. ]

let private boostClusters (road: V2 list) (w: float) =
    let arr = List.toArray road
    let n = arr.Length
    let h = w / 2.
    let shape =
        function
        | 2 -> [ -0.55, -0.35; 0.55, 0.35 ]
        | 3 -> [ 0.0, -0.55; -0.6, 0.3; 0.6, 0.3 ]
        | _ -> [ -0.6, -0.45; 0.6, -0.45; -0.3, 0.5; 0.3, 0.5 ]
    [ for (f, k) in [ 0.06, 3; 0.19, 2; 0.32, 4; 0.45, 2; 0.58, 3; 0.71, 2; 0.86, 4 ] do
        let i = int (f * float n) % n
        let p = arr.[i]
        let t = norm (arr.[(i + 1) % n] - arr.[(i + n - 1) % n])
        let s = v -t.Y t.X
        for (lat, lon) in shape k -> p + s * (lat * h) + t * (lon * h), padRefill, 0 ]

let tracks =
    [| { Rocks = [ v 0. -500., 50.; v 300. 300., 46.; v -200. -700., 40.; v -700. 450., 44. ]
         Pads =
           boostClusters grandPrix 280.
           @ [ v 1050. -400., healAmount, 1
               v -1150. 550., healAmount, 1 ]
         Crates = [ v 600. -1000.; v 750. 400.; v -550. 650.; v -550. -350. ]
         Track = Some(grandPrix, 280., 3)
         Hole = None
         Size = arenaDefault }

       { Rocks = [ v 400. -50., 50.; v 400. 500., 46.; v -700. -50., 44. ]
         Pads =
           boostClusters hairpin 260.
           @ [ v -1180. -50., healAmount, 1
               v 650. -350., healAmount, 1 ]
         Crates = [ v -200. -850.; v 1200. -600.; v -300. 0.; v 1200. 500. ]
         Track = Some(hairpin, 260., 3)
         Hole = None
         Size = arenaDefault }

       { Rocks = [ v 0. 450., 46.; v 0. -450., 46. ]
         Pads =
           boostClusters figureEight 300.
           @ [ v 750. 480., healAmount, 1
               v -750. -480., healAmount, 1 ]
         Crates = [ v 750. -480.; v -750. 480.; v 1050. 300.; v -1050. -300. ]
         Track = Some(figureEight, 300., 2)
         Hole = None
         Size = arenaDefault } |]

let private grow k (l: Layout) =
    { Rocks = l.Rocks |> List.map (fun (p, r) -> p * k, r)
      Pads = l.Pads |> List.map (fun (p, a, kind) -> p * k, a, kind)
      Crates = l.Crates |> List.map (fun p -> p * k)
      Track = l.Track |> Option.map (fun (r, w, e) -> List.map (fun (p: V2) -> p * k) r, w * 1.15, e)
      Hole = l.Hole |> Option.map (fun (p, c, g) -> p * k, c, g)
      Size = raceHalf }

let private custom: Layout array = [||]
let layouts =
    if custom.Length > 0 then
        custom
    else
        Array.append arenas (tracks |> Array.map (grow (raceHalf / arenaDefault)))
let isTrack i = layouts.[i].Track.IsSome
