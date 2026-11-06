# P.I.D.R. blue art and research terminal, Revision 02

**BLUE-ART-TERMINAL-CANDIDATE-R02**  
Worktree `BlueArtTerminal`, branch `feature/blue-art-research-terminal`, baseline `d5694f569de90113f0a7bcd71d96e68bcf55c98e`. Unity 6000.3.26f1.

The developer's original Windows review recorded seven passes, one failure, and two unverified checks. That review and the original 334 PlayMode / 26 Editor passes are historical evidence. This report records the corrective work and fresh results.

## Root causes and corrections

- **Blink.** The original visible renderer already alternated `BlueTerminal_Blink` and `BlueTerminal_Norm` over multiple loops in Play Mode. The empty `m_Sprite` binding resolves because the Animator and SpriteRenderer sit on the same visual child. The default state, loop, controller and both sprite references work. The two source frames differ at a single pixel, (25,14) from the top left, which changes from RGB (32,214,199) to (166,252,219). A 3×3 pixel status accent at that point now tracks the actual blink frame. It uses the existing square sprite. The original frames, clip, controller, terminal position and proximity range retain their original values. Opening the modal pauses animation along with gameplay.
- **Textbox.** The original `FitInParent` fitter ran under the full-screen Canvas and drove the frame anchors past the intended margins. The turquoise area and blue rectangle are both part of Ace's original 32×18 textbox sprite. The authored text started blank. The fitter now operates inside a centered bounds element, keeping the 16:9 art within 64% of the viewport width and 68% of its height. A separate dimming image sits behind it. The title, body and Close control have dedicated, contrasting regions inside the frame.
- **Editor navigation.** Before correction, the saved Editor scene setup pointed at `Assets/Scenes/SampleScene.unity`. An actual Play entry in that scene reproduced the missing player and bank. The blue verification scene spawned one of each, enabled the input component and Rigidbody2D, and started at time scale 1 with the terminal closed. A focused Game view accepted the scripted keyboard and mouse route on two separate Play entries. The developer's historical Game view focus remains unknown. The candidate bootstrap, shared input asset and global Editor input settings retained their existing behavior.
- **Platform collision.** The isolated room has 3-cell and 7-cell traversal samples made from the existing blue prefabs. Their art meets the colliders at the left, right and top edges, with Point filtering and the existing 100 PPU art / 5× visual scaling. Jett landed, walked, jumped from one to the other and dashed across the wider platform. No traversal collision gap was observed on that route. Decorative tile repetitions are visual. The production grid and player collision dimensions retain their existing values.

## Editor and Windows retest

1. In the **BlueArtTerminal** Unity project, open `Assets/Game/Scenes/Verification/BlueArtTerminal.unity`. Press Play and click inside **Game** before using the keyboard. The scene supports direct Play entry. Its exit uses the already registered verification destination. Main Menu → New Game opens production Level01.
2. Alternatively, run `Builds/BlueArtTerminal/R02/ProjectPIDR-BlueCandidate-R02.exe`. This development executable opens the isolated room. The original candidate executable remains available in its original folder.
3. Move with A/D or Left/Right, jump with W/Up and dash with Space. Use Escape to pause or resume during gameplay. Gamepad equivalents are left stick, South to jump, Right Shoulder to dash and Start to pause. The automated checks used virtual keyboard and mouse.
4. Approach the terminal at X=8 and press E or gamepad West/X. Release the opening control. Click Close, press E or Escape, or use the existing gamepad close controls West/X, East/B or Start. Release held inputs before returning to movement.
5. Walk and jump across the two added platforms, then enter the exit. To repeat the isolated test, stop and re-enter Play with the blue scene open, or relaunch the R02 executable. The normal Game Over Restart button goes to canonical Level01.

To author a different entry, edit the TMP **Text** on `Title - edit TMP text` and `Lore - edit TMP text` inside the terminal prefab or on a scene instance. Instances can show different entries using the same frame. An empty or missing body shows the empty-entry message. The verification sample is labeled temporary. For content beyond the tested wrapped entry, check the target viewport in Play Mode. The frame uses bounded auto-sizing and truncates overflow at its minimum text size.

| Retest | What to check |
| --- | --- |
| A. Blink | Watch the terminal status change for at least two loops in Game view and in the executable. Verify the cabinet stays in place. |
| B. Textbox | Verify the title, sample entry, margins and Close control. Try a longer instance entry and an empty instance. Reopen after moving. |
| C. Editor | Confirm the exact scene and focused Game view. Move left/right, jump, dash, interact, close, exit, then stop and re-enter Play. |
| D. Platforms | Land on both widths, walk their tops, jump from the first to the second and dash along the wide top. Check both edges of each solid. |
| E. Regression | Carry forward the seven original passes. Recheck proximity, Close and input release, Pause/Game Over priority, HUD, doors, and the exit. The presentation and two isolated traversal samples are the areas changed in R02. |

## Verification evidence

The two focused, visible Editor Game View sessions each completed left/right movement, jump, dash, both sprite frames, opening with E, actual UI button close, Escape close, wrapped and empty text, Pause/Resume, two-width platform traversal and the physical exit. The visible sessions captured at 2560×1440. The historical startup reproduction and runtime captures are in `Logs/BlueIntegration/R02/VisibleEditor/`.

| Fresh check | Result |
| --- | --- |
| Complete PlayMode runs 1 and 2 | 336/336 each, zero failures or skips |
| Complete Editor runs 1 and 2 | 26/26 each, zero failures or skips |
| Final Windows development build | Succeeded, zero errors and warnings |
| Final standalone run | Exit 0, including a fresh-room restart and repeated terminal interaction |
| Display sizes in the final Windows run | 1280×720, 1920×1080, 2560×1440, 1366×900 |
| Preservation and asset references | 616 of 622 original paths byte-identical, zero missing paths, duplicate asset GUIDs, missing metadata or unresolved references in the changed scene and prefab |

| Parity check | Focused Unity Game view | Windows executable |
| --- | --- | --- |
| Startup | Direct Play of the blue scene, twice | Isolated room launches directly |
| Movement | A/D left and right | A/D left and right |
| Jump | W, landing and jumping from the narrow sample | Same route |
| Dash | Space, one charge spent across wide sample | Same route |
| Blink | Both frames recur and the terminal stays still | Both rendered frames differ and recur |
| Interaction | E in range, with release gating | Same input sequence |
| Textbox | Short, wrapped and empty captures | Same states, including wrapped captures at all four sizes |
| Close | Virtual mouse button and Escape | Same inputs, movement resumes |
| Pause/Resume | Escape while outside the terminal | Same inputs |
| Exit/repeat | Physical exit, then a second fresh Play entry | Physical exit and fresh room reload |

The checks use scripted keyboard and mouse events through Unity input and real Rigidbody2D movement. Physical gamepad verification remains open. Existing PlayMode tests cover the terminal's pause and Game Over ownership, proximity, release gating and scene unload. The additional cases cover repeated sprite changes and independent terminal entries, including blank and missing bodies.

The first Windows verifier completed the route, but its asynchronous screenshot calls produced identical blink PNGs. The final verifier reads each rendered frame synchronously at the end of the frame and checks the actual LED pixel. The repeat build and full standalone route passed. At 1280×720, the LED capture difference spans 6×6 pixels and shows both original colors. This last adjustment affects the opt-in evidence capture. The full suites ran on the same gameplay code and assets.

Runtime evidence under `Logs/BlueIntegration/R02/StandaloneFinal/`

- `01-terminal-normal.png` and `02-terminal-blink.png` show the two frames.
- `03-research-short.png` and `05-empty-entry.png` show the short entry and empty state.
- `04-research-1280x720.png`, `04-research-1920x1080.png`, `04-research-2560x1440.png` and `04-research-1366x900.png` show the wrapped entry.
- `06-platform-traversal.png` shows the wider platform after the dash. `result.txt` records the input route.

## Exact revision inventory

| Path | Change |
| --- | --- |
| `Assets/Game/Editor/BlueIntegration/BlueIntegrationBuilder.cs` | Targeted prefab and isolated-scene updates plus the separate R02 build output |
| `Assets/Game/Prefabs/BlueEnvironment/BlueTerminal.prefab` | Bounded frame, backdrop, text roles, empty-state label and status accent |
| `Assets/Game/Runtime/UI/BlueTerminalDevelopmentCheck.cs` | Opt-in focused input, physics route and runtime evidence checks |
| `Assets/Game/Runtime/UI/ResearchTerminal.cs` | Editable title reference and per-open empty-entry presentation |
| `Assets/Game/Runtime/UI/TerminalBlinkIndicator.cs` and `.cs.meta` | New renderer that follows the original Animator's blink sprite |
| `Assets/Game/Scenes/Verification/BlueArtTerminal.unity` | Temporary example entry and two traversal samples |
| `Assets/Game/Tests/BlueIntegration/ResearchTerminalTests.cs` | Runtime blink and entry regressions |
| `BLUE-ART-TERMINAL-CANDIDATE-R02.md` | This retest and verification report |

The audit covers all 622 original candidate paths. Ace's 16 imported art, animation and metadata paths match source commit `1ab8f22556336cd34361920bc5e49403ad966dd7`. Existing asset GUIDs, production scenes, Worm's levels present in the baseline, movement/input assets, player and bank prefabs, menus, Credits, Pause and Game Over integration, and level authoring tools match the original candidate. The original build and acceptance evidence remain available. Branch HEAD is the original baseline and staging is empty. See `Logs/BlueIntegration/R02/preservation-r02.json` for the byte and reference checks.
