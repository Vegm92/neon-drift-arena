module RenderTypes

open System
open Browser.Types
open Vec
open Domain
open Domain.Cfg
open Three

let colors = [| 0x00f6ff; 0xff2bd6; 0x7cff3a; 0xffb300 |]
let teamColors = [| 0; 0x3b7bff; 0xff3b5c |]

let shipColor (s: Ship) =
    if s.Team > 0 then teamColors.[s.Team] else colors.[playerColor.[s.Id]]

let trailLen = 48
let bulletPool = 96
let minePool = 24
let rockPool = 8
let portalPool = 2
let portalHex = 0xb36bff
let holeHex = 0xd48cff
let maxCamH = (arenaHalf + 100.) / tan (20. * Math.PI / 180.)
let minCamH = maxCamH * 0.75
let rnd = Random()
let sheet = 1536., 1024.
let cell = 530., 450.
let cells = [| 217., 40.; 790., 46.; 12., 452.; 502., 481.; 984., 474. |]

let spriteOf (s: Ship) =
    if s.Team > 0 then s.Team - 1 else [| 0; 1; 3; 4 |].[playerColor.[s.Id]]

let flat (g: Three.BufferGeometry) = g.rotateX (-Math.PI / 2.)

let glowMat hex opacity =
    Three.three.MeshBasicMaterial(
        box {| color = hex; transparent = true; opacity = opacity; blending = Three.three.AdditiveBlending |}
    )

let lineMat hex opacity =
    Three.three.LineBasicMaterial(
        box {| color = hex; transparent = true; opacity = opacity; blending = Three.three.AdditiveBlending |}
    )

type ShipView =
    { Shield: Mesh
      Root: Object3D
      Body: Mesh
      Flame: Mesh
      Retro: Object3D
      Coil: Mesh
      Laser: Mesh
      Tether: Mesh
      Bubble: Mesh
      Warn: Mesh
      Mark: Mesh
      Trail: Mesh
      History: ResizeArray<V2> }

type Burst =
    { Points: Mesh
      Vel: float[]
      Decay: float
      mutable Life: float }

type Shard =
    { Obj: Mesh
      Spin: float
      Span: float
      mutable Pos: V2
      mutable Vel: V2
      mutable Puff: float
      mutable Life: float }

type Flash =
    { Obj: Mesh
      Grow: float
      Span: float
      mutable Life: float }

type View =
    { Scene: Object3D
      Camera: Camera
      Renderer: Renderer
      Composer: Composer
      Bloom: Bloom
      Ships: ShipView[]
      mutable Pads: Mesh[]
      mutable Rocks: Object3D[]
      mutable Layout: int
      Bullets: Mesh[]
      Mines: Object3D[]
      Boulders: Object3D[]
      Gates: Mesh[]
      Hole: Object3D
      Horizon: Mesh
      Halo: Mesh
      Crates: Object3D[]
      Panels: HTMLElement[]
      Tags: HTMLElement[]
      Names: string[]
      Spawns: Mesh[]
      mutable Intro: float
      Border: Object3D
      Clock: HTMLElement
      Feed: HTMLElement
      Banner: HTMLElement
      Vignette: HTMLElement
      mutable Bursts: Burst list
      mutable Shards: Shard list
      mutable Flashes: Flash list
      Hp: float[]
      Shake: float[]
      Smoke: float[]
      mutable Puff: float
      mutable Spike: float
      mutable Jolt: float
      mutable Tint: float
      mutable TintHex: string
      mutable Cam: V2
      mutable CamH: float }
