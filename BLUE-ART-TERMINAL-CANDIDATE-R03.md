# P.I.D.R. blue art and research terminal, Revision 03

**BLUE-ART-TERMINAL-CANDIDATE-R03**  
Unity 6000.3.26f1. Worktree `BlueArtTerminal`, branch `feature/blue-art-research-terminal`, baseline `d5694f569de90113f0a7bcd71d96e68bcf55c98e`.

The developer's R02 retest recorded four passes and one visual failure. Blink, Editor navigation, platform traversal and regression remain the accepted reference behavior. The textbox is the visual retest focus.

## Research terminal frame

The turquoise field, blue top-left corner and mint bottom stripe are all pixels in Ace's original 32×18 `BlueTerminalTextbox.png`. Its Multiple sprite import supplies the 32×18 slice at 100 pixels per unit with Point filtering. The existing Canvas Image displays that sprite at its original aspect. R02 used most of the turquoise field as the text background.

R03 uses the supplied sprite as the outer frame. The inset navy chassis occupies the uniform turquoise region, leaving the corner and stripe exposed. The header, reading area and action rail use separate surfaces and clear gutters. The light title and body have separate margins, and Close sits at the right edge of the rail. The dim backdrop is its own Canvas element.

Edit the title and body on each terminal instance using the existing `Title - edit TMP text` and `Lore - edit TMP text` references. The entries in the verification room are labeled temporary. Blank or missing body text displays the empty-state message. The tested long entry wraps within the reading surface. Longer entries still use the existing minimum font size and overflow truncation.

## Held controls at scene entry

The focused baseline held each control before crossing the real BlueExit trigger. The fixture settles the outgoing physics and positions the body inside the trigger. It then observes the actual destination player, with the virtual key still held, for 12 fixed ticks. A separate visible route walks across the room and through the exit with Up and D held.

| Input at boundary | R02 baseline | R03 focused check |
| --- | --- | --- |
| Up held through exit | Fresh jump, 9.411 peak upward velocity | No upward impulse. Fresh press after release jumps. |
| W held through exit | Fresh jump, 9.411 peak upward velocity | No upward impulse. Fresh press after release jumps. |
| A held through exit | 1.440 units left, continuous movement | Same 1.440 units left |
| D held through exit | 1.440 units right, continuous movement | Same 1.440 units right |
| Space held through exit | One intentional source dash, four charges at arrival | Four charges at arrival. Fresh Space after release spends once. |
| Keys released before exit | Idle at arrival, four charges after earlier source dash | Same idle entry and four charges |
| Up and Space held at New Game | Fresh jump, five charges | Idle entry and five charges. Fresh Jump and Dash work after release. |

The private Move value action runs an initial state check on enable. Its W and Up controls are also bound to Jump. The Input System signals the shared control monitor, which explains the Jump callback even with Jump's own initial check disabled. The input adapter now keeps Jump disabled when a bound Jump control is already held at enable. It enables Jump after observing release. Move stays active and the existing Dash arbitration stays in place.

The first probe sampled charges before the outgoing physics tick. It reported two ambiguous charge failures. The corrected baseline settles the source dash before crossing and reproduces three failures, all fresh Jump impulses. The same 15-case fixture passed after the Jump gate, with the expected source spending and arrival balances.

## Restart and developer retest

Game Over **Restart Run** calls `NewGameLauncher.TryStartNewGame`, initializes the run, and loads canonical **Level01**. The focused Blue-room failure test confirmed that destination and the fresh five-charge balance. To replay the isolated room, stop Play, reopen `Assets/Game/Scenes/Verification/BlueArtTerminal.unity`, and press Play with **Game** focused. For Windows, close and relaunch `Builds/BlueArtTerminal/R03/ProjectPIDR-BlueCandidate-R03.exe`.

1. Open the terminal with **E** near X=8. Inspect the frame, header, reading surface and action rail.
2. Check the text and button at 1280×720, 1920×1080, 2560×1440 and 1366×900. Try the short entry, a wrapped entry and an empty instance.
3. Close with the button or **Escape** after releasing E. Reopen after moving and confirm movement resumes on close.
4. Approach the exit while holding **D** and **Up**. Keep them down through the transition. Confirm walking continues and a fresh jump requires release and another press. Repeat with **W**.
5. Hold **Space** through the exit after one deliberate dash in the source room. Compare the balance immediately before and after crossing.
6. Verify **Restart Run** returns to Level01 with five charges. Use the isolated-scene launch steps above to replay the blue room.
7. Check the accepted blink and both blue platforms again. **A/D** move, **W/Up** jump, and **Space** dashes. The existing gamepad controls are available for hands-on retest.

## Verification evidence

The before capture is R02's `Logs/BlueIntegration/R02/StandaloneFinal/04-research-1280x720.png`. It uses the same wrapped verification entry and blue room as R03, so the change in framing and reading contrast can be compared directly.

| Executed verification | Result |
| --- | --- |
| Corrected baseline focus fixture | 12/15. Fresh Jump reproduced with held Up, held W and New Game with Up and Space held. |
| Same focus fixture after correction | 15/15 |
| Complete PlayMode run 1 | 343/343, zero failures or skips |
| Complete PlayMode run 2 | 343/343, zero failures or skips |
| Complete Editor run 1 | 26/26, zero failures or skips |
| Complete Editor run 2 | 26/26, zero failures or skips |
| R03 Windows development build | Succeeded, zero errors and warnings |
| Focused visible Editor Play | Passed at 2560×1440, including blink, terminal UI, Pause/Resume, platform traversal and the held-Up/D exit |
| R03 Windows route | Exit 0. Same gameplay route, four display sizes and a fresh verification-room replay. |

The focused routes use a virtual keyboard and mouse through Unity input and real physics. Physical gamepad retest remains open. The Windows result records the actual dimensions and the input sequence. All four runtime images show the complete frame, the wrapped sample and Close over the blue room.

- **R03 after**, matched 1280×720. `Logs/BlueIntegration/R03/StandaloneFinal/04-research-1280x720.png`
- **Other Windows sizes.** `04-research-1920x1080.png`, `04-research-2560x1440.png`, `04-research-1366x900.png` in the same folder.
- **Short and empty entries.** `03-research-short.png` and `05-empty-entry.png`.
- **Previously accepted visuals.** `01-terminal-normal.png`, `02-terminal-blink.png` and `06-platform-traversal.png`.
- **Logs.** `Logs/BlueIntegration/R03/VisibleEditor/result.txt`, `Logs/BlueIntegration/R03/StandaloneFinal/result.txt`, and the XML reports in the R03 log folder.

## Exact revision inventory

| Path | Change |
| --- | --- |
| `Assets/Game/Editor/BlueIntegration/BlueIntegrationBuilder.cs` | Authors the existing terminal prefab's inset surfaces and builds the separate R03 executable |
| `Assets/Game/Prefabs/BlueEnvironment/BlueTerminal.prefab` | Navy header, reading surface, action rail and updated TMP colors |
| `Assets/Game/Runtime/Movement/PlayerMovementInput2D.cs` | Gates a held Jump binding on enable until release |
| `Assets/Game/Runtime/UI/BlueTerminalDevelopmentCheck.cs` | Checks text margins on the styled surfaces and walks the held-Up exit route |
| `Assets/Game/Tests/BlueIntegration/ResearchTerminalTests.cs` | Seven boundary cases covering Up, W, A, D, Space, neutral entry and canonical new-run routes |
| `BLUE-ART-TERMINAL-CANDIDATE-R03.md` | This report and retest handoff |

The preservation audit found 620 of 625 original R02 paths byte-identical. The five modified paths above account for the difference. The blue verification scene itself, production scenes, existing metadata, camera, player and bank prefabs, shared input asset, and navigation and failure systems match R02. All 16 imported Ace paths match art commit `1ab8f22556336cd34361920bc5e49403ad966dd7`. The audit found zero duplicate GUIDs, missing metadata or unresolved references in the changed prefab. Staging is empty and HEAD is the original baseline. Audit details are in `Logs/BlueIntegration/R03/preservation-r03.json`.
