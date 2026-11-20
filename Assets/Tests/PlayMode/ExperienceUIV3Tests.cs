using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WindEnergy.Tests.PlayMode
{
    public class ExperienceUIV3Tests
    {
        GameObject root;
        ExperienceUI ui;
        ExperienceState state;
        int resets;
        [SetUp] public void Setup()
        {
            root=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab"));
            ui=root.GetComponent<ExperienceUI>();state=new ExperienceState();resets=0;
            ui.Bind(state,()=>{},()=>{resets++;state.Reset();});
        }
        void Place(){state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);state.SetCandidate(true);state.TryBeginPlacement(out int t);state.CompletePlacement(t,true);state.SetTracking(true,true,true);}
        void Act(string name){var method=typeof(ExperienceUI).GetMethod(name);Assert.That(method,Is.Not.Null,"Missing presentation action: "+name);method.Invoke(ui,null);}
        string Mode(){var prop=typeof(ExperienceUI).GetProperty("PanelMode");Assert.That(prop,Is.Not.Null,"Missing PanelMode");return prop.GetValue(ui).ToString();}
        T Field<T>(string name) where T:UnityEngine.Object {var field=typeof(ExperienceUI).GetField(name);Assert.That(field,Is.Not.Null,name);var value=field.GetValue(ui) as T;Assert.That(value,Is.Not.Null,name+" binding");return value;}
        [Test] public void PlacementStartsCompact(){Place();Assert.That(Mode(),Is.EqualTo("Compact"));Assert.That(ui.tabs.activeSelf,Is.False);Assert.That(ui.windPanel.activeInHierarchy,Is.True);}
        [Test] public void FocusRestoresDetailsAndSelection(){Place();Act("ExpandDetails");state.TogglePause();state.SetTab(LearningTab.Parts);state.SelectPart(TurbinePart.Generator);Act("EnterFocus");Assert.That(ui.sheet.gameObject.activeSelf,Is.False);Act("RestoreControls");Assert.That(Mode(),Is.EqualTo("Expanded"));Assert.That(state.Tab,Is.EqualTo(LearningTab.Parts));Assert.That(state.Part,Is.EqualTo(TurbinePart.Generator));Assert.That(state.ManualPaused,Is.True);}
        [Test] public void CollapseReturnsWindWithoutReset(){Place();Act("ExpandDetails");state.SetWind(WindLevel.High);state.TogglePause();state.SetTab(LearningTab.Parts);state.SelectPart(TurbinePart.Generator);Act("CollapseDetails");Assert.That(Mode(),Is.EqualTo("Compact"));Assert.That(state.Tab,Is.EqualTo(LearningTab.Wind));Assert.That(state.Wind,Is.EqualTo(WindLevel.High));Assert.That(state.Part,Is.EqualTo(TurbinePart.Generator));Assert.That(state.ManualPaused,Is.True);Assert.That(resets,Is.Zero);}
        [Test] public void FocusZeroAndPartsCannotResume(){Place();state.SetWind(WindLevel.None);Act("EnterFocus");var pause=Field<Button>("focusPauseButton");Assert.That(pause.interactable,Is.False);pause.onClick.Invoke();Assert.That(state.CanAnimate,Is.False);Act("RestoreControls");Act("ExpandDetails");state.SetWind(WindLevel.High);state.SetTab(LearningTab.Parts);Act("EnterFocus");Assert.That(pause.interactable,Is.False);pause.onClick.Invoke();Assert.That(state.ManualPaused,Is.False);Assert.That(state.CanAnimate,Is.False);}
        [Test] public void TrackingLossExitsFocus(){Place();Act("EnterFocus");state.SetTracking(false,false,true);Assert.That(Mode(),Is.EqualTo("Compact"));Assert.That(ui.sheet.gameObject.activeSelf,Is.True);Assert.That(Field<GameObject>("focusDock").activeSelf,Is.False);state.SetTracking(true,true,true);Assert.That(Mode(),Is.EqualTo("Compact"));}
        [Test] public void ResetAndRebindClearPresentation(){Place();Act("EnterFocus");state.Reset();Assert.That(Mode(),Is.EqualTo("Compact"));Place();Act("EnterFocus");ui.Bind(new ExperienceState(),()=>{},()=>{});Assert.That(Mode(),Is.EqualTo("Compact"));Assert.That(ui.sheet.gameObject.activeSelf,Is.True);}
        [Test] public void CompactCameraClearance(){Place();var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)root.transform).sizeDelta=new Vector2(390,844);ui.ApplySafeArea(new Rect(0,34,390,763),new Vector2(390,844));Canvas.ForceUpdateCanvases();ui.Layout();Assert.That(ui.header.rect.height,Is.LessThanOrEqualTo(64));Assert.That(ui.sheet.rect.height,Is.LessThanOrEqualTo(260));Assert.That(ui.safeArea.rect.height-ui.header.rect.height-ui.sheet.rect.height-ui.sheet.anchoredPosition.y,Is.GreaterThanOrEqualTo(400));}
        [Test] public void FocusHidesLargeChrome(){Place();Act("EnterFocus");Assert.That(ui.header.gameObject.activeSelf,Is.False);Assert.That(ui.sheet.gameObject.activeSelf,Is.False);Assert.That(Field<GameObject>("focusDock").activeSelf,Is.True);Assert.That(Field<Button>("restoreButton").IsInteractable(),Is.True);}
        [Test] public void ScaledLayoutsRemainReachable()
        {
            Place();root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            foreach(float width in new[]{320f,390f,430f})
            foreach(float scale in new[]{1f,2f})
            foreach(LearningTab tab in Enum.GetValues(typeof(LearningTab)))
            foreach(TurbinePart part in Enum.GetValues(typeof(TurbinePart)))
            foreach(string mode in new[]{"Compact","Expanded","Focus"})
            {
                ((RectTransform)root.transform).sizeDelta=new Vector2(width,844);
                ui.CollapseDetails();ui.ExpandDetails();state.SetTab(tab);state.SelectPart(part);
                if(mode=="Compact")ui.CollapseDetails();else if(mode=="Focus")ui.EnterFocus();
                ui.SetTextScale(scale);ui.ApplySafeArea(new Rect(0,34,width,763),new Vector2(width,844));
                Canvas.ForceUpdateCanvases();ui.Layout();Canvas.ForceUpdateCanvases();
                string context=$"{width}/{scale}/{tab}/{part}/{mode}";
                foreach(var text in ui.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing,Is.False,context+" "+text.name);
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),context+" "+text.name);
                }
                foreach(var button in ui.GetComponentsInChildren<Button>())
                {
                    var rect=(RectTransform)button.transform;
                    Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(48),context+" "+button.name);
                    var corners=new Vector3[4];rect.GetWorldCorners(corners);
                    foreach(var corner in corners){float x=ui.safeArea.InverseTransformPoint(corner).x;Assert.That(x,Is.InRange(ui.safeArea.rect.xMin-1,ui.safeArea.rect.xMax+1),context+" "+button.name);}
                }
                if(mode=="Focus")
                {
                    foreach(var rect in new[]{(RectTransform)ui.focusDock.transform,(RectTransform)ui.focusStatus.transform.parent})
                    {
                        var corners=new Vector3[4];rect.GetWorldCorners(corners);
                        foreach(var corner in corners){float y=ui.safeArea.InverseTransformPoint(corner).y;Assert.That(y,Is.InRange(ui.safeArea.rect.yMin-1,ui.safeArea.rect.yMax+1),context);}
                    }
                }
                else if(ui.scroll.vertical)
                {
                    ui.scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                    var corners=new Vector3[4];ui.content.GetWorldCorners(corners);
                    Assert.That(ui.scroll.viewport.InverseTransformPoint(corners[0]).y,Is.GreaterThanOrEqualTo(ui.scroll.viewport.rect.yMin-1),context+" scroll bottom reachable");
                }
            }
        }
        [Test] public void RebindDoesNotDuplicateModeListeners()
        {
            Place();ui.Bind(state,()=>{},()=>state.Reset());ui.Bind(state,()=>{},()=>state.Reset());
            ui.detailButton.onClick.Invoke();Assert.That(ui.PanelMode,Is.EqualTo(ExperiencePanelMode.Expanded));
            ui.detailButton.onClick.Invoke();Assert.That(ui.PanelMode,Is.EqualTo(ExperiencePanelMode.Compact));
            ui.focusButton.onClick.Invoke();ui.focusPauseButton.onClick.Invoke();Assert.That(state.ManualPaused,Is.True);
            ui.restoreButton.onClick.Invoke();Assert.That(ui.PanelMode,Is.EqualTo(ExperiencePanelMode.Compact));
        }
        [UnityEngine.TestTools.UnityTest] public System.Collections.IEnumerator OverlayDoesNotTriggerPlacement()
        {
            int placements=0;ui.Bind(state,()=>placements++,()=>{});
            state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);state.SetCandidate(true);
            var events=new GameObject("V3 EventSystem",typeof(UnityEngine.EventSystems.EventSystem));
            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();ui.Layout();Canvas.ForceUpdateCanvases();
                var results=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                var point=RectTransformUtility.WorldToScreenPoint(null,ui.sheet.TransformPoint(new Vector3(0,ui.sheet.rect.height-4)));
                var data=new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>()){position=point};
                root.GetComponent<GraphicRaycaster>().Raycast(data,results);
                Assert.That(results,Is.Not.Empty,$"point={point} screen={Screen.width}x{Screen.height} canvas={root.GetComponent<Canvas>().renderMode} sheet={ui.sheet.rect} depth={ui.sheet.GetComponent<Image>().depth} active={ui.sheet.gameObject.activeInHierarchy}");
                Assert.That(results[0].gameObject.transform.IsChildOf(ui.sheet),Is.True);
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(results[0].gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                Assert.That(placements,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(events);}
        }
        [TearDown] public void TearDown(){UnityEngine.Object.DestroyImmediate(root);}
    }
}
