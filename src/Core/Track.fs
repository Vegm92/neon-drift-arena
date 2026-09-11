module Track

open Vec
open State

let private gateAhead i = gates.[(i + 1) % gates.Length]

let roadAhead i = road.[(i + 1) % road.Length]

let segClosest (a: V2) (b: V2) (p: V2) =
    let ab = b - a
    let l2 = dot ab ab
    if l2 < 1e-9 then a else a + ab * (max 0. (min 1. (dot (p - a) ab / l2)))

let segDist (a: V2) (b: V2) (p: V2) = len (p - segClosest a b p)

let onTrack (p: V2) =
    not race
    || road |> Array.mapi (fun i g -> segDist g (roadAhead i) p) |> Array.min < trackWidth / 2.

let smoothSamples = 8

let smoothLoop (pts: V2[]) =
    let n = pts.Length
    [| for i in 0 .. n - 1 do
           let p0, p1, p2, p3 = pts.[(i + n - 1) % n], pts.[i], pts.[(i + 1) % n], pts.[(i + 2) % n]
           for j in 0 .. smoothSamples - 1 do
               let t = float j / float smoothSamples
               let t2, t3 = t * t, t * t * t
               yield (p0 * (-t3 + 2. * t2 - t) + p1 * (3. * t3 - 5. * t2 + 2.) + p2 * (-3. * t3 + 4. * t2 + t) + p3 * (t3 - t2)) * 0.5 |]