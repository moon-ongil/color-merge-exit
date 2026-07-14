using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorMergeExit.Game
{
    public enum TutorialKind { Drag, Tap }

    /// <summary>One tutorial demo step. <see cref="TutorialKind.Drag"/> animates a ghost block +
    /// hand sliding From→To (teaching a move); <see cref="TutorialKind.Tap"/> animates a hand
    /// tapping at From (teaching an item button).</summary>
    public struct TutorialStep
    {
        public TutorialKind Kind;
        public Vector3 From;
        public Vector3 To;
        public Color GhostColor;

        public static TutorialStep Drag(Vector3 from, Vector3 to, Color ghost) =>
            new TutorialStep { Kind = TutorialKind.Drag, From = from, To = to, GhostColor = ghost };
        public static TutorialStep Tap(Vector3 at) =>
            new TutorialStep { Kind = TutorialKind.Tap, From = at, To = at };
    }

    /// <summary>
    /// Animated, (almost) text-free tutorial: dims the board and plays a looping ghost-hand demo of
    /// the gesture — dragging a translucent ghost block to its exit, or tapping an item button. A
    /// pulsing chevron at the bottom signals "tap to continue"; each tap advances, the last fires
    /// <c>onDone</c>. While <see cref="Blocking"/> the caller freezes the clock + ignores game input.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private const int OrderScrim = 500, OrderGhost = 503, OrderHand = 505, OrderChevron = 506;

        private Camera _cam;
        private List<TutorialStep> _steps;
        private int _index;
        private Action _onDone;

        private GameObject _root;
        private SpriteRenderer _ghost, _hand, _chevron;
        private float _cooldownUntil, _stepStart;

        public bool Active { get; private set; }
        // Only block while the overlay actually exists (belt-and-suspenders: if the root is ever gone,
        // input can never be permanently swallowed) plus a short post-dismiss cooldown.
        public bool Blocking => (Active && _root != null) || Time.unscaledTime < _cooldownUntil;

        /// <summary>Force the overlay off (called when a new level starts) so it can never leave a
        /// stale blocking state on a level that has no tutorial of its own.</summary>
        public void Stop()
        {
            Active = false;
            _cooldownUntil = 0f;
            _onDone = null;
            if (_root != null) { Destroy(_root); _root = null; }
        }

        public void Play(Camera cam, List<TutorialStep> steps, Action onDone)
        {
            if (steps == null || steps.Count == 0) { onDone?.Invoke(); return; }
            _cam = cam; _steps = steps; _onDone = onDone; _index = 0;
            Build();
            Active = true;
            ShowStep(0);
        }

        private void Build()
        {
            if (_root != null) Destroy(_root);
            _root = new GameObject("TutorialRoot");
            _root.transform.SetParent(transform, false);

            float half = _cam.orthographicSize;
            float aspect = _cam.aspect <= 0f ? 0.5625f : _cam.aspect;
            float halfW = half * aspect;
            Vector3 c = _cam.transform.position;

            var scrim = MakeSprite("Scrim", new Vector3(c.x, c.y, 0f), VisualAssets.Square(),
                new Color(0f, 0f, 0f, 0.5f), OrderScrim);
            scrim.transform.localScale = new Vector3(halfW * 4f, half * 4f, 1f);

            _ghost = MakeSprite("Ghost", Vector3.zero, VisualAssets.GlossyBlock(), Color.white, OrderGhost);
            _hand = MakeSprite("Hand", Vector3.zero, VisualAssets.ArrowCursor(), Color.white, OrderHand);

            // pulsing "tap to continue" chevron at the very bottom (a downward triangle — no text)
            _chevron = MakeSprite("Chevron", new Vector3(c.x, c.y - half * 0.82f, -0.2f),
                VisualAssets.Chevron(), new Color(1f, 1f, 1f, 0.9f), OrderChevron);
            var cb = _chevron.sprite.bounds.size;
            float cs = 0.9f / Mathf.Max(0.0001f, cb.x);
            _chevron.transform.localScale = new Vector3(cs, cs, 1f);
        }

        private void ShowStep(int i)
        {
            _stepStart = Time.unscaledTime;
            var s = _steps[i];
            bool drag = s.Kind == TutorialKind.Drag;
            _ghost.gameObject.SetActive(drag);
            if (drag)
            {
                _ghost.color = new Color(s.GhostColor.r, s.GhostColor.g, s.GhostColor.b, 0.72f);
                var gb = _ghost.sprite.bounds.size;
                float gs = 1.0f / Mathf.Max(0.0001f, gb.x);   // ~one cell
                _ghost.transform.localScale = new Vector3(gs, gs, 1f);
            }
            var hb = _hand.sprite.bounds.size;
            float hs = 1.5f / Mathf.Max(0.0001f, hb.y);
            _hand.transform.localScale = new Vector3(hs, hs, 1f);
        }

        private void Update()
        {
            if (!Active) return;
            float t = Time.unscaledTime - _stepStart;
            var s = _steps[_index];

            if (s.Kind == TutorialKind.Drag)
            {
                // loop: 0.35s press, 1.05s slide, 0.5s hold+fade, 0.3s gap
                const float press = 0.35f, slide = 1.05f, hold = 0.5f, gap = 0.3f;
                float period = press + slide + hold + gap;
                float lt = Mathf.Repeat(t, period);
                float p;             // 0..1 along the path
                float ghostA = 0.72f;
                if (lt < press) p = 0f;
                else if (lt < press + slide) p = Ease((lt - press) / slide);
                else if (lt < press + slide + hold) { p = 1f; ghostA = 0.72f * (1f - (lt - press - slide) / hold); }
                else { p = 0f; ghostA = 0f; }
                Vector3 pos = Vector3.Lerp(s.From, s.To, p);
                _ghost.transform.position = new Vector3(pos.x, pos.y, -0.05f);
                _ghost.color = new Color(s.GhostColor.r, s.GhostColor.g, s.GhostColor.b, ghostA);
                // hand fingertip rests on the block's lower-right, with a tiny press dip
                float dip = lt < press ? 0.12f * Mathf.Sin(lt / press * Mathf.PI) : 0f;
                _hand.transform.position = new Vector3(pos.x + 0.18f, pos.y - 0.12f - dip, -0.06f);
            }
            else // Tap: hand taps in place, pulsing
            {
                float lt = Mathf.Repeat(t, 1.0f);
                float dip = 0.16f * Mathf.Max(0f, Mathf.Sin(lt * Mathf.PI * 2f));
                _hand.transform.position = new Vector3(s.From.x + 0.18f, s.From.y - 0.12f - dip, -0.06f);
            }

            // chevron bob
            if (_chevron != null)
            {
                float by = 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
                var cp = _chevron.transform.position;
                _chevron.transform.position = new Vector3(cp.x, cp.y, cp.z);
                var col = _chevron.color; col.a = 0.6f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                _chevron.color = col;
                _chevron.transform.localScale = _chevron.transform.localScale; // (bob applied via color only to avoid drift)
            }

            if (Input.GetMouseButtonDown(0)) Advance();
        }

        private static float Ease(float x) => x * x * (3f - 2f * x); // smoothstep

        private void Advance()
        {
            _index++;
            if (_index >= _steps.Count) Finish();
            else ShowStep(_index);
        }

        private void Finish()
        {
            Active = false;
            _cooldownUntil = Time.unscaledTime + 0.25f;
            if (_root != null) { Destroy(_root); _root = null; }
            var cb = _onDone; _onDone = null;
            cb?.Invoke();
        }

        private SpriteRenderer MakeSprite(string name, Vector3 pos, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
