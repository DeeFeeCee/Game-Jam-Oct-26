# Mobility Reserve HUD review

Developer decision: PASS-1F-D-HUD-ACCEPTED. Devin Padin completed all 10/10 manual
acceptance checks and authorized publication for DeeFeeCee's review. Readability,
fatal-charge warning, scene integration, modal priority, resolution scaling, input
coexistence and repeated Play Mode sessions were verified by the developer.

Worktree MobilityHUDFoundation; branch feature/mobility-reserve-hud. The exact starting
commit is accepted Pause 0838ee9e4c81aa21c133d8fafe7b62b5534b5f8a. PRs #7, #8 and #9
remain open/unmerged at preflight; remote main remains
961b5ff90f48fd18fd17c69c959651d60c912e77. Their dependency ancestry is preserved and
existing worktrees are untouched. Publication preflight confirmed these same remote
commits and open dependency PRs. The HUD PR targets feature/pause-foundation so its
review diff contains only HUD changes. No merge, rebase or history rewrite is performed.

## Read-only integration

MobilityReserveHUDBootstrap installs one scene-owned Resources/UI/MobilityReserveHUD
prefab when a loaded scene contains accepted LevelSetup. The pattern matches the
accepted Pause/Game Over bootstraps. MainMenu and old fixtures without LevelSetup
are excluded. Scene unload destroys the HUD and unsubscribes it. Static scene
subscriptions are cleared at SubsystemRegistration for domain-reload-disabled Play.
The intended contract is ordinary single-scene levels, not a persistent additive UI
shell. Future levels using LevelSetup need no separate monitor wiring.

MobilityReserveHUDController observes RunDashBank.Instance, RemainingCharges,
StartingDashCount and IsGameOver. It subscribes once to ChargesChanged and
NewRunStarted, reads existing state immediately on binding, and unsubscribes on
disable or bank replacement. ChargesChanged already follows committed fatal state,
so zero is shown synchronously without another GameOver listener. Missing/disabled
banks show -- / MONITOR OFFLINE without creating or initializing a bank. A cheap
identity/availability comparison in Update handles late/replacement banks; rendering
and segment changes are event-driven, not rebuilt every frame.

The production controller/bootstrap never spend, reset, freeze, approve or navigate.
The bank, movement and accepted UI own all gameplay/run transitions. Explicit test
and development diagnostics may drive those accepted APIs to verify the observer.

## Presentation and artwork

The screen-space overlay sorts at 10, below Pause (90) and Game Over (100). It owns
no EventSystem, input action, selectable or raycaster. All graphics ignore raycasts,
and the CanvasGroup blocks neither pointers nor navigation. The same HUD remains
under the modal overlays, shows zero on failure, refreshes on Restart/New Game and
disappears when gameplay unloads for MainMenu. Pause/time scale do not affect it.

Default panel: 320x108 reference pixels, upper-left anchor/pivot, offset (16,-16).
Change MonitorPanelBackground's serialized RectTransform to reposition/resize it.
CanvasScaler uses the accepted 1280x720 reference and width/height match .5. Camera,
pixel-perfect reference, room geometry and player scale are unchanged.

The main background Image, labels, identifier, number and warning TMP references,
template Image and optional facility logo are editable. Segment sprite overrides and
normal/inactive/critical colors are serialized on the controller. Placeholder shapes
reuse accepted TMP resources and need no new art from Ace. No source artwork changes.

Segment count comes from the current starting allocation, never a hard-coded five.
A pool is built only when that allocation changes; spending updates existing images
and text. A compact grid fits the configured columns/rows with reduced spacing when
needed. Positive alternative allocations are supported without source edits; very
large allocations produce fine segments, so the numeric counter remains the precise
display and such allocations need visual review. The inherited five-charge bank
remains provisional. Numerals use at least two digits without truncating larger values.

At one remaining charge the counter and sole active segment turn amber and display
NEXT DASH WILL TERMINATE RUN. At zero all segments are inactive and the exhausted
message appears behind Game Over. The warning/status strings and colors are editable;
the mandatory warning is the authored default. There is no flashing or animation.

## Developer review

Open this isolated project in Unity 6000.3.26f1, or use the Windows build under
Builds/HUD. Start New Game, verify five lit segments/05, spend two dashes and verify
three/03, then reach one/01 and read the fatal warning before the final dash. Confirm
fatal zero travel, 00, Game Over priority and Restart restoring five. Test Pause,
Controls/Back and Resume at a spent balance, then menu return without refill and
New Game. Inspect the bank directly in the Editor for exact balances.

Review 1280x720, 1920x1080, 2560x1440 and 1024x768, including full-room puzzle
visibility, warning legibility, pointer/gamepad/keyboard ownership, direct Play entry,
destination progression and repeated scene/restart cycles. Try another positive
starting allocation on a local verification bank and explicitly initialize a new run.
Artwork, final branding/allocation, preferred HUD position and visual acceptance
remain team decisions. Automated checks do not grant developer acceptance.

## Executed verification

Unity 6000.3.26f1: final-1.xml and final-2.xml each passed **237/237**, zero failures
or skips, in separate Editor processes with the inherited domain-reload-disabled
setting. The entire 204-test accepted baseline is retained and 33 focused HUD tests
are added. The initial full run also passed 237/237; no HUD compile or test failures
were encountered. The only subsequent source adjustment was selecting native
borderless output for the explicit QHD capture, before the final two full runs.

Tests cover immediate event-driven values/colors/warning, actual fatal zero-travel
behavior, Restart/New Game, modal/input priority, framework transitions, repeat loads
and subscriptions, destroyed/late/disabled banks, positive allocations 1/3/8/17/101,
segment reuse, read-only rendering effects, duplicate removal and layout bounds.
The reference-layout tests use the CanvasScaler equation; actual output dimensions
and visual evidence were separately verified in the standalone application.

The Windows development build succeeded with zero reported build errors. The visible
standalone check completed and exited through the accepted Quit action with code 0.
It drove actual eligible upward dashes and natural landings rather than editing the
bank. State and every segment color matched the bank throughout. The final dash
still froze the player immediately, with no travel, and showed zero behind Game Over.
Restart, Pause/Resume, menu return and another New Game all passed with one bank and
one HUD. This is executed programmatic evidence, not hands-on developer acceptance.

| Phase | Bank | HUD | Time scale |
| --- | ---: | --- | ---: |
| New Game | 5 | 05, five teal segments | 1 |
| Two actual dashes | 3 | 03, three teal segments | 1 |
| Fourth actual dash | 1 | 01, one amber segment and mandatory warning | 1 |
| Fatal fifth dash | 0 | 00, no illuminated segments, Game Over above HUD | 1 |
| Restart Run | 5 | 05 | 1 |
| Pause / Resume | 5 | 05 unchanged | 0 / 1 |
| One more dash / menu return | 4 | 04 / HUD removed | 1 |
| Second New Game | 5 | 05 | 1 |

Actual runtime output and PNG dimensions were verified at 1280x720, 1920x1080,
2560x1440 and 1024x768. QHD uses native FullScreenWindow output; the other three use
Windowed output. The HUD panel stayed within bounds and its text did not overflow at
all four sizes. Measured panel sizes were respectively 320x108, 480x162, 640x216 and
approximately 296x100 pixels. The inherited camera letterboxing remains at 4:3; the
screen-anchored HUD occupies the upper-left letterbox area there. No camera or render
configuration was changed. All eight runtime images were visually inspected.

Evidence remains local under ignored Logs/HUD: final-1.xml, final-2.xml, build.log,
build-summary.txt, metadata-audit.json, baseline-audit.json, screenshot-dimensions.json,
and Standalone/checks.txt, state.txt, resolutions.txt, complete.txt, the four
five-WIDTHxHEIGHT.png images, three.png, one-critical.png, zero-game-over.png and
hud-behind-pause.png. MobilityReserveHUDDevelopmentCheck requires the explicit
-hud-check output-folder argument in an Editor/development compilation and adds no
behavior to ordinary play or release compilation.

The standalone log contains a graphics diagnostic and unreachable Unity online-service
messages in this restricted network environment; no managed runtime exceptions were
found. No accepted regression test was removed, weakened or edited.

## Preservation and remaining acceptance

All 393 baseline paths are retained. Every accepted gameplay, resource, menu, Game
Over, Pause, art, scene, prefab, assembly and regression fixture file is byte-identical
to the starting commit. There are no intended baseline modifications: only 17 new
HUD source, test, prefab, documentation and metadata paths. Unity-generated settings
serialization was backed up locally and restored to exact initial bytes in this new
worktree; IDE settings, generated solution and scene-template files remain local and
excluded. The audit found no duplicate asset GUIDs, missing new metadata or unresolved
prefab references. Source inspection and effect tests confirm the production observer
and bootstrap never spend/reset resources, approve movement, navigate or change time.

Developer manual acceptance passed 10/10 checks. Automated scene/enable cycles and
virtual-device navigation remain distinct from that hands-on verification; automated
full runs used separate Editor processes. Future official level layouts and display
configurations require their own integration review. The five-charge allocation,
final branding/artwork and preferred corner placement remain provisional.
Very large allocations fit more/finer segments and need visual review even though
the numeric balance remains precise. No new gameplay decision or production adoption
is implied. Publication rechecked all 17 accepted candidate hashes and saved evidence
without rerunning Unity or changing gameplay/visuals. Only this document's acceptance
and publication record was updated afterward. PRs #7, #8 and #9 are unchanged; this
publication does not authorize merging or additional functionality.
