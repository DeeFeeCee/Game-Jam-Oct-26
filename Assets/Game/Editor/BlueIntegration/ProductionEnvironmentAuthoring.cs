using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameJam.LevelAuthoring.Editor;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Presentation;
using GameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameJam.BlueIntegration.Editor
{
    /// <summary>Applies environment presentation and removes obsolete decorative-terminal collision.</summary>
    public static class ProductionEnvironmentAuthoring
    {
        public const string PreviewPath = "Assets/Game/Scenes/Verification/YellowGreenArtPreview.unity";
        public const string SurfaceName = "Environment surface art";
        public const string ExitName = "Environment exit art";
        public const string EntranceName = "Environment entrance art";
        public const string TerminalName = "Environment terminal art";
        public const string TurretName = "Environment turret art";
        public const string BlueBackgroundPath = "Assets/Art/BlueBG.png";
        public const string YellowBackgroundPath = "Assets/Art/Levels/YGbgNew.png";
        public const string OrangeBackgroundPath = "Assets/Art/Levels/Orangebg.png";
        private const string Art = "Assets/Art/Levels/";
        private const string Grid = "Assets/Game/Prefabs/GridPlatform32x18.prefab";
        private const string TerminalPlaceholder = "Assets/Game/Prefabs/Terminal placeholder.prefab";
        private const string YellowAdapters = "Assets/Game/Art/YellowGreenPlatformAdapters";
        private const string OrangeAdapters = "Assets/Game/Art/OrangePlatformAdapters";
        private const string Unlit = "Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderUnlit.mat";
        private const int BackgroundOrder = -20;

        [MenuItem("Game Jam/Environment Art/Apply Configured Theme")]
        private static void ApplyConfigured() => Apply(SceneManager.GetActiveScene());

        [MenuItem("Game Jam/Environment Art/Assign Blue")]
        private static void ApplyBlue() => Apply(SceneManager.GetActiveScene(), EnvironmentZone.Blue);

        [MenuItem("Game Jam/Environment Art/Assign Yellow-Green")]
        private static void ApplyYellow() => Apply(SceneManager.GetActiveScene(), EnvironmentZone.YellowGreen);

        [MenuItem("Game Jam/Environment Art/Assign Orange")]
        private static void ApplyOrange() => Apply(SceneManager.GetActiveScene(), EnvironmentZone.Orange);

        public static void Apply(Scene scene, EnvironmentZone? assignment = null)
        {
            var roots = scene.GetRootGameObjects();
            var setups = roots.SelectMany(root => root.GetComponentsInChildren<LevelSetup>()).ToArray();
            var exits = roots.SelectMany(root => root.GetComponentsInChildren<LevelExit>()).ToArray();
            if (setups.Length != 1 || exits.Length != 1 || setups[0].spawn == null ||
                exits[0].transition == null || !exits[0].TryGetComponent<BoxCollider2D>(out var trigger) || !trigger.isTrigger)
                throw new InvalidOperationException($"Expected one configured room setup and exit in {scene.path}.");

            var background = roots.SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Single(renderer => AssetDatabase.GetAssetPath(renderer.sprite) == BlueBackgroundPath);
            var theme = background.GetComponent<EnvironmentTheme>();
            if (theme == null)
            {
                if (!assignment.HasValue) throw new InvalidOperationException($"Assign an environment theme to {scene.name} first.");
                theme = background.gameObject.AddComponent<EnvironmentTheme>();
            }
            if (assignment.HasValue)
            {
                var fields = new SerializedObject(theme);
                fields.FindProperty("zone").enumValueIndex = (int)assignment.Value;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            if (theme.UsesTemporaryBlueBackground)
            {
                var fields = new SerializedObject(theme);
                fields.FindProperty("orangeBackground").objectReferenceValue = Sprite("Orangebg", "Orangebg_0");
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            ApplyBackground(background, theme);
            if (theme.Zone == EnvironmentZone.Orange)
            {
                ApplyOrangePresentation(roots, exits[0], trigger);
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }

            bool yellowGreen = theme.Zone == EnvironmentZone.YellowGreen;
            var placeholders = new List<BoxCollider2D>();
            foreach (var root in roots)
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (box.isTrigger) continue;
                    var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(box.gameObject);
                    if (source == TerminalPlaceholder)
                        placeholders.Add(box);
                    else if (box.TryGetComponent<MovingPlatform2D>(out var moving))
                        ApplySurface(box, moving.surface, yellowGreen, ground: false);
                    else if (source == Grid || box.transform.Find(BluePlatformAuthoring.ArtName) != null)
                        ApplyGrid(box, yellowGreen);
                }
                foreach (var terminal in root.GetComponentsInChildren<ResearchTerminal>())
                    ApplyTerminal(terminal, yellowGreen);
                foreach (var turret in root.GetComponentsInChildren<Turret2D>())
                    ApplyTurret(turret);
            }
            var roomColliders = roots.SelectMany(root => root.GetComponentsInChildren<BoxCollider2D>()).ToArray();
            foreach (var box in placeholders)
                ApplyDecorativeTerminal(box, yellowGreen, roomColliders);
            ApplyEntrance(scene, setups[0], yellowGreen);
            ApplyExit(exits[0], trigger, yellowGreen);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ApplyOrangePresentation(GameObject[] roots, LevelExit exit, BoxCollider2D trigger)
        {
            foreach (var root in roots)
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (box.isTrigger) continue;
                    var prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(box.gameObject);
                    if (prefab == TerminalPlaceholder)
                        ApplyOrangeTerminal(box);
                    else if (box.TryGetComponent<MovingPlatform2D>(out var moving))
                        ApplySurface(box, moving.surface, EnvironmentZone.Orange, ground: false);
                    else if (prefab == Grid || box.transform.Find(BluePlatformAuthoring.ArtName) != null)
                        ApplyGrid(box, EnvironmentZone.Orange);
                }
            }

            // The finale loads its destination immediately. Keep the source door closed in the room.
            if (exit.transition.destinationScene == EndingSequence.ScenePath)
            {
                var door = Visual(exit.transform, ExitName, Material());
                var sprite = Sprite("FinalExit", "FinalExit_0");
                Vector3 bottom = trigger.offset - Vector2.up * (trigger.size.y / 2f);
                float localUnit = 1f / Mathf.Abs(exit.transform.lossyScale.y);
                Paint(door, sprite, bottom + Vector3.up * (sprite.pivot.y / 20f * localUnit), -1);
            }
        }

        private static void ApplyOrangeTerminal(BoxCollider2D box)
        {
            // This decorative fixture already has a noninteractive, aligned visual and no active solid.
            var art = box.transform.Find(TerminalName)?.GetComponent<SpriteRenderer>();
            if (art == null || box.enabled)
                throw new InvalidOperationException($"Expected an authored decorative terminal on {box.name}.");

            var sprite = Sprite("OrangeTerminal", "OrangeTerminal_0");
            var baseY = VisibleTerminalBase(art, art.sprite);
            var animator = art.GetComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art + "OrangeAnims/OrangeTerminal.controller");
            // Swapping the controller can restore the prior binding's default SpriteRenderer value.
            // Write the scene's idle frame after the swap.
            if (art.sprite != sprite)
            {
                Paint(art, sprite, art.transform.localPosition, art.sortingOrder);
                art.transform.position += Vector3.up * (baseY - VisibleTerminalBase(art, sprite));
            }
            // The six-frame spark clip has its own highlights. Remove the old Blue accent.
            if (art.TryGetComponent<TerminalBlinkIndicator>(out var indicator))
                Object.DestroyImmediate(indicator);
            var accent = art.transform.Find("Blink status accent");
            if (accent != null) Object.DestroyImmediate(accent.gameObject);
        }

        private static void ApplyGrid(BoxCollider2D box, bool yellowGreen) =>
            ApplyGrid(box, yellowGreen ? EnvironmentZone.YellowGreen : EnvironmentZone.Blue);

        private static void ApplyGrid(BoxCollider2D box, EnvironmentZone zone)
        {
            var floor = box.transform.Find("Floor visuals")?.GetComponent<SpriteRenderer>();
            if (floor == null) throw new InvalidOperationException("Grid platform is missing its floor renderer.");
            var bounds = box.bounds;
            bool ground = bounds.min.y < .01f || bounds.max.y > 17.99f ||
                (box.size.x <= 1.01f && (bounds.min.x < .01f || bounds.max.x > 31.99f));
            var verificationArt = box.transform.Find(BluePlatformAuthoring.ArtName)?.GetComponent<SpriteRenderer>();
            if (verificationArt != null) verificationArt.enabled = false;
            SetLegacyFrame(box.transform, keepTop: true);
            if (ground && zone == EnvironmentZone.Blue)
            {
                floor.enabled = true; // Already authored using Ace's 20-PPU Blue ground tile.
                var prior = box.transform.Find(SurfaceName)?.GetComponent<SpriteRenderer>();
                if (prior != null) prior.enabled = false;
                return;
            }
            ApplySurface(box, floor, zone, ground);
        }

        private static void ApplySurface(BoxCollider2D box, SpriteRenderer original, bool yellowGreen, bool ground) =>
            ApplySurface(box, original, yellowGreen ? EnvironmentZone.YellowGreen : EnvironmentZone.Blue, ground);

        private static void ApplySurface(BoxCollider2D box, SpriteRenderer original, EnvironmentZone zone, bool ground)
        {
            if (original == null || Mathf.Approximately(box.transform.lossyScale.x, 0) || Mathf.Approximately(box.transform.lossyScale.y, 0))
                throw new InvalidOperationException("Expected a nonzero platform scale and its existing renderer.");
            bool small = box.bounds.size.x < 1.5f;
            Sprite sprite = zone switch
            {
                EnvironmentZone.Orange => ground ? Sprite("Orangefloor", "Orangefloor_0") :
                    Sprite("OrangePlatforms", small ? "OrangePlatforms_3" : "OrangePlatforms_0"),
                EnvironmentZone.YellowGreen => ground ? Sprite("YGLvlGround") :
                    Sprite("YGPlatforms", small ? "YGPlatforms_Small" : "YGPlatforms_Large"),
                _ => Sprite("BluePlatforms", small ? "BluePlatforms_Small" : "BluePlatform_Big")
            };
            var art = Visual(box.transform, SurfaceName, original.sharedMaterial);
            art.sprite = zone switch
            {
                EnvironmentZone.Orange => BluePlatformAuthoring.TilingSprite(sprite, OrangeAdapters),
                EnvironmentZone.YellowGreen => BluePlatformAuthoring.TilingSprite(sprite, YellowAdapters),
                _ => BluePlatformAuthoring.TilingSprite(sprite)
            };
            art.sortingLayerID = original.sortingLayerID;
            art.sortingOrder = original.sortingOrder;
            art.drawMode = SpriteDrawMode.Tiled;
            art.tileMode = SpriteTileMode.Continuous;
            art.enabled = true;
            original.enabled = false;
            BluePlatformAuthoring.FitToSurface(art, box, fillHeight: ground);
        }

        private static void ApplyBackground(SpriteRenderer background, EnvironmentTheme theme)
        {
            const string yellowName = "Yellow-green background tiles";
            const string orangeName = "Orange background tiles";
            bool yellow = theme.Zone == EnvironmentZone.YellowGreen;
            bool orange = theme.Zone == EnvironmentZone.Orange && theme.OrangeBackground != null;
            background.enabled = !yellow && !orange;
            SetBackgroundActive(background.transform, yellowName, yellow);
            SetBackgroundActive(background.transform, orangeName, orange);
            if (yellow) TileBackground(background, yellowName, Sprite("YGbgNew"), new Color(.72f, .72f, .72f));
            if (orange) TileBackground(background, orangeName, theme.OrangeBackground, new Color(.52f, .52f, .52f));
        }

        private static void SetBackgroundActive(Transform parent, string name, bool active)
        {
            var group = parent.Find(name);
            if (group != null) group.gameObject.SetActive(active);
        }

        private static void TileBackground(SpriteRenderer background, string name, Sprite sprite, Color tint)
        {
            var group = Child(background.transform, name);
            foreach (var tileRenderer in group.GetComponentsInChildren<SpriteRenderer>(true))
                tileRenderer.enabled = false;
            Vector2 tile = sprite.rect.size / 20f;
            Vector3 leftBottom = background.transform.InverseTransformPoint(Vector3.zero);
            for (int y = 0; y < Mathf.CeilToInt(18 / tile.y); y++)
            for (int x = 0; x < Mathf.CeilToInt(32 / tile.x); x++)
            {
                var renderer = Visual(group, $"Tile {x}-{y}", background.sharedMaterial);
                Paint(renderer, sprite, leftBottom + new Vector3((x + .5f) * tile.x, (y + .5f) * tile.y, 0), BackgroundOrder);
                renderer.flipX = (x & 1) != 0;
                renderer.flipY = (y & 1) != 0;
                renderer.color = tint;
                renderer.enabled = true;
            }
        }

        private static void ApplyEntrance(Scene scene, LevelSetup setup, bool yellowGreen)
        {
            var original = scene.GetRootGameObjects().SingleOrDefault(root =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == BlueIntegrationBuilder.Folder + "/BlueEntrance.prefab");
            if (original != null) original.transform.Find("Door").GetComponent<SpriteRenderer>().enabled = false;
            var art = Visual(original != null ? original.transform : setup.spawn, EntranceName, Material());
            var sprite = yellowGreen ? Sprite("YGEnter", "YGEnter_0") : Sprite("BlueEnter");
            float baseY = original != null ? 0f : -setup.playerPrefab.GetComponent<BoxCollider2D>().size.y / 2f;
            Paint(art, sprite, Vector3.up * (baseY + sprite.pivot.y / 20f), -2);
        }

        private static void ApplyExit(LevelExit exit, BoxCollider2D trigger, bool yellowGreen)
        {
            var placeholder = exit.GetComponent<SpriteRenderer>();
            if (placeholder == null) placeholder = exit.transform.Find("Door")?.GetComponent<SpriteRenderer>();
            if (placeholder != null) placeholder.enabled = false;
            var door = Visual(exit.transform, ExitName, Material());
            var sprite = yellowGreen ? Sprite("YGExit", "YGExit_0") : Sprite("BlueExit");
            Vector3 bottom = trigger.offset - Vector2.up * (trigger.size.y / 2f);
            float localUnit = 1f / Mathf.Abs(exit.transform.lossyScale.y);
            Paint(door, sprite, bottom + Vector3.up * (sprite.pivot.y / 20f * localUnit), -1);
            const string signName = "Environment exit sign";
            if (yellowGreen)
            {
                var sign = Visual(exit.transform, signName, Material());
                var signSprite = Sprite("YGExit", "YGExit_1");
                Paint(sign, signSprite, bottom + Vector3.up * ((sprite.rect.height + signSprite.pivot.y) / 20f * localUnit), -1);
                sign.enabled = true;
            }
            else
            {
                var sign = exit.transform.Find(signName)?.GetComponent<SpriteRenderer>();
                if (sign != null) sign.enabled = false;
            }
        }

        private static void SetLegacyFrame(Transform owner, bool keepTop)
        {
            var frame = owner.Find("Gameplay surface frame");
            if (frame == null) return;
            var top = frame.Find("Top");
            foreach (var renderer in frame.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.enabled = keepTop && renderer.transform == top;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        public static void ApplyDecorativeTerminal(BoxCollider2D box, bool yellowGreen, IEnumerable<BoxCollider2D> roomColliders)
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(box.gameObject) != TerminalPlaceholder ||
                box.GetComponentInChildren<ResearchTerminal>(true) != null ||
                box.GetComponentsInChildren<Collider2D>(true).Any(shape => shape != box && shape.enabled))
                throw new InvalidOperationException($"Unexpected decorative-terminal components on {box.name}.");

            var floor = box.transform.Find("Floor visuals").GetComponent<SpriteRenderer>();
            floor.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
            SetLegacyFrame(box.transform, keepTop: false);
            var sprite = TerminalSprite(yellowGreen);
            var previousPosition = box.transform.Find(TerminalName)?.localPosition;
            var art = Visual(box.transform, TerminalName, Material());
            var scale = box.transform.lossyScale;
            var authoredSize = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            float fit = Mathf.Min(1, authoredSize.x / (sprite.rect.width / 20f), authoredSize.y / (sprite.rect.height / 20f));
            float localSpriteHeight = sprite.rect.height * fit / (20f * Mathf.Abs(box.transform.lossyScale.y));
            Paint(art, sprite, box.offset + Vector2.up * ((box.size.y - localSpriteHeight) / 2f), -2);
            art.transform.localScale *= fit;
            if (previousPosition.HasValue)
            {
                var position = art.transform.localPosition;
                position.x = previousPosition.Value.x;
                art.transform.localPosition = position;
            }
            if (!AlignTerminalVisual(box, art, roomColliders) && previousPosition.HasValue)
                art.transform.localPosition = previousPosition.Value;
            var animator = art.GetComponent<Animator>();
            if (animator == null) animator = art.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = TerminalController(yellowGreen);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ConfigureDecorativeBlinkAccent(art, yellowGreen);

            // Keep the prefab instance and its authored measurements for repeat authoring.
            box.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }

        private static void ConfigureDecorativeBlinkAccent(SpriteRenderer art, bool yellowGreen)
        {
            var blink = yellowGreen ? Sprite("YGTerminal", "YGTerminal_1") : Sprite("BlueTerminal", "BlueTerminal_Blink");
            var accent = Visual(art.transform, "Blink status accent", art.sharedMaterial);
            accent.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderSquare.png");
            // Both source sheets change only pixel (25, 14), measured from the upper left.
            var pixelCenter = new Vector2(25.5f, blink.rect.height - 14.5f);
            accent.transform.localPosition = (pixelCenter - blink.pivot) / blink.pixelsPerUnit;
            float side = 3f / blink.pixelsPerUnit;
            var size = accent.sprite.bounds.size;
            accent.transform.localScale = new Vector3(side / size.x, side / size.y, 1f);
            accent.sortingLayerID = art.sortingLayerID;
            accent.sortingOrder = art.sortingOrder + 1;
            accent.color = yellowGreen ? new Color32(214, 242, 100, 255) : new Color32(166, 252, 219, 255);
            accent.enabled = false;

            var indicator = art.GetComponent<TerminalBlinkIndicator>();
            if (indicator == null) indicator = art.gameObject.AddComponent<TerminalBlinkIndicator>();
            var fields = new SerializedObject(indicator);
            fields.FindProperty("blinkFrame").objectReferenceValue = blink;
            fields.FindProperty("indicator").objectReferenceValue = accent;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        public static bool AlignTerminalVisual(BoxCollider2D terminal, SpriteRenderer art, IEnumerable<BoxCollider2D> roomColliders)
        {
            // Collider bounds are empty once disabled. The original shape still identifies the nearby platform.
            var center = terminal.transform.TransformPoint(terminal.offset);
            float footY = terminal.transform.TransformPoint(terminal.offset - Vector2.up * terminal.size.y / 2f).y;
            var supports = roomColliders.Where(box => box != terminal && !box.isTrigger && box.enabled &&
                    box.bounds.min.x < center.x && box.bounds.max.x > center.x &&
                    Mathf.Abs(box.bounds.max.y - footY) <= .5f)
                .Select(box => box.transform.Find("Gameplay surface frame/Top")?.GetComponent<SpriteRenderer>())
                .Where(top => top != null && top.enabled).Take(2).ToArray();
            if (supports.Length != 1)
            {
                Debug.LogWarning($"{terminal.gameObject.scene.name}: no unique platform top for {terminal.name}; terminal art position retained.", terminal);
                return false;
            }

            float baseY = VisibleTerminalBase(art, art.sprite);
            var position = art.transform.position;
            position.y += supports[0].bounds.max.y - baseY;
            art.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(art.transform);
            return true;
        }

        public static float VisibleTerminalBase(SpriteRenderer art, Sprite sprite)
        {
            string source = AssetDatabase.GetAssetPath(sprite.texture);
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                // Read the source PNG independently so the original texture import remains untouched.
                if (!ImageConversion.LoadImage(pixels, File.ReadAllBytes(source)) ||
                    pixels.width != sprite.texture.width || pixels.height != sprite.texture.height)
                    throw new InvalidOperationException($"Cannot measure the source sprite pixels for {sprite.name}.");

                var rect = sprite.rect;
                var colors = pixels.GetPixels32();
                for (int y = 0; y < (int)rect.height; y++)
                {
                    int row = ((int)rect.y + y) * pixels.width + (int)rect.x;
                    for (int x = 0; x < (int)rect.width; x++)
                    {
                        if (colors[row + x].a == 0) continue;
                        float localY = (y - sprite.pivot.y) / sprite.pixelsPerUnit;
                        return art.transform.TransformPoint(0f, localY, 0f).y;
                    }
                }
                throw new InvalidOperationException($"Terminal sprite {sprite.name} has no visible pixels.");
            }
            finally
            {
                Object.DestroyImmediate(pixels);
            }
        }

        private static void ApplyTerminal(ResearchTerminal terminal, bool yellowGreen)
        {
            var animator = terminal.GetComponentsInChildren<Animator>(true).Single(anim => anim.TryGetComponent<SpriteRenderer>(out _));
            animator.GetComponent<SpriteRenderer>().sprite = TerminalSprite(yellowGreen);
            animator.runtimeAnimatorController = TerminalController(yellowGreen);
            var frame = terminal.overlay.transform.Find("Textbox bounds/Ace textbox frame").GetComponent<UnityEngine.UI.Image>();
            frame.sprite = yellowGreen ? Sprite("YGTerminalTextbox", "YGTerminalTextbox_0") : Sprite("BlueTerminalTextbox");
            PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
            EditorUtility.SetDirty(frame);
            var indicator = animator.GetComponent<TerminalBlinkIndicator>();
            if (indicator != null)
            {
                var serialized = new SerializedObject(indicator);
                serialized.FindProperty("blinkFrame").objectReferenceValue = yellowGreen ?
                    Sprite("YGTerminal", "YGTerminal_1") : Sprite("BlueTerminal", "BlueTerminal_Blink");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void ApplyTurret(Turret2D turret)
        {
            var root = turret.transform;
            var placeholder = root.Find("Replaceable turret")?.GetComponent<SpriteRenderer>();
            if (turret.muzzle == null || placeholder == null)
                throw new InvalidOperationException("Expected the accepted turret muzzle and replaceable renderer.");
            placeholder.enabled = false;
            var visuals = Child(root, TurretName);
            Vector2 direction = turret.firingDirection.sqrMagnitude > .0001f ? turret.firingDirection.normalized : Vector2.left;
            bool right = direction.x > 0f;
            Vector2 sourceDirection = right ? Vector2.right : Vector2.left;
            visuals.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(sourceDirection, direction));
            visuals.localScale = new Vector3(right ? -1 : 1, 1, 1);
            var muzzle = visuals.InverseTransformPoint(turret.muzzle.position);
            var mount = Visual(visuals, "Mount", placeholder.sharedMaterial);
            var barrel = Visual(visuals, "Barrel", placeholder.sharedMaterial);
            Paint(mount, Sprite("BlueTurretSet", "Mount"), muzzle + new Vector3(1f, -.26f), 2);
            Paint(barrel, Sprite("BlueTurretSet", "Turret_Barrel"), muzzle + new Vector3(.7f, 0), 3);
        }

        public static void ApplyProjectile(TurretProjectile2D projectile)
        {
            var art = projectile.transform.Find("Replaceable projectile").GetComponent<SpriteRenderer>();
            var shape = projectile.GetComponent<CircleCollider2D>();
            Paint(art, Sprite("BlueTurretSet", "Bullet"), (Vector3)shape.offset + Vector3.down * .02f, art.sortingOrder);
            art.transform.localScale *= (shape.radius * 2f) / .3f;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static SpriteRenderer Visual(Transform parent, string name, Material material)
        {
            var child = Child(parent, name);
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static void Paint(SpriteRenderer renderer, Sprite sprite, Vector3 position, int order)
        {
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = order;
            renderer.transform.localPosition = position;
            var parentScale = renderer.transform.parent.lossyScale;
            float scale = sprite.pixelsPerUnit / 20f;
            renderer.transform.localScale = new Vector3(scale / Mathf.Abs(parentScale.x), scale / Mathf.Abs(parentScale.y), scale);
        }

        private static Sprite Sprite(string file, string name = null) =>
            AssetDatabase.LoadAllAssetsAtPath(Art + file + ".png").OfType<Sprite>().Single(sprite => name == null || sprite.name == name);

        private static Material Material() => AssetDatabase.LoadAssetAtPath<Material>(Unlit);
        private static Sprite TerminalSprite(bool yellowGreen) => yellowGreen ?
            Sprite("YGTerminal", "YGTerminal_0") : Sprite("BlueTerminal", "BlueTerminal_Norm");
        private static RuntimeAnimatorController TerminalController(bool yellowGreen) =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art + (yellowGreen ? "YGAnims/YGTerminal.controller" : "BlueAnims/TerminalBlink.controller"));
    }
}
