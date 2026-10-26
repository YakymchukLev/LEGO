using System.Collections.Generic;
using UnityEngine;

namespace LegoPuzzle.Runtime
{
    /// <summary>
    /// Drives the Menu scene intro: balance widgets slide in from the right.
    /// Lives on the Canvas and updates in LateUpdate so layout rebuilds cannot snap
    /// widgets back to rest before the animation finishes.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuBalanceSlideIn : MonoBehaviour
    {
        private const float Duration = 0.7f;
        private const int SettleFrames = 2;

        private sealed class TrackedWidget
        {
            public RectTransform Rect;
            public Vector2 Rest;
            public float Delay;
            public bool Done;
        }

        private readonly List<TrackedWidget> widgets = new List<TrackedWidget>(4);
        private int settleFramesLeft = SettleFrames;
        private float elapsed;
        private bool playing;

        public bool IsAnimating => playing && !AllDone();
        public bool HasRestPosition { get; private set; }
        public Vector2 RestAnchoredPosition { get; private set; }

        private void LateUpdate()
        {
            if (!Application.isPlaying || widgets.Count == 0) return;

            if (settleFramesLeft > 0)
            {
                settleFramesLeft--;
                for (int i = 0; i < widgets.Count; i++)
                {
                    ApplyOffscreen(widgets[i]);
                }

                if (settleFramesLeft == 0)
                {
                    playing = true;
                    elapsed = 0f;
                }

                return;
            }

            if (!playing) return;

            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < widgets.Count; i++)
            {
                TrackedWidget item = widgets[i];
                if (item == null || item.Rect == null || item.Done) continue;

                float t = (elapsed - item.Delay) / Duration;
                if (t < 0f)
                {
                    ApplyOffscreen(item);
                    continue;
                }

                if (t >= 1f)
                {
                    item.Rect.anchoredPosition = item.Rest;
                    item.Done = true;
                    continue;
                }

                float progress = EaseOutCubic(t);
                Vector2 start = GetOffscreenPosition(item);
                item.Rect.anchoredPosition = Vector2.LerpUnclamped(start, item.Rest, progress);
            }

            if (AllDone())
            {
                playing = false;
                enabled = false;
            }
        }

        private void ApplyOffscreen(TrackedWidget item)
        {
            if (item == null || item.Rect == null) return;
            item.Rect.anchoredPosition = GetOffscreenPosition(item);
        }

        private Vector2 GetOffscreenPosition(TrackedWidget item)
        {
            return new Vector2(item.Rest.x + GetTravelDistance(item.Rect), item.Rest.y);
        }

        private static float GetTravelDistance(RectTransform rect)
        {
            float width = 0f;
            RectTransform parentRt = rect != null ? rect.parent as RectTransform : null;
            if (parentRt != null)
            {
                width = parentRt.rect.width;
            }

            if (width < 80f)
            {
                Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
                if (canvas != null)
                {
                    RectTransform canvasRt = canvas.transform as RectTransform;
                    if (canvasRt != null) width = canvasRt.rect.width;
                    if (width < 80f && canvas.scaleFactor > 0.01f)
                    {
                        width = Screen.width / canvas.scaleFactor;
                    }
                }
            }

            if (width < 80f)
            {
                width = Screen.width > 0 ? Screen.width : 1200f;
            }

            return width + 160f;
        }

        private bool AllDone()
        {
            if (widgets.Count == 0) return false;
            for (int i = 0; i < widgets.Count; i++)
            {
                if (widgets[i] != null && widgets[i].Rect != null && !widgets[i].Done)
                    return false;
            }

            return true;
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public void Register(RectTransform widget, float delay)
        {
            if (!Application.isPlaying || widget == null) return;

            for (int i = 0; i < widgets.Count; i++)
            {
                if (widgets[i].Rect == widget) return;
            }

            var item = new TrackedWidget
            {
                Rect = widget,
                Rest = widget.anchoredPosition,
                Delay = delay,
                Done = false
            };

            widgets.Add(item);
            HasRestPosition = true;
            RestAnchoredPosition = item.Rest;
            ApplyOffscreen(item);

            playing = false;
            settleFramesLeft = SettleFrames;
            elapsed = 0f;
            enabled = true;
        }

        /// <summary>
        /// Rest position lookup used by hearts shake so it does not fight the intro.
        /// </summary>
        public static bool TryGetRestPosition(RectTransform widget, out Vector2 rest)
        {
            rest = widget != null ? widget.anchoredPosition : Vector2.zero;
            if (widget == null) return false;

            MenuBalanceSlideIn intro = widget.GetComponentInParent<MenuBalanceSlideIn>();
            if (intro == null) return false;

            for (int i = 0; i < intro.widgets.Count; i++)
            {
                if (intro.widgets[i].Rect == widget)
                {
                    rest = intro.widgets[i].Rest;
                    return true;
                }
            }

            return false;
        }

        public static bool IsWidgetAnimating(RectTransform widget)
        {
            if (widget == null) return false;
            MenuBalanceSlideIn intro = widget.GetComponentInParent<MenuBalanceSlideIn>();
            if (intro == null || !intro.playing) return false;

            for (int i = 0; i < intro.widgets.Count; i++)
            {
                if (intro.widgets[i].Rect == widget)
                    return !intro.widgets[i].Done;
            }

            return false;
        }

        public static MenuBalanceSlideIn AttachTo(GameObject widget, float delay)
        {
            if (widget == null) return null;
            if (!Application.isPlaying) return null;

            RectTransform rect = widget.GetComponent<RectTransform>();
            if (rect == null) return null;

            Canvas canvas = widget.GetComponentInParent<Canvas>();
            GameObject host = canvas != null ? canvas.gameObject : widget;

            MenuBalanceSlideIn intro = host.GetComponent<MenuBalanceSlideIn>();
            if (intro == null)
            {
                intro = host.AddComponent<MenuBalanceSlideIn>();
            }

            intro.Register(rect, delay);
            return intro;
        }
    }
}
