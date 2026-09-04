module Vec

[<Struct>]
type V2 =
    { X: float; Y: float }
    static member (+)(a: V2, b: V2) = { X = a.X + b.X; Y = a.Y + b.Y }
    static member (-)(a: V2, b: V2) = { X = a.X - b.X; Y = a.Y - b.Y }
    static member (*)(a: V2, s: float) = { X = a.X * s; Y = a.Y * s }

let v x y = { X = x; Y = y }
let zero = v 0. 0.
let dot (a: V2) (b: V2) = a.X * b.X + a.Y * b.Y
let len (a: V2) = sqrt (dot a a)

let norm (a: V2) =
    let l = len a
    if l < 1e-9 then zero else a * (1. / l)

let ofAngle t = v (cos t) (sin t)

let clampLen m (a: V2) =
    let l = len a
    if l > m then a * (m / l) else a
