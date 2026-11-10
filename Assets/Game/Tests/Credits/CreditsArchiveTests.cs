#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.Resources;
using GameJam.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class CreditsArchiveTests
    {
        private MainMenuView view;
        private CreditsArchiveView credits;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;
        private InputSettings previous,settings;
        private string previousJson;
        private HideFlags previousFlags;
        [UnitySetUp] public IEnumerator Setup()
        {
            previous=InputSystem.settings;previousJson=JsonUtility.ToJson(previous);previousFlags=previous.hideFlags;
            if(previousFlags==HideFlags.HideAndDontSave)previous.hideFlags|=HideFlags.HideInInspector;
            settings=Object.Instantiate(previous);settings.hideFlags=HideFlags.HideAndDontSave;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();
            EditorSceneManager.LoadSceneInPlayMode(MenuBuilder.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            view=Object.FindFirstObjectByType<MainMenuView>();credits=view.credits;Assert.NotNull(credits);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(RunDashBank.Instance!=null)Object.Destroy(RunDashBank.Instance.gameObject);
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Credits test cleanup"));
            if(old.IsValid()&&old.isLoaded)yield return SceneManager.UnloadSceneAsync(old);
            foreach(var d in new InputDevice[]{keyboard,gamepad,mouse})if(d!=null&&d.added)InputSystem.RemoveDevice(d);
            keyboard=null;gamepad=null;mouse=null;
            if(previous==null){previous=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(previousJson,previous);}
            InputSystem.settings=previous;previous.hideFlags=previousFlags;Object.Destroy(settings);yield return null;LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Press(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        private IEnumerator Pad(GamepadButton button)
        {if(gamepad==null)gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;yield return null;}
        private IEnumerator Click(RectTransform rect)
        {
            if(mouse==null)mouse=InputSystem.AddDevice<Mouse>();Canvas.ForceUpdateCanvases();var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
        }
        private string Texts()=>string.Join("\n",credits.archive.content.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text));
        [UnityTest] public IEnumerator CreditsIsThirdAndNavigationOrderMatchesApproval()
        {
            Assert.IsFalse(view.CreditsOpen);yield return Press(Key.DownArrow);Assert.AreEqual(view.controlsButton.gameObject,EventSystem.current.currentSelectedGameObject);
            yield return Press(Key.DownArrow);Assert.AreEqual(view.creditsButton.gameObject,EventSystem.current.currentSelectedGameObject);
            yield return Press(Key.DownArrow);Assert.AreEqual(view.quitButton.gameObject,EventSystem.current.currentSelectedGameObject);Assert.IsFalse(view.QuitRequested);
        }
        [UnityTest] public IEnumerator OpenSelectsArchiveAndHidesRootButtons()
        {
            view.creditsButton.onClick.Invoke();Assert.IsTrue(view.CreditsOpen);Assert.IsFalse(view.mainPanel.activeSelf);Assert.IsFalse(view.ControlsOpen);
            Assert.AreEqual(credits.scrollbar.gameObject,EventSystem.current.currentSelectedGameObject);Assert.AreEqual(1,credits.archive.verticalNormalizedPosition,.001f);yield return null;
        }
        [UnityTest] public IEnumerator KnownRosterAndOnlyConfirmedToolsAreVisible()
        {
            view.OpenCredits();string text=Texts();foreach(var token in new[]{"Devin Padin / Jarheadd1","Douglas (DeeFeeCee)","Sarah (AceofHome)","Alexzander (mrdrworm)","Unity 6000.3.26f1","C#","Git","GitHub"})StringAssert.Contains(token,text);
            foreach(var old in new[]{"Index Code","Canyon Drive","Tinblood","Lux Fracta"})StringAssert.DoesNotContain(old,text);yield return null;
        }

        [Test]
        public void ConfirmedDeeFontIsBakedAndScopedToMenuAndPause()
        {
            const string folder = "Assets/Game/UI/Fonts/";
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(folder + "DeeFont1.17 SDF.asset");
            Assert.IsNotNull(font);
            Assert.AreEqual("DeeFont", font.faceInfo.familyName);
            Assert.AreEqual(TMPro.AtlasPopulationMode.Static, font.atlasPopulationMode);
            using var sha = System.Security.Cryptography.SHA256.Create();
            string hash = System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(folder + "DeeFont1.17.ttf"))).Replace("-", "");
            Assert.AreEqual("531F50D6BAD6D243FA4BC31343B5EA0227759BA4EE532B4EA110F5B75B4F5CF7", hash);

            foreach (var path in new[] { MenuBuilder.PrefabPath, PauseBuilder.PrefabPath })
            {
                var labels = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.IsNotEmpty(labels);
                foreach (var label in labels)
                {
                    Assert.AreSame(font, label.font, label.name);
                    Assert.AreSame(font.material, label.fontSharedMaterial, label.name);
                    var plain = System.Text.RegularExpressions.Regex.Replace(label.text, "<[^>]*>", "");
                    foreach (var glyph in plain.Where(c => !char.IsControl(c)).Distinct())
                        Assert.IsTrue(font.HasCharacter(glyph), $"{label.name}: missing U+{(int)glyph:X4}");
                }
            }

            Assert.AreNotSame(font, TMPro.TMP_Settings.defaultFontAsset);
            foreach (var path in new[] { MenuBuilder.ButtonPath, "Assets/Game/UI/Resources/UI/GameOver.prefab",
                "Assets/Game/UI/Resources/UI/MobilityReserveHUD.prefab" })
            {
                var labels = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.IsNotEmpty(labels);
                foreach (var label in labels) Assert.AreNotSame(font, label.font, label.name);
            }
        }
        [UnityTest] public IEnumerator SarahsCreditIsDisplayedInTheRosterAndArtSection()
        {
            view.OpenCredits();
            Assert.IsTrue(view.CreditsOpen);
            Canvas.ForceUpdateCanvases();
            var roster = credits.archive.content.Find("PROJECT TEAMBody").GetComponent<TMPro.TMP_Text>();
            var art = credits.archive.content.Find("ART & ANIMATIONBody").GetComponent<TMPro.TMP_Text>();
            CollectionAssert.Contains(roster.text.Split('\n'), "Sarah (AceofHome)");
            Assert.AreEqual("<b>Sarah (AceofHome)</b>\nArtwork, animation and narrative collaboration.", art.text);
            art.ForceMeshUpdate();
            Assert.IsFalse(art.isTextOverflowing);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeeKeepsDevelopmentCreditAndHasConfirmedAudioCredit()
        {
            view.OpenCredits();
            var content = credits.archive.content;
            var development = content.Find("GAME DEVELOPMENTBody").GetComponent<TMPro.TMP_Text>();
            StringAssert.Contains("<b>Douglas (DeeFeeCee)</b>\nRepository coordination, integration and gameplay mechanics.", development.text);
            var audio = content.Find("AUDIO & MUSICBody").GetComponent<TMPro.TMP_Text>();
            Assert.AreEqual("<b>Douglas (DeeFeeCee)</b>\nMusic & Sound FX", audio.text);
            Assert.AreEqual("CONFIRMED CONTRIBUTION", content.Find("AUDIO & MUSICRecord").GetComponent<TMPro.TMP_Text>().text);
            var typography = content.Find("TYPOGRAPHY & FONT DESIGNBody").GetComponent<TMPro.TMP_Text>();
            Assert.AreEqual("<b>Douglas (DeeFeeCee)</b>\nOriginal Typeface: DeeFont1.17", typography.text);

            Canvas.ForceUpdateCanvases();
            var viewport = credits.archive.viewport;
            float scrollable = content.rect.height - viewport.rect.height;
            Assert.Greater(scrollable, 0f);
            foreach (var credit in new[] { audio, typography })
            {
                var center = credit.rectTransform.TransformPoint(credit.rectTransform.rect.center);
                float offset = -content.InverseTransformPoint(center).y - viewport.rect.height / 2f;
                credits.archive.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / scrollable);
                yield return null;
                credit.ForceMeshUpdate();
                Assert.IsFalse(credit.isTextOverflowing);
                Assert.LessOrEqual(credit.textBounds.size.x, credit.rectTransform.rect.width);
                var corners = new Vector3[4];
                credit.rectTransform.GetWorldCorners(corners);
                Assert.IsTrue(viewport.rect.Contains(viewport.InverseTransformPoint(corners[0])));
                Assert.IsTrue(viewport.rect.Contains(viewport.InverseTransformPoint(corners[2])));
            }
            Assert.IsTrue(credits.returnButton.IsInteractable());
        }

        [UnityTest] public IEnumerator ProvisionalCategoriesAndUnconfirmedSectionsAreMarked()
        {
            view.OpenCredits();string text=Texts();foreach(var token in new[]{"PROJECT TEAM","GAME DEVELOPMENT","ART & ANIMATION","LEVEL & PUZZLE DESIGN","TOOLS & TECHNOLOGY","AUDIO & MUSIC","SPECIAL THANKS","NOT FINAL OR COMPLETE","PENDING CONFIRMATION"})StringAssert.Contains(token,text);yield return null;
        }
        [UnityTest] public IEnumerator CreditsTextRendersAndWrapsWithoutHorizontalOverflow()
        {
            view.OpenCredits();Canvas.ForceUpdateCanvases();
            foreach(var text in credits.archive.content.GetComponentsInChildren<TMPro.TMP_Text>())
            {text.ForceMeshUpdate();Assert.Greater(text.textInfo.characterCount,0);Assert.Greater(text.rectTransform.rect.height,0);Assert.IsFalse(text.isTextOverflowing,text.name);}
            yield return null;
        }
        [UnityTest] public IEnumerator KeyboardScrollsAndRightReachesFixedReturn()
        {
            view.OpenCredits();float top=credits.archive.verticalNormalizedPosition;yield return Press(Key.DownArrow);Assert.Less(credits.archive.verticalNormalizedPosition,top);
            yield return Press(Key.RightArrow);Assert.AreEqual(credits.returnButton.gameObject,EventSystem.current.currentSelectedGameObject);
            yield return Press(Key.Enter);Assert.IsFalse(view.CreditsOpen);Assert.AreEqual(view.creditsButton.gameObject,EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest] public IEnumerator GamepadScrollsAndEastReturnsWithFocus()
        {
            view.OpenCredits();yield return Pad(GamepadButton.DpadDown);Assert.Less(credits.archive.verticalNormalizedPosition,.999f);
            yield return Pad(GamepadButton.East);Assert.IsFalse(view.CreditsOpen);Assert.AreEqual(view.creditsButton.gameObject,EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest] public IEnumerator GamepadStickScrollsWithoutActivatingRootButtons()
        {
            view.OpenCredits();gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.down});yield return new WaitForSecondsRealtime(.2f);
            Assert.Less(credits.archive.verticalNormalizedPosition,.999f);Assert.IsFalse(view.QuitRequested);Assert.IsFalse(view.launcher.IsLoading);
        }
        [UnityTest] public IEnumerator MouseWheelScrollsTheViewport()
        {
            view.OpenCredits();mouse=InputSystem.AddDevice<Mouse>();Canvas.ForceUpdateCanvases();var rect=credits.archive.viewport;var p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            float tick=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms?1:120;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=p,scroll=new Vector2(0,-tick)});yield return null;yield return null;
            Assert.Less(credits.archive.verticalNormalizedPosition,.999f);Assert.Greater(credits.archive.verticalNormalizedPosition,.01f,"One wheel tick should not skip the whole archive.");
        }
        [UnityTest] public IEnumerator MouseOpensAndFixedReturnWorksAtBottom()
        {
            yield return Click(view.creditsButton.GetComponent<RectTransform>());Assert.IsTrue(view.CreditsOpen);credits.archive.verticalNormalizedPosition=0;
            yield return Click(credits.returnButton.GetComponent<RectTransform>());Assert.IsFalse(view.CreditsOpen);Assert.AreEqual(view.creditsButton.gameObject,EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest] public IEnumerator EscapeReturnsAndNeverQuitsAtRoot()
        {view.OpenCredits();yield return Press(Key.Escape);Assert.IsFalse(view.CreditsOpen);yield return Press(Key.Escape);Assert.IsFalse(view.QuitRequested);Assert.IsFalse(view.launcher.IsLoading);}
        [UnityTest] public IEnumerator ReopeningResetsTopAndDoesNotDuplicateContent()
        {
            view.OpenCredits();int count=credits.archive.content.childCount;for(int i=0;i<5;i++){credits.archive.verticalNormalizedPosition=0;view.CloseCredits();view.OpenCredits();Assert.AreEqual(1,credits.archive.verticalNormalizedPosition,.001f);Assert.AreEqual(count,credits.archive.content.childCount);}yield return null;
        }
        [UnityTest] public IEnumerator ReturnStaysOutsideViewportAndDoesNotMove()
        {
            view.OpenCredits();Canvas.ForceUpdateCanvases();var rect=credits.returnButton.GetComponent<RectTransform>();Vector3 before=rect.position;
            credits.archive.verticalNormalizedPosition=0;yield return null;Assert.AreEqual(before,rect.position);Assert.IsFalse(rect.IsChildOf(credits.archive.content));
            Assert.IsTrue(credits.returnButton.IsInteractable());Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator ShortAndLongContentKeepReturnUsable()
        {
            view.OpenCredits();var bodies=credits.archive.content.GetComponentsInChildren<TMPro.TMP_Text>();
            foreach(var label in bodies)label.gameObject.SetActive(false);view.CloseCredits();view.OpenCredits();Assert.IsTrue(credits.returnButton.IsInteractable());
            foreach(var label in bodies)label.gameObject.SetActive(true);view.CloseCredits();view.OpenCredits();Assert.Greater(credits.archive.content.rect.height,credits.archive.viewport.rect.height);Assert.AreEqual(1,credits.archive.verticalNormalizedPosition,.001f);yield return null;
        }
        [UnityTest] public IEnumerator BrowsingNeverCreatesResetsOrSpendsBank()
        {
            Assert.IsNull(RunDashBank.Instance);view.OpenCredits();yield return Press(Key.DownArrow);view.CloseCredits();Assert.IsNull(RunDashBank.Instance);
            var bank=Object.Instantiate(AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab"));bank.TrySpendDash();int balance=bank.RemainingCharges;int resets=0,changes=0;bank.NewRunStarted+=()=>resets++;bank.ChargesChanged+=_=>changes++;
            for(int i=0;i<4;i++){view.OpenCredits();credits.archive.verticalNormalizedPosition=0;view.CloseCredits();}Assert.AreEqual(balance,bank.RemainingCharges);Assert.AreEqual(0,resets);Assert.AreEqual(0,changes);
        }
        [UnityTest] public IEnumerator ControlsStillWorksAfterCredits()
        {view.OpenCredits();view.CloseCredits();view.OpenControls();Assert.IsTrue(view.ControlsOpen);yield return Press(Key.Escape);Assert.IsFalse(view.ControlsOpen);Assert.AreEqual(view.controlsButton.gameObject,EventSystem.current.currentSelectedGameObject);}
        [UnityTest] public IEnumerator ReenabledViewHasOneCancelHandlerAndNoDuplicatePanels()
        {
            for(int i=0;i<3;i++){view.enabled=false;view.enabled=true;view.OpenCredits();yield return Press(Key.Escape);Assert.IsFalse(view.CreditsOpen);Assert.AreEqual(view.creditsButton.gameObject,EventSystem.current.currentSelectedGameObject);}
            Assert.AreEqual(1,Object.FindObjectsByType<CreditsArchiveView>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator AllMainButtonsFitAfterAddingFourthRow()
        {
            Canvas.ForceUpdateCanvases();var buttons=new[]{view.newGameButton,view.controlsButton,view.creditsButton,view.quitButton};
            for(int i=0;i<buttons.Length;i++){var rect=buttons[i].GetComponent<RectTransform>();Assert.Greater(rect.rect.height,40);if(i>0)Assert.Less(rect.position.y,buttons[i-1].transform.position.y);}yield return null;
        }
        [UnityTest] public IEnumerator NewGameRemainsExplicitAndResetsAfterBrowsing()
        {
            var bank=Object.Instantiate(AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab"));bank.TrySpendDash();view.OpenCredits();view.CloseCredits();
            Assert.AreEqual(4,bank.RemainingCharges);view.newGameButton.onClick.Invoke();string path=view.launcher.gameplayScene;
            for(int i=0;i<240&&SceneManager.GetActiveScene().path!=path;i++)yield return null;
            Assert.AreEqual(path,SceneManager.GetActiveScene().path);Assert.AreEqual(5,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator LoadingBlocksOpeningCredits()
        {view.newGameButton.onClick.Invoke();Assert.IsFalse(view.creditsButton.interactable);view.OpenCredits();Assert.IsFalse(view.CreditsOpen);for(int i=0;i<240&&SceneManager.GetActiveScene().path==MenuBuilder.ScenePath;i++)yield return null;}
        [Test] public void ScrollHierarchyAndMetadataReferencesAreComplete()
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.PrefabPath).GetComponent<MainMenuView>().credits;
            Assert.NotNull(p.archive.viewport.GetComponent<UnityEngine.UI.Mask>());Assert.NotNull(p.archive.viewport.GetComponent<UnityEngine.UI.Image>());
            Assert.NotNull(p.archive.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>());Assert.NotNull(p.archive.content.GetComponent<UnityEngine.UI.ContentSizeFitter>());
            Assert.IsFalse(p.panel.activeSelf);Assert.AreEqual(p.scrollbar,p.archive.verticalScrollbar);
        }
    }
}
#endif
