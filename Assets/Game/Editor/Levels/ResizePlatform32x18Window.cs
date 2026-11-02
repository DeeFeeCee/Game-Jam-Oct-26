using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameJam.LevelAuthoring.Editor
{
    public sealed class ResizePlatform32x18Window : EditorWindow
    {
        private Label selection;
        private Vector2IntField bottomLeft, cells;

        public void CreateGUI()
        {
            selection = new Label();
            rootVisualElement.Add(selection);
            rootVisualElement.Add(new HelpBox("Select a static platform root at scale (1,1,1). Collision, tiled art and foreground edges update together. Ctrl+Z undoes the change.", HelpBoxMessageType.Info));
            bottomLeft = new Vector2IntField("Bottom-left cell");
            cells = new Vector2IntField("Size in cells") { value = new Vector2Int(4, 1) };
            var error = new HelpBox("", HelpBoxMessageType.Error);
            error.SetEnabled(false);
            rootVisualElement.Add(bottomLeft);
            rootVisualElement.Add(cells);
            rootVisualElement.Add(new Button(() =>
            {
                try { LevelAuthoring32x18.Resize(Selection.activeGameObject, bottomLeft.value, cells.value); error.text = ""; error.SetEnabled(false); }
                catch (Exception exception) { error.text = exception.Message; error.SetEnabled(true); }
            }) { text = "Apply To Selected Platform" });
            rootVisualElement.Add(error);
            ReadSelection();
        }

        private void OnSelectionChange() => ReadSelection();
        private void ReadSelection()
        {
            if (selection == null) return;
            var selected = Selection.activeGameObject;
            selection.text = "Selected platform: " + (selected != null ? selected.name : "None");
            if (selected != null && selected.TryGetComponent(out BoxCollider2D box))
            {
                bottomLeft.value = Vector2Int.RoundToInt(box.bounds.min);
                cells.value = Vector2Int.RoundToInt(box.size);
            }
        }
    }
}
