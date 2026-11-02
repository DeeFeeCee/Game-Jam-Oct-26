# 32×18 Level Production Guide

For mrdrworm and DeeFeeCee. Pass 1G adopts the accepted single-screen room format
without changing movement, dash, resource, animation or UI behavior.

## Create a level

1. Save current scene edits and stop Play Mode.
2. Choose **Game Jam > Levels > Create 32x18 Level**.
3. Enter `2` or `Level02`, then **Create And Open**. Use letters, numbers,
   underscores or hyphens for custom names; omit paths and `.unity`.
4. Unity copies `Assets/Game/Scenes/Levels/LevelTemplate32x18.unity`, assigns a
   separate scene GUID and opens the copy. Existing scenes are never overwritten.

The canonical template has the framework, camera, four perimeter solids,
industrial background, spawn and exit. It has no finished puzzle. Its exit
destination is deliberately blank until you configure a completed level.
`LevelExample32x18.unity` is an empty independent review copy, not Level02 and not
a build entry. Do not use the one-time integration builder to regenerate levels.

## Room and hierarchy

- World bounds: X **0..32**, Y **0..18**; 32 columns, 18 rows, 1×1-unit cells.
- Cell `(x,y)` covers `[x,x+1] × [y,y+1]`. Platform edges use whole numbers;
  odd widths or heights naturally produce half-unit centers.
- Perimeter: floor Y0..1, ceiling Y17..18, left wall X0..1 and right wall X31..32.
  The usable interior is 30×16 units. Keep walls inside the room.
- **Level framework - 32 x 18**: LevelSetup, SceneDashSettings, LevelTransition,
  RoomGrid32x18, Player spawn and Exit.
- **Geometry - static whole-cell platforms**: editable platform prefab instances.
- **Background and decoration**: noncolliding art, separate from gameplay solids.
- **Main Camera**: stationary at (16,9,-10), orthographic size 9. URP
  PixelPerfectCamera uses 20 assets PPU, 640×360 reference, UpscaleRenderTexture
  and Windowbox. No camera-follow script; LevelSetup's levelCamera stays empty.

Turn Scene view **2D** and **Gizmos** on to see the room grid and collider outlines.
The guide has no runtime snapping or camera behavior. The player moves continuously.
Do not move/scale the whole framework as a way to change room dimensions.

## Platforms and artwork

1. Drag `Assets/Game/Prefabs/GridPlatform32x18.prefab` into Geometry, or duplicate
   an existing platform instance. Keep root scale (1,1,1) and rotation zero.
2. Select its root and use **Game Jam > Levels > Resize Selected Platform**.
3. Enter bottom-left cell and width/height in cells, then Apply. Collision,
   tiled floor art, backing and foreground edges update together. **Ctrl+Z** undoes it.
4. After freely moving a root, use **Snap Selected Platform Bounds To Cells** to
   intentionally snap its bounds. Nothing snaps automatically during gameplay.

Do not resize only the SpriteRenderer, only the collider, or stretch the root.
Use positive whole-cell dimensions inside the room. Physics stays on the root;
foreground trim is visual only. The light top edge and darker backing identify
intended walkable surfaces and mandatory obstacles against Ace's industrial art.
The background remains decorative and is not a collision map.

Preserve Ace's source sprites, animation references, GUIDs and import settings:
Point filtering, no compression or mipmaps. The floor is authored at 20 PPU,
character source at 100 PPU with its existing visual-child scale of 5, and
background at 12 PPU. Do not force every import to 20 PPU. The inherited background
ratio is a visual compromise; windowboxed integer output does not resample its source.
Hidden rooms and concealed passages are deferred design concepts, not implemented here.

## Player spawn and dash budget

Move the assigned **Player spawn** Transform; do not place another Player prefab
or add another spawn script. **LevelSetup owns player spawning** and creates the
authoritative persistent bank only when absent. The unchanged player collider is
0.8×1.6 units; leave clear space around the spawn. Default center (3.5,1.81) puts
its feet just above the floor at Y1. Do not scale the player or alter its physics.

SceneDashSettings defaults **Allow Air Dash Chaining** to false. Only approved
puzzle scenes should enable it; it never adds charges or permits overlapping dashes.
Normal level transitions, jumping and landing never refill the run bank.

Starting allocation remains **provisional five**: four successful dashes are
available; spending the fifth/final charge immediately fails the run with no dash
travel. The team must review the cumulative budget across all fifteen levels.
Do not solve the budget problem by resetting the bank at each exit. New Game and
Restart Run are the existing explicit full-run resets to Level01.

## Exit and build registration

Move/resize the green **Exit** trigger as needed, keeping its collider a trigger
and its LevelExit.transition assigned to the framework's LevelTransition. Set
**destinationScene** on that LevelTransition to a saved, enabled build-scene path,
for example `Assets/Game/Scenes/Levels/Level03.unity`. The exit loads once, freezes
the departing player and preserves the same bank. Failed runs cannot exit.
Blank or unregistered paths fail closed. Final victory presentation is not supplied.

After a level is ready, open it and choose **Game Jam > Levels > Register Current
Completed Level In Build**. This saves it and appends/enables its build entry while
preserving MainMenu first and existing scene order. Alternatively use **File >
Build Profiles > Scene List** and retain MainMenu as the first enabled scene.
Register destinations too. Do not register the canonical template or review example.
Level01 temporarily exits to the existing VerificationDestination fixture; that
fixture is retained unchanged and is not a newly authored Level02.

## Playtest and share

Test direct scene Play entry first: exactly one player/bank should appear with HUD,
Pause and Game Over available. Then enter from MainMenu and test New Game, actual
jumps/dashes, the real exit, preserved charge balance, Pause/Resume and Restart.
Controls remain A/D or Left/Right movement, W/Up jump, Space dash with directional
aim; gamepad stick, South jump, Right Shoulder dash and Start pause. Escape pauses.
Cardinal ties choose horizontal, so use unambiguous vertical aim for an upward dash.

Level01's provisional route climbs three ledges, jumps from the third and dashes
upward over the tall barrier, then moves right and drops to the green exit. It costs
one successful dash. Its upper room space is mostly unused and the layout may be
replaced by Worm's final design. Review at 1280×720, 1920×1080, 2560×1440 and a
non-16:9 size. At 1366×900, a centered 1280×720 room uses integer scale 2 with bars.
Windows below 640×360 are outside the verified contract.

Work on your own branch, save the scene, include its `.meta` plus any intentionally
new assets and metadata, and inspect the diff before sharing a PR. Do not regenerate
GUIDs, overwrite someone else's level or include Library/Logs/Builds/IDE output.
Coordinate scene ownership with Worm/Dee before editing a shared scene.

## Separate follow-up

Worm reported horizontal slowdown when holding A/D and pressing W/Up to jump.
Normalized diagonal Move input is a likely cause. This needs a separate movement
correction and regression review; Pass 1G leaves the input asset and movement code
unchanged. Turrets, breakable walls, kill-plane deaths and new hazard systems are
also outside this pass. Mainline Credits is integrated and preserved in the
publication branch; include Credits and New Game in integration playtests.
