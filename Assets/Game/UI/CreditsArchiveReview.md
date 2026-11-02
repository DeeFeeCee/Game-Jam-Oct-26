# Main Menu Credits Archive

Developer decision: PASS-1F-E-CREDITS-ACCEPTED. Devin Padin completed all 10/10 manual
acceptance checks and authorized publication for maintainer review. Mouse scrolling,
scrollbar dragging, keyboard/physical gamepad navigation, fixed Return, focus
restoration, four display resolutions and repeated Play sessions were verified.

CreditsFoundation / feature/credits-archive starts at exact accepted Main Menu commit
c270a0b38f325aade6425014ba952e3231eb985d. At preflight PRs #7–#10 remain open/unmerged;
remote main remains 961b5ff90f48fd18fd17c69c959651d60c912e77. This independent extension
depends only on #7 and does not include Game Over, Pause or HUD. Applicable baseline:
150 tests, not the later dependent UI suites. Existing branches/worktrees are untouched.

Publication preflight confirmed the same remote baseline and that PR #7 remains open.
This independent Credits PR targets feature/ui-foundation so its comparison contains
only Credits changes. PRs #7–#10 are unchanged; no rebase or history rewrite is performed.

The existing MainMenuView owns Credits opening/closing, loading lockout, Cancel and
focus. CreditsArchiveView only manages the panel and top-reset/stop of its ScrollRect.
The accepted MenuInputScope and EventSystem are unchanged. Credits opens with the
vertical scrollbar selected: Up/Down or stick/d-pad scroll, Right selects the fixed
Return control, Left/Up returns to the scrollbar, and Escape/East returns to root.
Closing restores Credits focus. Mouse wheel/drag/scrollbar are supported. Reopening
resets to the top; static editable content is never duplicated at runtime.

The accepted MainMenu prefab is edited in place with PrefabUtility.LoadPrefabContents;
its GUID, existing object identities and original MainMenu scene are preserved.
MenuButton remains unchanged. Only the root PrimaryActions layout gains a fourth row:
52-pixel preferred heights and 4-pixel spacing within a slightly taller anchored area.
Controls, title, illustration and accepted scene/build registration are preserved.
Use Game Jam > UI > Add Credits To Accepted Menu If Absent only for an unextended menu;
the tool refuses to overwrite an existing Credits panel. Edit TMP text directly afterward.

Credits has cyan category headings, amber record labels and dark biomedical surfaces.
The scroll body is Viewport + Image/Mask, with top-pivot Content using VerticalLayoutGroup
and preferred-height ContentSizeFitter. Return remains outside the scroll body. The
scrollbar uses an amber selection state; no extra input map, manager or EventSystem.
All names, roles, labels and placeholder text are editable TMP objects in the prefab.

Provisional roster: Devin Padin / Jarheadd1; DeeFeeCee; AceofHome; mrdrworm. Contributions
follow only the supplied descriptions. Categories are PROJECT TEAM, GAME DEVELOPMENT,
ART & ANIMATION, LEVEL & PUZZLE DESIGN, TOOLS & TECHNOLOGY, AUDIO & MUSIC and SPECIAL
THANKS. Confirmed tools are Unity 6000.3.26f1, C#, Git and GitHub. Audio/music and Special
Thanks are explicitly pending confirmation. The roster is explicitly neither final nor
complete. No Lux Fracta people, asset attributions, music or licenses are imported.

Manual review: use mouse wheel/scrollbar drag, keyboard and physical gamepad, reach
both ends of the archive, switch to fixed Return and close with button or Escape/B.
Confirm Credits focus, repeated top reset, Controls/Back, explicit Quit, New Game
and unchanged bank behavior. Review 720p, 1080p, QHD and 1024x768 including names,
role wrapping, clipping, fixed-control separation and amber focus visibility.

The known navigation regression test for Quit must gain one Down press because the
approved third row is now Credits. No other accepted regression expectation changes.
Final roster completeness, attribution wording, audio/music/licenses, acknowledgments
and final branding require team confirmation. Physical controller and visual acceptance
were completed separately by the developer, rather than inferred from virtual input.

## Executed verification

Unity 6000.3.26f1: verified-1.xml and verified-2.xml each passed **171/171**, zero
failures or skips, in separate Editor processes with the inherited domain-reload-
disabled setting. All 150 applicable baseline tests are retained; 21 Credits tests
are added. Only the approved fourth-row Quit-navigation expectation gained one Down
press. Game Over/Pause/HUD tests are not on this independent menu-based branch and
were not claimed as executed here; combined integration remains a later decision.

The first full run was 170/171: the unchanged cardinal-dash gamepad test failed its
IsDashing assertion after a 0.06-second wait. It passed in isolation without a source
change. No dash code, tuning or fixture assertion was changed; subsequent complete
runs, including the final pair, passed. The cause of that one transient result was
not conclusively established.

The first standalone capture helper injected an oversized synthetic wheel delta for
the project's normalized scrolling setting. The helper/test now use one tick and
explicitly verify a partial scroll rather than skipping the entire archive. Runtime
menu scrolling behavior was unchanged. The full final pair above includes this check.

The refreshed Windows development build succeeded with zero reported build errors.
StandaloneVerified completed and exited through accepted Quit with code 0. It used
actual UI input paths with synthetic keyboard and mouse events to open Credits, scroll
one wheel tick partway, reach the bottom via keyboard and activate fixed Return.
Escape restored root focus; Controls/Back, repeated top reset and New Game still
worked. Browsing alone created no bank. An explicit diagnostic setup returned an
existing bank to the menu at 4 charges: Credits left it at 4, and New Game restored
the same bank to 5. That diagnostic scene return is not a new gameplay menu feature.

Actual output/PNG sizes were verified at 1280x720, 1920x1080, 2560x1440 and 1024x768.
QHD used native FullScreenWindow; other captures used Windowed output. No credits
text overflow was reported and fixed Return remained on-screen. Contributor text is
masked only at the scrolling viewport edges, not horizontally truncated. Runtime
images include the four-row main menu, archive top, partial scroll, archive bottom
and all four output sizes. Images were visually inspected. These are programmatic
checks; the developer's separate 10/10 manual acceptance includes physical gamepad use.

Local evidence: Logs/Credits/verified-1.xml, verified-2.xml, verified-build.log,
build-summary.txt, initial-full.xml, dash-isolated.xml, metadata-audit.json,
baseline-audit.json, screenshot-dimensions.json and StandaloneVerified/checks.txt,
resolutions.txt, complete.txt and PNG captures. CreditsDevelopmentCheck requires the
explicit -credits-check output-folder argument in an Editor/development compilation;
it never runs during ordinary play. Earlier captures remain separately under Standalone.

The player log contains graphics/unreachable Unity online-service diagnostics; no
managed runtime exceptions were found. No other accepted regression was edited,
removed or weakened. This does not claim physical hardware verification.

## Preservation and acceptance boundary

All 353 baseline paths are retained. Intended existing-file edits are only
MainMenuView.cs, MainMenu.prefab and the one MenuTests.cs expectation. The original
MainMenu scene, its GUID, the prefab GUID, MenuButton, input asset, build registration,
gameplay/bank implementation, art, camera and historical fixtures remain byte-identical.
All 115 original menu-prefab local object IDs remain; 134 IDs were added. No duplicate
GUIDs, missing new metadata or unresolved prefab references were found. Generated
Unity settings serialization is backed up locally and restored to exact initial bytes
in this worktree. IDE settings, generated solution and scene-template files remain
local and excluded. Existing worktrees and PRs #7–#10 are untouched.

Developer manual acceptance checklist: main-menu order; roster/roles; mouse wheel
and dragging; keyboard scroll and fixed Return; physical gamepad stick/d-pad/confirm/
East; Escape/Return focus; repeated top reset; Controls/New Game/explicit Quit;
four-resolution readability/control separation; repeated Play entry/exit within one
open Editor. Please confirm final roster completeness, preferred identities/wording,
audio/music contributors/assets/licenses and Special Thanks before finalizing content.
The developer completed this checklist with 10/10 checks passed. Publication rechecked
all 16 accepted candidate hashes and saved evidence without rerunning Unity or changing
functionality/visuals. Only this document's acceptance/publication record was updated.
The initial transient dash-test result and subsequent successful reruns above remain
unchanged. Before final release the team must confirm credit identities, contribution
descriptions, roster completeness, audio/music attributions and Special Thanks.
Publication is authorized; merging and additional implementation are not.
