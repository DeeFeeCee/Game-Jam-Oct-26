# Pass 1I — Unified Gameplay Foundation candidate

This report preserves the original candidate verification below. See the publication verification
addendum at the end for developer acceptance and results after integrating Pass 1G.

Designation: `PASS-1I-UNIFIED-GAMEPLAY-CANDIDATE`. Implementation/local verification only;
developer manual acceptance and publication are separate. No commit, push, PR, merge, campaign
level, final art assignment, or deferred mechanic is part of this candidate.

## Repository baseline

- Isolated worktree: `UnifiedGameplayFoundation`; branch `feature/unified-gameplay-foundation`.
- Starting/current HEAD: `1daf7b6975b16136f79725fe4474920dbb0912a8` (main at preflight).
- Remote main rechecked during closeout: same SHA. PR #14 remains open/unmerged, source
  `3ec001df3efde42d0a4a5b87a10694ad5690b4d8`; it was inspected, not modified or imported.
- No unfinished moving-platform/Pass 1H source was found in the available branches/worktrees.
- Production Level01, the original single-screen experiment, accepted prefab fixtures, Ace art,
  animation assets, shared Input System asset, scene GUIDs, and all 258 baseline PlayMode tests are retained.
- Only necessary existing runtime integrations and appending the verification scene to build registration
  are intentional modifications. MainMenu remains first; canonical New Game/Restart destinations are unchanged.
- All paths, including folder metadata, are listed in `ChangeInventory.md`. Logs, builds, captures,
  solutions, `.vscode/settings.json`, and SceneTemplateSettings are excluded. Git staging remains empty.

## Stage results and architecture

| Stage | Delivered behavior | Evidence |
|---|---|---|
| A: input | Keyboard-only digital Move composite on private action copy; full horizontal axis with W/Up. Shared asset and analog gamepad response retained. | Six pre-fix failures reproduced the slowdown; focused corrected suite 61/61; physical travel comparisons and analog regression. |
| B: failure/fall zone | Typed environmental failure in the existing bank; no spending or ChargesChanged; existing synchronous player freeze and deferred UI; actual HUD balance. | Ten focused tests passed, including reentrancy, final charge, exits, HUD, Pause priority, Restart, real fall trigger. |
| C: platforms | Kinematic MovePosition route with stationary endpoints; reusable support velocity consumed by the sole player controller. No parenting or inherited departure momentum. | 79/79 combined input/movement/platform tests; actual horizontal/vertical/reversal riding. |
| D: turrets | Fixed-direction timed, scene-owned swept projectiles; first solid blocks; game-time pause; no catch-up or post-failure shots. | Seven strengthened focused tests passed; standalone hit and suppression. |
| E: walls | Minimal `IDashImpactSurface2D` pure query + approved impact. Sorted predictive sweep, ordinary-solid precedence, continued collision checking behind removed wall. | Nine wall cases in complete suite, including flush contact, rejection, final-charge zero travel, occlusion, and one spend. |
| F: crusher | Armed → Warning → Falling → Landed; downward shape sweep with relative closing motion, underside-only failure, shared safe rider support, solid landing/max descent. | Nine crusher cases including a faster falling-away player; standalone underside death and safe top ride/walk/jump/dash. |
| G: integration | Six reusable prefabs, isolated room, authoring checks, development build, opt-in standalone traces/captures, designer guide and art matrix. | Final evidence below. |

The Movement assembly contains only generic support and dash-surface interfaces. Mechanics depends on
Movement/Resources/Levels, with no competing player/body owner or persistent manager. UI continues
to use the existing bank and Game Over controller. New mechanics poll run state and subscribe to no
resource events. Existing resource reentrancy and deferred input-release safeguards remain active.
SubsystemRegistration still resets the authoritative bank; scene-owned mechanic state initializes per
instance. The existing domain-reload-disabled project option is unchanged.

Environmental failure sets `RunFailureCause.Environmental` and notifies the existing GameOver event
once. Mobility exhaustion still reaches zero and prevents fatal dash travel. HUD reads actual remaining
charges; failure reason becomes SUBJECT INTEGRITY LOST. A new run clears the cause through the existing
explicit initialization flow. There is no health, checkpoint, separate death manager, or resource balance.

## Reusable assets

All are under `Assets/Game/Prefabs/` with Unity-generated metadata:

- `FallDeathZone.prefab`: invisible resizable lethal trigger.
- `MovingPlatform.prefab`: root with stationary A/B and moving body; horizontal/vertical/diagonal routes.
- `Turret.prefab`: assigned muzzle and projectile reference, world-space aim/timing.
- `TurretProjectile.prefab`: swept circle, configurable lifetime, scene cleanup.
- `BreakableWall.prefab`: ordinary solid until an approved nonfatal dash reaches it.
- `FallingCrusherPlatform.prefab`: stationary activation volume plus descending safe-top body.

Verification scene: `Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity`.
It uses the accepted LevelSetup/player/bank, existing Ace background, replaceable colored square sprites,
and a stationary 32×18 camera. It is not a finished campaign level or replacement for Level01.
Safe-top strips explicitly render above their surfaces. No source artwork/import settings were changed.

## Executed validation

Final run counts and preservation totals are recorded at closeout below. Preliminary complete suites:
**310/310 PlayMode**, then **311/311 twice**, all with zero failures/skips. An additional moving-apart
crusher regression was then added and its reproduced false positive corrected. No accepted test was removed or weakened.

Evidence lives in ignored `Logs/UnifiedGameplay/`:

- `input-before.xml`, `keyboard-travel.csv`: original physical reproduction and corrected comparisons.
- `stage-a.xml`, `stage-b.xml`, `stage-c.xml`, `stage-d-verified.xml`: focused stage evidence.
- `playmode-candidate-1.xml`, `playmode-candidate-2.xml`: final complete runs.
- `editor-candidate-corrected.xml`, `editor-candidate-2.xml`: final complete authoring/reference runs.
- `build-summary.txt`, `build-messages.txt`, `build-release-final.log`: final Windows development build.
- `StandaloneCandidate/results.txt`, `physics.csv`, PNG captures: final real-physics standalone checks.
- `preservation-audit.json`, `change-inventory.csv`: hash/GUID/reference and exact path audits.

Standalone verification explicitly opts in with `-unified-check <absolute evidence folder>` in a
development build. No checker object exists in ordinary scenes/play. Case initialization places the
player before measurement, then actual Rigidbody2D simulation and the accepted movement/input/UI paths
perform traversal. The checker records fixed-step positions, velocities, grounding, dashing, and charges.
Input is simulated, not physical-controller or developer-manual validation. D and W+D each traveled
1.2 units over ten fixed ticks in the corrected standalone run. The two complete PlayMode processes
use the existing domain-reload-disabled option; the added Editor test also enters/exits Play twice
in the same Editor process, exercising one bank, fresh crusher state, actual charged dashes and failure
on each entry. The nine-case Editor suite repeats that check in both complete runs.

The final development executable also accepts `-mechanics-room` for manual room entry. The generated
`Builds/UnifiedGameplay/PlayVerification.cmd` supplies that explicit test flag. Ordinary startup and
canonical Restart remain unchanged. This is development verification tooling, not a new menu feature.

### Initial failures/corrections retained

- Six intentional pre-fix input comparisons failed: diagonal horizontal travel was about .707 of
  single-axis travel (3.3941 versus 4.8 units over .8 seconds). Corrected comparisons are 4.8 in both cases.
- First wall test compilation used a nonexistent bank initialization overload. The fixture now uses
  accepted bank spending to reach one charge; no resource API was changed to accommodate a test.
- Two wall fixtures incorrectly replaced the bank-owned approval delegate and triggered its existing
  exclusive-ownership guard. Isolating the hook required honoring the link's deliberate disable/freeze.
  The intermediate combined run was 102/103. The corrected complete preliminary run passed 310/310.
- The initial turret timing fixture could count inactive spawned objects. It was strengthened to assert
  an active projectile and actual movement; spawned shots are explicitly active, and all seven passed.
- First standalone run passed through crusher underside but its following top setup stayed frozen:
  diagnostic new-run reset before old lethal scene unload allowed the old hazard to fail it again.
  Diagnostic reset now happens after an interstitial Main Menu unloads old hazards/presenters and before
  the new gameplay scene loads. Production Restart was not changed; a new
  regression checks its existing departing-player freeze from the actual crusher failure.
- Verification spawn clearance and safe-top sprite sorting were corrected after fixture/screenshot review.
  Temporary Editor patch helpers were removed. Final tests/build/captures follow those corrections.
- After the preliminary 311/311 runs, an added underside regression reproduced a false death when the
  player was already falling away faster than the crusher (0/1 in `crusher-relative-before.xml`). The
  swept lethal test now also checks relative closing distance. Actual collision-normal fallback remains.
  One attempted focused launch while another test Editor still held the project lock exited without
  executing tests; it was rerun only after the first Editor exited. No licensing/settings changes were made.
- The initial build's ten warnings were traced to new SpriteRenderer resizing inside OnValidate.
  Collider initialization remains in Awake; renderer updates occur in Start or a one-shot Editor update.
  The added Inspector resize test first exposed an unsuitable unsaved additive-scene fixture (7/8),
  then a delayed callback that did not execute in the batch test (7/8). An isolated preview scene and
  one-shot Editor update corrected both; the full Editor suite then passed 8/8. These failures were
  not skipped. The final build and full-suite repeats use the corrected authoring code.
- The same-Editor repeated-Play test initially had a fixed-yield timing assumption and an empty-scene
  restoration error (6/9, including two consequent teardown failures). Waiting on the actual resource
  result through Editor updates and safely restoring an empty Editor corrected the fixture. It passed
  9/9 twice, including two real Play entries per run with domain reload disabled.
- Screenshot review caught a stale failure overlay in the diagnostic's top-rider capture: resetting
  after loading a gameplay scene let its normal Game Over presenter open before the bank reset. Case
  setup now unloads through the existing Main Menu, resets the bank, then loads verification and asserts
  that Game Over is hidden. Gameplay/UI controllers were not changed for this correction.

## Limits and remaining decisions

Developer ten-item manual acceptance, particularly physical gamepad controls and level-authoring feel,
is still required; see `AuthoringGuide.md`. Scripted standalone verification is not manual acceptance.
Mechanic use/level placement, final speeds/delays/sizes, warning presentation, artwork, artist assignments,
and final five-charge balance remain provisional/TBD. The matrix does not assign new work to Ace.

Kinematic platform routes need designer-authored headroom and clear paths; no general squeeze/side-crush
solver is included. Crusher sides are nonlethal, its descending underside is lethal, and it never
automatically rearms. Projectile/body moves obey the engine's existing 2D translation limit. Muzzles
must start outside blocking geometry. Place a kill plane only below a deliberately accessible pit.
Breakable walls need matching collision/visual dimensions; ordinary solids still block dashes.

The final build reports zero errors and zero warnings. Existing native graphics cleanup diagnostics
remain recorded in the player logs. No claim is made that the earlier stutter observation was fixed.
No additional campaign levels, art variants, dialogue, lore, new victory sequence, health, or checkpoints
were implemented. Future PR #14 integration requires a separate combined regression pass after its merge.

## Final closeout record

| Final check | Executed result |
|---|---|
| Complete PlayMode candidate run 1 | 312/312, zero failures/errors/skips |
| Complete PlayMode candidate run 2 | 312/312, zero failures/errors/skips |
| Complete Editor candidate run 1 | 9/9, zero failures/errors/skips |
| Complete Editor candidate run 2 | 9/9, zero failures/errors/skips |
| Accepted regression retention | All 258 baseline PlayMode tests unchanged; 54 new PlayMode cases |
| Windows development build | Succeeded, zero build errors and zero warnings |
| Final executable real-physics checks | Passed, exit code 0; ten focused PNG captures and physics CSV |
| Manual room entry shortcut | Launch/scene entry and normal window close verified; no manual gameplay/hardware claim |
| Developer manual acceptance | Not performed by the agent; ten-item checklist awaits developer review |

The final standalone checks cover equal D/W+D travel, fall-zone death with five charges, cause-specific
Game Over and canonical Restart, horizontal/vertical carry, Pause/Resume, platform jump/dash departure,
turret failure and suppression, approved wall destruction with one spend, persistence through scene
transition, lethal crusher underside, and safe top descent/walk/jump/dash. Case setup and all simulated
input are disclosed above; no measured movement bypasses the accepted player Rigidbody/controller.

The 94 intended paths comprise 87 additions and seven modifications, with no deletions. Of 423 baseline
files, 415 are byte-identical; differences are the seven authorized existing paths and excluded IDE settings.
All existing metadata, artwork, animations, scenes, prefabs and test fixtures remain byte-identical.
Audit results: no missing baseline paths, unexpected modifications, missing metadata, duplicate GUIDs,
or unresolved changed-asset GUIDs. All nine other worktrees retain their original HEAD and status.

Five incidental Unity build-serialization changes were backed up under ignored Logs and restored to
their verified initial bytes. The three excluded IDE/solution/scene-template paths remain present and
untouched by cleanup. Temporary patch helpers are absent. `git diff --check` passes; the staging area
is empty. Branch HEAD remains the starting SHA; no commit, push, PR, or merge occurred.

Gameplay implementation is complete for developer inspection; final balance, chosen level usage,
art assignments/polish, manual physical-controller acceptance, and future combined PR #14 integration
remain separate decisions. Stop here pending developer acceptance/publication authorization.

## Publication verification after Pass 1G integration

Devin Padin granted `PASS-1I-UNIFIED-GAMEPLAY-ACCEPTED` after **10/10 developer manual checks**.
The original results above remain the acceptance record. Commit
`2316b232f7c11e3e7696d40253af86afe3fd71c2` captured the exact 94 accepted paths. The feature
branch then merged main `2b1781b12686b5e5baf30897a7094669b03a7fd5` (PR #14) without conflicts.
The merge preserved Pass 1G’s production rooms, legacy fixture, templates, authoring tools and tests.
Pass 1I’s runtime, scene, prefab, test and metadata bytes remain as accepted. Only this report and
the authoring guide were updated for the handoff.

| Integrated verification | Result |
|---|---|
| Complete PlayMode run 1 | 328/328, zero failures or skips |
| Complete PlayMode run 2 | 328/328, zero failures or skips |
| Complete Editor run 1 | 23/23, zero failures or skips |
| Complete Editor run 2 | 23/23, zero failures or skips |
| Fresh Windows development build | Succeeded, zero errors and warnings |
| Mechanics standalone | Passed, exit code 0 |
| Official production-room standalone | Passed, exit code 0 |

The integrated suites include all 54 Pass 1I PlayMode cases, 16 Pass 1G production-room cases,
21 Credits cases, and 14 Pass 1G Editor cases. Pass 1G’s renamed legacy route case and the new
official Level01 route both pass. The production executable check confirmed Main Menu → New Game,
one player and bank, the stationary 32×18 camera at four output sizes, the physical exit route,
HUD accuracy, mid-dash Pause/Resume, zero-travel final-charge failure, Restart and menu return.
The mechanics check confirmed equal D and W+D travel, bank-preserving environmental failure,
platform riding and departure, turret hits, wall destruction, and safe/lethal crusher surfaces.
The standalone runs used real physics and scripted inputs; they add no physical hardware claim.

The PR comparison still comprises **94 paths: 87 additions, seven modifications, no deletions**.
It excludes production scenes, shared input data, existing player/bank prefabs, credits and artwork.
The GUID/reference audit found no missing metadata, duplicate asset GUIDs or unresolved candidate
scene/prefab references. The three excluded local files were preserved byte-for-byte after testing.
Raw reports, standalone traces and captures, and the preservation audit are retained under ignored
`Logs/UnifiedGameplay/Publication/`.
