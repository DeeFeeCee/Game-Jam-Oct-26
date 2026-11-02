# P.I.D.R. Gameplay Mechanics Authoring Guide

Pass 1I. These mechanics are selectable tools, not a requirement for every level.
No final art, campaign layout, ten/fifteen-level count, checkpoint policy, or new resource allocation is established here.

## Start with the verification room

Open `Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity`.
It is an isolated 32×18 test room, with a stationary camera at (16,9,-10), orthographic size 9,
640×360 reference resolution, and 20 PPU. Main Menu remains the first build scene.
The accepted `Player.prefab`, `RunDashBank.prefab`, and `LevelSetup` supply the player and single run balance.
Do not drag another player or bank into the room. Existing UI bootstraps provide HUD, Pause, and Game Over.

For the Windows development build, double-click `Builds/UnifiedGameplay/PlayVerification.cmd` to
enter this room for manual play. This supplies the explicit `-mechanics-room` test flag; ordinary
`PIDR.exe` still opens the accepted Main Menu. Restart retains its canonical Level01 destination,
so relaunch the verification shortcut to return to the mechanics room after a failure/restart.

The integrated branch includes the official 32×18 production framework from Pass 1G (PR #14).
Use the existing level-authoring tools for production scenes, then drag the desired prefab from
`Assets/Game/Prefabs/` into a team-approved room. Pass 1I places its examples only in the isolated
verification room; it does not add mechanics to Level01 or change any production layout.

Controls remain A/D or Left/Right to walk, W/Up to jump, Space to dash, Escape to pause;
gamepad left stick to move/aim, South to jump, Right Shoulder to dash, Start to pause.
Keyboard diagonal walking keeps full horizontal speed. Dash remains cardinal, dominant axis,
horizontal on exact ties, last horizontal facing when neutral. Dash wins over same-update Jump.

All existing tuning is retained: speed 6, jump 10, gravity scale 3, dash speed 18 and duration .16,
five starting charges. These are provisional values, not final balance decisions. A final charge
ends the run before movement. Environmental death keeps the current charge count.

## Add a moving platform

1. Drag `Assets/Game/Prefabs/MovingPlatform.prefab` into your scene.
2. Move the whole root to the desired starting area. Expand it in the Hierarchy.
3. Move **Endpoint A** and **Endpoint B** to the two desired world positions. They are siblings
   of **Physics body**, never children of it. The cyan gizmo shows the path.
4. Select **Physics body**. Set Speed, Endpoint Delay, Start At B, and Size.
   A starts toward B by default; Start At B starts toward A. Horizontal, vertical, and diagonal paths work.
5. Keep the root and body scale at (1,1,1) and rotation at zero. Change Size rather than scaling physics.
   The surface and bright safe-top strip resize together. Replace their sprites/materials if approved.
6. Test landing, riding through both reversals, walking, jumping, and dashing away.

Endpoint positions are sampled at scene initialization. Moving endpoint objects during Play does
not retarget the platform. Fresh scene load resets the route. There is no parenting of the player,
friction shortcut, or permanent momentum transfer. Jump uses the existing world jump speed;
dash uses the existing world dash speed. Walking adds platform transport only while supported.
The player controller remains the only owner of player velocity.

Leave headroom and keep routes clear of unrelated solid terrain. These kinematic platforms do
not include pathfinding, squeeze resolution, or a general moving-platform crush-death mechanic.
Pause freezes simulation; environmental failure stops the route. Endpoint delay uses game time.

## Place a fall-death zone

Drag `Assets/Game/Prefabs/FallDeathZone.prefab` below an **accessible** pit.
Resize its BoxCollider2D Size/Offset to cover the fall, leaving Is Trigger checked.
The editor wire box is visible with Gizmos on; it has no ordinary gameplay renderer.

A standard room's closed bottom prevents reaching a kill plane. Deliberately leave a gap in the
intended pit floor, then place the trigger below it. Do not remove unrelated room boundaries.
The verification room's pit spans x=12..15 and its zone is centered at (13.5,-1.5).
It ignores unrelated bodies, requests one authoritative environmental failure, spends no charges,
and never reloads the level itself. Restart Run uses the existing Game Over flow and canonical Level01.

## Configure a turret

Drag `Assets/Game/Prefabs/Turret.prefab` into the scene. Assignments are already supplied for
Projectile Prefab and child Muzzle. Set Firing Direction (a **world-space** vector), Initial Delay,
Shot Interval, Projectile Speed, and Automatic Activation. The red gizmo shows aim.
Reposition the muzzle to sit outside nearby blocking solids. Rotating the sprite does not rotate
the world direction; change the direction vector explicitly. Diagonal projectiles are supported.

The shared `TurretProjectile.prefab` has a CircleCollider2D trigger, kinematic Rigidbody2D,
replaceable .24×.24 sprite, and configurable Lifetime. It sweeps its collider each fixed step:
the first solid blocks the shot, and an accepted bank-linked player causes environmental failure.
Do not add a solid turret collider around its muzzle without testing shot clearance.

Automatic Activation off leaves it idle; the small `SetFiring(bool)` interface is available for a
future approved activator. No targeting AI or new switch system is included. Default initial delay
is .5 seconds, interval 1.5 seconds, speed 6, lifetime 5 seconds. Pause freezes both timer and shots,
with no catch-up burst. Failure prevents later shots. Projectiles belong to the turret's scene and
are destroyed with it. Very high configured speeds are capped at Unity's existing 2D translation
limit rather than changing that project-wide limit.

## Place a dash-breakable wall

Drag `Assets/Game/Prefabs/BreakableWall.prefab` into the scene. Default collision is 1×3 units.
For another size, adjust the BoxCollider2D and both child SpriteRenderer sizes/positions to match.
Use the bright edge and magenta placeholder to distinguish it from ordinary solids.

Walking and jumping remain ordinary solid collisions. An approved, nonfatal dash's predictive
collision sweep removes the wall. This is not an extra dash and does not spend another charge.
The pre-approval check is side-effect free: a flush reactive wall may request approval, but it
cannot disappear on a rejected, ineligible, or fatal final-charge attempt. Ordinary geometry
before or overlapping the wall blocks it; geometry behind it still stops that same dash.
Fresh scene load restores the wall. No projectile damage, health, fragments, or destruction animation is required.

## Set up a falling crusher

Drag `Assets/Game/Prefabs/FallingCrusherPlatform.prefab` into the scene.
Place its root at the starting height. Expand it:

- **Physics body** owns Size, Warning Delay, Fall Speed, Maximum Descent, and replaceable visuals.
  Defaults: 4×.75 units, .6-second warning, speed 6, maximum descent 12.
- **Stationary activation region** owns the resizable BoxCollider2D trigger and its Crusher reference.
  Keep it outside the moving body's hierarchy. Resize/move it to the intended approach area.

Its states are Armed → Warning → Falling → Landed. An accepted player entering the region starts
the one-time sequence. Warning uses game time; there is no shaking animation requirement.
It descends toward the configured limit and stops at solid terrain sooner if necessary. It does
not reset or oscillate; reload the scene for a fresh Armed instance. The selected gizmo shows descent.

Only a descending underside impact is lethal. A downward shape sweep, relative closing-motion check, and contact-normal backup
distinguish an overlapping player below from a top rider or side contact. Top surfaces remain safe
for landing, riding down, walking, jumping, and charged dashing. Sides are nonlethal by default;
an Armed or Landed crusher is ordinary solid terrain. Place wide enough landing terrain and leave
rider clearance. Do not design side-squeezing puzzles around the nonlethal side policy.

To test both cases independently, place the player spawn below it for the trigger/underside case;
for safe-top testing place the spawn above it (center about 1.2 units above the body) and arrange
the stationary activation region to include that approach. Save, then enter Play afresh. This is
test setup, not a runtime teleport or checkpoint. Return the verification spawn afterward.

## Troubleshoot and replace visuals

- Check Console first. Missing platform endpoints, LevelSetup references, or required UI references
  are authoring errors; do not add another manager to hide them.
- Keep platform Endpoint A/B and the crusher activation region stationary. Do not parent Jett.
- Keep colliders enabled, non-trigger on solids; the fall zone, activation region, and projectile are triggers.
- Keep object layers colliding with the player's layer in the existing Physics2D matrix.
- Pink/missing sprites: check the assigned Sprite and accepted unlit material. Prefabs reuse the existing
  placeholder square; there are no new texture imports or Ace art edits.
- Import approved pixel art with Point filtering, no compression, and intentional PPU/pivot.
  Preserve the supplied `.meta` files/GUIDs. Put art on child visuals; do not resize Jett's validated collider.
- No animation is mechanically necessary for these mechanics. A bright safe-top edge and recognizable
  hazard silhouette are readability goals. See the separate Art Requirements Matrix before assigning work.
- Never call `InitializeNewRun` from landing, a turret, a wall, a scene transition, or scene chaining settings.
  Chaining controls eligibility only. The authoritative bank approves each eligible dash separately.
- Duplicate managers are prevented by the accepted bank/UI bootstraps. Scene-owned mechanics subscribe
  to no bank events; they inspect run state. There is no new persistent hazard manager.

## Decisions still requiring team approval

Final mechanic usage and level placement, art identities/assignments, optional environment looks,
timing/speeds/sizes, warning presentation, hazard collision corner cases, and final starting allocation
remain team choices. Fully blocked ordinary dashes still spend nothing; a valid breakable-wall impact
spends once. Environmental failure preserves the balance and Restart resets the entire run to canonical
Level01, with no checkpoints. These existing/provisional policies are not new final GDD requirements.
The proposed ten-level baseline/fifteen-level stretch is planning only. No levels are auto-generated.

## Ten-item developer manual acceptance checklist

1. Compare A/D with W+A/W+D and arrow combinations; horizontal speed feels equal, W/Up jumps once per press.
2. Check Space/cardinal aim, neutral facing, horizontal ties, simultaneous Jump/Dash precedence, and physical gamepad South/Right Shoulder.
3. Fall through the accessible pit; confirm SUBJECT INTEGRITY LOST, unchanged HUD charges, frozen player, blocked exit, and canonical Restart.
4. Ride the horizontal platform through both reversals/delay, walk relative to it, jump and dash off without drift.
5. Ride the vertical platform up and down, including reversals; verify grounding and ordinary world-speed jumping.
6. Let a turret shot hit; check environmental failure and no later shots. Place solid cover and verify it blocks shots.
7. Walk/jump against the wall, then dash into it; verify one charge and cleared collision. Check rejected/fatal attempts leave it solid.
8. Trigger a crusher from below; verify warning then underside death without spending. Check sides and stationary contact are safe.
9. Start above a crusher; land, ride down, walk, jump, and dash from its top without false death. Check solid-floor landing stops it.
10. Pause/resume moving mechanics and a dash; restart, return to menu, re-enter Play twice, check Console, HUD, duplicate UI/players, and a fresh scene reset.

Automated and scripted standalone evidence is distinct from developer/manual or physical-controller acceptance.
See CandidateReview for exact executed results and limitations.
