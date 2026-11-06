# Blue environment and research terminal integration

Target: **BLUE-ART-TERMINAL-INTEGRATION-CANDIDATE**, pending developer hands-on acceptance.

Work is isolated on `feature/blue-art-research-terminal`, based on main `d5694f569de90113f0a7bcd71d96e68bcf55c98e`. Source art: AceofHome commit `1ab8f22556336cd34361920bc5e49403ad966dd7`. Nothing is committed, pushed, proposed as a PR, or merged.

## Review entry points

- Open `Assets/Game/Scenes/Verification/BlueArtTerminal.unity` in Unity 6000.3.26f1. This scene is isolated from production levels.
- Reusable prefabs: `Assets/Game/Prefabs/BlueEnvironment/` (entrance, exit, terminal, four platform variants).
- Windows candidate: `Builds/BlueArtTerminal/ProjectPIDR-BlueCandidate.exe` when build verification is complete. It starts in the isolated room; production build settings are unchanged.
- In the room, walk right from the spawn to the terminal at X=8. Press **E** or **gamepad west/X** to open. Close with the button, E/X, Escape, gamepad east/B, or Start. Release held controls before the interface or gameplay regains input.

## Authoring

Entrance is visual only: align its base with the floor beneath `LevelSetup.spawn`. It does not spawn another actor or perform transitions. The complete door image includes its sign because Ace's entrance and exit importers use Single Sprite mode despite retaining old slice records.

Exit has a trigger and the existing `LevelExit` / `LevelTransition` components. Assign a registered destination scene on its `LevelTransition`. The reusable prefab intentionally has no destination; the verification instance points at the existing verification destination. When replacing a level exit, preserve its world collider bounds and adjust only the Door visual as needed.

Terminal lore is intentionally empty. Edit the **Lore - edit TMP text** child inside the terminal prefab or a scene instance. It is the referenced TextMeshPro component, with editable font, size, wrapping, and text. Keep content within the visible panel; overflow is truncated. No story text or art assignments were added. The Close button is wired by the terminal component at runtime. Each terminal has its own initially inactive UI; only one terminal may own interaction at a time.

The original five PNGs, their metadata, `TerminalBlink.controller`, and `Terminal_Blinking.anim` are preserved. Original `Assets/Art/BlueBG.png` and `BlueLvlGround-newt.png` and their metadata stay untouched. The duplicate background/ground copies from Ace's art branch are excluded.

Blue artwork remains imported at **100 PPU**; visual children use **5× scale** for the production **20 effective PPU** grid. Platform roots and colliders retain unit scale and existing geometry. Full-rect Sprite assets in `Assets/Game/Art/BluePlatformAdapters/` reference the original platform texture; they avoid Unity's tight-mesh tiling warning without changing its importer or duplicating the PNG. Tiling/cropping preserves pixel dimensions and places the top of the artwork at the collider top. Existing surface backing and borders still indicate the complete solid geometry. Four variants are available for designer review; their edge shapes are not automatically inferred as terrain connections.

The existing **Game Jam > Levels > Resize Selected Platform** operation also fits an attached blue visual. Its collider behavior is unchanged. The original generic grid platform prefab and all production scenes remain unchanged.

## Safeguards

Opening the terminal freezes simulation and disables only player input, preserving movement state and dash charges. A neutral-input gate applies on entry and close. Escape used to close cannot also open Pause that frame. Pause blocks terminal opening, terminal ownership blocks Pause, and Game Over takes priority. Focus loss disarms UI until focus returns and controls release. Disable/unload releases terminal time/input ownership. Interaction is rejected during an active level transition.

## Verification

Evidence is under `Logs/BlueIntegration/` (ignored local artifacts): editor/play-mode NUnit reports, logs, preservation report, standalone results, and offscreen authoring previews. The preservation audit is rerunnable with `node Tools/verify-blue-integration.cjs` from the project root.

- Editor suite: **26/26 passed** (`editmode.xml`).
- Final complete play-mode suite: **334/334 passed** (`playmode-final.xml`). The first run exposed a test-cleanup input-settings lifetime issue; the cleanup was fixed and the entire suite rerun successfully.
- Windows development build: **Succeeded, zero build errors** (`Builds/BlueArtTerminal/build-summary.txt`). Unity logged an ignored licensing-client validation diagnostic before resolving entitlement; compilation, tests, and the build completed.
- Standalone Windows smoke check: **PASS** for open/close, time/input restoration, and charge preservation (`standalone/result.txt`).
- Visual review: offscreen Unity renders of the room, blank terminal, temporary text fixture, and narrow viewport are in `previews/`. Temporary fixture text was not saved in any scene/prefab/build. Hidden-window screen capture was blank and is not visual evidence; actual windowed presentation remains part of developer hands-on acceptance.
- No duplicate asset GUIDs; all original tracked files are preserved except three intentional one-line integration hooks in pause, game over, and platform resize. Imported source files and metadata match Ace's commit.

Developer hands-on acceptance should cover:

1. Entrance alignment, exit art and trigger alignment, and successful transition from the verification room.
2. Terminal blink, keyboard/gamepad opening, mouse/button closing, and editable TMP text at 16:9 and a narrow window size.
3. Hold movement/dash/confirm while opening and closing; check no new dash spend or stuck input. Check focus loss, Pause, Game Over, restart, and scene unload.
4. Inspect all four platform variants and resize a disposable instance to verify collider bounds and art alignment. Decide whether the repeated/cropped edge motifs are visually acceptable before placing them in production levels.

Do not publish or merge until explicitly authorized after hands-on acceptance.
