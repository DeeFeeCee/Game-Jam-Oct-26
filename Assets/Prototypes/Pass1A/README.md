# Pass 1A: movement foundation

Open `Scenes/MovementTest.unity` with Unity **6000.3.26f1**. This isolated scene does
not replace SampleScene and is intentionally not added to the build scene list.
Use a 16:9 Game view to see the full test area at once.

## Controls and quick verification

- Move: A/D, left/right arrows, or gamepad left stick.
- Jump: Space or gamepad south button.
- Click the Game view before testing keyboard input.

The cyan rectangle is the placeholder player. Check the floor, purple overhead
ceiling near the spawn, three raised platforms, and tall purple wall on the right.
There is no restart, death, dash, dash bank, or puzzle behavior in this prototype.

1. Move both directions, release the controls, and reverse direction.
2. Jump from the floor. Press again while airborne: no additional jump should occur.
3. Keep Jump held through landing: it should not automatically jump again.
4. Release Jump, then press again after landing: jumping should work again.
5. Jump into the low ceiling. The player should stop rising and fall normally.
6. Move against the tall wall, including while airborne. It should block movement
   without granting a wall jump. Reverse direction to move away.
7. Jump up the steps, then walk off an edge. Jumping should require ground again.
8. Select the player and adjust movement and Rigidbody2D values in the Inspector.
   Play Mode changes are temporary unless deliberately saved outside Play Mode.
9. Exit and re-enter Play Mode several times with the project's existing domain
   reload setting. Confirm a fresh spawn, no stuck movement or spontaneous jump,
   and no unexpected Console errors. Also check losing/regaining Game view focus.

## Components

`Runtime/PlayerMovementInput2D.cs` clones the assigned project Input System asset
for the lifetime of the enabled component. It enables only `Player/Move` and
`Player/Jump`, polls horizontal input in Update, and queues individual jump presses.
It disables its action copy and removes its callback when disabled. It does not
edit the shared input asset, depend on a generated wrapper, or use static state.

`Runtime/PlayerMovement2D.cs` owns physics. FixedUpdate sets horizontal
`Rigidbody2D.linearVelocity` and consumes the queued jump once. Jump eligibility
comes from a short downward BoxCast at the feet. The inset query ignores the
player's own body and triggers, applies the selected ground mask, and requires an
upward-facing hit normal. Upward velocity prevents floor proximity from allowing
another jump while rising. Airborne jump presses are discarded, not buffered.

`Prefabs/PlaceholderPlayer.prefab` combines those components, a BoxCollider2D, a
dynamic Rigidbody2D, and a separate placeholder visual child. Rotation is frozen,
collision detection is Continuous, interpolation is enabled, and the player's
physics material has zero friction/bounce so it can slide along walls.

## Provisional tuning, not final design requirements

| Setting | Initial prototype value | Where to adjust |
| --- | --- | --- |
| Horizontal speed | 6 units/second | PlayerMovement2D |
| Jump speed | 10 units/second upward | PlayerMovement2D |
| Ground probe distance | 0.06 units | PlayerMovement2D |
| Ground probe inset per side | 0.05 units | PlayerMovement2D |
| Maximum ground angle | 45 degrees | PlayerMovement2D |
| Ground layers | Everything; own body/triggers excluded | PlayerMovement2D |
| Gravity scale | 3 | Rigidbody2D |
| Collider size | 0.8 by 1.6 units | BoxCollider2D |

This pass uses immediate stopping/reversal, full horizontal air control, one
fixed-strength jump per press, and no coyote time or jump buffering. These are
small prototype choices for review, not permanent movement requirements.

Keep the physics root upright and at unit scale; change the collider dimensions
and the visual child as needed. The bounds-based foot query is intended for this
upright box player. Very narrow ledges may need a smaller probe inset. Moving
platforms, unusual slopes, one-way platforms, multiplayer device pairing, and
dash-related collision behavior are not established by this pass.

## Automated checks

Open **Window > General > Test Runner**, choose **PlayMode**, and run
`GameJam.Movement.Tests.PlayerMovementTests`. Tests use the actual prefab and
existing input bindings, synthetic keyboard/gamepad devices, and live 2D physics.
The prototype scene itself is also loaded and exercised without changing build
settings. Tests remove their objects/devices and fail on unexpected Unity logs.

The suite covers movement/stop/reversal, arrows/gamepad input, grounded jumps,
midair rejection, landing, held Jump, floors/walls/ceilings, platform edges,
triggers, ground masks, Inspector tuning, input disable/re-enable, and scene wiring.
Re-enabling input is a lifecycle check; it alone does not prove repeated full
Play sessions with domain reload disabled.

Validation on October 20, 2026 used the interactive Unity **6000.3.26f1** Editor:

- Unity imported/compiled the runtime, builder, and tests and generated the actual
  prefab and scene. Earlier standalone compilation also checked the APIs against
  the same installed version's assemblies.
- Initial Play Mode run: 12 passed, 3 failed in test setup/cleanup. The fixture was
  corrected to restore unloaded default Input Settings safely and clear residual
  falling velocity before zero-gravity wall/trigger checks. The runtime movement
  code did not need changes for those failures.
- Corrected Play Mode run 1: **15 passed, 0 failed, 0 skipped**.
- Corrected Play Mode run 2: **15 passed, 0 failed, 0 skipped**.
- Both corrected runs used the existing **DisableDomainReload** option. Each test
  checked for unexpected Unity logs during its execution and teardown.
- A temporary automation helper produced an Editor test-scene restoration error
  outside the passing test cases by starting another run before Test Runner cleanup
  finished. This is a validation-tool limitation, not a claimed clean end-to-end
  Editor session. The temporary helper is not part of the delivered prototype.
- A later automation attempt did not produce a completed test report and is not
  counted as an executed passing run. No hands-on keyboard/gamepad or visual
  validation is claimed; perform the manual checklist above before acceptance.

NUnit reports are retained locally in the ignored `Library/Pass1A/` folder as
`playmode-initial.xml`, `playmode-1.xml`, and `playmode-2.xml`. Clear earlier failed
test/helper messages from the Console before checking the prototype manually.
Run the suite from Test Runner normally, allowing each run to fully return to
Edit Mode before starting another.

## Delivered assets

The prefab, scene, placeholder sprite, unlit material, and physics material were
generated through Unity APIs. Temporary generation and automated-run helpers were
removed after use. Open the delivered scene directly; use the prefab to create
additional test scenes without needing any generation tool.

All work belongs to `feature/player-movement-dash`. No commit, publication, push,
merge, character-art replacement, or dash implementation is part of Pass 1A.
