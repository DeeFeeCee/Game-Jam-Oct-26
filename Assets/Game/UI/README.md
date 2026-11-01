# Main menu foundation

Developer decision: **PASS-1F-A-MENU-ACCEPTED**, November 1, 2026. Devin Padin
reported **7/7 manual checks passed**, including physical gamepad navigation.
Publication is approved for maintainer review; merging remains a separate decision.

## Isolation and entry points

Worktree MenuFoundation, branch feature/ui-foundation; clean starting main commit
961b5ff90f48fd18fd17c69c959651d60c912e77. Existing worktrees are preserved.

Open Assets/Game/Scenes/MainMenu.unity. The MainMenu prefab and MenuButton prefab
are under Assets/Game/UI/Prefabs. Runtime UI lives in Runtime/UI, authoring/build
tools in Editor/UI and focused tests in Tests/UI. There is one implementation.

The menu is registered first while preserving existing build scenes. Use Game Jam
> UI > Build Menu Windows Development Player for the menu-enabled development build.
The older foundation/experiment build tools retain their explicit scene lists.
Opening Level01 directly still uses its unchanged LevelSetup bootstrap.

## New Game and resources

NewGameLauncher.gameplayScene is an Inspector-configurable registered scene path.
The provisional destination is Assets/Game/Scenes/Levels/Level01.unity. This is the
current approved production entry, not Worm's future first level and not the merged
single-screen demonstrator. Point the field to the approved first level when ready.
The destination must provide the accepted level/bootstrap integration.

An unavailable path fails before resetting resources. A valid request locks loading
before any callbacks, rejects duplicate/reentrant requests, and calls the existing
bank's InitializeNewRun. If there is no bank, the destination LevelSetup creates it.
The menu never creates a resource manager or modifies charge-spending/fatal rules.
Opening the menu or Controls alone never resets an existing run. There is no Continue,
save-game, checkpoint, retry, pause or HUD system in this pass.

## UI input and view state

One EventSystem uses InputSystemUIInputModule. MenuInputScope owns a private clone
of the existing InputSystem_Actions asset and enables only the UI action map. It
removes callbacks and destroys its references/actions on disable. The shared asset
and gameplay mappings stay unchanged. No static menu state is retained across Play.

Mouse clicks, arrows/WASD and Enter, gamepad stick/d-pad and South navigate/submit.
Controls opens a separate panel and selects Back. Back, Escape or gamepad East/B
returns to the main view and restores selection to Controls. Cancel at the root is
ignored. Explicit vertical navigation does not wrap past Quit; selection alone
never activates it. Quit requires a click or Submit and exits only the standalone
player. In the Editor it logs the request and leaves Unity running.

MainMenuView owns panel visibility/button wiring; NewGameLauncher owns new-run
initialization and navigation. MenuButton and the Canvas can support later UI work
without adding pause/HUD behavior now. A future pause screen must explicitly handle
its own gameplay input ownership; that integration is not implemented here.

## Presentation and replaceable assets

The working title PROJECT: P.I.D.R. is serialized editable TMP text, not final
branding. BackgroundPlaceholder, IllustrationPanelPlaceholder, CharacterIllustrationSlot
and LogoPlaceholder are replaceable Image components in the prefab. Ace's existing
art is neither changed nor required. The simple shapes are provisional UI decoration.

Canvas uses Screen Space Overlay, Scale With Screen Size, reference 1280x720 and
0.5 width/height matching. Anchors and a VerticalLayoutGroup place the main controls.
TMP SDF text is scalable UI typography; it is independent of gameplay pixel-perfect
camera settings. Normal, hover, selected, pressed and disabled button colors are
editable in the shared MenuButton prefab. Decoration/text does not block raycasts.

The existing uGUI package supplies TextMeshPro. Only TMP Essential Resources are
imported through its noninteractive importer; no new packages or examples are added.
Imported resources and their license are retained under UI/TextResources with GUIDs
preserved. Do not reimport duplicate copies or remove the font's license.

## Verification tools and developer checks

MenuDevelopmentCheck exists only in Editor/development compilation and runs only
with the explicit -menu-check output-folder argument. It captures the two menu views,
checks build startup, screen sizes, first-run initialization, existing-bank reset,
and requests the real Quit button. It adds no ordinary gameplay behavior.

After automated verification, manually review the menu and Controls at 720p/1080p
and a 4:3 window. Test mouse hover/click, keyboard navigation/Enter/Escape and gamepad
stick/d-pad/South/East. Confirm Quit requires activation. Start a new game, test the
accepted controls, spend charges, and confirm the original gameplay remains intact.
Run the development executable to verify actual Quit and the temporary Level01
entry. Developer visual/gamepad/hands-on acceptance is separate from automated tests.

## Executed results and limitations

Fresh Play Mode verification passed **150/150 twice**, zero failures/skips:
`Logs/Menu/initial-full.xml` and `Logs/Menu/full-2.xml`. This retains all 128 accepted
movement/dash/resource/production/experiment tests and adds 22 menu tests. The tests
include synthetic keyboard, mouse and gamepad navigation; they do not substitute for
hands-on hardware/visual acceptance. Both runs entered/exited Play Mode with the
inherited domain-reload configuration and repeatedly loaded/unloaded the menu.

Windows development build: **Succeeded, zero reported errors**. Output is
`Builds/Menu/GameJam-Menu.exe` with its complete adjacent folders. In a visible
window the explicit development check verified menu startup, screenshots at 1280x720,
1920x1080 and 1024x768, visible main buttons, Controls/Back, first-run initialization,
menu-without-reset, existing-bank New Game reset, and the real Quit button. The
process exited with code **0**. Evidence is `Logs/Menu/Standalone-visible/checks.txt`,
`quit-requested.txt`, `Logs/Menu/standalone-visible-exit.txt` and build-summary.txt.
The main-menu and Controls screenshots in Standalone-visible were visually inspected.

Initial content generation needed an asynchronous TMP import-completion callback;
the first attempt stopped before creating the scene and was corrected. The first
standalone check used a hidden window: startup succeeded, but capture timed out,
so it exited 1 before completing the flow. The same build passed the visible-window
check. That hidden-window attempt is not claimed as successful validation.

The final preservation audit found only the intended EditorBuildSettings change
among the 240 baseline files. All other 239 files, including gameplay code, both
production scenes, single-screen prototype, art, prefabs, inputs and fixtures, are
byte-identical to the clean baseline. Incidental IDE/Unity serialization changes
were backed up locally and restored only in MenuFoundation. Existing worktrees were
not edited. No duplicate GUIDs, missing new metadata or unresolved new asset GUIDs
were found. TMP's font license and sprite attribution remain with the essentials.

The test/build scripts generate only the new menu content and refuse to overwrite
an existing menu. No one-time helper remains; MenuDevelopmentCheck is retained as an
explicit opt-in development diagnostic. Logs, captures, builds, caches, generated
solution and scene-template settings remain local and are excluded from publication.

Developer manual review is complete: placeholder styling/layout, mouse/keyboard and
physical gamepad navigation, New Game, Controls/Back, standalone Quit, new-run bank
behavior and multiple display resolutions were accepted. Menu return from gameplay,
pause, HUD, official level selection and final assets remain future work.
Publication preparation changed only this acceptance note after verifying the accepted
file hashes and existing test/build evidence; it did not rerun Unity or change behavior.
The published change requires DeeFeeCee's review and a separate merge decision.
