using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace WindEnergy.Editor
{
    public static class P4ProjectSetup
    {
        private static TMP_FontAsset font;
        private static Sprite buttonShape;
        private static readonly Color Cream = new Color32(255,253,248,255);
        private static readonly Color Green = new Color32(23,90,70,255);
        public static void Configure()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("Import TMP essentials first.");
            buttonShape = Rounded("Button",32,8);
            var panelShape = Rounded("Panel",80,20);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WindEnergyAR.unity");
            var roots=scene.GetRootGameObjects();
            var controller=roots.SelectMany(r=>r.GetComponentsInChildren<ARExperienceController>(true)).Single();
            var old=roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).Single();
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=Node("Experience Canvas",null);
            var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(390,844);scaler.matchWidthOrHeight=0;
            root.AddComponent<GraphicRaycaster>();
            var ui=root.AddComponent<ExperienceUI>();
            ui.safeArea=(RectTransform)Node("Safe Area",root.transform).transform;Stretch(ui.safeArea);
            ui.header=(RectTransform)Node("Header",ui.safeArea).transform;ui.header.gameObject.AddComponent<Image>().color=Cream;
            ui.appTitle=Text("App title",ui.header,"Energi Angin",21);ui.appTitle.fontStyle=FontStyles.Bold;
            ui.subtitle=Text("Subtitle",ui.header,"Konversi energi dalam AR",13);ui.subtitle.color=new Color32(79,97,88,255);
            ui.sheet=(RectTransform)Node("Learning sheet",ui.safeArea).transform;
            var panel=ui.sheet.gameObject.AddComponent<Image>();panel.color=Cream;panel.sprite=panelShape;panel.type=Image.Type.Sliced;
            var viewport=(RectTransform)Node("Viewport",ui.sheet).transform;Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
            ui.content=(RectTransform)Node("Content",viewport).transform;ui.content.anchorMin=new Vector2(0,1);ui.content.anchorMax=Vector2.one;ui.content.pivot=new Vector2(.5f,1);ui.content.sizeDelta=Vector2.zero;
            ui.scroll=ui.sheet.gameObject.AddComponent<ScrollRect>();ui.scroll.viewport=viewport;ui.scroll.content=ui.content;ui.scroll.horizontal=false;ui.scroll.movementType=ScrollRect.MovementType.Clamped;
            ui.sheetControls=Node("Sheet controls",ui.content);
            ui.detailButton=Button("Detail",ui.sheetControls.transform);ui.focusButton=Button("Fokus AR",ui.sheetControls.transform);
            ui.focusDock=Node("Focus dock",ui.safeArea);
            ui.focusPauseButton=Button("Jeda",ui.focusDock.transform);ui.restoreButton=Button("Buka kontrol",ui.focusDock.transform);
            var focusSurface=Node("Focus status surface",ui.safeArea);var focusImage=focusSurface.AddComponent<Image>();focusImage.color=Cream;focusImage.sprite=panelShape;focusImage.type=Image.Type.Sliced;focusImage.raycastTarget=false;
            ui.focusStatus=Text("Focus status",focusSurface.transform,"",16);
            ui.detailContent=Node("Wind detail",ui.content);
            ui.placementPanel=Node("Placement",ui.content);ui.tabs=Node("Tabs",ui.content);ui.windPanel=Node("Wind",ui.content);ui.partsPanel=Node("Parts",ui.content);
            ui.status=Text("Status",ui.placementPanel.transform,"",21);ui.status.fontStyle=FontStyles.Bold;
            ui.description=Text("Placement description",ui.placementPanel.transform,"",16);
            ui.buttons=new Button[9];
            ui.buttons[0]=Button("Place",ui.placementPanel.transform);ui.buttons[1]=Button("Reset",ui.header);ui.buttons[2]=Button("Retry",ui.placementPanel.transform);
            ui.buttons[3]=Button("Wind tab",ui.tabs.transform);ui.buttons[4]=Button("Parts tab",ui.tabs.transform);
            ui.windLabel=Text("Wind label",ui.windPanel.transform,"",16);
            ui.windSlider=MakeSlider(ui.windPanel.transform);
            ui.windSlider.GetComponent<ControlFeedback>().focusLabel=ui.windLabel;
            ui.windScale=(RectTransform)Node("Wind scale",ui.windPanel.transform).transform;
            ui.windTicks=new TMP_Text[2];
            for(int i=0;i<2;i++){ui.windTicks[i]=Text("Scale "+i,ui.windScale,ExperienceCopy.WindNames[i==0?0:3],13);ui.windTicks[i].color=new Color32(79,97,88,255);ui.windTicks[i].alignment=i==0?TextAlignmentOptions.Left:TextAlignmentOptions.Right;}
            ui.motion=Text("Motion feedback",ui.windPanel.transform,"",16);ui.buttons[5]=Button("Pause",ui.windPanel.transform);
            ui.flow=Text("Conversion flow",ui.detailContent.transform,"Angin → Rotor → Generator → Listrik\n\nAngin memutar bilah. Gerak rotor diteruskan ke generator untuk menghasilkan listrik.",16);
            ui.disclaimer=Text("Disclaimer",ui.detailContent.transform,ExperienceCopy.Disclaimer,13);ui.disclaimer.color=new Color32(79,97,88,255);
            for(int i=0;i<3;i++)ui.buttons[i+6]=Button(ExperienceCopy.PartNames[i],ui.partsPanel.transform);
            ui.partTitle=Text("Part title",ui.partsPanel.transform,"",21);ui.partTitle.fontStyle=FontStyles.Bold;
            ui.partCopy=Text("Part description",ui.partsPanel.transform,"",16);
            ui.partNote=Text("Part note",ui.partsPanel.transform,"",13);ui.partNote.color=new Color32(79,97,88,255);
            ui.SetTextScale(1);
            ui.Bind(new ExperienceState(),()=>{},()=>{});
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/ExperienceCanvas.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExperienceCanvas.prefab"),scene);
            var view=instance.GetComponent<ExperienceUI>();view.controller=controller;
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            var input=roots.Where(r=>r!=null).SelectMany(r=>r.GetComponentsInChildren<InputSystemUIInputModule>(true)).Single();
            var actions = new DefaultInputActions();
            if(actions.UI.Submit.bindings.All(b=>b.effectivePath!="<Keyboard>/space")) actions.UI.Submit.AddBinding("<Keyboard>/space");
            input.actionsAsset=actions.asset;
            input.submit=InputActionReference.Create(actions.UI.Submit);
            input.cancel=InputActionReference.Create(actions.UI.Cancel);
            input.move=InputActionReference.Create(actions.UI.Navigate);
            input.point=InputActionReference.Create(actions.UI.Point);
            input.leftClick=InputActionReference.Create(actions.UI.Click);
            input.rightClick=InputActionReference.Create(actions.UI.RightClick);
            input.middleClick=InputActionReference.Create(actions.UI.MiddleClick);
            input.scrollWheel=InputActionReference.Create(actions.UI.ScrollWheel);
            input.trackedDevicePosition=InputActionReference.Create(actions.UI.TrackedDevicePosition);
            input.trackedDeviceOrientation=InputActionReference.Create(actions.UI.TrackedDeviceOrientation);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("P4_UI_CONFIGURED");
        }
        private static GameObject Node(string name,Transform parent)
        {
            var g=new GameObject(name,typeof(RectTransform));if(parent!=null)g.transform.SetParent(parent,false);return g;
        }
        private static void Stretch(RectTransform r)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static TMP_Text Text(string name,Transform parent,string value,float size)
        {
            var t=Node(name,parent).AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=new Color32(23,46,39,255);
            t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;return t;
        }
        private static Button Button(string name,Transform parent)
        {
            var g=Node(name,parent);var image=g.AddComponent<Image>();image.sprite=buttonShape;image.type=Image.Type.Sliced;image.color=Color.white;
            var outline=g.AddComponent<Outline>();outline.effectColor=Green;outline.effectDistance=new Vector2(1,-1);
            var b=g.AddComponent<Button>();b.targetGraphic=image;
            var label=Text("Label",g.transform,name,16);label.alignment=TextAlignmentOptions.Center;Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(12,10);label.rectTransform.offsetMax=new Vector2(-12,-10);
            g.AddComponent<ControlFeedback>();return b;
        }
        private static Slider MakeSlider(Transform parent)
        {
            var g=Node("Wind slider",parent);var hit=g.AddComponent<Image>();hit.color=Color.clear;
            var slider=g.AddComponent<Slider>();slider.direction=Slider.Direction.LeftToRight;slider.minValue=0;slider.maxValue=3;slider.wholeNumbers=true;
            var rail=(RectTransform)Node("Rail",g.transform).transform;rail.anchorMin=new Vector2(0,.5f);rail.anchorMax=new Vector2(1,.5f);rail.sizeDelta=new Vector2(-40,6);rail.gameObject.AddComponent<Image>().color=Green;
            var handleArea=(RectTransform)Node("Handle area",g.transform).transform;handleArea.anchorMin=new Vector2(0,.5f);handleArea.anchorMax=new Vector2(1,.5f);handleArea.sizeDelta=new Vector2(-48,32);
            var handle=(RectTransform)Node("Handle",handleArea).transform;handle.sizeDelta=new Vector2(32,0);var image=handle.gameObject.AddComponent<Image>();image.sprite=buttonShape;image.type=Image.Type.Sliced;image.color=Green;
            slider.handleRect=handle;slider.targetGraphic=image;
            var label=Text("Handle label",handle,"",16);Stretch(label.rectTransform);label.color=Cream;label.alignment=TextAlignmentOptions.Center;
            g.AddComponent<ControlFeedback>();return slider;
        }
        private static Sprite Rounded(string name,int size,int radius)
        {
            Directory.CreateDirectory("Assets/Art/UI");string path="Assets/Art/UI/"+name+".png";
            if(!File.Exists(path))
            {
                var t=new Texture2D(size,size,TextureFormat.RGBA32,false);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius)),dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));
                    float d=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                    t.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius+.5f-d)));
                }
                t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
            }
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.spriteBorder=new Vector4(radius,radius,radius,radius);importer.mipmapEnabled=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        public static void ImportEssentials()
        {
            var path = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).Single();
            AssetDatabase.importPackageCompleted += _ => {
                AssetDatabase.Refresh();
                Debug.Log("P4_TMP_IMPORTED_COMPLETE");
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (_, error) => { Debug.LogError(error); EditorApplication.Exit(1); };
            AssetDatabase.ImportPackage(path, false);
        }
    }
}
