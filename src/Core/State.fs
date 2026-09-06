module State

open Vec
open Domain

let mutable race = false
let mutable road: V2[] = [||]
let mutable corners: V2[] = [||]
let mutable gates: V2[] = [||]
let mutable gateEvery = 1
let mutable trackWidth = 0.

let mutable layout = 0
let mutable practice = false
let mutable target = -1
let arsenal = [| Rail; Mines; Swarm; Pulse; Scatter; Tractor |]
let raceArsenal = [| Scatter; Mines; Swarm |]
let mutable mutator = 0
let mutators = 4
let mutable asteroids: Asteroid[] = [||]
let mutable cratePositions: V2[] = [||]
let mutable catchUp = true

let nextRng r = abs (r * 1664525 + 1013904223) % 1000003