# ADR 0001: Host loss ends the match, lobby stays 4 players max

Status: accepted

## Context

Issue #73 ships the multiplayer path to the internet. Two open questions
needed a decision before the CrazyGames build could go out:

1. What happens when the host's browser disconnects mid-match (tab closed,
   crash, lost connection)?
2. What lobby sizes does this game declare for its CrazyGames listing?

The netcode is host-authoritative: exactly one browser runs `Sim.step` and
broadcasts `nda:state`; every other connected browser is a passive mirror
that renders whatever it last received (`client ()` in `src/Main.fs` treats
"no `nda:state` in the last second" as "I must be the host"). There is no
election or hand-off protocol between peers, and building one touches the
host/peer role logic in `src/Platform/Input.fs` and the relay fan-out in
`deploy/server.mjs` — both owned by the concurrent work on issue #72.

## Decision

**Host leaves mid-match ends the match for everyone; the room survives by
handing the host seat to the oldest remaining desktop peer, but the match
state is not migrated.**

The relay picks the successor (`deploy/server.mjs`, the `close` handler) and
tells it over the existing `nda:role` message; a phone pad is never promoted.
The promoted machine restarts from the lobby like every other mirror. This
keeps a room usable after the host closes their tab without any of the state
transfer that true migration would need.

When a mirror stops hearing `nda:state` for over a second, it already falls
back to `Sim.initial` and `Menu.show ()` (back to the lobby) instead of
trying to keep the match alive — `src/Main.fs`, the `wasClient` transition in
`frame`. That fallback now also raises the same on-screen banner the game
already uses for other match events (`Strings.t.HostLeft`, "HOST LEFT - BACK
TO LOBBY"), so the reset reads as a deliberate outcome instead of a silent
glitch.

Migration was rejected because:
- It needs a leader-election step so exactly one survivor takes over as
  host, and a way to hand it the current `World` — logic that lives in the
  host/peer split in `Input.fs`, out of this issue's surface.
- A 4-slot couch/party game usually loses its physical inputs (keyboard +
  attached phones) along with the host machine, so continuing without it
  rarely has anyone left to play.

**Lobby sizes: 2 players minimum for a scored match, 4 players maximum, 1
player for PRACTICE.** These numbers are not new — `Menu.fs` already hard
codes 4 slots (`Array.create 4 ...`) and gates START on `joined.Count >= 2`
outside practice — this ADR just records them as the figures Victor submits
to the CrazyGames listing (min/max players fields), since the CrazyGames
side is dashboard metadata, not a code path.

## Consequences

- No new "reconnect as host" feature; a disconnected host's teammates land
  back in the lobby and can start a fresh match.
- If the CrazyGames build ever needs true host migration (e.g. ranked
  online play beyond couch/party matches), that is a separate, larger
  change spanning `Input.fs` and `deploy/server.mjs`.
