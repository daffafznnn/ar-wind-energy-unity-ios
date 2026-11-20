using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace WindEnergy
{
    public enum ExperiencePanelMode { Compact, Expanded, Focus }

    public class ExperienceUI : MonoBehaviour
    {
        public ExperiencePanelMode PanelMode { get; private set; }
        public Button detailButton, focusButton, restoreButton, focusPauseButton;
        public GameObject focusDock, detailContent, sheetControls;
        public TMP_Text focusStatus;
        private ExperiencePanelMode previousMode;

        public void ExpandDetails()
        {
            if (!Learning) return;
            PanelMode = ExperiencePanelMode.Expanded;
            Render();
        }

        public void CollapseDetails()
        {
            if (!Learning) return;
            PanelMode = ExperiencePanelMode.Compact;
            state.SetTab(LearningTab.Wind);
            Render();
        }

        public void EnterFocus()
        {
            if (!Learning || PanelMode == ExperiencePanelMode.Focus) return;
            previousMode = PanelMode;
            PanelMode = ExperiencePanelMode.Focus;
            Render();
            FocusControl(restoreButton);
        }

        public void RestoreControls()
        {
            if (PanelMode != ExperiencePanelMode.Focus) return;
            PanelMode = previousMode;
            Render();
            FocusControl(focusButton);
        }

        private void ToggleDetails()
        {
            if (PanelMode == ExperiencePanelMode.Expanded) CollapseDetails();
            else ExpandDetails();
        }

        private void Pause()
        {
            if (state != null && state.Phase == ExperiencePhase.Wind && state.Wind != WindLevel.None)
                state.TogglePause();
        }

        private static void FocusControl(Selectable control)
        {
            if (EventSystem.current != null && control != null && control.IsActive() && control.IsInteractable())
                EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
        public ARExperienceController controller;
        public Button[] buttons;
        public Slider windSlider;
        public GameObject placementPanel, windPanel, partsPanel, tabs;
        public TMP_Text status, description, windLabel, motion, partTitle, partCopy, partNote;
        public TMP_Text appTitle, subtitle, flow, disclaimer;
        public TMP_Text[] windTicks;
        public RectTransform windScale;
        public RectTransform safeArea, header, sheet, content;
        public ScrollRect scroll;
        private ExperienceState state;
        private UnityAction[] listeners;
        private float textScale = 1;
        private Rect lastSafe;
        private Vector2 lastScreen;
        private bool Learning => state != null && (state.Phase == ExperiencePhase.Wind || state.Phase == ExperiencePhase.Parts);

        private void OnEnable()
        {
            if (controller != null) Bind(controller.State, controller.Place, controller.ResetExperience);
        }

        public void Bind(ExperienceState value, Action place, Action reset)
        {
            Unbind();
            state = value ?? throw new ArgumentNullException(nameof(value));
            PanelMode = previousMode = ExperiencePanelMode.Compact;
            detailButton?.onClick.AddListener(ToggleDetails);
            focusButton?.onClick.AddListener(EnterFocus);
            restoreButton?.onClick.AddListener(RestoreControls);
            focusPauseButton?.onClick.AddListener(Pause);
            listeners = new UnityAction[] {
                () => { if (state.CanPlace) place(); },
                () => { if (state.HasPlacement) reset(); },
                () => { if (state.Phase == ExperiencePhase.TrackingLost) reset(); },
                () => { if (Learning) state.SetTab(LearningTab.Wind); },
                () => { if (Learning) state.SetTab(LearningTab.Parts); },
                Pause,
                () => SelectPart(TurbinePart.Rotor), () => SelectPart(TurbinePart.Generator), () => SelectPart(TurbinePart.Tower)
            };
            for (int i = 0; i < buttons.Length; i++) buttons[i].onClick.AddListener(listeners[i]);
            windSlider.minValue = 0; windSlider.maxValue = 3; windSlider.wholeNumbers = true;
            windSlider.onValueChanged.AddListener(ChangeWind);
            state.Changed += Render;
            Render();
        }

        private void SelectPart(TurbinePart part)
        {
            if (state.Phase == ExperiencePhase.Parts) state.SelectPart(part);
        }

        private void ChangeWind(float value)
        {
            if (state.Phase == ExperiencePhase.Wind) state.SetWind((WindLevel)Mathf.RoundToInt(value));
        }

        private void Unbind()
        {
            detailButton?.onClick.RemoveListener(ToggleDetails);
            focusButton?.onClick.RemoveListener(EnterFocus);
            restoreButton?.onClick.RemoveListener(RestoreControls);
            focusPauseButton?.onClick.RemoveListener(Pause);
            if (state != null) state.Changed -= Render;
            if (listeners != null)
                for (int i = 0; i < listeners.Length; i++)
                    if (buttons[i] != null) buttons[i].onClick.RemoveListener(listeners[i]);
            if (windSlider != null) windSlider.onValueChanged.RemoveListener(ChangeWind);
            listeners = null;
        }

        private void OnDisable() => Unbind();

        public void Render()
        {
            if (state == null) return;
            var phase = state.Phase;
            bool learning = Learning;
            if (!learning) PanelMode = previousMode = ExperiencePanelMode.Compact;
            else if (state.Tab == LearningTab.Parts && PanelMode == ExperiencePanelMode.Compact) PanelMode = ExperiencePanelMode.Expanded;
            bool focused = learning && PanelMode == ExperiencePanelMode.Focus;
            bool expanded = learning && PanelMode == ExperiencePanelMode.Expanded;
            if (header != null) header.gameObject.SetActive(!focused);
            if (sheet != null) sheet.gameObject.SetActive(!focused);
            if (focusDock != null) focusDock.SetActive(focused);
            if (focusStatus != null) { focusStatus.transform.parent.gameObject.SetActive(focused); focusStatus.text = state.Tab == LearningTab.Parts ? ExperienceCopy.PartTitles[(int)state.Part] + " · rotor dijeda" : "Angin " + ExperienceCopy.WindNames[(int)state.Wind].ToLowerInvariant() + (state.ManualPaused ? " · dijeda" : ""); }
            if (sheetControls != null) sheetControls.SetActive(learning);
            if (detailContent != null) detailContent.SetActive(expanded && state.Tab == LearningTab.Wind);
            if (subtitle != null) subtitle.gameObject.SetActive(false);
            if (motion != null) motion.gameObject.SetActive(false);
            SetPresentationButton(detailButton, expanded ? "Ringkas" : "Detail", learning);
            SetPresentationButton(focusButton, "Fokus AR", learning);
            SetPresentationButton(restoreButton, "Buka kontrol", focused);
            SetPresentationButton(focusPauseButton, state.Tab == LearningTab.Parts ? "Jeda: lihat bagian" : state.Wind == WindLevel.None ? "Rotor berhenti" : state.ManualPaused ? "Lanjutkan" : "Jeda", focused && state.Phase == ExperiencePhase.Wind && state.Wind != WindLevel.None);
            placementPanel.SetActive(!learning);
            tabs.SetActive(expanded);
            windPanel.SetActive(phase == ExperiencePhase.Wind);
            partsPanel.SetActive(phase == ExperiencePhase.Parts);
            SetButton(0, phase == ExperiencePhase.Placing ? "Menempatkan turbin" : state.CanPlace ? "Tempatkan turbin" : "Menunggu permukaan", state.CanPlace);
            buttons[0].gameObject.SetActive(phase == ExperiencePhase.Ready || phase == ExperiencePhase.Scanning || phase == ExperiencePhase.Placing);
            SetButton(1, "Atur ulang", state.HasPlacement);
            buttons[1].gameObject.SetActive(state.HasPlacement);
            SetButton(2, "Pindai ulang", phase == ExperiencePhase.TrackingLost);
            buttons[2].gameObject.SetActive(phase == ExperiencePhase.TrackingLost);
            SetButton(3, phase == ExperiencePhase.Wind ? "Angin (aktif)" : "Angin", learning);
            SetButton(4, phase == ExperiencePhase.Parts ? "Bagian turbin (aktif)" : "Bagian turbin", learning);
            SetButton(5, state.Wind == WindLevel.None ? "Rotor berhenti" : state.ManualPaused ? "Lanjutkan" : "Jeda", phase == ExperiencePhase.Wind && state.Wind != WindLevel.None);
            for (int i = 0; i < 3; i++)
                SetButton(i + 6, ExperienceCopy.PartNames[i] + ((int)state.Part == i ? " (dipilih)" : ""), phase == ExperiencePhase.Parts);
            windSlider.interactable = phase == ExperiencePhase.Wind;
            windSlider.SetValueWithoutNotify((int)state.Wind);
            windLabel.text = "Angin: " + ExperienceCopy.WindNames[(int)state.Wind];
            motion.text = state.Wind == WindLevel.None ? "Tanpa angin, rotor berhenti." : state.ManualPaused ? "Animasi dijeda, pengaturan tetap." : "Rotor berputar.";
            partTitle.text = ExperienceCopy.PartTitles[(int)state.Part];
            partCopy.text = ExperienceCopy.PartDescriptions[(int)state.Part];
            partNote.text = ExperienceCopy.PartNotes[(int)state.Part];
            status.text = phase switch {
                ExperiencePhase.Initializing => "Menyiapkan kamera AR",
                ExperiencePhase.Ready => "Permukaan ditemukan",
                ExperiencePhase.Placing => "Menempatkan turbin",
                ExperiencePhase.CameraDenied => "Izin kamera belum aktif",
                ExperiencePhase.Unsupported => "AR tidak tersedia",
                ExperiencePhase.TrackingLost => "Tracking terganggu",
                _ => "Cari permukaan meja"
            };
            description.text = phase switch {
                ExperiencePhase.CameraDenied => "Izin kamera belum aktif. Aktifkan izin kamera untuk aplikasi ini di Pengaturan, lalu buka kembali aplikasi.",
                ExperiencePhase.Unsupported => "AR tidak tersedia pada perangkat ini.",
                ExperiencePhase.TrackingLost => "Tracking terganggu. Arahkan kamera kembali ke area turbin.",
                ExperiencePhase.Initializing => "Tunggu sampai kamera dan sesi AR siap.",
                ExperiencePhase.Placing => "Tunggu sebentar. Turbin sedang ditempatkan pada permukaan.",
                _ => state.PlacementFailed ? ExperienceCopy.PlacementFailureMessage : "Gerakkan ponsel perlahan. Turbin bisa ditempatkan setelah permukaan ditemukan."
            };
            Layout();
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && (!selected.activeInHierarchy || !selected.TryGetComponent<Selectable>(out var control) || !control.IsInteractable()))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static void SetPresentationButton(Button button, string label, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            text.color = new Color32(23,90,70,255);
            var colors = button.colors;
            colors.normalColor = colors.selectedColor = colors.disabledColor = new Color32(255,253,248,255);
            colors.highlightedColor = new Color32(229,238,231,255);
            colors.pressedColor = new Color32(204,220,206,255);
            colors.fadeDuration = 0;
            button.colors = colors;
        }

        private void SetButton(int index, string label, bool enabled)
        {
            var button = buttons[index];
            button.interactable = enabled;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            bool primary = index == 0 || index == 2 || index == 3 && state.Tab == LearningTab.Wind || index == 4 && state.Tab == LearningTab.Parts || index >= 6 && index - 6 == (int)state.Part;
            Color cream = new Color32(255,253,248,255), green = new Color32(23,90,70,255);
            var colors = button.colors;
            colors.normalColor = colors.selectedColor = primary ? green : cream;
            colors.highlightedColor = primary ? new Color32(18,74,58,255) : new Color32(229,238,231,255);
            colors.pressedColor = primary ? new Color32(16,61,48,255) : new Color32(204,220,206,255);
            colors.disabledColor = primary ? new Color32(79,97,88,255) : cream;
            colors.colorMultiplier = 1; colors.fadeDuration = 0;
            button.colors = colors;
            text.color = primary ? cream : green;
        }

        public void ApplySafeArea(Rect area, Vector2 screen)
        {
            if (screen.x <= 0 || screen.y <= 0 || safeArea == null) return;
            safeArea.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeArea.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Layout();
        }

        public void SetTextScale(float scale)
        {
            textScale = Mathf.Clamp(scale, 1, 2);
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                label.fontSize = (label == appTitle || label == status || label == partTitle ? 21 : label == subtitle || label == disclaimer || label == partNote || windTicks != null && Array.IndexOf(windTicks,label)>=0 ? 13 : 16) * textScale;
            Layout();
        }

        private void LateUpdate()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafe || screen != lastScreen)
            {
                lastSafe = Screen.safeArea; lastScreen = screen;
                ApplySafeArea(lastSafe, lastScreen);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || state == null) return;
            if (keyboard.tabKey.wasPressedThisFrame) Navigate(keyboard.shiftKey.isPressed);
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (PanelMode == ExperiencePanelMode.Focus) RestoreControls();
                else if (PanelMode == ExperiencePanelMode.Expanded) { CollapseDetails(); FocusControl(detailButton); }
            }
            // Submit/move belong to InputSystemUIInputModule. Only Tab and Escape are added here.
        }

        public void Navigate(bool reverse)
        {
            if (EventSystem.current == null) return;
            var order = new List<Selectable>();
            void Add(Selectable item) { if (item != null && item.IsActive() && item.IsInteractable()) order.Add(item); }
            Add(buttons[1]); Add(buttons[0]); Add(buttons[2]); Add(detailButton); Add(focusButton); Add(buttons[3]); Add(buttons[4]); Add(windSlider);
            Add(focusPauseButton); Add(restoreButton);
            for (int i = 5; i < buttons.Length; i++) Add(buttons[i]);
            if (order.Count == 0) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            int current = order.FindIndex(s => s.gameObject == selected);
            int next = current < 0 ? (reverse ? order.Count - 1 : 0) : (current + (reverse ? -1 : 1) + order.Count) % order.Count;
            EventSystem.current.SetSelectedGameObject(order[next].gameObject);
            if (scroll != null && order[next].transform.IsChildOf(content))
            {
                Canvas.ForceUpdateCanvases();
                var point = (RectTransform)order[next].transform;
                float y = -content.InverseTransformPoint(point.TransformPoint(point.rect.center)).y;
                float overflow = content.rect.height - scroll.viewport.rect.height;
                if (overflow > 0) scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((y - scroll.viewport.rect.height / 2) / overflow);
            }
        }

        private static void Box(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }

        public void Layout()
        {
            if (header == null || content == null || safeArea.rect.width <= 0) return;
            float width = safeArea.rect.width, inner = width - 56;
            float TextHeight(TMP_Text t, float w) => Mathf.Ceil(t.GetPreferredValues(t.text, w, 10000).y) + 4;
            float ButtonHeight(Button b, float w) => Mathf.Max(48, TextHeight(b.GetComponentInChildren<TMP_Text>(true), w - 24) + 20);
            float resetWidth = textScale > 1 ? 156 : 104;
            float titleWidth = width - 32 - (buttons[1].gameObject.activeSelf ? resetWidth + 12 : 0);
            float titleH = TextHeight(appTitle, titleWidth);
            float resetH = ButtonHeight(buttons[1],resetWidth);
            float headerH = Mathf.Max(titleH, buttons[1].gameObject.activeSelf ? resetH : 0) + 12;
            Box(header,0,0,width,headerH);
            Box(appTitle.rectTransform,16,6,titleWidth,titleH);
            Box((RectTransform)buttons[1].transform,width-16-resetWidth,6,resetWidth,resetH);
            float y = 12;
            void Text(TMP_Text t) { float h = TextHeight(t,inner); Box(t.rectTransform,0,y,inner,h); y += h + 8; }
            void Button(Button b) { if(!b.gameObject.activeSelf)return;float h=ButtonHeight(b,inner);Box((RectTransform)b.transform,0,y,inner,h);y+=h+8; }
            void Row(params Button[] row)
            {
                if(textScale>1.2f){foreach(var b in row)Button(b);return;}
                float w=(inner-8*(row.Length-1))/row.Length,h=48;
                foreach(var b in row)h=Mathf.Max(h,ButtonHeight(b,w));
                for(int i=0;i<row.Length;i++)Box((RectTransform)row[i].transform,i*(w+8),y,w,h);
                y+=h+8;
            }
            void Group(GameObject g, Action rows)
            {
                if (g == null || !g.activeSelf) return;
                float start=y;y=0;rows();float h=y;Box((RectTransform)g.transform,16,start,inner,h);y=start+h;
            }
            Group(sheetControls,()=>Row(detailButton,focusButton));
            Group(placementPanel,()=>{Text(status);Text(description);Button(buttons[0]);Button(buttons[2]);});
            Group(tabs,()=>Row(buttons[3],buttons[4]));
            Group(windPanel,()=>{
                Text(windLabel);
                Box((RectTransform)windSlider.transform,0,y,inner,48);y+=48;
                if(windScale!=null && windTicks.Length>0)
                {
                    float w=(inner-8)/2,h=0;
                    foreach(var tick in windTicks)h=Mathf.Max(h,TextHeight(tick,w));
                    for(int i=0;i<windTicks.Length;i++)Box(windTicks[i].rectTransform,(i%2)*(w+8),(i/2)*(h+4),w,h);
                    float total=Mathf.Ceil(windTicks.Length/2f)*(h+4);
                    Box(windScale,0,y,inner,total);y+=total+4;
                }
                Button(buttons[5]);
            });
            Group(detailContent,()=>{Text(flow);Text(disclaimer);});
            Group(partsPanel,()=>{Row(buttons[6],buttons[7],buttons[8]);Text(partTitle);Text(partCopy);Text(partNote);});
            content.sizeDelta = new Vector2(0,y+4);
            float available = Mathf.Max(80,safeArea.rect.height-headerH-100);
            float height = Mathf.Min(y+4,available);
            sheet.anchorMin = new Vector2(0,0);sheet.anchorMax=new Vector2(1,0);sheet.pivot=new Vector2(.5f,0);
            sheet.anchoredPosition=new Vector2(0,8);sheet.sizeDelta=new Vector2(-24,height);
            if(scroll != null) scroll.vertical = y+4>height;
            if(focusDock != null)
            {
                float dockWidth=width-32;
                float w=textScale>1.2f?dockWidth:(dockWidth-8)/2;
                float a=ButtonHeight(focusPauseButton,w), b=ButtonHeight(restoreButton,w);
                float h=textScale>1.2f?a+b+8:Mathf.Max(a,b);
                var dock=(RectTransform)focusDock.transform;
                dock.anchorMin=new Vector2(0,0);dock.anchorMax=new Vector2(1,0);dock.pivot=new Vector2(.5f,0);
                dock.anchoredPosition=new Vector2(0,8);dock.sizeDelta=new Vector2(-32,h);
                Box((RectTransform)focusPauseButton.transform,0,0,w,a);
                Box((RectTransform)restoreButton.transform,textScale>1.2f?0:w+8,textScale>1.2f?a+8:0,w,b);
                float statusH=TextHeight(focusStatus,dockWidth-24);
                Box((RectTransform)focusStatus.transform.parent,16,8,dockWidth,statusH+16);
                Box(focusStatus.rectTransform,12,8,dockWidth-24,statusH);
            }
        }
    }
}
