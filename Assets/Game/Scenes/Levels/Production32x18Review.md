# Pass 1G — Production 32×18 integration review

Target: **PASS-1G-32X18-INTEGRATION-CANDIDATE**. Developer acceptance is pending.
Implementation and validation only; no commit, push, PR or merge is authorized.

## Baseline and scope

- Worktree: `Production32x18`; branch: `feature/32x18-production-integration`.
- Exact starting SHA: `038ec69d35ab5c7dc42c716cef989ab6cddac28a` from fetched origin/main.
- Main includes the accepted production foundation, PR #6 experiment and Main Menu,
  Game Over, Pause and Mobility Reserve HUD dependency chain.
- PR #11 was merged into its menu parent branch but is not in this mainline.
  No Credits content was imported or changed.
- No new committed teammate level work was found during preflight. Other branches,
  worktrees and unrelated local changes are preserved.

## Implementation

Official Level01 remains `Assets/Game/Scenes/Levels/Level01.unity`, with scene GUID
`54cd109dfa8beb54088109b65f2d16b5` preserved. It now uses the accepted experiment's
32×18 whole-cell geometry and one-dash demonstration route. Perimeter solids stay
inside X0..32/Y0..18. Three steps lead to a jump/upward-dash over the barrier and
a drop to the existing LevelExit. Layout tuning is provisional, replaceable by Worm.

`LevelTemplate32x18.unity` is the canonical empty-puzzle template. It contains
LevelSetup, its spawn/player/bank references, SceneDashSettings, LevelTransition,
an assigned exit trigger, camera, perimeter and separately grouped background.
Its transition destination is deliberately empty. `LevelExample32x18.unity` is an
independent template copy for review, not a finished additional production level.
Neither is registered in the playable build list.

The stationary camera is (16,9,-10), orthographic size 9, URP PixelPerfectCamera,
20 assets PPU, 640×360 reference, UpscaleRenderTexture and Windowbox. Local HDR,
MSAA, dynamic resolution and post-processing are off. There is no follow component;
the existing LevelSetup.levelCamera is intentionally null. No global render,
quality, physics or input settings are changed.

Production owns its `RoomGrid32x18` guide and Editor tooling; production scenes,
prefab and tools have no experimental asset/assembly dependencies. The guide only
draws Editor gizmos and never changes transforms or runs gameplay updates.

`GridPlatform32x18.prefab` is a new production counterpart of the accepted grid
platform: static BoxCollider2D, tiled industrial art, dark backing and one-pixel
foreground edges. Explicit resize/snap tools update these together, record prefab
overrides and support Undo. No runtime platform snapping or player scaling is added.

**Game Jam > Levels > Create 32x18 Level** validates names/paths, rejects existing
scenes or metadata, copies the template with AssetDatabase.CopyAsset, checks the
new GUID and opens the copy after preserving dirty scene edits. The registration
command explicitly appends/enables completed levels while preserving MainMenu first
and existing order. Templates/review examples are refused. No registration changes
were needed for this pass because Level01's path/GUID remain the accepted ones.

LevelSetup remains the only spawner. RunDashBank remains the one persistent resource
owner. LevelExit/LevelTransition, scene chaining, New Game, Restart, Pause and HUD
are reused without gameplay code or prefab changes. The current Level01 exit still
leads to the unchanged VerificationDestination, not a new Level02 or victory flow.

## Existing files changed and coverage retained

| Existing file | Reason |
|---|---|
| `Assets/Game/Scenes/Levels/Level01.unity` | Convert the provisional layout/camera to the official room contract; preserve scene metadata. |
| `Assets/Game/Tests/Production/FoundationTests.cs` | Keep the old physical scrolling-route test against its new archival fixture; other foundation tests still exercise current Level01. |
| `Assets/Game/Tests/Production/SurfaceReadabilityTests.cs` | Preserve the old five-solid/background assertions against that fixture; new tests cover all nine production solids. |
| `Assets/Game/Tests/CardinalDash/PlayMode/CardinalDashTests.cs` | Synchronize the existing gamepad binding test with input Update then FixedUpdate instead of an arbitrary 60 ms wait; both original assertions retained. |

`Assets/Game/Tests/Fixtures/Production32x18/LegacyLevel01.unity` is a Unity-safe copy
of the old scene with a new scene GUID and byte-identical scene content. Its actual
jump/dash route and original surface geometry assertions remain active tests.
The original experimental scene, its seven tests, and all earlier fixtures remain.
No accepted movement, dash, input, bank, presentation, UI or animation code changed.

## Validation history

The untouched 237-test mainline run passed **236/237**, failing
`GamepadRightShoulderUsesCardinalStickDirection` at its IsDashing assertion.
The unchanged isolated rerun also failed. A temporary read-only trace later passed
and showed the requested upward dash; the trace does not conclusively establish
every earlier failure's cause. A native-input isolation trial found zero native
devices and still failed; that helper was removed as ineffective.

The first integrated run passed **251/253**: the same legacy check plus a new test
that compared SpriteRenderer depth against a 2D collider. The new bounds assertion
was corrected to compare XY. The initial Editor run passed **12/14**; two new tests
tried to create an additive scene while Unity held an unsaved untitled scene.
Their fixture setup now opens the template additively and discards only test edits.
The corrected Editor suite passed **14/14**.

A subsequent full run passed **252/253**, failing only the legacy gamepad timing
check. Its wait was then changed to an input frame followed by a physics tick,
retaining the IsDashing and upward-direction assertions. Gameplay code, tuning,
transaction rules and input bindings were not changed. Temporary diagnostic sources
and metadata were removed. Final results and evidence are recorded below.

The focused additions cover the room/camera contract, all solids/foreground bounds,
unchanged production player parameters, one-player/one-bank spawning, actual physical
route and trigger traversal, persistence, fatal zero-travel behavior, menu/start,
restart, mid-dash Pause/Resume, HUD, template entry, example references, repeated scene
entry, safe copying/GUIDs, overwrite refusal, registration policy and platform Undo.

The opt-in `ProductionRoomDevelopmentCheck` runs only in Editor/development builds
with `-production-room-check <output-folder>`. It records actual standalone output
sizes/screenshots and Rigidbody2D route positions, then checks transitions, charges,
Pause, HUD, final-charge failure, Restart and return to menu. It has no ordinary-play
behavior. Automated virtual keyboard/API checks are not developer manual acceptance.

## Final executed results

| Verification | Result | Local evidence under `Logs/Production32x18/` |
|---|---|---|
| Final-source full PlayMode run 1 | 253 passed / 0 failed / 0 skipped | `verified-play-3.xml` |
| Final-source full PlayMode run 2 | 253 passed / 0 failed / 0 skipped | `verified-play-4.xml` |
| Editor authoring run 1 | 14 passed / 0 failed / 0 skipped | `edit-corrected-1.xml` |
| Editor authoring run 2 | 14 passed / 0 failed / 0 skipped | `edit-corrected-2.xml` |
| Windows development build | Succeeded, 0 build errors | `build-final.log`, `build-summary.txt` |
| Final standalone verification | Exit code 0; 86 PASS records, no FAIL records | `standalone-final-exit.txt`, `StandaloneFinal/checks.txt`, `StandaloneFinal/complete.txt` |

All 237 mainline PlayMode cases are retained (the old scrolling route was renamed
to describe its fixture), plus 16 new PlayMode cases. The 14 Editor cases are new.
Earlier complete passing PlayMode runs 1/2 are also retained; runs 3/4 compile the
final standalone-helper source. Domain reload remains disabled in the unchanged
EditorSettings. These are separate batch Editor processes; same-Editor repeated
Play and physical-device feel still require the developer checklist below.

The first standalone attempt captured all four outputs but failed its pause-position
check. The helper sampled during a fixed-step continuation; sampling after a completed
frame corrected the verification. A second attempt timed out on simulated menu input.
The helper now waits explicitly for application focus before input/output checks.
The final attempt passed without modifying gameplay or UI implementation. Its pause
trace records timeScale 0, identical position (19.58,6.13), an active dash, and four
charges before/after the pause. Earlier failed attempts remain in `Standalone/` and
`StandaloneVerified/`; final evidence is in `StandaloneFinal/`.

The final standalone uses a virtual keyboard for New Game and existing request/button
APIs thereafter. Actual Rigidbody2D traversal, collision and trigger loading are used;
the route never teleports the player. It recorded 206 physics samples, reached X28.58,
peaked at Y8.65 and spent one dash. The actual exit retained the same bank at four.
Landing between subsequent actual dashes never refilled it; the final charge froze
the actor with zero travel, Game Over appeared, and Restart returned to current
Level01 with five charges. HUD, mid-dash Pause/Resume and menu return passed.

| Actual standalone output | Integer scale | Gameplay viewport |
|---|---|---|
| 1280×720 | 2 | 1280×720, origin (0,0) |
| 1920×1080 | 3 | 1920×1080, origin (0,0) |
| 2560×1440 | 4 | 2560×1440, origin (0,0) |
| 1366×900 | 2 | 1280×720, origin (43,90), black bars |

Every measured camera viewport spans exactly (0,0)..(32,18). PNG headers confirm
the actual requested image dimensions. All four room outputs plus Pause, route,
destination, Game Over and Restart screenshots were visually inspected. The final
log has no managed runtime exceptions; Unity emits a D3D12 debug-info query diagnostic
and a native ComputeBuffer cleanup warning at exit. Those engine diagnostics are
not resolved or attributed to a gameplay cause in this pass.

Build processors serialized five incidental settings files; each was restored to
its exact preflight bytes, with generated versions backed up under ignored
`Logs/Production32x18/Incidental/`. Transient performance-test Resources JSON files
were generated/removed by Unity's build pipeline and are not publication content.
No build, screenshot, log, cache, IDE file or SceneTemplateSettings is intended for
publication. Developer gameplay acceptance remains pending.

Unity's actual Scene view saved `Logs/Production32x18/editor-grid.png` and the
loaded hierarchy was exported to `editor-hierarchy.txt`. The Windows full-window
capture helper failed with `FrameArrived timed out` and `window capture timed out`;
hierarchy-pane clicking was unavailable (`coordinate input geometry is unavailable`).
There is **no full Editor hierarchy screenshot**. Capture that during developer review;
the grid image is a real Unity render, not a reconstructed Editor screenshot.
The review Editor also logged a Unity Connect project-service HTTP 403. No C#
compilation or missing-reference error was found, and no licensing/account or
connection settings were changed to address the service error. The saved review
Editor was closed after capture; other worktrees/Editors were not touched.

## Preservation and limitations

- Keep all Ace sprite/animation GUIDs, source imports and accepted player scale.
  Floor PPU20, character source PPU100 with visual scale5, background PPU12 remain.
  Windowboxing preserves integer output scale; mixed background PPU remains an
  inherited source-pixel mapping compromise. No source resampling was performed.
- All UI runtime/prefab assets, existing bank transactions, movement, dash, input
  bindings, verification destination, experiment and historical fixtures are kept.
- Five starting charges remain provisional: only four dashes succeed before fatal
  exhaustion. The team must allocate a viable cumulative budget across fifteen levels.
- Empty template exits fail closed until assigned an enabled destination scene.
  Final victory, checkpoints and game-over presentation refinements remain separate.
- The upper room is mostly unused; this is a framework/demo route, not final puzzle design.
- The accepted screen-overlay HUD keeps its top-left footprint and can obscure that
  corner of the room. Account for it during puzzle layout; moving it is a separate UI decision.
- Worm's A/D + W/Up horizontal slowdown is recorded separately in the production
  guide. Likely normalized Move input is not corrected here. The earlier stutter
  investigation remains inconclusive; this pass does not claim to resolve it.
- No turrets, breakable walls, kill-plane deaths, hidden rooms, new hazards or additional
  finished levels are implemented. Credits/mainline integration remains separate.
- Generated IDE files, SceneTemplateSettings, logs, screenshots, builds and caches
  are excluded from the intended publication inventory. Staging must remain empty.

## Developer manual acceptance checklist

1. Open this worktree in Unity 6000.3.26f1; confirm no compilation/Console errors.
2. Start MainMenu and New Game; verify it loads current Level01 with one player,
   one bank and provisional five-charge HUD.
3. Confirm the whole room is visible and the camera stays fixed while moving.
4. Check A/D or arrows, W/Up jumping, Space/cardinal dash and gamepad controls.
   Keep the separately reported diagonal-input slowdown distinct from this pass.
5. Traverse the ledges and upward-dash barrier; confirm exactly one charge is spent.
6. Pause during a dash, view Controls and resume; verify time, dash and charges recover.
7. Enter the actual exit; verify destination load once and persistent four-charge bank.
8. Spend remaining charges with landings; final charge must show Game Over with no
   fatal dash travel. Confirm Restart returns to converted Level01 with five charges.
9. Inspect room/HUD readability at 1280×720, 1920×1080, 2560×1440 and non-16:9 output.
10. Enter Level01 directly in repeated Play sessions; check owners, UI and state cleanup.
11. Open the canonical template/example and create a disposable named copy through
    the menu; verify independent metadata and preserved prefab/spawn/exit references.
12. Resize/snap a platform, Undo, configure spawn/exit and review build registration
    without changing MainMenu's first position. Confirm Level01 overwrite is refused.

See `32x18LevelProductionGuide.md` for the concise designer workflow. Developer
acceptance and publication remain separate gates.

## Complete intended file inventory

47 paths: 43 additions and 4 modifications. All new assets and folders include Unity metadata; no baseline deletions.

```text
A Assets/Game/Editor/Levels.meta
A Assets/Game/Editor/Levels/CreateLevel32x18Window.cs
A Assets/Game/Editor/Levels/CreateLevel32x18Window.cs.meta
A Assets/Game/Editor/Levels/GameJam.LevelAuthoring.Editor.asmdef
A Assets/Game/Editor/Levels/GameJam.LevelAuthoring.Editor.asmdef.meta
A Assets/Game/Editor/Levels/LevelAuthoring32x18.cs
A Assets/Game/Editor/Levels/LevelAuthoring32x18.cs.meta
A Assets/Game/Editor/Levels/ProductionRoomBuilder.cs
A Assets/Game/Editor/Levels/ProductionRoomBuilder.cs.meta
A Assets/Game/Editor/Levels/ProductionRoomReview.cs
A Assets/Game/Editor/Levels/ProductionRoomReview.cs.meta
A Assets/Game/Editor/Levels/ResizePlatform32x18Window.cs
A Assets/Game/Editor/Levels/ResizePlatform32x18Window.cs.meta
A Assets/Game/Prefabs/GridPlatform32x18.prefab
A Assets/Game/Prefabs/GridPlatform32x18.prefab.meta
A Assets/Game/Runtime/LevelVerification.meta
A Assets/Game/Runtime/LevelVerification/GameJam.LevelVerification.asmdef
A Assets/Game/Runtime/LevelVerification/GameJam.LevelVerification.asmdef.meta
A Assets/Game/Runtime/LevelVerification/ProductionRoomDevelopmentCheck.cs
A Assets/Game/Runtime/LevelVerification/ProductionRoomDevelopmentCheck.cs.meta
A Assets/Game/Runtime/Levels/RoomGrid32x18.cs
A Assets/Game/Runtime/Levels/RoomGrid32x18.cs.meta
A Assets/Game/Scenes/Levels/32x18LevelProductionGuide.md
A Assets/Game/Scenes/Levels/32x18LevelProductionGuide.md.meta
A Assets/Game/Scenes/Levels/LevelExample32x18.unity
A Assets/Game/Scenes/Levels/LevelExample32x18.unity.meta
A Assets/Game/Scenes/Levels/LevelTemplate32x18.unity
A Assets/Game/Scenes/Levels/LevelTemplate32x18.unity.meta
A Assets/Game/Scenes/Levels/Production32x18Review.md
A Assets/Game/Scenes/Levels/Production32x18Review.md.meta
A Assets/Game/Tests/Fixtures/Production32x18.meta
A Assets/Game/Tests/Fixtures/Production32x18/LegacyLevel01.unity
A Assets/Game/Tests/Fixtures/Production32x18/LegacyLevel01.unity.meta
A Assets/Game/Tests/Production32x18.meta
A Assets/Game/Tests/Production32x18/Editor.meta
A Assets/Game/Tests/Production32x18/Editor/GameJam.LevelAuthoring.EditModeTests.asmdef
A Assets/Game/Tests/Production32x18/Editor/GameJam.LevelAuthoring.EditModeTests.asmdef.meta
A Assets/Game/Tests/Production32x18/Editor/LevelAuthoringTests.cs
A Assets/Game/Tests/Production32x18/Editor/LevelAuthoringTests.cs.meta
A Assets/Game/Tests/Production32x18/GameJam.Production32x18.PlayModeTests.asmdef
A Assets/Game/Tests/Production32x18/GameJam.Production32x18.PlayModeTests.asmdef.meta
A Assets/Game/Tests/Production32x18/ProductionRoomTests.cs
A Assets/Game/Tests/Production32x18/ProductionRoomTests.cs.meta
M Assets/Game/Scenes/Levels/Level01.unity
M Assets/Game/Tests/CardinalDash/PlayMode/CardinalDashTests.cs
M Assets/Game/Tests/Production/FoundationTests.cs
M Assets/Game/Tests/Production/SurfaceReadabilityTests.cs
```
