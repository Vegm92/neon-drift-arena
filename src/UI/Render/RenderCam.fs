module RenderCam

open System
open Browser
open Browser.Types
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes

let private smooth x = x * x * (3. - 2. * x)

let flyby (vw: View) (w: World) dt =
    vw.Intro <- vw.Intro - dt
    let stops =
        if Sim.race && Sim.gates.Length > 0 then
            let n = min 8 Sim.gates.Length
            Array.init (n + 1) (fun i -> Sim.gates.[i * Sim.gates.Length / n % Sim.gates.Length])
        else
            w.Ships |> Array.filter (fun s -> s.Active) |> Array.map (fun s -> Sim.spawnPos s.Id)
    if stops.Length > 0 then
        let segs = max 1 (stops.Length - 1)
        let t = 1. - vw.Intro / introTime
        let u = min (float segs - 0.001) (t / 0.8 * float segs) |> max 0.
        let seg = int u
        let a, b = stops.[seg], stops.[min (seg + 1) (stops.Length - 1)]
        vw.Cam <- a + (b - a) * smooth (u - float seg)
    vw.CamH <- minCamH () * 0.5

let frameCamera (vw: View) (w: World) dt =
    let alive = w.Ships |> Array.filter (fun s -> s.Alive)
    let lo, hi =
        if alive.Length = 0 then
            v (-arenaHalf) (-arenaHalf), v arenaHalf arenaHalf
        else
            let xs = alive |> Array.map (fun s -> s.Pos.X)
            let ys = alive |> Array.map (fun s -> s.Pos.Y)
            v (Array.min xs) (Array.min ys), v (Array.max xs) (Array.max ys)
    let center = (lo + hi) * 0.5
    let pad = 220.
    let t = tan (20. * Math.PI / 180.)
    let hY = ((hi.Y - lo.Y) / 2. + pad) / t
    let hX = ((hi.X - lo.X) / 2. + pad) / (t * vw.Camera.aspect)
    // Ships still in the match. Stocks only drop in STOCK BATTLE, so this stays
    // steady across a respawn there and counts the unfinished racers in RACE;
    // in PRACTICE nothing is ever spent, so it is simply the joined slots.
    let remaining =
        w.Ships
        |> Array.filter (fun s -> s.Active && s.Stocks > 0 && s.Finish = 0.)
        |> Array.length
    let floorH = if remaining <= duelShips then minCamH () * duelCamFactor else minCamH ()
    // floorH is only a floor: hX/hY still hold every alive ship in frame.
    let h = max hX hY |> max floorH |> min (maxCamH ())
    // Below minCamH the recentre ramp is done, so sit on the ships, never past them.
    let zoomed = min 1. ((maxCamH () - h) / (maxCamH () - minCamH ()))
    let ease r = 1. - exp (-r * dt)
    let center, h, kc, kh =
        match w.Phase with
        | Over(Some i) -> w.Ships.[i].Pos, victoryCamH, ease 7., ease 1.6
        | _ -> center * zoomed, h, ease 4., ease 4.
    if vw.Intro > introTime * 0.2 then
        flyby vw w dt
    else
        vw.Intro <- max 0. (vw.Intro - dt)
        vw.Cam <- vw.Cam + (center - vw.Cam) * kc
        vw.CamH <- vw.CamH + (h - vw.CamH) * kh
    vw.Jolt <- if screenShake then max 0. (vw.Jolt - dt * 3.) else 0.
    let j = vw.Jolt * vw.Jolt * vw.CamH * 0.02
    let jx, jy = (rnd.NextDouble() - 0.5) * j, (rnd.NextDouble() - 0.5) * j
    vw.Camera.position.set (vw.Cam.X + jx, vw.CamH, vw.Cam.Y + vw.CamH * 0.3 + jy)
    vw.Camera.lookAt (vw.Cam.X + jx, 0., vw.Cam.Y + jy)

let aspect () =
    let w, h = window.innerWidth, window.innerHeight
    if h > 0. then w / h else 16. / 9.

let resize (vw: View) =
    let w, h = window.innerWidth, window.innerHeight
    vw.Camera.aspect <- aspect ()
    vw.Camera.updateProjectionMatrix ()
    vw.Renderer.setSize (w, h)
    vw.Composer.setSize (w, h)
