# KING OF THE HILL and TAG

Status: design, two decisions open (marked **OPEN**). Sumo was considered and dropped.

Both are `Domain.Mode` cases beside `Arena | Practice | Race`. Every rule below is a
`match State.mode` branch or a `Cfg` tunable; nothing is a new flag.

## Shared

| Rule | KOTH | TAG |
|---|---|---|
| Lives | infinite, pips hidden | infinite, pips hidden |
| Death | respawn as today | respawn as today, still marked if marked |
| Launcher / ghost | none (nobody is ever out) | none |
| Catch-up toggle | ignored | ignored |
| Wormholes | on | on |
| Black holes | off | off |
| Sudden death border | only as the tiebreak below | never |
| Teams | yes, "alone" means one side inside | yes, one marked ship in total |
| Crates | adapted arsenal, mines carry 1 | adapted arsenal, mines carry 1 |
| Result column | points | seconds marked |
| Medals | kill medals as today, nothing new | same |

## KING OF THE HILL

- One hill, radius `hillRadius` = 150.
- It orbits the centre at `hillOrbit` = 500 and moves at `hillSpeed` = 40 u/s (ships do 270).
- Empty for `hillJumpAfter` = 8 s → jumps to a free spot, picked by the wormhole free-spot picker.
- Score: 1 point per second while exactly one side is inside. Two sides inside, nobody scores.
- Kills are counted on the panel and the result screen. They never decide the match.
- Ends at first to `hillTarget` points (**OPEN**, proposal 60) or at the 2:30 bell, most points.
- Tie at the bell: sudden death. Hill freezes, border shrinks as in Arena, first side to hold it alone for 3 s wins.
- Arsenal: barrier, repulsor, zapper, time bubble, sentry, seekers, mag mines (1). No railgun, no tractor.
- Danger arc: points at the hill whenever you are outside it, hazards first as today.
- HUD: points replace stocks; the clock counts down.
- Bot chain, in priority: existing hazard and rock dodges → fight whoever is in the hill when it is contested → sit in the hill when it is ours or empty → flee the hill when we would lose it → crates.

## TAG

- Random ship is "it" at 0:00. Runners get a 3 s head start while "it" is frozen.
- Any contact passes the mark: `Impact.pair` reports the pair, the sim moves the mark. No new physics.
- The freshly marked ship is stunned `tagStun` = 2 s. That is the whole anti-bounce rule.
- Teammate contact never passes the mark.
- "It" dies (ring-out): respawns still marked. Marked time keeps counting through the respawn delay.
- **OPEN, bleed shape** — pick one:
  - (a) Timer. Marked seconds accumulate; lowest at the bell wins. Nothing else drains.
  - (b) Pool. Everyone starts with `tagPool` = 60. "It" bleeds 1/s. At 0 you are out of the tag (still fly, cannot be marked, cannot win). Last with a pool wins, or most pool at the bell. Team pool is the sum.
  - (b) has an early end and a visible number on every panel. (a) has none. Recommend (b).
- Tie at the bell: extra time, 30 s, repeat until broken.
- "It" kit: `tagItSpeed` = 1.2× in every axis, no blaster, every special is TRACTOR with `tractorAmmo` refilled on every mark. A tractor latch is a tag once the tow ends in contact, as it does today.
- Runner kit: blaster on, each hit stuns 0.5 s and deals nothing; `tagFireCooldown` = 0.4 s (Arena is 0.11). Heat as today. Specials from crates: barrier, repulsor, time bubble, zapper, mag mines (1).
- Danger arc: runners see "it"; "it" sees the nearest runner. Hazards first as today.
- HUD: pool or marked seconds replaces stocks; "it" is drawn with a persistent ring and the tag shout on every pass.
- Bot chain, "it": existing dodges → tractor when lined up inside range → chase the nearest runner → crates never.
- Bot chain, runner: existing dodges → keep `tagFlee` = 400 from "it", biased toward open space and away from the rim → crates → orbit the centre.

## Tunables (all `Domain.Cfg`, all in the dev TUNING block)

```
hillRadius 150   hillOrbit 500   hillSpeed 40   hillJumpAfter 8   hillTarget 60   hillSuddenHold 3
tagStun 2   tagPool 60   tagItSpeed 1.2   tagFireCooldown 0.4   tagHitStun 0.5   tagFlee 400   tagHeadStart 3   tagExtraTime 30
```

Adding them moves nothing in the golden run until a mode branch reads them, so the hash
changes only in the PR that wires the branch, and `npm run retune` accepts it there.

## Order of work

1. Rename stock → life across strings and docs. Own PR.
2. `Mode` gains `Hill` and `Tag`; lobby cycle FFA → TEAMS → PRACTICE → RACE → HILL → TAG.
3. KOTH sim + HUD + bot + `test/Check.fs` cases (scores alone, not contested; jump after empty; sudden-death hold).
4. TAG sim + HUD + bot + checks (mark passes on contact, not to a teammate, stun on pass, respawn keeps the mark, pool hits zero).
