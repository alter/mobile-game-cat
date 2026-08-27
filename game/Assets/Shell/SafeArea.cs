using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.Shell
{
    /// <summary>
    /// Keeps the UI clear of the notch, the Dynamic Island and the home
    /// indicator by padding the panel root with <see cref="Screen.safeArea"/>.
    ///
    /// Without it the board's title ran under the Dynamic Island on iPhone —
    /// visible in the first simulator screenshot taken after the art landed.
    /// UI Toolkit does not do this on its own: the panel fills the whole
    /// screen, cutout included.
    ///
    /// It re-applies on rotation and on any resolution change rather than
    /// reading the rectangle once, because the safe area on a phone is not a
    /// constant — turn the device and the inset moves to the side.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == _applied && screen == _screen) return;
            Apply();
        }

        private void Apply()
        {
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;

            if (Screen.width <= 0 || Screen.height <= 0) return;
            var area = Screen.safeArea;

            // Screen pixels are not panel units: PanelSettings scales the UI
            // with the screen, so a 59-pixel inset is not 59 units of padding.
            // The panel's own width against the screen's gives the factor —
            // the scale is uniform, so one axis is enough.
            //
            // The root is measured before it is padded, and padding does not
            // change its width, so the factor stays right on every pass.
            // Layout is not ready on the first frame; Update retries until it
            // is, which is why the applied rectangle is only recorded here.
            var width = root.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) return;
            var scale = width / Screen.width;

            _applied = area;
            _screen = new Vector2Int(Screen.width, Screen.height);

            // The padded strip belongs to the panel root, and an unpainted
            // root is black — the inset then reads as two black bars around
            // the game rather than as the screen's own edges. Paper, the same
            // colour the board is on (DebugGame.uss).
            root.style.backgroundColor = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);

            // Screen.safeArea is bottom-up, padding is top-down: the top inset
            // is what lies above the rectangle, the bottom inset is its own y.
            root.style.paddingLeft = area.xMin * scale;
            root.style.paddingRight = (Screen.width - area.xMax) * scale;
            root.style.paddingTop = (Screen.height - area.yMax) * scale;
            root.style.paddingBottom = area.yMin * scale;
        }
    }
}
