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
    let stops = w.Ships |> Array.filter (fun s -> s.Active) |> Array.map (fun s -> Sim.spawnPos s.Id)
    if stops.Length > 0 then
        let segs = max 1 (stops.Length - 1)
        let t = 1. - vw.Intro / introTime
        let u = min (float segs - 0.001) (t / 0.8 * float segs) |> max 0.
        let seg = int u
        let a, b = stops.[seg], stops.[min (seg + 1) (stops.Length - 1)]
        vw.Cam <- a + (b - a) * smooth (u - float seg)
    vw.CamH <- minCamH * 0.5

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
    let h = max hX hY |> max minCamH |> min maxCamH
    let zoomed = (maxCamH - h) / (maxCamH - minCamH)
    let center = center * zoomed
    let k = 1. - exp (-4. * dt)
    if vw.Intro > introTime * 0.2 then
        flyby vw w dt
    else
        vw.Intro <- max 0. (vw.Intro - dt)
        vw.Cam <- vw.Cam + (center - vw.Cam) * k
        vw.CamH <- vw.CamH + (h - vw.CamH) * k
    vw.Camera.position.set (vw.Cam.X, vw.CamH, vw.Cam.Y + vw.CamH * 0.3)
    vw.Camera.lookAt (vw.Cam.X, 0., vw.Cam.Y)

let aspect () =
    let w, h = window.innerWidth, window.innerHeight
    if h > 0. then w / h else 16. / 9.

let resize (vw: View) =
    let w, h = window.innerWidth, window.innerHeight
    vw.Camera.aspect <- aspect ()
    vw.Camera.updateProjectionMatrix ()
    vw.Renderer.setSize (w, h)
    vw.Composer.setSize (w, h)
