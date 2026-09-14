module CrazyGames

open Fable.Core

type CGUser = { username: string; profilePictureUrl: string }

/// True only in the package `scripts/pack-crazygames.mjs` builds, which stamps
/// `window.NDA_CG`; `?cg=1` turns it on locally. The SDK exposes no environment
/// property, so the packer is the signal.
[<Import("isCrazyGames", "./CrazyGames.js")>]
let isCrazyGames () : bool = jsNative

[<Import("init", "./CrazyGames.js")>]
let init (onMute: bool -> unit) : JS.Promise<unit> = jsNative

[<Import("gameplayStart", "./CrazyGames.js")>]
let gameplayStart () : unit = jsNative

[<Import("gameplayStop", "./CrazyGames.js")>]
let gameplayStop () : unit = jsNative

[<Import("loadingStart", "./CrazyGames.js")>]
let loadingStart () : unit = jsNative

[<Import("loadingStop", "./CrazyGames.js")>]
let loadingStop () : unit = jsNative

[<Import("getInviteRoom", "./CrazyGames.js")>]
let getInviteRoom () : string = jsNative

[<Import("updateRoom", "./CrazyGames.js")>]
let updateRoom (roomId: string, isJoinable: bool) : unit = jsNative

/// The room's CrazyGames invite link, fetched by `updateRoom` and cached there;
/// "" until the SDK answers, or on a build with no SDK.
[<Import("inviteLink", "./CrazyGames.js")>]
let inviteLink () : string = jsNative

/// True while the player has chat switched off in the CrazyGames settings.
[<Import("chatDisabled", "./CrazyGames.js")>]
let chatDisabled () : bool = jsNative

[<Import("addJoinRoomListener", "./CrazyGames.js")>]
let addJoinRoomListener (callback: string -> unit) : unit = jsNative

[<Import("isInstantMultiplayer", "./CrazyGames.js")>]
let isInstantMultiplayer () : bool = jsNative

[<Import("leftRoom", "./CrazyGames.js")>]
let leftRoom () : unit = jsNative

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
