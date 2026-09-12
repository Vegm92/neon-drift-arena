module RenderHud

open System
open Browser
open Browser.Types
open Fable.Core.JsInterop
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes

let icon (w: Weapon) ring =
    let path =
        if ring then "M8 32 L48 32 M34 18 L48 32 L34 46"
        else
            match w with
            | Blaster -> "M10 32 L38 32 M42 32 L54 32"
            | Rail -> "M4 32 L60 32 M46 24 L46 40"
            | Mines -> "M32 14 L32 50 M14 32 L50 32 M19 19 L45 45 M45 19 L19 45 M32 32 m-9 0 a9 9 0 1 0 18 0 a9 9 0 1 0 -18 0"
            | Swarm -> "M10 40 L40 22 M40 22 L30 22 M40 22 L40 32 M22 46 L52 28"
            | Pulse -> "M24 16 a20 20 0 0 1 0 32 M36 10 a28 28 0 0 1 0 44 M10 32 L18 32"
            | Scatter -> "M8 32 L20 20 L28 40 L38 22 L46 42 L56 30"
            | Tractor -> "M8 32 L36 32 M36 32 m-10 0 a10 10 0 1 0 20 0 a10 10 0 1 0 -20 0 M56 20 L56 44"
            | Collision -> "M32 8 L36 26 L54 22 L40 34 L52 50 L34 42 L28 58 L26 40 L8 44 L22 32 L12 16 L28 24 Z"
            | Rock -> "M20 12 L44 10 L56 28 L50 50 L26 54 L10 38 Z M26 30 L36 26 L40 36"
            | Singularity -> "M32 32 m-22 0 a22 22 0 1 0 44 0 a22 22 0 1 0 -44 0 M32 32 m-9 0 a9 9 0 1 0 18 0 a9 9 0 1 0 -18 0 M10 32 L4 32 M54 32 L60 32"
            | Barrier -> "M14 20 L50 20 M14 20 L14 44 M50 20 L50 44 M14 44 L50 44 M32 20 L32 44 M20 52 L26 58 M38 58 L44 52"
            | Sentry -> "M32 12 L48 21 L48 39 L32 48 L16 39 L16 21 Z M32 30 L58 30 M32 30 m-5 0 a5 5 0 1 0 10 0 a5 5 0 1 0 -10 0"
            | Bubble -> "M32 32 m-22 0 a22 22 0 1 0 44 0 a22 22 0 1 0 -44 0 M32 18 L32 32 L42 38 M32 4 L32 10 M32 54 L32 60"
    sprintf "<svg viewBox=\"0 0 64 64\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"%s\"/></svg>" path

let medalIcon (i: int) =
    sprintf "<i class=\"mi\" style=\"background-position:%.4f%% %.4f%%\"></i>" (float (i % 4) * 100. / 3.) (float (i / 4) * 100. / 3.)

let feedLine (vw: View) (w: World) victim by wpn ring =
    let tag i cls = sprintf "<b class=\"%s\" style=\"color:#%06x\">%s</b>" cls (shipColor w.Ships.[i]) vw.Names.[i]
    let wep i r =
        let label = if r then Strings.t.RingOut else Strings.weaponName wpn
        sprintf "<span style=\"color:#%06x\">%s<em>%s</em></span>" (shipColor w.Ships.[i]) (icon wpn r) label
    let line = document.createElement "div"
    line.innerHTML <-
        if by >= 0 && by <> victim then tag by "" + wep by ring + tag victim "dead"
        else wep victim (ring || wpn <> Singularity) + tag victim "dead"
    vw.Feed?prepend line
    if by >= 0 && by <> victim then
        let panel = vw.Panels.[by]
        panel?style?animation <- "none"
        panel?offsetWidth |> ignore
        panel?style?animation <- "score .7s ease-out"
    while vw.Feed.children.length > 4 do
        vw.Feed?lastElementChild?remove ()
    window.setTimeout ((fun () -> line.remove ()), 5000) |> ignore

let private wname (w: Weapon) =
    sprintf "<span class=\"wname\">%s</span>" (Strings.weaponName w)

let private weaponHtml (s: Ship) =
    if Sim.launcher s then
        let w = if s.Ghosting then Mines else Rock
        sprintf "<span class=\"%s\">%s</span>%s<span class=\"ammo\">%s</span>"
            (if s.LaunchCd <= 0. then "ready" else "wait")
            (icon w false)
            (wname w)
            (Strings.t.SwapMode(Strings.bindKeys Binds.Swap))
    elif State.mode = Race && s.Weapon = Blaster then ""
    else
        let n = if weaponAmmo s.Weapon > 0 then s.Ammo else 0
        icon s.Weapon false + wname s.Weapon + sprintf "<span class=\"ammo\">%s</span>" (String.replicate n "●")

let drawHud (vw: View) (w: World) dt =
    w.Ships
    |> Array.iteri (fun i s ->
        let el = vw.Panels.[i]
        if s.Alive && s.Hp < vw.Hp.[i] then
            vw.Shake.[i] <- min 1. (vw.Shake.[i] + (vw.Hp.[i] - s.Hp) / 40.)
        vw.Hp.[i] <- if s.Alive then s.Hp else hpMax
        vw.Shake.[i] <- if screenShake then max 0. (vw.Shake.[i] - dt * 3.4) else 0.
        let k = vw.Shake.[i]
        let jolt = k * 9.
        el?style?transform <-
            sprintf
                "translate(%.1fpx,%.1fpx) scale(%.3f)"
                ((rnd.NextDouble() - 0.5) * jolt)
                ((rnd.NextDouble() - 0.5) * jolt)
                (1. + k * 0.09)
        el.className <-
            sprintf
                "panel p%d%s%s%s%s"
                i
                (if i % 2 = 1 then " r" else "")
                (if s.Active then "" else " off")
                (if Sim.hurting s then " hurt" else "")
                (if s.Locked > 0. then " cooked" else "")
        el?style?color <- sprintf "#%06x" (shipColor s)
        el.querySelector(".name")?textContent <- vw.Names.[i]
        el.querySelector(".name")?style?color <- sprintf "#%06x" (shipColor s)
        el.querySelector(".hp i")?style?width <- sprintf "%.0f%%" (max 0. s.Hp / hpMax * 100.)
        el.querySelector(".shield i")?style?width <- sprintf "%.0f%%" (s.Shield / shieldAmount * 100.)
        el.querySelector(".boost i")?style?width <- sprintf "%.0f%%" (s.Boost / boostMax * 100.)
        el.querySelector(".heat i")?style?width <- sprintf "%.0f%%" (s.Heat / heatMax * 100.)
        let stocks = el.querySelector ".stocks" :?> HTMLElement
        let stocksHtml =
            if State.mode = Race then sprintf "<span>%s</span>" (Strings.t.Lap Strings.t.Places.[Sim.place w.Ships i - 1] (min (int laps) (s.Laps + 1)) (int laps))
            else
                let shipIcon = "<svg class=\"stock-icon\" viewBox=\"0 0 48 48\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"4.5\" stroke-linejoin=\"round\"><path d=\"M24 4 L41 40 L24 32 L7 40 Z\"/></svg>"
                String.concat "" [ for _ in 1 .. max 0 s.Stocks -> shipIcon ]
        if stocks?dataset?html <> stocksHtml then
            stocks?dataset?html <- stocksHtml
            stocks.innerHTML <- stocksHtml
        stocks?style?color <- sprintf "#%06x" (shipColor s)
        let wep = el.querySelector ".wep" :?> HTMLElement
        let html = weaponHtml s
        if wep?dataset?html <> html then
            wep?dataset?html <- html
            wep.innerHTML <- html
        let medals = el.querySelector ".medals" :?> HTMLElement
        let mHtml =
            let m = ResizeArray()
            if s.FirstBloodMedal > 0 then m.Add(sprintf "<span class=\"medal fb\" title=\"%s\">%s</span>" Strings.t.FirstBlood (medalIcon 0))
            if s.DoubleKillMedals > 0 then m.Add(sprintf "<span class=\"medal dk\" title=\"%s\">%s</span>" Strings.t.DoubleKill (medalIcon 1))
            if s.TripleKillMedals > 0 then m.Add(sprintf "<span class=\"medal tk\" title=\"%s\">%s</span>" Strings.t.TripleKill (medalIcon 2))
            if s.RailKillMedals > 0 then m.Add(sprintf "<span class=\"medal rk\" title=\"%s\">%s</span>" Strings.t.RailKillMedal (medalIcon 3))
            if s.RamKillMedals > 0 then m.Add(sprintf "<span class=\"medal rm\" title=\"%s\">%s</span>" Strings.t.RamKillMedal (medalIcon 4))
            if s.OnFireMedals > 0 then m.Add(sprintf "<span class=\"medal of\" title=\"%s\">%s</span>" Strings.t.OnFireMedal (medalIcon 5))
            if s.VoidMedals > 0 then m.Add(sprintf "<span class=\"medal vd\" title=\"%s\">%s</span>" Strings.t.VoidMedal (medalIcon 6))
            if s.HoleMedals > 0 then m.Add(sprintf "<span class=\"medal eh\" title=\"%s\">%s</span>" Strings.t.HoleMedal (medalIcon 7))
            if s.AbductMedals > 0 then m.Add(sprintf "<span class=\"medal ab\" title=\"%s\">%s</span>" Strings.t.AbductMedal (medalIcon 8))
            if s.GraveMedals > 0 then m.Add(sprintf "<span class=\"medal gr\" title=\"%s\">%s</span>" Strings.t.GraveMedal (medalIcon 9))
            String.concat "" m
        if medals?dataset?html <> mHtml then
            medals?dataset?html <- mHtml
            medals.innerHTML <- mHtml)

let drawTint (vw: View) dt =
    vw.KillCd <- max 0. (vw.KillCd - dt)
    vw.Tint <- if reduceFlash then 0. else max 0. (vw.Tint - dt * tintFade)
    vw.Vignette?style?opacity <- string vw.Tint
    if vw.Tint > 0. then
        vw.Vignette?style?boxShadow <- sprintf "inset 0 0 %.1fvmin 0 %s" tintEdgeVmin vw.TintHex

let drawPost (vw: View) dt =
    vw.Spike <- if reduceFlash then 0. else max 0. (vw.Spike - dt * 2.6)
    vw.Bloom.strength <- 1.3 + vw.Spike * 1.7

let drawTags (vw: View) (w: World) =
    w.Ships
    |> Array.iteri (fun i s ->
        let el = vw.Tags.[i]
        let p = (three.Vector3(s.Pos.X, 0., s.Pos.Y)).project vw.Camera
        let show =
            vw.Intro <= 0. && p.z < 1. && ((s.Alive && s.Invuln > 0.) || Sim.launcher s)
        el.hidden <- not show
        if show then
            el.textContent <- vw.Names.[i]
            let x = (p.x + 1.) / 2. * window.innerWidth
            let y = (1. - p.y) / 2. * window.innerHeight - 44.
            el?style?left <- sprintf "%.0fpx" (max 40. (min (window.innerWidth - 40.) x))
            el?style?top <- sprintf "%.0fpx" (max 40. (min (window.innerHeight - 40.) y))
            el?style?opacity <- if Sim.launcher s then "0.55" else "1"
            el?style?color <- sprintf "#%06x" (shipColor s))

let drawMarks (vw: View) (w: World) =
    vw.Marks
    |> Array.iteri (fun i m ->
        let wanted = w.Ships |> Array.exists (fun s -> s.Active && s.Alive && s.Next = i)
        m.material.opacity <- if wanted then 0.6 + 0.3 * sin (w.Time * 6.) else 0.18)

let drawSpawns (vw: View) (w: World) =
    Array.iter2
        (fun (m: Mesh) (s: Ship) ->
            m.visible <- s.Active && State.mode <> Race
            m.material.color.setHex (shipColor s))
        vw.Spawns
        w.Ships
