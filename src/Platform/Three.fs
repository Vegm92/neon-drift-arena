module Three

open Fable.Core
open Fable.Core.JsInterop

type Vector3 =
    abstract x: float with get, set
    abstract y: float with get, set
    abstract z: float with get, set
    abstract set: float * float * float -> unit
    abstract project: obj -> Vector3

type Color =
    abstract setHex: int -> unit

type Vector2 =
    abstract set: float * float -> unit

type Texture =
    abstract offset: Vector2
    abstract repeat: Vector2
    abstract colorSpace: string with get, set
    abstract clone: unit -> Texture

type Material =
    abstract opacity: float with get, set
    abstract transparent: bool with get, set
    abstract color: Color with get, set

type BufferAttribute =
    abstract array: float[]
    abstract needsUpdate: bool with get, set

type BufferGeometry =
    abstract setAttribute: string * BufferAttribute -> unit
    abstract getAttribute: string -> BufferAttribute
    abstract setDrawRange: int * int -> unit
    abstract rotateX: float -> BufferGeometry
    abstract rotateZ: float -> BufferGeometry
    abstract translate: float * float * float -> BufferGeometry

type Object3D =
    abstract position: Vector3
    abstract rotation: Vector3
    abstract scale: Vector3
    abstract visible: bool with get, set
    abstract children: Object3D[]
    abstract add: Object3D -> unit
    abstract remove: Object3D -> unit
    abstract lookAt: float * float * float -> unit

type Mesh =
    inherit Object3D
    abstract material: Material
    abstract geometry: BufferGeometry

type Camera =
    inherit Object3D
    abstract aspect: float with get, set
    abstract updateProjectionMatrix: unit -> unit

type Renderer =
    abstract domElement: Browser.Types.HTMLCanvasElement
    abstract setSize: float * float -> unit
    abstract setPixelRatio: float -> unit

type Composer =
    abstract addPass: obj -> unit
    abstract setSize: float * float -> unit
    abstract render: unit -> unit

type Shape =
    abstract moveTo: float * float -> unit
    abstract lineTo: float * float -> unit

type Lib =
    [<Emit("new $0.Scene()")>]
    abstract Scene: unit -> Object3D
    [<Emit("new $0.Group()")>]
    abstract Group: unit -> Object3D
    [<Emit("new $0.PerspectiveCamera($1,$2,$3,$4)")>]
    abstract PerspectiveCamera: float * float * float * float -> Camera
    [<Emit("new $0.WebGLRenderer({antialias:true,preserveDrawingBuffer:true})")>]
    abstract WebGLRenderer: unit -> Renderer
    [<Emit("new $0.Color($1)")>]
    abstract Color: int -> Color
    [<Emit("new $0.Vector2($1,$2)")>]
    abstract Vector2: float * float -> obj
    [<Emit("new $0.Vector3($1,$2,$3)")>]
    abstract Vector3: float * float * float -> Vector3
    [<Emit("new $0.Shape()")>]
    abstract Shape: unit -> Shape
    [<Emit("new $0.BufferGeometry()")>]
    abstract BufferGeometry: unit -> BufferGeometry
    [<Emit("new $0.Float32BufferAttribute($1,$2)")>]
    abstract Float32BufferAttribute: float[] * int -> BufferAttribute
    [<Emit("new $0.ShapeGeometry($1)")>]
    abstract ShapeGeometry: Shape -> BufferGeometry
    [<Emit("new $0.ExtrudeGeometry($1,$2)")>]
    abstract ExtrudeGeometry: Shape * obj -> BufferGeometry
    [<Emit("new $0.EdgesGeometry($1)")>]
    abstract EdgesGeometry: BufferGeometry -> BufferGeometry
    [<Emit("new $0.RingGeometry($1,$2,$3,1,$4,$5)")>]
    abstract Arc: float * float * int * float * float -> BufferGeometry
    [<Emit("new $0.RingGeometry($1,$2,$3)")>]
    abstract RingGeometry: float * float * int -> BufferGeometry
    [<Emit("new $0.CircleGeometry($1,$2)")>]
    abstract CircleGeometry: float * int -> BufferGeometry
    [<Emit("new $0.IcosahedronGeometry($1,$2)")>]
    abstract IcosahedronGeometry: float * int -> BufferGeometry
    [<Emit("new $0.BoxGeometry($1,$2,$3)")>]
    abstract BoxGeometry: float * float * float -> BufferGeometry
    [<Emit("new $0.PlaneGeometry($1,$2)")>]
    abstract PlaneGeometry: float * float -> BufferGeometry
    [<Emit("new $0.MeshBasicMaterial($1)")>]
    abstract MeshBasicMaterial: obj -> Material
    [<Emit("new $0.LineBasicMaterial($1)")>]
    abstract LineBasicMaterial: obj -> Material
    [<Emit("new $0.PointsMaterial($1)")>]
    abstract PointsMaterial: obj -> Material
    [<Emit("new $0.Mesh($1,$2)")>]
    abstract Mesh: BufferGeometry * Material -> Mesh
    [<Emit("new $0.Line($1,$2)")>]
    abstract Line: BufferGeometry * Material -> Mesh
    [<Emit("new $0.LineLoop($1,$2)")>]
    abstract LineLoop: BufferGeometry * Material -> Mesh
    [<Emit("new $0.LineSegments($1,$2)")>]
    abstract LineSegments: BufferGeometry * Material -> Mesh
    [<Emit("new $0.Points($1,$2)")>]
    abstract Points: BufferGeometry * Material -> Mesh
    [<Emit("$0.AdditiveBlending")>]
    abstract AdditiveBlending: int
    [<Emit("new $0.TextureLoader().load($1)")>]
    abstract loadTexture: string -> Texture
    [<Emit("$0.SRGBColorSpace")>]
    abstract SRGBColorSpace: string

type Bloom =
    abstract strength: float with get, set

[<ImportAll("three")>]
let three: Lib = jsNative

[<Import("EffectComposer", "three/addons/postprocessing/EffectComposer.js")>]
let private effectComposerCtor: obj = jsNative

[<Import("RenderPass", "three/addons/postprocessing/RenderPass.js")>]
let private renderPassCtor: obj = jsNative

[<Import("UnrealBloomPass", "three/addons/postprocessing/UnrealBloomPass.js")>]
let private bloomPassCtor: obj = jsNative


let effectComposer (r: Renderer) : Composer = createNew effectComposerCtor r |> unbox
let renderPass (scene: Object3D, camera: Camera) : obj = createNew renderPassCtor (scene, camera)

let bloomPass (size: obj, strength: float, radius: float, threshold: float) : Bloom =
    createNew bloomPassCtor (size, strength, radius, threshold) |> unbox
