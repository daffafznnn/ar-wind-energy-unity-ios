using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace WindEnergy.Editor
{
    public static class P4VisualEvidence
    {
        public static void Capture()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab"));
            var ui = root.GetComponent<ExperienceUI>();
            var camera = new GameObject("Editor evidence camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(170,181,174,255);camera.nearClipPlane=.01f;
            var target=new RenderTexture(1170,2532,24);target.Create();camera.targetTexture=target;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var state=new ExperienceState();ui.Bind(state,()=>{},()=>state.Reset());
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../evidence/test-runs/p4-v3/visual"));Directory.CreateDirectory(output);
            foreach(float scale in new[]{1f,2f})
            {
                state.Reset();state.SetAvailability(Availability.Checking);CaptureState("initializing");
                state.SetAvailability(Availability.Ready);state.SetTracking(true,false,true);CaptureState("scanning");
                state.SetCandidate(true);CaptureState("ready");state.TryBeginPlacement(out int ticket);CaptureState("pending");
                state.CompletePlacement(ticket,false);state.SetCandidate(true);CaptureState("failure");
                state.TryBeginPlacement(out ticket);state.CompletePlacement(ticket,true);state.SetTracking(true,true,true);CaptureState("wind");
                ui.ExpandDetails();CaptureState("expanded-wind");ui.EnterFocus();CaptureState("focus-wind");ui.RestoreControls();ui.CollapseDetails();
                state.SetWind(WindLevel.None);CaptureState("zero");state.SetWind(WindLevel.Medium);state.TogglePause();CaptureState("paused");
                state.SetTab(LearningTab.Parts);
                foreach(TurbinePart part in Enum.GetValues(typeof(TurbinePart))){state.SelectPart(part);CaptureState("part-"+part);}
                ui.EnterFocus();CaptureState("focus-parts");ui.RestoreControls();
                state.SetTracking(false,false,true);CaptureState("tracking");state.SetAvailability(Availability.CameraDenied);CaptureState("denied");state.SetAvailability(Availability.Unsupported);CaptureState("unsupported");
                void CaptureState(string name)
                {
                    ui.SetTextScale(scale);Canvas.ForceUpdateCanvases();ui.ApplySafeArea(new Rect(0,102,1170,2289),new Vector2(1170,2532));ui.Layout();Canvas.ForceUpdateCanvases();
                    ui.scroll.verticalNormalizedPosition=1;
                    Save(name+"-"+scale);
                    foreach(var text in ui.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate();if(text.isTextOverflowing || text.preferredHeight>text.rectTransform.rect.height+1)throw new InvalidOperationException("Overflow "+name+" scale="+scale+" "+text.name);
                    }
                    if(ui.scroll.vertical){ui.scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();Save(name+"-"+scale+"-bottom");}
                }
            }
            camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("P4_V3_VISUAL_PASS: compact/detail/focus plus lifecycle at text scales 1/2; Editor renders, not iPhone acceptance.");
            void Save(string name)
            {
                string file=Path.Combine(output,name+".png");
                if(File.Exists(file))throw new IOException("Evidence already exists "+file);
                camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
