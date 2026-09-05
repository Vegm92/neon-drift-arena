module CrazyGames

open Fable.Core
open Fable.Core.JsInterop

type CGUser = { username: string; profilePictureUrl: string }

[<Import("init", "./CrazyGames.js")>]
let init (onMuteCallback: bool -> unit) : JS.Promise<unit> = jsNative

[<Import("gameplayStart", "./CrazyGames.js")>]
let gameplayStart () : unit = jsNative

[<Import("gameplayStop", "./CrazyGames.js")>]
let gameplayStop () : unit = jsNative

[<Import("getInviteParams", "./CrazyGames.js")>]
let getInviteParams () : obj = jsNative

[<Import("getInviteLink", "./CrazyGames.js")>]
let getInviteLink (roomId: string) : string = jsNative

[<Import("updateRoom", "./CrazyGames.js")>]
let updateRoom (roomId: string, isJoinable: bool) : unit = jsNative

[<Import("addRoomJoinListener", "./CrazyGames.js")>]
let addRoomJoinListener (callback: string -> unit) : unit = jsNative

[<Import("isUserAvailable", "./CrazyGames.js")>]
let isUserAvailable () : JS.Promise<bool> = jsNative

[<Import("getUser", "./CrazyGames.js")>]
let getUser () : JS.Promise<CGUser> = jsNative

[<Import("showAuthPrompt", "./CrazyGames.js")>]
let showAuthPrompt () : JS.Promise<CGUser> = jsNative

[<Import("addAuthListener", "./CrazyGames.js")>]
let addAuthListener (callback: CGUser -> unit) : unit = jsNative
