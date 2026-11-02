# Game Over and Restart Run review

Developer decision: PASS-1F-B-GAMEOVER-ACCEPTED. Devin Padin completed all 6/6 manual
acceptance checks and authorized publication for maintainer review. Final-charge
behavior, Restart Run, Return to Main Menu, physical gamepad input and repeatability
were verified by the developer.

## Baseline and dependency

Worktree GameOverFoundation; branch feature/game-over-flow. PR #7 was open at
preflight, so the branch starts at its exact accepted menu commit
c270a0b38f325aade6425014ba952e3231eb985d. Remote main was
961b5ff90f48fd18fd17c69c959651d60c912e77. The change depends on the accepted menu
implementation; publication/integration must account for that dependency later.
No existing worktree is edited. PR #7 remains open at publication preflight, with
the same accepted menu commit and remote main. This dependent PR targets
feature/ui-foundation so its review diff contains only Game Over changes. PR #7 is
not modified; maintainer review and merge decisions remain separate.

## Integration contract

GameOverSceneBootstrap subscribes once to sceneLoaded, clearing that subscription
at SubsystemRegistration for Play sessions with domain reload disabled. It installs
one scene-owned Resources/UI/GameOver prefab when a loaded scene contains LevelSetup.
MainMenu and old prototype fixtures without LevelSetup are deliberately excluded.
No scene-name checks select where the failure logic works. No accepted level scene,
LevelSetup script, player prefab or bank transaction implementation is changed.

The presenter is scoped to gameplay scenes and is destroyed when they unload. It
binds to the authoritative bank, handles already-failed state, and unsubscribes on
disable. Installation is idempotent. Future single-scene level templates using the
accepted LevelSetup inherit the behavior automatically. The prefab root should be
active; its overlay and input EventSystem are authored inactive until failure.
Production Level01, VerificationDestination and the 32x18 room use the same hook.

The resource GameOver callback only marks presentation pending. The next Update
shows the overlay; no reset, navigation or UI activation occurs inside the resource
transaction. The accepted final-charge freeze and zero-travel behavior stay owned
by the unchanged RunDashBank, PlayerDashBankLink and movement controllers.

## Restart and menu behavior

Restart Run delegates to the unchanged NewGameLauncher on the presentation prefab.
It validates the registered destination, rejects duplicate/reentrant requests and
uses InitializeNewRun on the existing bank. The provisional allocation remains 5.
The old departing player is kept frozen after reset, while the newly loaded level
initializes its normal replacement. Exactly one persistent bank remains.

Both menu New Game and failure Restart use the same accepted implementation. Their
serialized gameplayScene destinations should be updated together when the team's
official first level is approved. For now both point at production Level01, not
the 32x18 demonstrator. Restart means the entire run; no checkpoint/per-level refund
or Continue behavior exists.

Return to Main Menu validates its own serialized scene path, locks navigation and
loads the accepted menu. It neither resets nor creates a bank. The failed balance
remains zero until the user selects New Game in the existing menu.

Invalid destinations show an editable message, keep the failed state and leave
buttons available for another choice. Both actions share one navigation lock.

## Input and presentation

The overlay uses the accepted MenuButton prefab and TMP resources, Canvas scaling
at 1280x720 and sorting order 100 above the world. Text, colors, failure illustration,
background and research-status graphic placeholders are editable; an empty optional
animation slot is provided without creating any artwork or animation system.

The underlying scene remains visible. Environment time is not globally paused;
the failed player is frozen by the existing resource link. No general Pause or HUD
system is introduced.

On failure the presenter suspends any other active EventSystem/input scope, activates
its own EventSystem and focuses Restart Run. Its buttons and UI action scope arm
only after held keyboard dash/submit/navigation, gamepad buttons/sticks/d-pad and
mouse-left input release. This prevents the fatal input from confirming a restart.
The status prompt explains the release gate. A fresh click/Submit is required.
Escape and gamepad Cancel have no dismiss/resume action.

Only a private UI action copy is enabled, through the unchanged MenuInputScope.
The presenter cleans up its scope, listeners and objects on scene transitions and
restores suspended UI owners if they still exist. New gameplay gets normal player
input from the accepted prefab/bootstrap.

## Build and manual review

Use Game Jam > UI > Build Game Over Windows Development Player. The build keeps the
accepted main menu first and preserves its enabled gameplay/verification scenes.
Open levels directly in the Editor as usual; no separate failure scene is needed.

Manual checks: enter Level01 from New Game, spend the final allocation, confirm no
fatal dash travel, release controls, navigate with mouse/keyboard/physical gamepad,
and confirm Cancel cannot resume Jett. Restart Run should restore 5 and load the
beginning; Return should show a usable menu while preserving 0 until New Game.
Repeat failure/restart cycles and inspect the Inspector bank if needed (no dash HUD
is implemented). Review the overlay against the underlying room and replaceable art
slots. Check direct play in the destination and experimental room as well.

GameOverDevelopmentCheck runs only under an explicit -gameover-check output-folder
argument in an Editor/development compilation. It generates automated screenshots
and balance/flow evidence; it adds no normal gameplay or save-system behavior.

## Executed verification

Unity 6000.3.26f1: the complete suite passed 174/174 twice against the final runtime
sources (verified-1.xml and verified-2.xml), with zero failures or skips. This retains
all 150 accepted tests and adds 24 focused Game Over tests. Each run used a separate
Editor process with the inherited domain-reload-disabled setting. Repeated recovery
cycles, scene compatibility, input ownership and subscription cleanup are also tested.

The initial run passed 173/174. A new test fixture retained a removed virtual gamepad
between tests; clearing its device references in teardown fixed that test helper.
No accepted gameplay code or regression expectation was changed to resolve it. A
corrected pair also passed before the final edit-mode bootstrap guard was added;
the final pair above verifies that guard as well.

The Windows development build succeeded with zero reported build errors. The
visible 1280x720 standalone application then completed an automated flow using the
actual dash request and UI button listeners: fatal failure, Restart Run, another
failure, Return to Main Menu, New Game, and the accepted Quit action. Exit code was 0.
The fatal attempt had zero travel and the failed player remained frozen. Restart
reused the same bank, left exactly one bank, unloaded the old UI and initialized a
normal replacement player. These are executed programmatic checks, not developer
hands-on gameplay acceptance or physical-controller verification.

Observed balances (starting allocation 5 remains provisional):

| Phase | Remaining | Failed |
| --- | ---: | --- |
| First run | 5 | No |
| Before final dash | 1 | No |
| Failed run | 0 | Yes |
| Restart Run | 5 | No |
| Return after a second failure | 0 | Yes |
| New Game after returning | 5 | No |

Local evidence is retained under ignored Logs/GameOver: verified-1.xml,
verified-2.xml, verified-build.log, build-summary.txt and Standalone/checks.txt,
balances.txt, complete.txt, game-over.png, restarted-run.png and
returned-main-menu.png. The three images were visually reviewed. No managed runtime
exceptions were found in the standalone log; it contains graphics diagnostic and
unreachable Unity online-service messages in this restricted network environment.

The audit retains all 353 baseline files; 352 are byte-identical and the sole
intentional edit is GameJam.UI.asmdef, adding references to the existing Levels and
Movement assemblies. Unity's incidental settings serialization was backed up under
Logs/GameOver/Incidental and restored to its exact initial bytes in this new worktree.
Build-generated shader-prefilter/runtime-settings caches are not publication changes.
Generated solution/scene-template files are retained locally and excluded from the
implementation inventory. No existing worktree, accepted fixture, art, scene, player
physics, bank transaction, shared menu component or build-scene registration changed.

Developer manual review is complete (6/6 acceptance checks). Future integration
still requires visual review in new official level layouts and supported display
configurations. The current integration targets ordinary single-scene levels, not
a persistent additive UI shell. Final artwork/animation, the five-charge allocation
and the official starting level remain team decisions.

Publication rechecked all 22 accepted candidate hashes and the saved validation
evidence; it did not rerun Unity or modify gameplay. Only this documentation's
acceptance/publication record was updated afterward. Generated files, logs, builds,
caches and unrelated IDE changes are excluded. Publication is authorized; merging
and additional development are not.
