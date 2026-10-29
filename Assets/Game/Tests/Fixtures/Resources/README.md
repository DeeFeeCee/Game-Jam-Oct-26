# Pass 1C: persistent run dash bank

Unity **6000.3.26f1**, branch `feature/global-dash-bank`, based on accepted
`be2750d0d45f41eb038748db0ab95e10c8df50dc`. This is isolated implementation and
verification content. Developer acceptance and publication authorization were confirmed on October 28, 2026. Further gameplay implementation is outside this publication pass.

## Inspector configuration and prototype controls

Open `Scenes/DashBankTestA.unity`. The `Run Dash Bank` root uses
`Prefabs/RunDashBank.prefab`; its **Starting Dash Count** is Inspector configurable.
The default **5** is a provisional testing value, not approved final tuning.
Counts are positive integers, clamped to at least one. One means the first accepted
spend is immediately fatal, so that dash cannot travel.

Both scenes use `Prefabs/BankedPlayer.prefab`, a placeholder copy of the accepted
Pass 1B player with `PlayerDashBankLink` added. Movement controls remain A/D/arrows
or left stick, Up/W or south button, and Space/right shoulder for Dash.

- Scene A: **Allow Air Dash Chaining** is off.
- Scene B: chaining is on. This changes movement permission, never resource balance.
- In Play Mode, use **Game Jam > Pass 1C > Log Bank State** to inspect charges and
  failure state. The persistent bank can also be inspected in the DontDestroyOnLoad
  hierarchy with the Inspector's Debug mode (runtime fields are hidden normally).
- Use the same Editor menu to **Load Verification Scene A/B** during Play Mode.
  These perform real single-scene transitions without changing build settings.
- **Initialize New Run (Resource Reset Only)** explicitly restores the configured
  bank and linked controls. The bank component also exposes that context-menu method.

The menu is Editor-only verification tooling, not a shipped UI or level system.
Game over currently freezes the player; there is no final overlay or game-over menu.

## Architecture and event contract

`RunDashBank` owns one remaining balance and game-over flag. The first manager
becomes authoritative and persists on a dedicated root GameObject. Duplicate bank
components destroy themselves before initialization, preserving the existing balance.
New scene prefabs therefore cannot restart the run or override its starting setting.
Do not attach unrelated scene content beneath the persistent bank root.

`InitializeNewRun()` is the only reset operation. Ordinary loads, landing, jumping,
component re-enabling, and scene chaining settings never call it. Changing the
Inspector starting value mid-run takes effect only on the next explicit reset.
There is no file save, checkpoint, recharge timer, or per-player/scene bank.

Instance C# events support future consumers:

- `ChargesChanged(int remaining)`: emitted once per successful spend and once per
  initialization, after the authoritative state is updated.
- `GameOver()`: emitted once on the transition to zero. It is not repeated by
  rejected requests after failure.
- `NewRunStarted()`: emitted on explicit initialization before the balance notification.

Subscribe in OnEnable, unsubscribe in OnDisable, and read the current properties
when binding (initial notifications can precede a newly loaded consumer). Event
callbacks cannot recursively spend or reset the transaction. No static events exist.
The static manager reference resets at SubsystemRegistration; repeated Play sessions
are validated with domain reload off and normal scene reload on, matching the project.
Skipping scene reload as well is outside this validation; scene objects must be reset
explicitly before that different Editor configuration is adopted.

`PlayerDashBankLink` binds the movement controller's existing `DashApproval` hook
to the bank, tracks manager replacement, and removes all bank subscriptions when
disabled/destroyed. It requires exclusive ownership of that hook; conflicting owners
fail closed instead of being silently replaced. It rebinds before movement physics
after re-enabling. An absent/disabled bank or disabled link freezes the opt-in player,
so missing wiring cannot silently provide unlimited dashes. Earlier unlinked
Pass 1A/1B prototypes remain isolated movement tests.

The final spend updates balance to zero and sets game over before notification.
Linked actors immediately stop dashes, disable movement/input, zero velocity, and
disable body simulation. `TrySpendDash()` returns false on that fatal spend even
though it consumed the charge. The controller detects the synchronous shutdown
and does not execute fatal dash or ordinary movement afterward. All linked actors,
including an actor already dashing, freeze. A newly loaded player also freezes if
the run has already failed. Restoring controls preserves their prior enabled flags.

## Eligibility and provisional blocked-dash policy

Movement/chaining eligibility is checked first. Then a first-physics-step clearance
sweep runs before `DashApproval`. Zero available travel (at or below 0.00001 units)
rejects the request without spending, notification, or spending movement eligibility.
Partially obstructed travel spends one charge; a later collision ends the dash
without a refund. Accepted active dashes cannot restart or spend repeatedly.
Every eligible chained dash independently spends from the same global balance.

This ordering is the only dash-physics change. The existing sweep, gravity handling,
cardinal direction rule, and scene chaining policy remain. The input adapter adds a
null guard for resource-driven shutdown before its Awake during a failed scene load.
Accepted scenes, shared input assets, art, animations, and movement tuning are preserved.

## Decisions still requiring team review

- Final starting count: currently 5 for testing.
- Fully blocked policy: currently no spend; partial travel spends once, no refund.
- Full-run restart: explicit resource reset restores controls and fresh movement
  eligibility at the current position; it does not rewind scenes, puzzles, or checkpoints.
- Game-over presentation: frozen actor and observable events only.
- Ordinary scene transitions preserve the bank. Checkpoint/retry flow, level reset,
  and any future victory event remain unimplemented. Escape must ultimately require
  at least one charge; no victory system is introduced here.
- Existing collision limitations remain: upright unit-scale box player and ordinary
  static solids are the target. Moving barriers, one-way platforms, collider overrides,
  and existing overlaps need additional decisions/validation.

## Automated validation

Run all three Play Mode test assemblies: `GameJam.Movement.PlayModeTests`,
`GameJam.CardinalDash.PlayModeTests`, and `GameJam.DashResources.PlayModeTests`.
The retained 55 tests regress the earlier passes; 25 new tests cover the bank,
approval ordering, final-charge shutdown, subscriptions, reset, and real scene loads.
Executed October 22, 2026 in Unity 6000.3.26f1, with domain reload disabled and
normal scene reload enabled:

- Initial runs: **80/80 passed each**, but an Input System assertion appeared on
  leaving Play Mode, outside the tests. Investigation of the installed package
  showed that replacing settings with exactly HideAndDontSave flags destroys the
  original transient Editor settings object.
- Corrected the three test fixtures to retain that original object during replacement
  and restore both its reference and original flags afterward. No test expectations,
  project settings, shared input assets, or package source were changed by this fix.
- Corrected full run 1: **80/80 passed**, zero failures or skips.
- Corrected full run 2: **80/80 passed**, zero failures or skips, in a separate Play
  session after complete Test Runner cleanup.
- Each corrected run includes 15 movement, 40 cardinal/chaining, and 25 resource tests.
  Editor-log verification found zero new assertions, compilation errors, or cleanup
  exceptions across the corrected pair. The ownership-conflict test deliberately
  logs one expected error per run; LogAssert verifies it.
- Temporary generation/runner sources were removed after validation. The permanent
  Editor verification menu remains available for developer testing.

NUnit reports are in ignored `Library/Pass1C/regression-1.xml` and `regression-2.xml`.
The first pair is retained as `initial-regression-1.xml` / `initial-regression-2.xml`.
The log check is recorded in `cleanup-log-summary.txt`. All seven source assemblies
also compiled against the required Editor's references. Accepted scene/prefab,
shared settings, package, and project-configuration hashes remained unchanged.
Developer acceptance was subsequently confirmed. No additional manual gameplay checks were performed during publication.

## Developer manual checklist

1. Open Scene A, enter Play Mode, and log the starting bank. Walk and jump; no spending.
2. Dash once, log the balance, then try again in midair: the rejected attempt is free.
   Land/jump and log again: no recharge. Dash again: exactly one further charge spent.
3. Dash while flush against a wall or downward on the floor: no travel, no spending.
   A dash that travels partly before hitting a solid spends once.
4. Load Scene B through the verification menu while still playing. Confirm the same
   bank survives, with exactly one manager and no balance reset. Complete consecutive
   airborne dashes; each independently spends one charge.
5. Leave one charge, then dash into open space. Confirm immediate game over with no
   fatal dash travel, falling, walking, jumping, or further spending.
6. Load the other verification scene after failure. Its player must remain frozen.
7. Initialize a new run explicitly. Check the restored configured balance and controls;
   the scene and player position remain unchanged under this provisional reset policy.
8. Stop/start Play Mode twice with domain reload disabled. Each new Play session starts
   fresh; ordinary scene changes within one Play session never do. Inspect the Console.
