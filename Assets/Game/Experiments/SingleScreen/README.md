# 32 x 18 single-screen experiment

Designation: PASS-1E-32X18-EXPERIMENTAL-CANDIDATE. This is an isolated experiment,
not a replacement for the accepted production level framework.

## Baseline and isolation

PR #5 is merged. Experiment starts at main's merge commit
`0c0cda9a6af5b719703cb303f02a2644114c967b`, whose tree matches accepted integration
`24dea536d9b8bdf5ae0552607e7814dbb17b6d03`.
Worktree: SingleScreenPrototype; branch: experiment/32x18-single-screen.
The existing ProductionFoundation worktree and its unrelated local changes are preserved.

## Grid contract and authoring

Provisional room: 32 columns by 18 rows, one world unit per cell. World bounds
are [0,32] x [0,18]; cell (x,y) occupies [x,x+1] x [y,y+1]. Static solid bounds
use integer edges; odd cell widths/heights naturally use half-integer centers.
Perimeter solids are inside the room: floor y0..1, ceiling y17..18, side walls
x0..1 and x31..32. This leaves a 30 x 16 interior and keeps outer walls visible.

Spawn uses cell-center x3.5; center y1.81 places the unchanged 1.6-high collider
0.01 above the y1 floor. The player moves continuously and is never grid-snapped.
Exit bounds occupy x28..30, y1..3. The grid guide draws only in the Editor.

Select unrotated, unit-scale experiment solids and use Game Jam > Single Screen
Experiment > Snap Selected Solid Bounds To Cells. This only operates in the new
scene and updates the collider, tiled visuals and accepted foreground borders
together. The GridPlatform prefab starts at 4 x 1 cells and remains configurable.
Background decoration is independent of the gameplay grid.

## Camera candidate

Camera at (16,9,-10), orthographic size 9; URP PixelPerfectCamera, 20 assets PPU,
640 x 360 reference, UpscaleRenderTexture and Windowbox. No LevelCamera2D or other
follow owner is attached; LevelSetup.levelCamera is intentionally unassigned.
Camera HDR, MSAA, dynamic resolution and post-processing are off locally.
No global quality, input, render pipeline, movement, physics or resource settings
are changed for this experiment.

Expected integer output scales: 1280x720=2, 1920x1080=3, 2560x1440=4.
At 1366x900, integer scale 2 centers a 1280x720 gameplay area with bars. This
preserves composition and crisp scaling at the cost of unused screen area.
Windows smaller than 640x360 are outside this candidate's supported range.

Ace's source import settings and sprite identities remain unchanged. Character
visual-child scale remains 5 (100 authored PPU -> 20 effective world PPU). Floor
art is 20 PPU; the industrial background remains 12 PPU, an inherited mixed-scale
tradeoff. Point filtering stays crisp but background source pixels do not all map
to equal 20-PPU grid pixel widths. No source-art resampling is performed.

## Room route and scope

Use A/D or Left/Right to move; W/Up to jump; Space to dash with WASD/arrows aim.
The gamepad retains stick movement, south-button jump and right-shoulder dash.
Simultaneous Jump/Dash precedence, cardinal rules, chaining policy and resource
transactions are the accepted implementation, unchanged.

Ascend the three low ledges, jump then dash upward before the tall barrier,
continue right over the barrier and landing ledge, then drop to the green exit.
The exit uses the existing transition to VerificationDestination. Normal room
chaining is disabled; the existing bank starts at provisional 5 and persists.
The upper room space deliberately remains available for future puzzle evaluation;
this is a one-route feasibility room, not a finished level or final dash budget.

Open Scenes/SingleScreen32x18.unity. The experimental build command under Game Jam
> Single Screen Experiment starts at this scene and includes only it and the
unchanged verification destination. It does not replace production build settings.

Developer review must judge character readability, grid practicality, hands-on
traversal and support for later level progression. Automated success alone is not
architectural acceptance. Hidden rooms, new mechanics and Levels 02-15 are excluded.

See ReviewReport.md for measured camera/character comparison, fresh test/build
results, initial validation issues, evidence paths, and the developer review checklist.
