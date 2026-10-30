# Pass 1E experiment — developer review

Designation: **PASS-1E-32X18-EXPERIMENTAL-CANDIDATE**. Production adoption is pending.

The isolated room demonstrates a 32 x 18 world-unit play area, stationary camera,
whole-cell collision bounds, unchanged banked player, and one physically verified
jump/dash route to the accepted verification destination. It is not a finished puzzle.

## Review captures

Local evidence lives in `Logs/SingleScreen` (excluded from Git):

- `accepted-level01.png`: fresh render of the unchanged accepted Level01.
- `room-1280x720.png`, `room-1920x1080.png`, `room-2560x1440.png`, `room-1366x900.png`:
  actual URP Game-camera captures without editor overlays.
- `editor-grid.png`: actual Unity Scene view render, with the one-unit cyan guide
  and yellow collider outlines. Captured inside the render callback after the
  earlier delayed read returned a blank target. Native window capture also timed out.
- Per-image `.json` records: measured world bounds, pixel ratio, viewport and camera.
- `pixel-analysis.json`: whole integer output blocks matched exactly at each size.

## Camera and character comparison

| View | Reference | World area | Visible Idle_1 artwork at 720p |
|---|---|---|---|
| Accepted Level01 | 320 x 180, 20 effective PPU | 16 x 9, horizontal follow | about 40 x 124 px |
| Experiment | 640 x 360, 20 effective PPU | 32 x 18, fixed at (16,9) | about 20 x 62 px |

Measured experiment outputs:

| Output | Integer scale | Gameplay viewport | Visible Idle_1 artwork |
|---|---|---|---|
| 1280 x 720 | 2 | full output | 20 x 62 px |
| 1920 x 1080 | 3 | full output | 30 x 93 px |
| 2560 x 1440 | 4 | full output | 40 x 124 px |
| 1366 x 900 | 2 | 1280 x 720, origin (43,90) | 20 x 62 px |

Ink dimensions use the nontransparent 10 x 31 pixel bounds of authored Idle_1;
other animation frames can differ slightly. The transparent sprite canvas is
64 x 64 source pixels. Character child scale remains 5 and collider remains 0.8 x 1.6.
Unity measured orthographic size 9 and world corners (0,0), (32,18) at all sizes.
No fractional output stretching was required. Smaller-than-reference windows are
not supported by this candidate. The inherited 12-PPU background remains a separate
source-pixel scale; it was neither resampled nor retuned.

## Feasibility and tradeoffs

The perimeter is entirely inside the frame, occupying one cell on every side:
usable interior is 30 x 16. Cell-aligned solid bounds do not require integer centers.
Spawn center x3.5/y1.81 accounts for the existing collider and 0.01 clearance.

The automated route walks and jumps the three ledges, jumps/up-dashes over the
barrier, lands, and physically enters the exit trigger. The transition preserves
one authoritative bank with 4 of the initial provisional 5 charges remaining.
The barrier rises 5 above the floor, so the unchanged jump plus up-dash cannot
clear it directly from the floor; the ledges provide the elevated launch point.

The route uses the lower portion of the room. Upper space is intentionally unused
in this single demonstrator. Future designs still need evaluation for meaningful
use of height, cumulative dash budgets, exits and route legibility. No additional
levels or hidden passages were implemented.

Retain the one-unit contract for hands-on evaluation first. If the character feels
too small at typical window sizes, compare a smaller visible room or explicit
experimental presentation changes in a separately reviewed pass. Do not enlarge
colliders, rebalance physics or silently reinterpret the cell size.

## Developer checklist

1. Open `Scenes/SingleScreen32x18.unity` in this experiment worktree, Unity 6000.3.26f1.
2. In Scene view enable Gizmos, select the room root, and use the Single Screen
   Experiment menu to show the grid. Verify whole-cell edges and readable solids.
3. Enter Play Mode. Compare the full-room view against Level01 at 720p and 1080p.
4. Walk/jump the ledges; near the high launch ledge jump then use Up/W + Space to
   dash upward. Traverse the barrier, continue right, and enter the green exit.
5. Confirm seven-second Look, keyboard/gamepad controls, and final-charge shutdown.
   Stop/start Play twice. The unchanged destination should retain the remaining bank.
6. Repeat with the experimental Windows development build. Review character
   readability and whether a fixed room gives enough useful puzzle space.

Build: `Builds/SingleScreen/GameJam-SingleScreen.exe` (the complete adjacent Data,
Mono and graphics folders are required). Its explicit startup scene is the experiment.
Production EditorBuildSettings and accepted scenes are unchanged.

Architectural acceptance requires developer visual and hands-on review. Automated
verification is evidence for the candidate, not approval to replace production.

## Executed verification

Historical accepted baseline: 121/121 twice, successful Windows build and developer
6/6 manual acceptance for Pass 1D-R1. These are not new manual results for Pass 1E.

Fresh final experiment suites: **128/128 twice**, zero failures or skips, after the
temporary capture trigger was removed (`Logs/SingleScreen/final-1.xml`, `final-2.xml`).
This includes the unchanged 121-test regression suite plus 7 focused experiment tests.
The Windows development build succeeded with zero reported build errors, starts
at SingleScreen32x18, and launched through graphics/input initialization. No manual
playability or architectural acceptance is claimed on the developer's behalf.

Initial validation found the experimental test assembly classified as Editor-only,
so the first run discovered only 121 tests; one existing 60-ms gamepad dash assertion
failed there. Classification was corrected only on the new assembly. Four subsequent
complete 128-test runs passed without changing that existing test or gameplay physics.
The initial failure's cause was not established; the result is retained in the logs.

The Scene view capture needed an Editor-render-callback read rather than a delayed
read of a recycled target; the valid editor-grid.png was visually inspected. The
capture helper was removed. The only changes after final test execution are review
documentation and restoration of incidental IDE/Unity settings to their initial bytes.
The runtime files in the Windows build match the final runtime sources.

Final file/GUID audit: all 209 accepted files are byte-identical to the initial clean
experiment checkout, including both accepted production scenes, all runtime systems,
artwork and regression fixtures. New asset references resolve; no duplicate GUIDs or
missing metadata were found. All new content is confined to Assets/Game/Experiments.
Logs, captures, build outputs and generated IDE files remain local. No commit, push,
PR creation, merge or production-framework replacement was performed.
