using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameJam.BlueIntegration.Editor;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Presentation;
using GameJam.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJam.BlueIntegration.Tests
{
    public sealed class ProductionEnvironmentArtTests
    {
        private const string Prefix = "Assets/Game/Scenes/Levels/Level";
        private const string Ending = "Assets/Game/Scenes/Ending/EndingSequence.unity";
        private const string Art = "Assets/Art/Levels/";

        [Test]
        public void EveryProductionRoomHasOnlyItsOriginalSolidsAndExit()
        {
            int[] solids = { 9, 9, 12, 14, 13, 17, 14, 23, 10, 17, 16, 17 };
            int[] triggers = { 1, 2, 2, 2, 1, 2, 2, 2, 7, 1, 2, 2 };
            for (int level = 1; level <= 12; level++)
            {
                int room = level;
                InScene($"{Prefix}{room:00}.unity", scene =>
                {
                    var boxes = All<Collider2D>(scene).ToArray();
                    bool hasDecorativeTerminal = room is 1 or 3 or 4 or 5 or 6 or 7 or 8 or 11;
                    Assert.AreEqual(solids[room - 1], boxes.Count(box => !box.isTrigger), scene.name);
                    Assert.AreEqual(solids[room - 1] - (hasDecorativeTerminal ? 1 : 0),
                        boxes.Count(box => !box.isTrigger && box.enabled), scene.name);
                    Assert.AreEqual(hasDecorativeTerminal ? 1 : 0, boxes.Count(box => !box.isTrigger && !box.enabled), scene.name);
                    Assert.AreEqual(triggers[room - 1], boxes.Count(box => box.isTrigger), scene.name);
                    var exit = All<LevelExit>(scene).Single();
                    Assert.IsTrue(exit.GetComponent<Collider2D>().isTrigger);
                    Assert.AreEqual(room == 12 ? Ending : $"{Prefix}{room + 1:00}.unity", exit.transition.destinationScene);
                    bool yellow = room >= 5 && room <= 8;
                    var door = exit.transform.Find(ProductionEnvironmentAuthoring.ExitName);
                    Assert.AreEqual(Art + (yellow ? "YGExit.png" : "BlueExit.png"), AssetDatabase.GetAssetPath(door.GetComponent<SpriteRenderer>().sprite));
                    var setup = All<LevelSetup>(scene).Single();
                    var entrance = setup.spawn.Find(ProductionEnvironmentAuthoring.EntranceName);
                    Assert.AreEqual(Art + (yellow ? "YGEnter.png" : "BlueEnter.png"), AssetDatabase.GetAssetPath(entrance.GetComponent<SpriteRenderer>().sprite));
                    Assert.IsEmpty(entrance.GetComponentsInChildren<Collider2D>(true));
                    Assert.IsEmpty(door.GetComponentsInChildren<Collider2D>(true));
                    CheckBackground(scene, room < 5 ? EnvironmentZone.Blue :
                        yellow ? EnvironmentZone.YellowGreen : EnvironmentZone.Orange);
                    Assert.IsEmpty(All<ResearchTerminal>(scene), "Only authored, noninteractive terminal placeholders are present.");
                    foreach (var art in All<SpriteRenderer>(scene).Where(renderer =>
                        renderer.name == ProductionEnvironmentAuthoring.SurfaceName && renderer.enabled))
                    {
                        var shape = art.GetComponentInParent<BoxCollider2D>();
                        Assert.IsNotNull(shape);
                        Assert.IsNotNull(art.sprite);
                        Assert.AreEqual(shape.bounds.max.y, art.bounds.max.y, .001f, art.transform.parent.name);
                        Assert.AreEqual(shape.bounds.size.x, art.bounds.size.x, .001f);
                    }
                });
            }
        }

        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        public void AuthoringTwicePreservesIdentityAndDoesNotAddCollidersOrTriggers(int room)
        {
            InScene($"{Prefix}{room:00}.unity", scene =>
            {
                var originals = All<Collider2D>(scene).ToDictionary(box => box, box => EditorJsonUtility.ToJson(box));
                var mechanics = All<MovingPlatform2D>(scene).Cast<Component>().Concat(All<FallingCrusherPlatform2D>(scene))
                    .Concat(All<Turret2D>(scene)).ToDictionary(component => component, component => EditorJsonUtility.ToJson(component));
                var exits = All<LevelExit>(scene).ToArray();
                var artObjects = All<Transform>(scene).Count();
                var warning = $"Level{room:00}: {ProductionEnvironmentAuthoring.OrangePending}. Temporary Blue kit retained.";
                LogAssert.Expect(LogType.Warning, warning);
                ProductionEnvironmentAuthoring.Apply(scene);
                LogAssert.Expect(LogType.Warning, warning);
                ProductionEnvironmentAuthoring.Apply(scene);
                Assert.AreEqual(artObjects, All<Transform>(scene).Count());
                CollectionAssert.AreEquivalent(originals.Keys, All<Collider2D>(scene));
                CollectionAssert.AreEqual(exits, All<LevelExit>(scene));
                foreach (var pair in originals)
                    Assert.AreEqual(pair.Value, EditorJsonUtility.ToJson(pair.Key));
                foreach (var pair in mechanics)
                    Assert.AreEqual(pair.Value, EditorJsonUtility.ToJson(pair.Key));
            });
        }

        [Test]
        public void YellowGreenPreviewHasOwnThemeAndUsesTheUnchangedResearchLog()
        {
            string acceptedLore = null;
            InScene(BlueIntegrationBuilder.Scene, scene => acceptedLore = All<ResearchTerminal>(scene).Single().lore.text);
            InScene(ProductionEnvironmentAuthoring.PreviewPath, scene =>
            {
                Assert.IsFalse(EditorBuildSettings.scenes.Any(entry => entry.path == scene.path));
                CheckBackground(scene, EnvironmentZone.YellowGreen);
                var terminal = All<ResearchTerminal>(scene).Single();
                Assert.IsFalse(string.IsNullOrWhiteSpace(acceptedLore));
                Assert.AreEqual(acceptedLore, terminal.lore.text);
                Assert.IsNotNull(terminal.closeButton);
                Assert.IsNotNull(terminal.inputScope);
                Assert.IsFalse(terminal.overlay.activeSelf);
                Assert.IsFalse(terminal.uiEventSystem.gameObject.activeSelf);
                var animator = terminal.GetComponentInChildren<Animator>();
                Assert.AreEqual(Art + "YGAnims/YGTerminal.controller", AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
                var frame = terminal.overlay.transform.Find("Textbox bounds/Ace textbox frame").GetComponent<UnityEngine.UI.Image>();
                Assert.AreEqual(Art + "YGTerminalTextbox.png", AssetDatabase.GetAssetPath(frame.sprite));
                Assert.AreEqual(15, All<SpriteRenderer>(scene).Count(renderer => renderer.enabled &&
                    AssetDatabase.GetAssetPath(renderer.sprite) == Art + "YGbgNew.png"));
            });
        }

        [Test]
        public void ReplacedPlatformFramesKeepOnlyTheSafeTopAndUseTheAssignedKit()
        {
            for (int level = 1; level <= 12; level++)
            {
                int room = level;
                InScene($"{Prefix}{room:00}.unity", scene =>
                {
                    bool yellow = room >= 5 && room <= 8;
                    var surfaces = All<SpriteRenderer>(scene).Where(renderer =>
                        renderer.name == ProductionEnvironmentAuthoring.SurfaceName && renderer.enabled).ToArray();
                    if (room != 9) Assert.IsNotEmpty(surfaces, scene.name);
                    foreach (var surface in surfaces)
                    {
                        string source = AssetDatabase.GetAssetPath(surface.sprite.texture);
                        Assert.IsTrue(yellow ? source == Art + "YGPlatforms.png" || source == Art + "YGLvlGround.png" :
                            source == Art + "BluePlatforms.png", surface.transform.parent.name);
                        Assert.IsEmpty(surface.GetComponentsInChildren<Collider2D>(true));
                    }
                    foreach (var shape in All<BoxCollider2D>(scene))
                    {
                        if (shape.isTrigger || shape.transform.Find(ProductionEnvironmentAuthoring.TerminalName) != null) continue;
                        var frame = shape.transform.Find("Gameplay surface frame");
                        if (frame == null || shape.transform.Find("Floor visuals") == null) continue;
                        Assert.IsTrue(frame.Find("Top").GetComponent<SpriteRenderer>().enabled);
                        foreach (var part in new[] { "Backing", "Bottom", "Left", "Right" })
                            Assert.IsFalse(frame.Find(part).GetComponent<SpriteRenderer>().enabled, $"{scene.name} {shape.name} {part}");
                    }
                });
            }
        }

        [TestCase(1, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        [TestCase(6, true)]
        [TestCase(7, true)]
        [TestCase(8, true)]
        [TestCase(11, false)]
        public void TerminalArtworkKeepsIdentityAndSupportWithoutActiveLegacyGeometry(int room, bool yellow)
        {
            InScene($"{Prefix}{room:00}.unity", scene =>
            {
                var terminal = All<Animator>(scene).Single(component => component.name == ProductionEnvironmentAuthoring.TerminalName);
                var shape = terminal.GetComponentInParent<BoxCollider2D>();
                Assert.IsNotNull(shape);
                Assert.IsFalse(shape.isTrigger);
                Assert.IsFalse(shape.enabled);
                CollectionAssert.AreEqual(new Collider2D[] { shape }, shape.GetComponentsInChildren<Collider2D>(true));
                var expectedRoot = room switch
                {
                    1 => new Vector3(9, 5.25f, 0),
                    3 => new Vector3(28.5f, 2, 0),
                    4 => new Vector3(16, 12, 0),
                    5 => new Vector3(20.5f, 2, 0),
                    6 => new Vector3(2.5f, 14, 0),
                    7 => new Vector3(29, 9, 0),
                    8 => new Vector3(2, 3, 0),
                    _ => new Vector3(11, 11, 0)
                };
                Assert.AreEqual(expectedRoot, shape.transform.position);
                Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, terminal.cullingMode);
                Assert.IsTrue(terminal.enabled);
                Assert.AreEqual(1, shape.GetComponentsInChildren<Animator>(true).Length);
                Assert.AreEqual(1, terminal.GetComponents<SpriteRenderer>().Length);
                Assert.AreEqual(1, terminal.GetComponents<TerminalBlinkIndicator>().Length);
                var accent = terminal.transform.Find("Blink status accent").GetComponent<SpriteRenderer>();
                Assert.IsFalse(accent.enabled);
                Assert.AreEqual(terminal.GetComponent<SpriteRenderer>().sortingOrder + 1, accent.sortingOrder);
                Assert.IsEmpty(terminal.GetComponentsInChildren<Collider2D>(true));
                Assert.IsEmpty(shape.GetComponentsInChildren<ResearchTerminal>(true));
                Assert.IsFalse(shape.transform.Find("Floor visuals").GetComponent<SpriteRenderer>().enabled);
                Assert.IsTrue(shape.transform.Find("Gameplay surface frame").GetComponentsInChildren<SpriteRenderer>(true)
                    .All(renderer => !renderer.enabled));
                Assert.AreEqual(Art + (yellow ? "YGTerminal.png" : "BlueTerminal.png"),
                    AssetDatabase.GetAssetPath(terminal.GetComponent<SpriteRenderer>().sprite));
                Assert.AreEqual(Art + (yellow ? "YGAnims/YGTerminal.controller" : "BlueAnims/TerminalBlink.controller"),
                    AssetDatabase.GetAssetPath(terminal.runtimeAnimatorController));
                var supportName = room switch
                {
                    3 => "Floor (1)",
                    4 => "GridPlatform32x18 (7)",
                    5 => "Floor",
                    6 => "GridPlatform32x18 (5)",
                    7 => "GridPlatform32x18 (7)",
                    8 => "GridPlatform32x18 (8)",
                    11 => "GridPlatform32x18 (2)",
                    _ => "GridPlatform32x18 (1)"
                };
                var support = All<BoxCollider2D>(scene).Single(box => box.name == supportName);
                var top = support.transform.Find("Gameplay surface frame/Top").GetComponent<SpriteRenderer>();
                Assert.AreEqual(support.bounds.max.y, top.bounds.max.y, .001f);
                Assert.IsTrue(top.enabled);
                var renderer = terminal.GetComponent<SpriteRenderer>();
                var clip = terminal.runtimeAnimatorController.animationClips.Single();
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                Assert.AreEqual(typeof(SpriteRenderer), binding.type);
                Assert.AreEqual("", binding.path);
                Assert.AreEqual("m_Sprite", binding.propertyName);
                Assert.IsTrue(clip.isLooping);
                var controller = (UnityEditor.Animations.AnimatorController)terminal.runtimeAnimatorController;
                Assert.AreEqual(clip, controller.layers.Single().stateMachine.defaultState.motion);
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(key => (Sprite)key.value).Distinct().ToArray();
                Assert.GreaterOrEqual(frames.Length, 2);
                foreach (var frame in frames)
                    Assert.AreEqual(top.bounds.max.y, ProductionEnvironmentAuthoring.VisibleTerminalBase(renderer, frame), .001f);
                var position = renderer.transform.position;
                var originalCollider = EditorJsonUtility.ToJson(shape);
                var supportCollider = EditorJsonUtility.ToJson(support);
                for (int i = 0; i < 2; i++)
                    ProductionEnvironmentAuthoring.ApplyDecorativeTerminal(shape, yellow, All<BoxCollider2D>(scene));
                Assert.AreEqual(accent, terminal.transform.Find("Blink status accent").GetComponent<SpriteRenderer>());
                Assert.AreEqual(1, terminal.GetComponents<TerminalBlinkIndicator>().Length);
                Assert.Less(Vector3.Distance(position, renderer.transform.position), .001f);
                Assert.AreEqual(originalCollider, EditorJsonUtility.ToJson(shape));
                Assert.AreEqual(supportCollider, EditorJsonUtility.ToJson(support));
            });
        }

        [Test]
        public void SwitchingTheThemeCanRestoreYellowGreenWithoutDuplicateObjectsOrColliders()
        {
            InScene(Prefix + "05.unity", scene =>
            {
                var originals = All<Collider2D>(scene).ToArray();
                int transforms = All<Transform>(scene).Count();
                CheckBackground(scene, EnvironmentZone.YellowGreen);
                ProductionEnvironmentAuthoring.Apply(scene, EnvironmentZone.Blue);
                CheckBackground(scene, EnvironmentZone.Blue);
                ProductionEnvironmentAuthoring.Apply(scene, EnvironmentZone.YellowGreen);
                CheckBackground(scene, EnvironmentZone.YellowGreen);
                Assert.AreEqual(transforms, All<Transform>(scene).Count());
                CollectionAssert.AreEquivalent(originals, All<Collider2D>(scene));
                Assert.AreEqual(1, All<EnvironmentTheme>(scene).Count());
            });
        }

        [Test]
        public void CorporateLogoCanvasClipPreservesEverySourceSpriteTimeAndLoop()
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Menu/MenuAnim/Logo_Scan.anim");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Game/UI/Animations/PromiseBeaconLogoUI.controller");
            var canvas = controller.animationClips.Single();
            var originalBinding = AnimationUtility.GetObjectReferenceCurveBindings(source).Single();
            var canvasBinding = AnimationUtility.GetObjectReferenceCurveBindings(canvas).Single();
            Assert.AreEqual(typeof(SpriteRenderer), originalBinding.type);
            Assert.AreEqual(typeof(UnityEngine.UI.Image), canvasBinding.type);
            Assert.AreEqual("", canvasBinding.path);
            Assert.AreEqual("m_Sprite", canvasBinding.propertyName);
            Assert.AreEqual(source.frameRate, canvas.frameRate);
            Assert.AreEqual(source.length, canvas.length);
            Assert.IsTrue(source.isLooping && canvas.isLooping);
            var originalKeys = AnimationUtility.GetObjectReferenceCurve(source, originalBinding);
            var canvasKeys = AnimationUtility.GetObjectReferenceCurve(canvas, canvasBinding);
            Assert.Greater(originalKeys.Length, 1);
            CollectionAssert.AreEqual(originalKeys.Select(key => key.time), canvasKeys.Select(key => key.time));
            CollectionAssert.AreEqual(originalKeys.Select(key => key.value), canvasKeys.Select(key => key.value));
            foreach (var key in canvasKeys)
            {
                Assert.IsInstanceOf<Sprite>(key.value);
                var sprite = (Sprite)key.value;
                Assert.AreEqual("Assets/Art/Menu/PBBG26Logo.png", AssetDatabase.GetAssetPath(sprite));
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            }
        }

        private static void CheckBackground(Scene scene, EnvironmentZone expected)
        {
            var theme = All<EnvironmentTheme>(scene).Single();
            Assert.AreEqual(expected, theme.Zone, scene.name);
            var blue = theme.GetComponent<SpriteRenderer>();
            Assert.AreEqual(ProductionEnvironmentAuthoring.BlueBackgroundPath, AssetDatabase.GetAssetPath(blue.sprite));
            Assert.AreEqual(expected == EnvironmentZone.Orange, theme.UsesTemporaryBlueBackground);
            Assert.IsNull(theme.OrangeBackground, "No Orange source was supplied for this candidate.");
            bool yellow = expected == EnvironmentZone.YellowGreen;
            Assert.AreEqual(!yellow, blue.enabled, scene.name);
            var tiles = All<SpriteRenderer>(scene).Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                AssetDatabase.GetAssetPath(renderer.sprite) == ProductionEnvironmentAuthoring.YellowBackgroundPath).ToArray();
            Assert.AreEqual(yellow ? 15 : 0, tiles.Length);
            foreach (var tile in tiles)
            {
                Assert.LessOrEqual(tile.sortingOrder, -20);
                Assert.IsEmpty(tile.GetComponentsInChildren<Collider2D>(true));
            }
            Assert.IsEmpty(theme.GetComponentsInChildren<Collider2D>(true));
        }

        [Test]
        public void TurretAndProjectileUseSourceSpritesWithoutMovingTheMuzzleOrCollider()
        {
            var turret = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Turret.prefab").GetComponent<Turret2D>();
            Assert.AreEqual(Vector2.left, turret.firingDirection);
            Assert.AreEqual(new Vector3(-.6f, 0f, 0f), turret.muzzle.localPosition);
            Assert.AreEqual(3f, turret.projectileSpeed);
            Assert.AreEqual(.75f, turret.shotInterval);
            Assert.AreEqual(1f, turret.initialDelay);
            var visuals = turret.transform.Find(ProductionEnvironmentAuthoring.TurretName);
            Assert.AreEqual("Mount", visuals.Find("Mount").GetComponent<SpriteRenderer>().sprite.name);
            Assert.AreEqual("Turret_Barrel", visuals.Find("Barrel").GetComponent<SpriteRenderer>().sprite.name);
            var projectile = turret.projectilePrefab;
            Assert.AreEqual(8f, projectile.lifetime);
            var shape = projectile.GetComponent<CircleCollider2D>();
            Assert.IsTrue(shape.isTrigger);
            Assert.AreEqual(.12f, shape.radius);
            Assert.AreEqual(Vector2.zero, shape.offset);
            Assert.AreEqual("Bullet", projectile.GetComponentInChildren<SpriteRenderer>().sprite.name);
        }

        [Test]
        public void BothTerminalClipsResolveTwoSourceSpriteFramesAndAssetGuidsAreUnique()
        {
            foreach (var clipPath in new[] { Art + "BlueAnims/Terminal_Blinking.anim", Art + "YGAnims/YGTermBlink.anim" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                Assert.IsNotNull(clip);
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                Assert.AreEqual("m_Sprite", binding.propertyName);
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.GreaterOrEqual(keys.Length, 2);
                Assert.IsTrue(keys.All(key => key.value is Sprite));
                Assert.AreEqual(2, keys.Select(key => key.value).Distinct().Count());
            }
            var guids = new HashSet<string>();
            foreach (string metadata in Directory.EnumerateFiles("Assets", "*.meta", SearchOption.AllDirectories))
            {
                var id = Regex.Match(File.ReadAllText(metadata), "^guid: ([a-f0-9]{32})\r?$", RegexOptions.Multiline).Groups[1].Value;
                Assert.IsNotEmpty(id, metadata);
                Assert.IsTrue(guids.Add(id), "Duplicate GUID at " + metadata);
            }
        }

        private static IEnumerable<T> All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static void InScene(string path, Action<Scene> check)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { Physics2D.SyncTransforms(); check(scene); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
