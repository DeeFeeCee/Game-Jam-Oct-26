# Pause and Resume foundation

Developer decision: PASS-1F-C-PAUSE-ACCEPTED. Devin Padin completed all 10/10 manual
acceptance checks and authorized publication for maintainer review. Mid-dash pause
and resume, resource preservation, gamepad input, Game Over priority, direct scene
entry and repeated navigation were confirmed.

This isolated PauseFoundation worktree, feature/pause-foundation, starts at accepted
Game Over commit 10a58bb260694d04023eb6e5df054274b4b0e31a. At preflight PRs #7 and #8
were published but unmerged; this work depends on both. Remote main was
961b5ff90f48fd18fd17c69c959651d60c912e77. Existing worktrees remain untouched.

Publication preflight confirmed both PRs remain open at their accepted commits and
remote main is unchanged. This dependent PR targets feature/game-over-flow so its
review diff contains only Pause changes. PRs #7 and #8 are not modified; integration
and merge decisions remain with the maintainer.

PauseSceneBootstrap installs one scene-owned Resources/UI/Pause prefab for loaded
single-scene levels containing accepted LevelSetup. Menu and old prototype fixtures
are excluded. Static scene subscriptions reset at SubsystemRegistration, as does
Time.timeScale, supporting domain-reload-disabled Play sessions. No scene edits,
packages, input-asset changes or global persistent pause manager are needed.

Escape and gamepad Start/Menu use a private Button action. PauseController owns the
modal state: active gameplay -> paused root/controls -> resumed input-release gate,
or menu navigation. Duplicate requests are rejected. The accepted Game Over Show
method has one minimal integration call to release Pause first; failure is also
checked before Pause actions every Update. No new resource event subscriptions are
introduced and no resource transaction or fatal-charge semantics change.

Time.timeScale is exactly 0 during Pause, and 1 on Resume, menu navigation, failure
handoff or pause cleanup. Time.fixedDeltaTime is unchanged. Physics and scaled-time
animation stop; UI input and Update remain live. Environment audio is not globally
paused in this foundation. The interface uses no scaled-time transitions.

Only enabled PlayerMovementInput2D components are suspended. Their accepted cleanup
clears buffered presses. PlayerMovement2D remains enabled: pausing does not call its
OnDisable, cancel an active dash, restore its gravity early, reset eligibility or
replace DashApproval. The remaining dash duration freezes with physics. Resume
continues it normally. Input requests are cleared at the boundaries. A resumed
player waits for held gameplay/submit/navigation/pause controls to release before
input re-enables; no input-held jump or dash crosses that boundary. Held movement
directions must also release once. No bank API runs on Pause or Resume.

The accepted MenuInputScope supplies a private UI action copy. Other enabled UI
owners are suspended while Pause owns focus; the scene-owned Pause EventSystem is
inactive otherwise. Resume is the default selection. Controls uses the accepted
movement mappings, plus Pause bindings. Back/Escape/gamepad East returns from
Controls to root and selects Controls without resuming. Start/Menu resumes only at
the root. Cancel at root does nothing; Escape/Start use explicit toggle presses.

Focus loss clears requests and, if paused, disarms UI until focus and neutral input
return. It does not silently resume or automatically pause active gameplay. This
follows the existing player input focus policy; automatic focus-loss pause remains
a team decision. Device release checks read currently connected devices and keep no
cached device references. Game Over keeps priority even if failure is triggered by
another system while scaled time is zero; it restores time before showing failure.

Return validates the configured registered MainMenu scene, locks navigation, restores
time and leaves the existing balance/run state intact. It never initializes a run.
Accepted MainMenu New Game and Game Over Restart remain the only explicit new-run
flows here. Invalid destinations leave the game paused with an editable error and
usable alternatives. Cleanup restores time and subscriptions on scene unload or
component disable. Integration assumes ordinary Single-mode level transitions; a
persistent additive-scene UI shell is outside scope.

The reusable prefab follows the accepted corporate research aesthetic and MenuButton
prefab/font. Text, colors, overlay and illustration/status/animation placeholders are
editable. Ace's art, level layouts, accepted room architecture and physics are unchanged.

Manual review: open the menu/build and New Game, then Escape or gamepad Start. Confirm
the world is visible and frozen, Resume focused, and UI navigation works at time 0.
Try Controls then Back/Escape/East; confirm root focus and continued pause. Resume
with mouse/Enter/gamepad, release controls and walk/jump/dash. Repeat during an active
dash and confirm no free dash/eligibility reset. Check exact bank balance, menu return
without refill, subsequent New Game, final-charge failure and Restart. Repeat Play
entry/exit, focus loss/device reconnect and display sizes. No dash HUD is added; inspect
the bank in Editor for balances. Physical-controller and visual acceptance remain
developer checks even when virtual-input automation passes.

Build via Game Jam > UI > Build Pause Windows Development Player. The accepted build
scene list and MainMenu entry are retained. No production scene is regenerated.

## Executed verification

Unity 6000.3.26f1: final-1.xml and final-2.xml each report **204/204 passed**, zero
failures or skips, in separate Editor processes. All 174 accepted regression tests
are retained and 30 Pause tests are added. The inherited domain-reload-disabled
setting is unchanged. Coverage includes direct scene loads, keyboard/mouse/virtual
gamepad input, active-dash preservation, no airborne eligibility reset, buffered input
clearing, unscaled UI navigation, focus loss/device removal, failure/restart priority,
component cleanup and repeated scene/input cycles.

The initial generation compile failed because a new test helper named Key shadowed
the Input System Key enum; renaming it PressKey resolved that compiler error. The
first full attempt used -nographics and crashed inside Unity's native renderer when
an unchanged foundation screenshot test submitted a render request. It produced no
test verdict and is not counted as a completed run. Graphics-enabled runs then passed
203/203 twice; no accepted rendering test or project graphics setting was weakened.

Subsequent review identified a restart transition window: the accepted Game Over
launcher resets the bank before the old failure overlay unloads. A new focused test
reproduced that Pause could open during this interval (0/1 passed before the fix).
Pause now also respects a visible/navigating Game Over controller, rather than only
the failed-bank flag. The new test and all others pass in the final 204/204 pair.
Game Over's transaction, reset/navigation implementation and presentation are
unchanged apart from the two-line Pause handoff before its Show operation.

The final Windows development build succeeded with zero reported build errors.
The visible 1280x720 standalone application completed automated checks of actual
nonfatal dash spending, Pause, frozen scaled time/body position, Controls, Back,
Resume, menu return without refill, menu interactivity, New Game, fatal zero-travel
failure, Game Over priority and Restart, and single bank/pause ownership. The accepted
Quit action exited with code 0. Button listeners and runtime APIs drove these checks;
this is programmatic evidence, not hands-on or physical-controller acceptance.

| Observed phase | Time scale | Remaining | Failed |
| --- | ---: | ---: | --- |
| Paused after one actual dash | 0 | 4 | No |
| Resumed | 1 | 4 | No |
| Returned menu | 1 | 4 | No |
| New Game | 1 | 5 | No |
| Fatal failure after pause flow | 1 | 0 | Yes |
| Game Over Restart | 1 | 5 | No |

The inherited starting allocation of 5 remains provisional. Final-charge zero travel
and immediate player freeze were retained. The standalone log contains graphics
diagnostic and unreachable Unity online-service messages; no managed runtime
exceptions were found. The pause, controls, resumed and returned-menu screenshots
were visually inspected. Developer manual acceptance subsequently passed 10/10
checks, including physical gamepad input and repeatability. Future official level
layouts and display configurations still need their own integration review.

Local evidence is under ignored Logs/Pause: final-1.xml, final-2.xml, final-build.log,
build-summary.txt, restart-window-before.xml, initial-full.log and Standalone/checks.txt,
state.txt, complete.txt, pause.png, pause-controls.png, resumed.png, returned-menu.png.
PauseDevelopmentCheck requires the explicit -pause-check output-folder argument in
an Editor/development compilation and never runs during ordinary play.

All 374 baseline paths are retained. The only intended baseline edit is the two-line
GameOverController handoff. Accepted physics, resource transactions, input assets,
art/GUIDs, scenes, fixtures, assembly definitions and scene registration are preserved.
Generated Unity settings serialization is backed up locally and restored to its
initial bytes in this worktree. Unrelated IDE settings, generated solution and
scene-template settings remain local and excluded. Full candidate inventory and
metadata audit are retained under Logs/Pause. Publication rechecked all 20 accepted
candidate hashes and saved evidence without rerunning Unity or changing gameplay.
Only this document's acceptance/publication record was updated afterward. PRs #7 and
#8 and their existing worktrees are untouched. No merge or additional implementation
is authorized by this publication pass.
