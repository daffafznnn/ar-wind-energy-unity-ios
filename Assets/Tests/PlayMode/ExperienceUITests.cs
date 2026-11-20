using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
namespace WindEnergy.Tests.PlayMode
{
    public class ExperienceUITests
    {
        GameObject root, events;
        ExperienceUI ui;
        ExperienceState state;
        int placements, resets;
        [SetUp] public void Setup()
        {
            placements = resets = 0;
            root = new GameObject("UI test", typeof(RectTransform));
            ui = root.AddComponent<ExperienceUI>();
            ui.buttons = new Button[9];
            for (int i=0;i<9;i++)
            {
                var go = Node("Button"+i,root.transform); go.AddComponent<Image>();
                ui.buttons[i] = go.AddComponent<Button>();
                Node("Label",go.transform).AddComponent<TextMeshProUGUI>();
            }
            ui.windSlider = Node("Slider",root.transform).AddComponent<Slider>();
            ui.placementPanel=Node("Placement",root.transform); ui.windPanel=Node("Wind",root.transform);
            ui.partsPanel=Node("Parts",root.transform); ui.tabs=Node("Tabs",root.transform);
            ui.buttons[0].transform.SetParent(ui.placementPanel.transform);ui.buttons[2].transform.SetParent(ui.placementPanel.transform);
            ui.buttons[3].transform.SetParent(ui.tabs.transform);ui.buttons[4].transform.SetParent(ui.tabs.transform);
            ui.buttons[5].transform.SetParent(ui.windPanel.transform);ui.windSlider.transform.SetParent(ui.windPanel.transform);
            for(int i=6;i<9;i++) ui.buttons[i].transform.SetParent(ui.partsPanel.transform);
            ui.status=Text();ui.description=Text();ui.windLabel=Text();ui.motion=Text();ui.partTitle=Text();ui.partCopy=Text();ui.partNote=Text();
            ui.safeArea=(RectTransform)Node("Safe",root.transform).transform;
            state=new ExperienceState();ui.Bind(state,()=>placements++,()=>{resets++;state.Reset();});
            events=new GameObject("Events",typeof(EventSystem));
        }
        GameObject Node(string n,Transform parent){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(parent,false);return g;}
        TMP_Text Text()=>Node("Text",root.transform).AddComponent<TextMeshProUGUI>();
        void Place(){state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);state.SetCandidate(true);state.TryBeginPlacement(out int t);state.CompletePlacement(t,true);state.SetTracking(true,true,true);}
        [Test] public void ButtonsInvokeOneActionPerClick()
        {
            Place();ui.Bind(state,()=>placements++,()=>{resets++;state.Reset();});
            ui.buttons[5].onClick.Invoke();Assert.That(state.ManualPaused,Is.True);
            ui.buttons[1].onClick.Invoke();Assert.That(resets,Is.EqualTo(1));
        }
        [Test] public void DisabledPlaceDoesNotInvokeController()
        {
            ui.buttons[0].onClick.Invoke();Assert.That(placements,Is.Zero);
            state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);state.SetCandidate(true);
            ui.buttons[0].onClick.Invoke();Assert.That(placements,Is.EqualTo(1));
            state.TryBeginPlacement(out _);ui.buttons[0].onClick.Invoke();Assert.That(placements,Is.EqualTo(1));
        }
        [Test] public void WindSliderMapsFourLabels()
        {
            Place();Assert.That(ui.windSlider.wholeNumbers,Is.True);Assert.That(ui.windSlider.maxValue,Is.EqualTo(3));
            string[] names={"Tanpa angin","Rendah","Sedang","Tinggi"};
            for(int i=0;i<4;i++){ui.windSlider.value=i;Assert.That((int)state.Wind,Is.EqualTo(i));StringAssert.Contains(names[i],ui.windLabel.text);}
        }
        [Test] public void HiddenPanelIsExcludedFromNavigation()
        {
            Place();ui.buttons[4].onClick.Invoke();Assert.That(state.Tab,Is.EqualTo(LearningTab.Parts));
            Assert.That(ui.windPanel.activeSelf,Is.False);Assert.That(ui.partsPanel.activeSelf,Is.True);
            for(int i=0;i<10;i++){ui.Navigate(false);Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(ui.windPanel.transform),Is.False);}
        }
        [Test] public void PartsAndPauseArePreservedUntilReset()
        {
            Place();ui.buttons[5].onClick.Invoke();ui.buttons[4].onClick.Invoke();ui.buttons[7].onClick.Invoke();
            Assert.That(state.Part,Is.EqualTo(TurbinePart.Generator));StringAssert.Contains("rumah mesin",ui.partCopy.text);
            ui.buttons[3].onClick.Invoke();Assert.That(state.ManualPaused,Is.True);
            ui.buttons[1].onClick.Invoke();Assert.That(state.HasPlacement,Is.False);
        }
        [Test] public void ApplySafeAreaUsesPixelBounds()
        {
            ui.ApplySafeArea(new Rect(0,34,390,763),new Vector2(390,844));
            Assert.That(ui.safeArea.anchorMin.y,Is.EqualTo(34f/844).Within(.0001));
            Assert.That(ui.safeArea.anchorMax.y,Is.EqualTo(797f/844).Within(.0001));
        }
        [Test] public void FailureCopySurvivesRefreshButPermissionWins()
        {
            state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);state.SetCandidate(true);
            state.TryBeginPlacement(out int t);state.CompletePlacement(t,false);state.SetCandidate(true);
            StringAssert.Contains("Turbin belum bisa ditempatkan",ui.description.text);
            state.SetAvailability(Availability.CameraDenied);StringAssert.Contains("Pengaturan",ui.description.text);
        }
        [Test] public void RetryAndAllPartsInvokeTheirExactActions()
        {
            Place();ui.buttons[4].onClick.Invoke();
            for(int i=0;i<3;i++){ui.buttons[i+6].onClick.Invoke();Assert.That((int)state.Part,Is.EqualTo(i));}
            state.SetTracking(false,false,true);Assert.That(ui.buttons[2].gameObject.activeSelf,Is.True);
            ui.buttons[2].onClick.Invoke();Assert.That(resets,Is.EqualTo(1));Assert.That(state.HasPlacement,Is.False);
            state.SetAvailability(Availability.Unsupported);ui.buttons[0].onClick.Invoke();ui.buttons[2].onClick.Invoke();Assert.That(placements,Is.Zero);Assert.That(resets,Is.EqualTo(1));
        }
        [Test] public void RebindDetachesPreviousState()
        {
            Place();var previous=state;state=new ExperienceState();ui.Bind(state,()=>{},()=>{});
            var copy=ui.status.text;previous.SetTab(LearningTab.Parts);Assert.That(ui.status.text,Is.EqualTo(copy));
            state.SetAvailability(Availability.CameraDenied);StringAssert.Contains("Izin",ui.status.text);
        }
        [Test] public void PointerPressHasShapeFeedbackAndReleaseRestoresIt()
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab");
            var instance=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view=instance.GetComponent<ExperienceUI>();view.Bind(state,()=>{},()=>{});Place();
                var b=view.buttons[5];var label=b.GetComponentInChildren<TMP_Text>();var position=label.transform.localPosition;
                ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerDownHandler);
                Assert.That(label.transform.localPosition.y,Is.EqualTo(position.y-2));
                ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerUpHandler);
                Assert.That(label.transform.localPosition,Is.EqualTo(position));
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator PointerClickThroughAllFinalControls()
        {
            var instance=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab"));
            var view=instance.GetComponent<ExperienceUI>();
            ui.enabled=false;
            view.Bind(state,()=>{placements++;state.TryBeginPlacement(out int t);state.CompletePlacement(t,true);state.SetTracking(true,true,true);},()=>{resets++;state.Reset();});
            try
            {
                Click(0);Assert.That(placements,Is.Zero);
                state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);state.SetCandidate(true);Click(0);Assert.That(placements,Is.EqualTo(1));
                yield return null;Canvas.ForceUpdateCanvases();view.Layout();Canvas.ForceUpdateCanvases();
                var area=(RectTransform)view.windSlider.handleRect.parent;var corners=new Vector3[4];area.GetWorldCorners(corners);
                for(int i=0;i<4;i++)
                {
                    var world=Vector3.Lerp((corners[0]+corners[1])/2,(corners[2]+corners[3])/2,i/3f);
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,world),button=PointerEventData.InputButton.Left};
                    ExecuteEvents.Execute(view.windSlider.gameObject,pointer,ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(view.windSlider.gameObject,pointer,ExecuteEvents.pointerUpHandler);
                    Assert.That((int)state.Wind,Is.EqualTo(i),"pointer slider level "+i);
                }
                Click(5);Assert.That(state.ManualPaused,Is.True);Click(5);Assert.That(state.ManualPaused,Is.False);
                ExecuteEvents.Execute(view.detailButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Assert.That(view.PanelMode,Is.EqualTo(ExperiencePanelMode.Expanded));
                ExecuteEvents.Execute(view.focusButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Assert.That(view.PanelMode,Is.EqualTo(ExperiencePanelMode.Focus));
                ExecuteEvents.Execute(view.focusPauseButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);Assert.That(state.ManualPaused,Is.True);
                ExecuteEvents.Execute(view.restoreButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Assert.That(view.PanelMode,Is.EqualTo(ExperiencePanelMode.Expanded));
                Click(4);Assert.That(state.Tab,Is.EqualTo(LearningTab.Parts));
                for(int i=6;i<9;i++){Click(i);Assert.That((int)state.Part,Is.EqualTo(i-6));}
                Click(3);Assert.That(state.Tab,Is.EqualTo(LearningTab.Wind));Click(1);Assert.That(resets,Is.EqualTo(1));
                state.SetCandidate(true);Click(0);state.SetTracking(false,false,true);Click(2);Assert.That(resets,Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            void Click(int index)=>ExecuteEvents.Execute(view.buttons[index].gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator KeyboardSubmitMoveTabEscapeAndFocus()
        {
            var input=events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            input.AssignDefaultActions();
            input.submit.action.Disable();
            input.submit.action.AddBinding("<Keyboard>/space");
            input.submit.action.Enable();
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab");
            var instance=UnityEngine.Object.Instantiate(prefab);
            var view=instance.GetComponent<ExperienceUI>();view.Bind(state,()=>placements++,()=>{resets++;state.Reset();});
            ui.enabled=false;
            Place();yield return null;
            try
            {
                EventSystem.current.SetSelectedGameObject(view.buttons[5].gameObject);
                Assert.That(view.buttons[5].GetComponentInChildren<TMP_Text>().fontStyle.HasFlag(FontStyles.Underline),Is.True);
                yield return Key(UnityEngine.InputSystem.Key.Enter);Assert.That(state.ManualPaused,Is.True,"Enter activates exactly once");
                yield return Key(UnityEngine.InputSystem.Key.Space);Assert.That(state.ManualPaused,Is.False,"Space activates exactly once");
                EventSystem.current.SetSelectedGameObject(view.windSlider.gameObject);
                yield return Key(UnityEngine.InputSystem.Key.RightArrow);Assert.That(state.Wind,Is.EqualTo(WindLevel.High));
                view.ExpandDetails();state.SetTab(LearningTab.Parts);yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(state.Tab,Is.EqualTo(LearningTab.Wind));
                Assert.That(view.PanelMode,Is.EqualTo(ExperiencePanelMode.Compact));
                view.EnterFocus();yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(view.PanelMode,Is.EqualTo(ExperiencePanelMode.Compact));
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(view.focusButton.gameObject));
                view.ExpandDetails();
                EventSystem.current.SetSelectedGameObject(view.buttons[3].gameObject);
                yield return Key(UnityEngine.InputSystem.Key.Tab);Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(view.buttons[4].gameObject));
                yield return Key(UnityEngine.InputSystem.Key.LeftShift,UnityEngine.InputSystem.Key.Tab);Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(view.buttons[3].gameObject));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor; }
            System.Collections.IEnumerator Key(params UnityEngine.InputSystem.Key[] keys)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));yield return null;yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;yield return null;
            }
        }
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator LargeTextFitsSafeArea()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab");
            Assert.That(prefab, Is.Not.Null, "P4 prefab must exist");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var canvas = instance.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)instance.transform).sizeDelta = new Vector2(390,844);
                var view = instance.GetComponent<ExperienceUI>(); view.Bind(state,()=>{},()=>state.Reset());
                Assert.That(instance.transform.Find("Safe Area/Learning sheet/Viewport/Content/Wind/Wind scale"),Is.Not.Null,"Endpoint labels from approved v3 mockup");
                view.ApplySafeArea(new Rect(0,34,390,763),new Vector2(390,844));
                Place();
                foreach (float scale in new[]{1f,2f})
                foreach (var tab in new[]{LearningTab.Wind,LearningTab.Parts})
                {
                    state.SetTab(tab); state.SelectPart(TurbinePart.Generator);view.SetTextScale(scale);
                    yield return null; Canvas.ForceUpdateCanvases();view.Layout();Canvas.ForceUpdateCanvases();
                    foreach(var text in view.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate();
                        Assert.That(text.isTextOverflowing,Is.False,text.name+" scale="+scale);
                        Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),text.name);
                    }
                    Assert.That(view.sheet.rect.height,Is.LessThanOrEqualTo(view.safeArea.rect.height));
                    foreach(var button in view.GetComponentsInChildren<Button>())
                        Assert.That(((RectTransform)button.transform).rect.height,Is.GreaterThanOrEqualTo(48));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        [TearDown] public void Teardown(){UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(events);}
    }
}
