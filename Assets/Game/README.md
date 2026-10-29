# Production foundation — Pass 1D review

## Git and preservation

Implementation is isolated in `ProductionFoundation` on `feature/production-foundation`.
Starting HEAD: `10be1a42bcfd809533754c59e71aaa952510bab1` (accepted main).
Integrated art: `464475913b7f6f5a9df4cae834140b4388bd5387` by AceofHome.
Art was integrated with an uncommitted merge. MERGE_HEAD remains pending review;
no new commit, push, pull request, or merge into main was performed.
The original Game-Jam-Oct-26 checkout and its two local editor files were preserved.

## Asset layout and migration

| Earlier location | Production/review location |
| --- | --- |
| Pass1A/Runtime | Game/Runtime/Movement |
| Pass1C/Runtime | Game/Runtime/Resources |
| Pass1A/Tests | Game/Tests/Movement |
| Pass1B/Tests | Game/Tests/CardinalDash |
| Pass1C/Tests | Game/Tests/Resources |
| Pass1C/Editor | Game/Editor/ResourceVerification |
| Remaining Pass1A assets | Game/Tests/Fixtures/Movement |
| Remaining Pass1B assets | Game/Tests/Fixtures/CardinalDash |
| Remaining Pass1C assets | Game/Tests/Fixtures/Resources |

Paths above are beneath Assets. Unity AssetDatabase moved every original asset and
its metadata; 51 migrated metadata GUIDs were checked and unchanged. All accepted
prototype players, scenes, materials, sprites, and physics materials remain as test
fixtures. Assembly names and dependencies are retained. Test/tool path literals were
updated. Cleanup/removal of fixtures is not authorized or performed.

The sole production player is `Prefabs/Player.prefab`, based on the accepted banked
player. The fixture prefabs remain regression inputs, not production player choices.
New runtime code is in Levels and Presentation; production verification tests are
in Tests/Production. Editor/Foundation contains reproducible migration/content/build
commands. Content generation refuses to overwrite already-created production assets.

## Gameplay and presentation

Physics remain 6 units/s movement, 10 units/s jump, gravity 3, continuous collision,
0.8×1.6 collider, unit root scale; dash remains 18 units/s for 0.16 s. No eligibility,
resource, final-charge, or collision rules were retuned. The input adapter adds a read-only Activity event for presentation, remaps the
keyboard on its private action copy, and resolves simultaneous Jump/Dash intents.
Accepted movement, dash, chaining and bank controller logic remains unchanged.

Visuals live on a child at scale 5, giving the 100 PPU character an effective 20 PPU
grid and approximately 1.5-unit visible height. Root motion is disabled. Ace's clips
bind SpriteRenderer.m_Sprite at an empty path, so Animator and renderer share that
child. No walking/jumping artwork was invented; Idle is the available fallback.

The presentation clock requests Look after seven seconds of grounded inactivity.
It stops Look after one authored clip duration even though the source clip loops.
Activity immediately resets it; another Look requires a fresh inactivity period.
Airborne/dashing/disabled control, pause, lost focus and scene initialization reset
the timer. This interruption/repetition policy remains provisional.

Authored PNG pixels, clip/controller assets and sprite subasset IDs are preserved.
All four PNGs were hydrated and their SHA-256 values matched Git LFS pointers.
Background remains 12 PPU, floor 20 PPU, character 100 PPU: they were not normalized.
Only the two tiled environment importers change Tight mesh to Full Rect, required
by tiled rendering. Point filtering, no compression and no mipmaps are retained.
The preview scene's stale initial sprite reference is repaired to existing Idle_1;
its composition, Animator and animation assets are preserved.

Camera uses the existing URP PixelPerfectCamera: 20 effective PPU, 320×180 reference,
integer render-texture upscale and windowboxing. HDR, camera MSAA, dynamic resolution
and post-processing are off on this camera. Global quality/pipeline settings are not
changed. The mixed-scale background is tiled independently and sorted behind solids.

## Level framework

Open `Scenes/Levels/Level01.unity`. Move with A/D or Left/Right; jump with W or Up; dash with Space. Aim with WASD or arrows. Gamepad: left stick, south button to jump, right shoulder to dash.
The cyan translucent marker at the right is a verification exit, not final victory art.
Level01 provides a practice step and a taller barrier for jump/up-dash traversal.

LevelSetup creates one dedicated persistent bank root if absent, then spawns the
production player and binds the camera. LevelCamera2D follows inside horizontal
bounds. Static platforms separate collision size from tiled presentation. When
authoring different platform sizes, keep BoxCollider2D.size and child renderer.size
aligned, as the supplied prefab/builder do. Background assignment is scene-local.

LevelExit accepts only a bank-linked player. LevelTransition validates a registered
destination, permits one transition, freezes departing controls, and loads it once.
It never initializes a new run or spends/refills resources. Failed players cannot exit.
The lightweight VerificationDestination scene enables chaining and preserves the same
bank. It has no onward exit or victory system. Both scenes are registered alongside
SampleScene; the standalone builder explicitly starts at Level01.

Only blue industrial art is integrated. Yellow-green and orange/natural batches,
Levels 2–15, checkpoints, retry/full-run flow, final charge tuning, UI and victory
presentation remain future work. The bank's starting 5 charges remain provisional.

## Verification and developer review

Historical evidence: two 80/80 runs from October 22. These are not new results.
Fresh pre-migration run on October 28: 80/80.
Post-migration intermediate runs passed 99/99 twice. Final runs, after adding the
physical traversal check and repairing the preview's initial reference, passed
**100/100 twice**, zero failures or skips: 15 movement + 40 cardinal/chaining +
25 resource + 20 production tests. Reports are `Logs/Pass1D/final-1.xml` and
`final-2.xml`. Initial content-generation errors (TextureImporterSettings API and
child-renderer lookup) were corrected before the successful runs; no regression
expectations or movement physics were changed to obtain these results.
The 80 retained tests plus production tests cover animation sprite playback, timer
policy, physics independence, pixel imports, spawning, exits, persistence and fatal
failure. The route test traverses Level01 with one nonfatal dash.

`level01.png` is rendered by Unity's URP camera as test evidence, not an art replacement.
Windows development build: `Builds/Pass1D/GameJam.exe`; full build folder is required.
Build result: **Succeeded, zero errors** (`Logs/Pass1D/build-summary.txt`).
No build artifacts or logs should be committed. The development build starts and
initializes Unity/input/graphics. Native window capture timed out; automated frame
captures do not substitute for interactive performance diagnosis.
The source preview also names a custom sorting-layer ID absent from main's
TagManager; its authored ID was preserved. Production uses explicit Default-layer
sorting orders, so it does not depend on that missing layer definition. Confirm the
source preview's intended layer naming with Ace before further preview editing.

Developer checks:
1. Open Level01 in Unity 6000.3.26f1, focus Game view and enter Play Mode.
2. Walk, jump onto the practice step, then jump/up-dash over the taller barrier.
3. Wait seven seconds grounded: Look should play once, return to Idle, then stay Idle
   until renewed activity. Interrupt Look with movement/Jump/Dash; repeat after focus
   loss, pause, airborne movement and game over. Root physics must remain unchanged.
4. Check floor alignment, repeating seams, character proportions, sorting and camera
   framing at 16:9 and a non-integer-fit window size.
5. Reach the exit. Verify exactly one bank and the same remaining charges in the
   verification destination; chaining there must still spend each charge.
6. On a fresh run spend the final charge. Confirm no fatal dash travel or successful
   exit. Stop/start Play twice with domain reload disabled.
7. Run all four GameJam PlayMode suites twice. Then test the development executable.

Recommended next milestone: developer acceptance of this one-level slice, then a
reviewed level/theme template and cumulative dash budget before authoring Levels 2–15.

## Playtest revisions: controls and surface readability

Jump and dash presses are collected for the same input update; Dash takes priority
when both occur, even if that dash is blocked or ineligible. Up/W remain vertical
aim inputs. Holding jump produces one press. The adapter remaps a private copy of
the shared input actions; gamepad bindings remain. Sprint is disabled by this adapter.
Inputs arriving in different updates remain separate actions.

The production floor, boundary walls, practice step, and dash barrier use a dark
backing and one-pixel foreground edges inside unchanged collider bounds. The reusable
IndustrialPlatform prefab includes the same trim. After resizing a platform, match
its Floor visuals renderer size to its collider, select the solid, and use Game Jam >
Refresh Selected Surface Borders. This is an explicit opt-in for intended gameplay
surfaces. Ace's industrial background remains unchanged; optional concealed passages
are a deferred concept and are not implemented here.

FrameTimingProbe is an opt-in development-only diagnostic; normal play never starts
it. Verification evidence is stored under Logs/ReadabilityControls.
## Accepted R1 verification and publication boundary

Final R1 Play Mode results: **121/121 twice**, no failures or skips, covering the
retained 100 tests plus 17 controls/resource checks and 4 surface/framework checks.
Evidence: `Logs/ReadabilityControls/regression-1.xml` and `regression-2.xml`.
The fixture instruction labels received a text-only correction after these runs.
No gameplay code or production scene was changed by that correction. The Windows
development build succeeded with zero reported errors. These results were inspected
during publication preparation; this preparation did not rerun Unity validation.

Bounded timing capture: approximately 30 measured seconds in each environment.
The Editor capture was unfocused; the standalone capture used a hidden window.
One 61.4 ms Editor frame and one 38.3 ms standalone frame occurred; neither capture
recorded a generation-zero garbage collection. Render-thread/Animator marker data
was unavailable. No cause for interactive stutter was established and no timing
or optimization change was made. Use the visible player and Unity CPU Timeline
when reproducing the reported intermittent stutter.

Exclude IDE/solution files, incidental pipeline/volume/scene-template serialization,
logs and builds from publication. Include EditorBuildSettings scene registration.
The temporary verification runner was removed. FrameTimingProbe is a retained,
opt-in development diagnostic and never starts during ordinary gameplay.
