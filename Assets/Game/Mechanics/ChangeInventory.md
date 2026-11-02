# Pass 1I exact change inventory

Candidate implementation: **94 paths — 87 additions, 7 modifications, no deletions**. Includes Unity file/folder metadata. No files are staged or committed.

Existing source changes are limited to keyboard input, shared platform support, approved dash reactions, typed bank failure, and cause-specific existing UI. Build registration appends only the isolated verification scene.

## Modified existing paths

```text
Assets/Game/Runtime/Movement/PlayerMovement2D.Dash.cs
Assets/Game/Runtime/Movement/PlayerMovement2D.cs
Assets/Game/Runtime/Movement/PlayerMovementInput2D.cs
Assets/Game/Runtime/Resources/RunDashBank.cs
Assets/Game/Runtime/UI/GameOverController.cs
Assets/Game/Runtime/UI/MobilityReserveHUDController.cs
ProjectSettings/EditorBuildSettings.asset
```

## Added paths

```text
Assets/Game/Editor/Mechanics.meta
Assets/Game/Editor/Mechanics/GameJam.Mechanics.Editor.asmdef
Assets/Game/Editor/Mechanics/GameJam.Mechanics.Editor.asmdef.meta
Assets/Game/Editor/Mechanics/MechanicsBuilder.cs
Assets/Game/Editor/Mechanics/MechanicsBuilder.cs.meta
Assets/Game/Mechanics.meta
Assets/Game/Mechanics/ArtRequirementsMatrix.md
Assets/Game/Mechanics/ArtRequirementsMatrix.md.meta
Assets/Game/Mechanics/AuthoringGuide.md
Assets/Game/Mechanics/AuthoringGuide.md.meta
Assets/Game/Mechanics/CandidateReview.md
Assets/Game/Mechanics/CandidateReview.md.meta
Assets/Game/Mechanics/ChangeInventory.md
Assets/Game/Mechanics/ChangeInventory.md.meta
Assets/Game/Prefabs/BreakableWall.prefab
Assets/Game/Prefabs/BreakableWall.prefab.meta
Assets/Game/Prefabs/FallDeathZone.prefab
Assets/Game/Prefabs/FallDeathZone.prefab.meta
Assets/Game/Prefabs/FallingCrusherPlatform.prefab
Assets/Game/Prefabs/FallingCrusherPlatform.prefab.meta
Assets/Game/Prefabs/MovingPlatform.prefab
Assets/Game/Prefabs/MovingPlatform.prefab.meta
Assets/Game/Prefabs/Turret.prefab
Assets/Game/Prefabs/Turret.prefab.meta
Assets/Game/Prefabs/TurretProjectile.prefab
Assets/Game/Prefabs/TurretProjectile.prefab.meta
Assets/Game/Runtime/Mechanics.meta
Assets/Game/Runtime/Mechanics/BreakableWall2D.cs
Assets/Game/Runtime/Mechanics/BreakableWall2D.cs.meta
Assets/Game/Runtime/Mechanics/CrusherActivationZone2D.cs
Assets/Game/Runtime/Mechanics/CrusherActivationZone2D.cs.meta
Assets/Game/Runtime/Mechanics/FallDeathZone2D.cs
Assets/Game/Runtime/Mechanics/FallDeathZone2D.cs.meta
Assets/Game/Runtime/Mechanics/FallingCrusherPlatform2D.cs
Assets/Game/Runtime/Mechanics/FallingCrusherPlatform2D.cs.meta
Assets/Game/Runtime/Mechanics/GameJam.Mechanics.asmdef
Assets/Game/Runtime/Mechanics/GameJam.Mechanics.asmdef.meta
Assets/Game/Runtime/Mechanics/HazardPlayer2D.cs
Assets/Game/Runtime/Mechanics/HazardPlayer2D.cs.meta
Assets/Game/Runtime/Mechanics/MovingPlatform2D.cs
Assets/Game/Runtime/Mechanics/MovingPlatform2D.cs.meta
Assets/Game/Runtime/Mechanics/PhysicsPlatform2D.cs
Assets/Game/Runtime/Mechanics/PhysicsPlatform2D.cs.meta
Assets/Game/Runtime/Mechanics/Turret2D.cs
Assets/Game/Runtime/Mechanics/Turret2D.cs.meta
Assets/Game/Runtime/Mechanics/TurretProjectile2D.cs
Assets/Game/Runtime/Mechanics/TurretProjectile2D.cs.meta
Assets/Game/Runtime/MechanicsVerification.meta
Assets/Game/Runtime/MechanicsVerification/GameJam.MechanicsVerification.asmdef
Assets/Game/Runtime/MechanicsVerification/GameJam.MechanicsVerification.asmdef.meta
Assets/Game/Runtime/MechanicsVerification/MechanicsStandaloneCheck.cs
Assets/Game/Runtime/MechanicsVerification/MechanicsStandaloneCheck.cs.meta
Assets/Game/Runtime/Movement/IDashImpactSurface2D.cs
Assets/Game/Runtime/Movement/IDashImpactSurface2D.cs.meta
Assets/Game/Runtime/Movement/IPlayerSupport2D.cs
Assets/Game/Runtime/Movement/IPlayerSupport2D.cs.meta
Assets/Game/Runtime/Movement/PlayerMovement2D.Support.cs
Assets/Game/Runtime/Movement/PlayerMovement2D.Support.cs.meta
Assets/Game/Runtime/Resources/RunFailureCause.cs
Assets/Game/Runtime/Resources/RunFailureCause.cs.meta
Assets/Game/Scenes/Verification.meta
Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity
Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity.meta
Assets/Game/Tests/UnifiedGameplay.meta
Assets/Game/Tests/UnifiedGameplay/BreakableWallTests.cs
Assets/Game/Tests/UnifiedGameplay/BreakableWallTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/CrusherTests.cs
Assets/Game/Tests/UnifiedGameplay/CrusherTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/Editor.meta
Assets/Game/Tests/UnifiedGameplay/Editor/GameJam.Mechanics.EditorTests.asmdef
Assets/Game/Tests/UnifiedGameplay/Editor/GameJam.Mechanics.EditorTests.asmdef.meta
Assets/Game/Tests/UnifiedGameplay/Editor/MechanicsAuthoringTests.cs
Assets/Game/Tests/UnifiedGameplay/Editor/MechanicsAuthoringTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/EnvironmentalFailureTests.cs
Assets/Game/Tests/UnifiedGameplay/EnvironmentalFailureTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/GameJam.UnifiedGameplay.PlayModeTests.asmdef
Assets/Game/Tests/UnifiedGameplay/GameJam.UnifiedGameplay.PlayModeTests.asmdef.meta
Assets/Game/Tests/UnifiedGameplay/KeyboardTravelTests.cs
Assets/Game/Tests/UnifiedGameplay/KeyboardTravelTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/MechanicsSceneTests.cs
Assets/Game/Tests/UnifiedGameplay/MechanicsSceneTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/MechanicsTestFixture.cs
Assets/Game/Tests/UnifiedGameplay/MechanicsTestFixture.cs.meta
Assets/Game/Tests/UnifiedGameplay/MovingPlatformTests.cs
Assets/Game/Tests/UnifiedGameplay/MovingPlatformTests.cs.meta
Assets/Game/Tests/UnifiedGameplay/TurretTests.cs
Assets/Game/Tests/UnifiedGameplay/TurretTests.cs.meta
```

## Excluded and preserved

- `.vscode/settings.json`
- `UnifiedGameplayFoundation.slnx`
- `ProjectSettings/SceneTemplateSettings.json`
- Ignored `Logs/`, `Builds/`, `Library/`, `Temp/`, and local evidence/captures.

The five incidental build-serialized settings files were backed up under ignored Logs and restored to their verified initial bytes. Temporary Editor patch helpers and their metadata were removed; none were accepted baseline files.

All accepted scenes, prefabs, art, animations, input asset, tests and existing `.meta` files are retained. See `CandidateReview.md` and `Logs/UnifiedGameplay/preservation-audit.json` for validation and preservation evidence.
