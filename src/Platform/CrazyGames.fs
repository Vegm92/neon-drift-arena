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

// Monetization (Video Ads)
[<Import("requestAd", "./CrazyGames.js")>]
let requestAd (adType: string, adStartedCallback: unit -> unit, adFinishedCallback: unit -> unit, adErrorCallback: string -> unit) : unit = jsNative

// Monetization (Banner Ads)
[<Import("requestBanner", "./CrazyGames.js")>]
let requestBanner (containerId: string, width: int, height: int) : unit = jsNative

[<Import("clearAllBanners", "./CrazyGames.js")>]
let clearAllBanners () : unit = jsNative

// Data Module (Cloud-synced progress)
[<Import("dataSetItem", "./CrazyGames.js")>]
let dataSetItem (key: string, value: string) : unit = jsNative

[<Import("dataGetItem", "./CrazyGames.js")>]
let dataGetItem (key: string) : string = jsNative

// Leaderboards
[<Import("submitLeaderboardScore", "./CrazyGames.js")>]
let submitLeaderboardScore (score: int) : unit = jsNative

type IRelaySocket =
    abstract member onmessage: (obj -> unit) with get, set
    abstract member onclose: (obj -> unit) with get, set
    abstract member send: string -> unit
    abstract member readyState: int

// WebRTC Socket Wrapper
[<Import("createRelaySocket", "./CrazyGames.js")>]
let createRelaySocket (url: string, isPad: bool) : IRelaySocket = jsNative
