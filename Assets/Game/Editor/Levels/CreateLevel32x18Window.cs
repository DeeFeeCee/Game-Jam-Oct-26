using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameJam.LevelAuthoring.Editor
{
    public sealed class CreateLevel32x18Window : EditorWindow
    {
        public void CreateGUI()
        {
            rootVisualElement.Add(new HelpBox("Copies the canonical template with a new scene GUID. Existing scenes are never overwritten. Save your current work before opening the copy.", HelpBoxMessageType.Info));
            var levelName = new TextField("New level name or number") { value = "Level02" };
            var error = new HelpBox("", HelpBoxMessageType.Error);
            error.SetEnabled(false);
            rootVisualElement.Add(levelName);
            rootVisualElement.Add(new Button(() =>
            {
                try { if (LevelAuthoring32x18.CreateFromTemplate(levelName.value) != null) Close(); }
                catch (Exception exception) { error.text = exception.Message; error.SetEnabled(true); }
            }) { text = "Create And Open" });
            rootVisualElement.Add(error);
        }
    }
}
