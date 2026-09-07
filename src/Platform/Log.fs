module Log

open System.Collections.Generic
open Browser
open Fable.Core
open Fable.Core.JsInterop

[<Emit("performance.now()")>]
let now () : float = jsNative

[<Emit("console.log('%c' + $0.toUpperCase() + '%c ' + $1, $2, 'color:inherit', $3)")>]
let private print (level: string) (message: string) (style: string) (fields: obj) : unit = jsNative

let private tint =
    dict
        [ "debug", "color:#8a8f98"
          "info", "color:#00f6ff"
          "warn", "color:#ffb347;font-weight:bold"
          "error", "color:#ff2bd6;font-weight:bold" ]

let private lastAt = Dictionary<string, float>()

/// True at most once per `ms` for `key`. A fault that repeats every frame has to
/// cost one line, not sixty a second — the relay drops everything past 500/s.
let once (key: string) (ms: float) =
    let t = now ()
    match lastAt.TryGetValue key with
    | true, prev when t - prev < ms -> false
    | _ ->
        lastAt.[key] <- t
        true

let write (level: string) (message: string) (fields: obj) =
    print level message tint.[level] fields
    Input.hotSend "nda:log" (createObj [ "level" ==> level; "message" ==> message; "fields" ==> fields ])

let debug message fields = write "debug" message fields
let info message fields = write "info" message fields
let warn message fields = write "warn" message fields
let error message fields = write "error" message fields

let init () =
    window.addEventListener (
        "error",
        fun e ->
            if once "uncaught" 2000. then
                error
                    (string e?message)
                    (createObj
                        [ "at" ==> string e?filename + ":" + string e?lineno
                          "stack" ==> (if isNullOrUndefined e?error then "" else string e?error?stack) ])
    )
    window.addEventListener (
        "unhandledrejection",
        fun e ->
            if once "rejection" 2000. then
                error "a promise failed and nothing caught it" (createObj [ "reason" ==> string e?reason ])
    )
