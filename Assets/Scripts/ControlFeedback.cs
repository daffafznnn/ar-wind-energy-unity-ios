using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace WindEnergy
{
    public sealed class ControlFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        public TMP_Text focusLabel;
        private TMP_Text label;
        private Vector3 restingPosition;
        private bool pressed;
        private void Awake() => label = focusLabel != null ? focusLabel : GetComponentInChildren<TMP_Text>(true);
        public void OnSelect(BaseEventData data) { if (label != null) label.fontStyle |= FontStyles.Underline; }
        public void OnDeselect(BaseEventData data) { if (label != null) label.fontStyle &= ~FontStyles.Underline; }
        public void OnPointerDown(PointerEventData data)
        {
            if (label == null || !GetComponent<Selectable>().IsInteractable() || pressed) return;
            restingPosition = label.transform.localPosition; pressed = true;
            label.transform.localPosition = restingPosition + Vector3.down * 2;
        }
        public void OnPointerUp(PointerEventData data) => Release();
        private void Release() { if (pressed && label != null) label.transform.localPosition = restingPosition; pressed = false; }
        private void OnDisable() { Release(); if(label != null) label.fontStyle &= ~FontStyles.Underline; }
    }
}
