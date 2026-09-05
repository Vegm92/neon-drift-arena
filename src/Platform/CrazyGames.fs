module CrazyGames

open Fable.Core

type CGUser = { username: string; profilePictureUrl: string }

[<Import("init", "./CrazyGames.js")>]
let init (onMute: bool -> unit) : JS.Promise<unit> = jsNative

[<Import("gameplayStart", "./CrazyGames.js")>]
let gameplayStart () : unit = jsNative

[<Import("gameplayStop", "./CrazyGames.js")>]
let gameplayStop () : unit = jsNative

[<Import("getInviteRoom", "./CrazyGames.js")>]
let getInviteRoom () : string = jsNative

[<Import("updateRoom", "./CrazyGames.js")>]
let updateRoom (roomId: string, isJoinable: bool) : unit = jsNative

[<Import("addJoinRoomListener", "./CrazyGames.js")>]
let addJoinRoomListener (callback: string -> unit) : unit = jsNative

[<Import("isUserAvailable", "./CrazyGames.js")>]
let isUserAvailable () : bool = jsNative

[<Import("getUser", "./CrazyGames.js")>]
let getUser () : JS.Promise<CGUser> = jsNative

[<Import("showAuthPrompt", "./CrazyGames.js")>]
let showAuthPrompt () : JS.Promise<CGUser> = jsNative

[<Import("addAuthListener", "./CrazyGames.js")>]
let addAuthListener (callback: CGUser -> unit) : unit = jsNative

[<Import("requestAd", "./CrazyGames.js")>]
let requestAd (adType: string, adStarted: unit -> unit, adFinished: unit -> unit, adError: string -> unit) : unit = jsNative

[<Import("dataSetItem", "./CrazyGames.js")>]
let dataSetItem (key: string, value: string) : unit = jsNative

[<Import("dataGetItem", "./CrazyGames.js")>]
let dataGetItem (key: string) : string = jsNative
