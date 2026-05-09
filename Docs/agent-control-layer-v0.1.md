# Agent Control Layer — Design Recommendation v0.1

**Status:** Proposed — awaiting MrObvious greenlight before implementation.  
**Authors:** AgentObvious (synthesis), Randy (initial architecture).  
**Date:** 2026-05-09

---

## 1. Why this exists

Bob's framing in #researchanddevelopment 2026-05-08 18:00:

> Everything in the game client should have a hook for an agent to control everything (movement, combat, communication, travel). That way we can do all unit testing with agents, humans in later rounds. […] If you guys can login to the game, move around, take screenshots, you can iterate without slow, sleepy humans getting in the way.

Today every test cycle blocks on a human in the Unity Editor. That's the bottleneck for every other gameplay-iteration loop on this project — the navel-sink fix shipped in v0.0.213 needed Stepo to stand up his rig, log in, and confirm. That model doesn't scale; it's also why R2's art deliveries lag (they're queued behind asset placement that needs a human eye in the client).

An **Agent Control Layer** lets Claude-driven agents (R2, AO, future Randy successor) drive the client headlessly: log in, move, fight, accept a quest, take a screenshot, assert on game state. Test cycles compress from "hours blocked on a human" to "minutes blocked on an LLM call."

This document is the recommended architecture, synthesized from Randy's 2026-05-08 18:03 post + my three pushes (18:09) + my consolidation pass (18:47) when Randy went silent.

## 2. Recommended architecture: `AgentControlLayer`

Three components, one PR, ~500 LoC across 4 new files + a refactor of input-reading sites.

### 2.1 `IInputProvider` interface

The injection seam. `PlayerController`, `TargetingSystem`, `OrbitCamera`, and the UI input layer read from `IInputProvider` instead of calling `Input.GetKey` / `Input.GetAxis` / `Input.GetMouseButton` directly.

```csharp
public interface IInputProvider
{
    // Continuous-axis inputs sampled per-frame
    Vector2 MoveAxis { get; }       // WASD or agent-directed
    Vector2 LookAxis { get; }       // mouse delta or agent-directed
    bool Sprint { get; }            // shift-held
    bool RightMouseHeld { get; }    // camera control

    // Discrete edge events queued and consumed by ConsumeEdge()
    bool ConsumeJump();
    bool ConsumeAttack();
    bool ConsumeInteract();
    bool ConsumeTargetCycleNext();
    bool ConsumeTargetCyclePrevious();
    bool ConsumeAbility(int slotIndex);
    bool ConsumeUIToggle(UIPanel panel);
}
```

The split between continuous (`Move`/`Look`/held flags) and discrete (`Consume*`) matters: continuous values smear, discrete events fire-once. An agent saying "press jump" must produce exactly one jump regardless of how many frames its command sits in the queue.

### 2.2 Two concrete providers

- **`KeyboardMouseInputProvider`** — wraps the existing `Input.*` calls. Default when no agent CLI flag. Keeps human-driven gameplay unchanged.
- **`AgentInputProvider`** — backed by an in-process `Channel<AgentCommand>`. Reads from the `AgentControlServer` queue, translates commands to axis values + edge events.

A `MonoBehaviour` factory (`AgentControlBootstrap`) in the boot scene reads `System.Environment.GetCommandLineArgs()`, picks the provider based on `--agent-mode` presence, and binds it via a small `InputProviderRegistry` (single-instance ServiceLocator pattern — Unity-friendly, avoids DI framework dependency).

> **Note on Randy's spec:** Randy referenced "GameBootstrap already parses CLI args" — that class does not currently exist in `orlo-client/Assets`. We need a new `AgentControlBootstrap` MonoBehaviour. Trivially small (~30 LoC).

### 2.3 `AgentControlServer` — wire protocol

Localhost-only listener bound to `127.0.0.1:9900`. **WebSocket for command + state stream, HTTP for one-shot ops.**

- **WS `/control`** (bidirectional)
  - Client → Server: `AgentCommand` JSON. Examples:
    - `{"type":"move","axis":[0.0,1.0]}` — start moving forward
    - `{"type":"sprint","held":true}`
    - `{"type":"attack"}` — discrete edge
    - `{"type":"interact","entityId":42}`
    - `{"type":"target","entityId":42}` — explicit target by ID (preferred over cycling for scripted tests)
    - `{"type":"chat","channel":"say","text":"hello"}`
    - `{"type":"ability","slot":3}`
    - `{"type":"ui_toggle","panel":"inventory"}`
  - Server → Client: state events on tick boundaries. Pushed proactively so agents don't poll. Examples:
    - `{"type":"state","schema_version":"1.0",...full state payload...}` — every 2 ticks (configurable)
    - `{"type":"event","event":"target_acquired","entityId":42}` — fires immediately on transition
    - `{"type":"event","event":"damage_taken","amount":15,"source":42}`
    - `{"type":"event","event":"quest_beat","questId":"main_arc_1.1","beat":3}`
  - Heartbeat: server emits `{"type":"ping"}` every 5s; client must echo `{"type":"pong"}` within 10s or connection drops.

- **HTTP** (one-shot, large payload or atomic ops)
  - `GET /state` — current state snapshot (for tests that need a single read, not a stream)
  - `GET /screenshot?format=png` — `ScreenCapture.CaptureScreenshotAsTexture()` → PNG bytes. Defaults to current viewport; future param `?camera=overhead` for fixed alt-cameras.
  - `POST /step?ticks=N` — advance N simulation ticks. Only valid in lockstep mode (returns 400 in live mode).
  - `GET /healthz` — liveness probe (returns `{"status":"ok","mode":"live|lockstep","schema_version":"1.0"}`).

WS port + HTTP port share `:9900` — `AgentControlServer` is a single `HttpListener` with WebSocket upgrade on `/control`.

Why WS (vs all-HTTP): a typical test scenario is "attack target, wait for it to die, assert quest beat advanced." With REST polling that's 30 polls/sec for `/state` + manual diff to detect state transitions. With WS push the agent receives `{"type":"event","event":"entity_killed",...}` and `{"type":"event","event":"quest_beat",...}` exactly when they happen. Cleaner, lower latency, lower load.

### 2.4 State schema v1.0 (locked before agent code)

Refactoring state shape after agents are coded against it breaks every test. So we lock v1.0 explicitly. Versioned (`schema_version` field on every state payload) so v1.1 additive changes don't break consumers and v2 breaking changes can be migrated deliberately.

```json
{
  "schema_version": "1.0",
  "tick": 184223,
  "timestamp_ms": 1778297512345,
  "player": {
    "id": 1,
    "position": [12.5, 1.2, -45.3],
    "orientation_y_deg": 90.0,
    "velocity": [0.0, 0.0, 5.0],
    "health": {"current": 78, "max": 100},
    "stamina": {"current": 60, "max": 100},
    "mana": {"current": 0, "max": 0},
    "is_grounded": true,
    "is_sprinting": false,
    "animation_phase": "idle",
    "ability_cooldowns": {"slot_1": 0.0, "slot_2": 1.4}
  },
  "target": {
    "entity_id": 42,
    "reticle_state": "valid",
    "distance": 4.2,
    "in_attack_range": true
  },
  "nearby_entities": [
    {"id": 42, "type": "creature", "subtype": "wolf", "faction": "wild",
     "position": [14.0, 1.0, -42.0], "distance": 4.2, "can_attack": true,
     "health_pct": 0.85},
    {"id": 99, "type": "npc", "subtype": "vendor", "faction": "discovery_guild",
     "position": [10.0, 1.2, -50.0], "distance": 7.4, "can_attack": false,
     "health_pct": 1.0}
  ],
  "inventory": {
    "slots_used": 12,
    "slots_total": 30,
    "summary": [
      {"item_id": "iron_dagger", "qty": 1, "equipped": true},
      {"item_id": "torch", "qty": 3, "equipped": false}
    ]
  },
  "quest": {
    "active_id": "main_arc_1.1",
    "beat": 3,
    "objective_text": "Reach the Signal Tower"
  },
  "chat": {
    "recent": [
      {"channel": "say", "from": "Vendor_Korreth", "text": "fresh blades", "ts_ms": 1778297500000}
    ]
  },
  "faction_reputation": {
    "discovery_guild": 200,
    "convergence_seekers": -50
  }
}
```

`nearby_entities` is capped (default 50) by distance from the player. `chat.recent` is a sliding window (default last 20 lines).

### 2.5 CLI modes

- **No flag → human play.** `KeyboardMouseInputProvider` bound, no server started. Zero-cost when off.
- **`--agent-mode` → live agent.** `AgentInputProvider` bound, `AgentControlServer` started, sim runs at normal frame rate. Agent commands consumed at next-frame.
- **`--agent-mode=lockstep` → CI determinism.** Same as live, plus sim pauses between commands. Frame loop waits for `POST /step?ticks=N` to advance. Same script across machines = same outcome (no flaky tests from frame-timing variance).
- **`--agent-port=PORT` → override 9900** (for parallel CI runs).

Implementation of lockstep is an additional ~50 LoC: a `bool _stepGate` checked at the top of `FixedUpdate`, set true by `/step`, decremented per tick, returns to false at zero. Time.timeScale stays at 1; we just drop ticks until the gate releases.

## 3. Migration strategy (Phase 1 ≠ migrate 99 call sites)

Current orlo-client has **99 raw `Input.*` calls across 22 files**. Migrating all of them alongside introducing the abstraction is too much surface for one PR — gameplay regressions become hard to attribute.

**Phase 1 (this PR — recommended scope):**
- Land the abstraction (`IInputProvider`, two providers, `InputProviderRegistry`, `AgentControlBootstrap`).
- Land `AgentControlServer` with WS + HTTP endpoints.
- Land state schema v1.0 + the state-builder that walks the live game and emits the JSON.
- Migrate exactly **two** call sites as proof points: `PlayerController` (movement+sprint+jump) and `TargetingSystem` (target cycling + attack).
- Don't touch the other 21 files yet.

This keeps the PR reviewable and gives us a working agent-mode for the bare-minimum smoke test ("login, move forward 10s, take screenshot") without rewriting the whole input layer.

**Phase 2 (separate PR, after Phase 1 lands and a smoke test passes):**
- Migrate the rest of the input call sites (`OrbitCamera`, `CombatHUD`, `InventoryUI`, `ChatUI`, etc.).
- Add discrete UI commands to the WS protocol (`ui_toggle`, `inventory_use`, `chat_send`).

**Phase 3 (after Phase 2):**
- First end-to-end test scenario driven by an agent (the Story Arc 1 mission test from my 2026-05-08 18:02 post: "spawn at Threshold, accept Mission 1.1, walk path to Signal Tower, screenshot at each beat marker").

## 4. Out of scope for this PR (explicit punt list)

These are real future needs, but each adds enough surface that bundling them muddies the design contract:

- **Multi-agent simultaneous connections** — single agent at a time on `:9900`. Multi-agent is v0.2.
- **Compile-out for ship builds** — Phase 1 ships the server in all builds. Adding `#if AGENT_CONTROL` guards is a day of cleanup; do it before public release, not now.
- **Replay log** — record-then-replay is straightforward (commands are JSON, state is JSON) but needs disk IO design. v0.2.
- **Agent identity / auth** — localhost-only binding is the only auth in v0.1. If we ever need agents over the network, that's a security rewrite, not a feature add.
- **Anti-cheat** — same answer; localhost-only and `#if AGENT_CONTROL` together prevent ship abuse.
- **Migrating input call sites beyond PlayerController + TargetingSystem** — Phase 2.

## 5. Test plan

- **Unit:** `AgentInputProvider` queue semantics — discrete-once on edge events, axis values clamp [-1,1], malformed JSON drops the message and logs.
- **Integration (manual, in Editor):** launch with `--agent-mode`, connect a Python WebSocket client, send `{"type":"move","axis":[0,1]}`, observe `state.player.velocity.z > 0`, send `{"type":"move","axis":[0,0]}`, observe velocity returns to 0.
- **Smoke (the proof point):** Phase 1 ships with a Python script `scripts/smoke_test_agent.py` that does: connect, take screenshot, move forward 5s, take screenshot, assert player position changed by ≥ 5 units along forward axis. Lives in orlo-client repo as a runnable example.
- **CI (Phase 3):** `--agent-mode=lockstep` driven by a per-arc test script that asserts on quest beat advancement.

## 6. Open questions for MrObvious

Before I start the PR I want explicit calls on:

1. **Server included in ship builds?** Recommend **no** for v0.1 (keep it simple), wrap in `#if AGENT_CONTROL` for v0.2. The risk in v0.1 is a public release accidentally shipping with `:9900` listening — mitigated by localhost-only binding but not eliminated. Alternative: condition on `Application.isEditor || Debug.isDebugBuild`. Slight preference for the `#if`.
2. **Auth surface for v0.1?** Recommend **localhost-only binding, no token** — the "agent runs in same machine as client" assumption holds for our test setup. Adds a token if/when we ever want agents on the LAN.
3. **State schema scope.** Section 2.4 above. Adding fields later is cheap (additive, schema_version stays 1.x); removing fields breaks tests. So I'd rather over-include now. Flag anything you'd cut, anything you'd add.
4. **Lockstep mode scope.** Recommend **shipping it in Phase 1**, even though it's tempting to defer. Reason: same-script-different-frame-timings flakiness is a class of bug we'd find immediately the first time we run a CI test, and refactoring lockstep in afterwards means rewriting every test that already exists. Cheaper now than later.

Default if no response: ship Phase 1 as specced above, with `#if AGENT_CONTROL` guard, localhost-only, schema as listed, lockstep included.

## 7. Estimate + timeline

~500 LoC across these new files:

| File | LoC | Purpose |
|------|-----|---------|
| `Assets/Scripts/Agent/IInputProvider.cs` | 30 | Interface + edge enums |
| `Assets/Scripts/Agent/KeyboardMouseInputProvider.cs` | 80 | Wraps `Input.*` |
| `Assets/Scripts/Agent/AgentInputProvider.cs` | 100 | Queue-driven, schema parsing |
| `Assets/Scripts/Agent/AgentControlServer.cs` | 200 | WS + HTTP listener, dispatch |
| `Assets/Scripts/Agent/AgentControlBootstrap.cs` | 30 | CLI parse + provider binding |
| `Assets/Scripts/Agent/StateBuilder.cs` | 60 | Walks game, emits JSON state |
| `scripts/smoke_test_agent.py` | 60 | Runnable example + smoke test |
| **Plus refactor:** | | |
| `Assets/Scripts/Player/PlayerController.cs` | ~30 lines changed | reads from registry |
| `Assets/Scripts/Player/TargetingSystem.cs` | ~20 lines changed | reads from registry |

Approximate effort: a focused day. Bottleneck is testing in the Editor, which needs human verification — propose I land the PR in a `feat/agent-control-v0.1` branch and pair with Stepo (or whoever picks up Randy's seat) on the in-Editor smoke pass.

---

## Appendix A: relationship to Randy's original spec

Randy's 2026-05-08 18:03 post proposed:
1. `IInputProvider` interface with `move`/`sprint`/`jump`/`attack`/`target`/`interact` — adopted as-is.
2. `AgentInputProvider` with in-process command queue — adopted, plus split between live/lockstep modes.
3. `AgentControlServer` HTTP listener with `/cmd`, `/state`, `/screenshot` — adopted, but pivoted from REST `/cmd`+`/state` to WebSocket `/control` (push-based state). HTTP retained for `/screenshot`, `/step`, `/state-snapshot`, `/healthz`.

Randy went silent at 22:29 ("On it — I'll pick that thread back up in there now") and didn't post again. This synthesis captures the design state as of his last input + my pushback, and is what I'd build against absent a counter from him.

## Appendix B: out-of-band notes

- The `~/.ao-state/orlo-triage-cursor.json` daemon (separate work, shipped 2026-05-09) gives us a live priority queue of Discord messages classified by Qwen. Randy's silence shows up there as no activity rather than as an outage signal — worth adding a "Randy heartbeat" indicator in a future iteration so the loop can route around an unresponsive teammate without a human noticing.
- R2 was briefed today (2026-05-09) on the new canon docs (locations bible v0.1, iconic NPC v2 lib, survivors ethnography). Art queue is being prepared for Stepo's weekend return — orthogonal to this design but useful context for "what's blocking on what."
