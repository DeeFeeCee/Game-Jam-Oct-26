# Pass 1B: cardinal dash prototype

Branch: `feature/cardinal-dash`, based on accepted Pass 1A commit
`df45355078394be6d5f7fa8cacf6f9cda8344597`. Unity: **6000.3.26f1**.

## Test scene and controls

Open `Scenes/CardinalDashTest.unity` and use a 16:9 Game view. The cyan placeholder
uses `Prefabs/CardinalDashPlayer.prefab`. The scene reuses the Pass 1A placeholder
art and materials; no character artwork or animation has been added.

- A/D or left/right arrows, or gamepad left stick: walk.
- Space or gamepad south button: jump.
- **Left Shift or gamepad right shoulder: Dash**, aimed with WASD/arrows or the left stick.

Left Shift replaces the former Q binding. The existing Sprint action also binds
Left Shift, but remains disabled on the player's private input copy: this adapter
enables only Move, Jump, and Dash. Another future input owner must preserve that
separation or explicitly resolve this binding conflict. The input component adds a Dash action only to its private
runtime copy, only for a dash-enabled player. The project's shared input asset is
unchanged. If a real `Player/Dash` action is supplied later, its bindings are used
instead. Repeated button presses during a dash are discarded, not queued.

## Provisional behavior for team review

All values and choices below are prototype defaults, not permanent requirements.

| Setting | Initial value |
| --- | --- |
| Dash enabled | Only on the Pass 1B prefab |
| Dash speed | 18 units/second |
| Dash duration | 0.16 seconds |
| Unobstructed travel | 2.88 units |
| Direction dead zone | 0.2 on the dominant axis |
| Collision query skin | 0.01 units |

These settings appear in PlayerMovement2D's Inspector. Walking/jump defaults remain
6 units/second and 10 units/second upward; the prefab's gravity scale remains 3.

- Grounded activation and the first airborne dash are allowed. The team-approved
  default disallows another airborne dash until landing; selected scenes can
  explicitly enable chaining, as described below. Active dashes never overlap.
- The largest absolute input axis wins. Exact ties select horizontal, preserving
  its sign. Below the dead zone, dash uses the most recent non-neutral horizontal
  movement input. A newly enabled actor initially faces right.
- Dash replaces both velocity axes and sets gravity scale to zero. Walking and
  jump input cannot bend its path; jump presses during the dash are discarded.
- At dash end, gravity scale returns to the value captured at activation. Vertical
  momentum starts from zero; regular horizontal input and gravity resume. This
  deliberately does not restore the pre-dash falling/jumping velocity.
- A shortened final physics step preserves speed multiplied by configured duration
  as the requested distance. State transitions occur on physics ticks, so a duration
  not divisible by the fixed timestep can take up to one extra tick to finish.
- A sweep checks the next step against ordinary solid geometry, excluding triggers,
  the player's body, ignored collider pairs, and layers disabled by the collision
  matrix. Its small inset avoids treating a floor beside a horizontal dash as a wall.
  The Rigidbody2D also retains Continuous collision detection.
- A blocking wall, ceiling, or floor ends the dash after the final clipped movement
  step. Downward dash on an existing floor can therefore activate with no travel.
  Collisions do not allow phasing or grant immunity. Existing overlaps, moving
  obstacles, one-way platforms, and collider layer overrides need separate design
  and validation; this pass targets ordinary static box geometry.
- The physics root must stay upright at unit scale with the existing BoxCollider2D.

## Architecture and future LM approval

`PlayerMovement2D.cs` still owns normal walking and jumping. The partial companion
`PlayerMovement2D.Dash.cs` keeps dash state, direction resolution, gravity handling,
and collision sweeps together without introducing another Rigidbody2D owner.
`PlayerMovementInput2D.cs` only translates Input System actions into requests.
Its Dash callback queues a press; Update reads the full directional state after all
actions have processed that input update. This avoids action-order dependence when
aim and Dash are pressed together. FixedUpdate resolves and consumes each request,
then chooses dash or normal physics.

The optional instance property `DashApproval` is a single `Func<Vector2, bool>`:
it receives the resolved cardinal direction once immediately before an eligible
activation. Returning false rejects it without entering dash state. Requests made
while dashing never call the hook. If approval disables the actor, physics does not
continue into a new dash. The owner must release/reassign its hook with its lifecycle;
the controller clears it when disabled. No static state is used.

An unassigned approval hook imposes no resource limit **only for isolated
movement testing**; the scene's airborne eligibility rule still applies.
There is no resource balance, persistence, recharge, failure,
or victory implementation. The later LM pass must enforce the confirmed finite,
nonrecharging bank across levels, immediate game over on the final charge, and
escape requiring at least one remaining charge. When to consume a charge for a
blocked/zero-distance dash remains a team decision; this pass does not settle it.

Pass 1A's prefab, scene, tests, and shared input asset are preserved. Its prefab has
dash disabled by default even though the shared controller gains the optional code.
Temporary generation/validation helpers are removed before handoff.

## Automated validation

Run both PlayMode assemblies from **Window > General > Test Runner**:
`GameJam.Movement.PlayModeTests` and `GameJam.CardinalDash.PlayModeTests`.
The first is the accepted 15-test walking/jumping regression. The second checks all
four axes, diagonal/neutral resolution, travel/timing, restored physics, re-entry
rejection, collisions, approval, input bindings/lifecycle, opt-in, and scene wiring.

Executed in Unity 6000.3.26f1 on October 20, 2026, with domain reload disabled:

- Initial run: 39/40 passed. Simultaneous keyboard aim and Dash exposed action-order
  dependence: the Dash callback could read the previous Move value. The input adapter
  now queues the press and samples direction in Update after input processing.
- Corrected run 1: **40/40 passed**, zero failures or skips (15 movement + 25 dash).
- Corrected run 2: **40/40 passed**, zero failures or skips, in a second Play session.
- Test Runner cleanup completed before the second run and after the final run.
  The earlier Pass 1A temporary Editor scene-restoration observation did not recur.
  The temporary asset-generation/test runner helper was removed after validation.

Local NUnit evidence is in ignored `Library/Pass1B/regression-1.xml` and
`regression-2.xml`; the initial failure is retained in `regression-initial.xml`.
These Library files are local evidence, not assets to include in a commit.
No manual Pass 1B gameplay validation is claimed on the developer's behalf.

## Developer manual checklist

1. Click the Game view. Walk, stop, reverse, jump, land, and repeat a jump.
2. Dash in all four directions on the floor and in the air. Try diagonal and neutral
   input, including neutral after walking left. Confirm no diagonal dash travel.
3. Mash/hold Dash while dashing: the current trajectory/duration must not restart.
4. Dash into the purple wall and ceiling, down onto the floor, and horizontally
   along the floor. Check that no solid is crossed or incorrectly treated as ground.
5. After each dash, confirm ordinary walking, falling, and jumping resume.
6. Change dash speed/duration in the Inspector during Play Mode and compare travel.
7. Stop and start Play Mode several times with domain reload disabled. Check the
   Console, initial right-facing fallback, restored gravity, and absence of ghost input.
8. Confirm Pass 1A's scene still supports its accepted movement without dash.

Do not interpret these defaults as the final dash economy or collision design.
Implementation and validation were completed before publication. Resource-bank implementation and Pass 1C work remain outside this prototype.

## Pass 1B-C1: team-approved scene chaining

Select the **Scene Dash Settings** object in CardinalDashTest. Its
`SceneDashSettings` component exposes **Allow Air Dash Chaining**, initially
unchecked. In another scene, add this component to one active scene object.
Missing, disabled, or multiple enabled settings components safely mean false.
The setting grants movement permission only; it never grants LM charges.

The player reads the **active scene** at each eligible request. There is no cached
global policy or scene-event subscription. Normal single-scene loads apply the
destination automatically. Additive-loading code must set the destination active
with `SceneManager.SetActiveScene`; a persistent player also follows that policy.
Airborne eligibility already spent by a surviving player is retained across scene
changes: entering a restricted scene does not grant an extra dash.

Grounded rearming is provisional: any approved dash spends movement eligibility,
including a grounded upward dash or a blocked dash. Outside an active dash,
confirmed ground contact restores it. Thus horizontal/blocked dashes that finish
on the floor can be used again, while an upward dash or dash off a ledge requires
landing when chaining is disabled. No rearming occurs during active dash travel.
Wall/ceiling contact alone never rearms. A fresh or re-enabled actor starts with
one eligible dash; disabling/re-enabling is an actor reset, not an LM recharge.
Post-dash momentum, collision behavior, and tuning remain provisional as above.

Ineligible second airborne requests and requests during an active dash do not call
`DashApproval`. An eligible attempt calls it once; rejection does not spend movement
eligibility. Each approved chained dash calls it independently. Future LM charges
must remain finite, nonrecharging, persistent between levels, and cause immediate
game over on the final charge, independent of this scene option.

Manual checks: with the option off, jump and dash, then press Dash again before
landing (rejected); land and try again (allowed). With it on, complete a dash in
the air and dash again (allowed); mashing Dash during either dash must not restart
it. Check Left Shift, gamepad right shoulder, Q doing nothing, collisions, gravity,
walking/jumping afterward, and repeated Play sessions. For additive transitions,
activate destination scenes with opposite settings and confirm no stale policy.

The original 15 movement and 25 dash tests are retained. The old repeat-distance
and input-subscription tests explicitly enable chaining; keyboard tests now use
Left Shift. Fifteen added tests cover scene policies, rearming, approval gating,
transitions, active-dash exclusion, Sprint isolation, and actor lifecycle.
Executed October 21, 2026 in Unity 6000.3.26f1 with domain reload disabled:

- Preparation initially paused on an unsaved CardinalDashTest scene. No tests ran
  and no unsaved edits were discarded; the developer saved the scene before retry.
- First complete run: **55/55 passed**, zero failures or skips.
- Second complete run: **55/55 passed**, zero failures or skips, in a separate
  Play session after Test Runner cleanup. Each run includes the 15 unchanged
  Pass 1A tests and all 40 dash tests (the retained 25 plus 15 additions).
- No test failures required correction in this pass. Test Runner cleanup completed
  after both runs, without the earlier temporary scene-restoration error.
- Temporary preparation/runner helper files were removed after verification.

Local NUnit reports are in ignored `Library/Pass1BC1/regression-1.xml` and
`regression-2.xml`. Source compilation against the required Editor's reference
assemblies also succeeded. The developer subsequently completed all eight manual
checks, including normal and puzzle-specific chaining modes, and accepted Pass 1B-C1.
