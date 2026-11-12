using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameJam.UI.Editor
{
    public static class CreditsArchiveBuilder
    {
        [MenuItem("Game Jam/UI/Add Credits To Accepted Menu If Absent")]
        public static void Upgrade()
        {
            var root=PrefabUtility.LoadPrefabContents(MenuBuilder.PrefabPath);
            try
            {
                var view=root.GetComponent<MainMenuView>();
                if(view.credits!=null)throw new InvalidOperationException("Credits already exists; edit its text and layout instead of regenerating.");
                var font=view.mainPanel.GetComponentInChildren<TMP_Text>().font;
                var c=root.AddComponent<CreditsArchiveView>();view.credits=c;
                var actions=view.controlsButton.transform.parent;
                view.creditsButton=Button(actions,"Credits","CREDITS",font);view.creditsButton.transform.SetSiblingIndex(view.quitButton.transform.GetSiblingIndex());
                var rect=actions.GetComponent<RectTransform>();Fit(rect,.075f,.18f,.45f,.49f);
                actions.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=4;
                foreach(var b in new[]{view.newGameButton,view.controlsButton,view.creditsButton,view.quitButton})
                {var layout=b.GetComponent<UnityEngine.UI.LayoutElement>();layout.minHeight=layout.preferredHeight=52;}
                Link(view.newGameButton,view.newGameButton,view.controlsButton);
                Link(view.controlsButton,view.newGameButton,view.creditsButton);
                Link(view.creditsButton,view.controlsButton,view.quitButton);Link(view.quitButton,view.creditsButton,view.quitButton);
                var canvas=view.mainPanel.transform.parent;
                var panel=Panel(canvas,"CreditsView",new Color(.035f,.085f,.125f));Fit(panel,.075f,.15f,.925f,.86f);c.panel=panel.gameObject;
                var ink=new Color(.87f,.94f,.94f);var cyan=new Color(.21f,.72f,.68f);var amber=new Color(.96f,.66f,.31f);
                var title=Label(panel,"Heading","PROJECT CREDITS",32,ink);title.fontStyle=FontStyles.Bold;Fit(title.rectTransform,.045f,.84f,.95f,.97f);
                var id=Label(panel,"ArchiveIdentifier","P.I.D.R. // PERSONNEL ARCHIVE",14,amber);Fit(id.rectTransform,.045f,.77f,.95f,.84f);
                var sub=Label(panel,"Subtitle","AUTHORIZED CONTRIBUTOR RECORDS  /  PROVISIONAL ARCHIVE",12,cyan);Fit(sub.rectTransform,.045f,.72f,.95f,.77f);
                var scrollObject=new GameObject("PersonnelArchive",typeof(RectTransform),typeof(UnityEngine.UI.ScrollRect));scrollObject.transform.SetParent(panel,false);
                Fit(scrollObject.GetComponent<RectTransform>(),.045f,.23f,.955f,.71f);
                var scroll=scrollObject.GetComponent<UnityEngine.UI.ScrollRect>();c.archive=scroll;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
                scroll.inertia=true;scroll.decelerationRate=.135f;scroll.scrollSensitivity=32;
                var viewport=Panel(scrollObject.transform,"Viewport",new Color(.02f,.045f,.075f));Fit(viewport,0,0,.965f,1);
                viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;scroll.viewport=viewport;
                var contentObject=new GameObject("Content",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup),typeof(UnityEngine.UI.ContentSizeFitter));contentObject.transform.SetParent(viewport,false);
                var content=contentObject.GetComponent<RectTransform>();content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
                var group=contentObject.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();group.padding=new RectOffset(16,20,12,24);group.spacing=18;group.childControlWidth=group.childControlHeight=true;group.childForceExpandHeight=false;group.childForceExpandWidth=true;
                contentObject.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;
                Section("PROJECT TEAM","WORKING ROSTER / NOT FINAL OR COMPLETE","Devin Padin / Jarheadd1\nDouglas (DeeFeeCee)\nSarah (AceofHome)\nAlexzander (mrdrworm)");
                Section("GAME DEVELOPMENT","PROVISIONAL CONTRIBUTION RECORDS","<b>Devin Padin / Jarheadd1</b>\nGameplay and UI foundation development; technical integration.\n\n<b>Douglas (DeeFeeCee)</b>\nRepository coordination, integration and gameplay mechanics.");
                Section("ART & ANIMATION","PROVISIONAL CONTRIBUTION RECORDS","<b>Sarah (AceofHome)</b>\nArtwork, animation and narrative collaboration.");
                Section("LEVEL & PUZZLE DESIGN","PROVISIONAL CONTRIBUTION RECORDS","<b>Alexzander (mrdrworm)</b>\nLevel, puzzle, and game design.");
                Section("TOOLS & TECHNOLOGY","CONFIRMED PROJECT TOOLS","Unity 6000.3.26f1\nC#\nGit\nGitHub");
                Section("AUDIO & MUSIC","CONFIRMED CONTRIBUTION","<b>Douglas (DeeFeeCee)</b>\nMusic & Sound FX");
                Section("TYPOGRAPHY & FONT DESIGN","CONFIRMED CONTRIBUTION","<b>Douglas (DeeFeeCee)</b>\nOriginal Typeface: DeeFont1.17");
                Section("SPECIAL THANKS","PENDING CONFIRMATION","Acknowledgments will be added after team confirmation.");
                var barRect=Panel(scrollObject.transform,"ArchiveScrollbar",new Color(.06f,.15f,.19f));Fit(barRect,.975f,0,1,1);barRect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                var sliding=new GameObject("SlidingArea",typeof(RectTransform));sliding.transform.SetParent(barRect,false);Fit(sliding.GetComponent<RectTransform>(),0,0,1,1);
                var handle=Panel(sliding.transform,"Handle",cyan);Fit(handle,0,0,1,1);handle.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                handle.GetComponent<UnityEngine.UI.Image>().color=Color.white;
                c.scrollbar=barRect.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();c.scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;c.scrollbar.handleRect=handle;c.scrollbar.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
                var colors=c.scrollbar.colors;colors.normalColor=cyan;colors.selectedColor=amber;colors.highlightedColor=amber;c.scrollbar.colors=colors;
                scroll.verticalScrollbar=c.scrollbar;scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
                c.returnButton=Button(panel,"ReturnFromCredits","RETURN TO MAIN MENU",font);Fit(c.returnButton.GetComponent<RectTransform>(),.045f,.065f,.43f,.185f);
                c.scrollbar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnRight=c.returnButton};
                c.returnButton.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnLeft=c.scrollbar,selectOnUp=c.scrollbar};
                var hint=Label(panel,"NavigationHint","UP/DOWN / STICK: SCROLL\nRIGHT: RETURN  /  ESC / B: BACK",13,cyan);Fit(hint.rectTransform,.47f,.055f,.97f,.19f);
                c.panel.SetActive(false);
                foreach(var component in root.GetComponentsInChildren<Component>(true))if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                PrefabUtility.SaveAsPrefabAsset(root,MenuBuilder.PrefabPath);AssetDatabase.SaveAssets();

                void Section(string heading,string record,string body)
                {
                    Label(content,heading,heading,20,cyan).fontStyle=FontStyles.Bold;
                    Label(content,heading+"Record",record,12,amber);
                    Label(content,heading+"Body",body,19,ink);
                }
                TextMeshProUGUI Label(Transform parent,string name,string text,int size,Color color)
                {
                    var obj=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));obj.transform.SetParent(parent,false);
                    var label=obj.GetComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.TopLeft;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;return label;
                }
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        private static RectTransform Panel(Transform parent,string name,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));obj.transform.SetParent(parent,false);var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return obj.GetComponent<RectTransform>();
        }
        private static void Fit(RectTransform rect,float x0,float y0,float x1,float y1)
        {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static UnityEngine.UI.Button Button(Transform parent,string name,string text,TMP_FontAsset font)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.ButtonPath),parent);
            obj.name=name;
            var label=obj.GetComponentInChildren<TMP_Text>();
            label.text=text;
            label.font=font;
            label.fontSharedMaterial=font.material;
            return obj.GetComponent<UnityEngine.UI.Button>();
        }
        private static void Link(UnityEngine.UI.Button b,UnityEngine.UI.Selectable up,UnityEngine.UI.Selectable down)
        {b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down};}
        [MenuItem("Game Jam/UI/Build Credits Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Credits");Directory.CreateDirectory("Logs/Credits");
            var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/Credits/GameJam-Credits.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("Logs/Credits/build-summary.txt",r.summary.result+" errors="+r.summary.totalErrors);if(r.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Credits build failed.");
        }
    }
}
