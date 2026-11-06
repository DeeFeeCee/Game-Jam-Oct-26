using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameJam.LevelAuthoring.Editor
{
    /// <summary>Art-only adapter. Imported sprite metadata and collider geometry are never edited.</summary>
    public static class BluePlatformAuthoring
    {
        public const string ArtName = "Blue platform art";
        public const float ArtScale = 5f; // Ace's 100 PPU / production 20 effective PPU.
        public static Sprite TilingSprite(Sprite source)
        {
            const string folder = "Assets/Game/Art/BluePlatformAdapters";
            string path = folder + "/" + source.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            // Standalone sprite wrapper references the original texture: no duplicated PNG,
            // no importer edits, and rectangular geometry suitable for SpriteRenderer tiling.
            var sprite = Sprite.Create(source.texture, source.rect, source.pivot / source.rect.size,
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
            sprite.name = source.name; AssetDatabase.CreateAsset(sprite, path); return sprite;
        }
        public static void Apply(GameObject platform, Sprite sprite)
        {
            var box = platform.GetComponent<BoxCollider2D>();
            if (box == null || box.isTrigger || platform.transform.localScale != Vector3.one || sprite == null)
                throw new InvalidOperationException("Use a unit-scale platform with a solid BoxCollider2D and a blue sprite.");
            var floor = platform.transform.Find("Floor visuals")?.GetComponent<SpriteRenderer>();
            if (floor == null) throw new InvalidOperationException("Expected the 32x18 Floor visuals child.");
            var child = platform.transform.Find(ArtName);
            if (child == null) { child = new GameObject(ArtName, typeof(SpriteRenderer)).transform; child.SetParent(platform.transform, false); }
            var art = child.GetComponent<SpriteRenderer>();
            art.sprite = TilingSprite(sprite); art.sharedMaterial = floor.sharedMaterial; art.sortingLayerID = floor.sortingLayerID;
            art.sortingOrder = floor.sortingOrder + 1; art.drawMode = SpriteDrawMode.Tiled;
            art.tileMode = SpriteTileMode.Continuous; floor.enabled = false;
            Fit(platform);
        }
        public static void Fit(GameObject platform)
        {
            var art = platform.transform.Find(ArtName)?.GetComponent<SpriteRenderer>();
            if (art == null || art.sprite == null) return;
            var box = platform.GetComponent<BoxCollider2D>();
            float height = Mathf.Min(box.size.y, art.sprite.rect.height / 20f);
            art.transform.localScale = Vector3.one * ArtScale;
            art.transform.localPosition = new Vector3(box.offset.x, box.offset.y + (box.size.y - height) / 2f, 0);
            art.size = new Vector2(box.size.x / ArtScale, height / ArtScale);
        }
    }
}
