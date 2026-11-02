using System.Collections.Generic;
using GameJam.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>Read-only projection of the authoritative run bank. Never owns resource state.</summary>
    public sealed class MobilityReserveHUDController : MonoBehaviour
    {
        [Header("Replaceable presentation")]
        public RectTransform panel;
        public RectTransform segmentArea;
        public UnityEngine.UI.Image segmentTemplate;
        public GridLayoutGroup segmentLayout;
        public TMP_Text counter, warning;
        public Sprite activeSegment, inactiveSegment, criticalSegment;
        public Color activeColor = new Color(.21f, .72f, .68f);
        public Color inactiveColor = new Color(.25f, .32f, .36f);
        public Color criticalColor = new Color(1f, .65f, .27f);
        public Color counterColor = new Color(.9f, .96f, .96f);
        public Color unavailableColor = new Color(.62f, .69f, .72f);
        public string criticalWarning = "NEXT DASH WILL TERMINATE RUN";
        public string exhaustedMessage = "ALLOCATION EXHAUSTED";
        public string environmentalFailureMessage = "SUBJECT INTEGRITY LOST";
        public string unavailableMessage = "MONITOR OFFLINE";
        public string unavailableCounter = "--";
        [Min(1)] public int maximumColumns = 12;
        [Min(0)] public float segmentGap = 4f;

        public RunDashBank BoundBank => bank;
        public int DisplayedCharges { get; private set; } = -1;
        public int DisplayedStartingCount { get; private set; }
        public bool IsCritical => bank != null && DisplayedCharges == 1;
        public IReadOnlyList<UnityEngine.UI.Image> Segments => segments;
        private RunDashBank bank;
        private readonly List<UnityEngine.UI.Image> segments = new List<UnityEngine.UI.Image>();
        private bool rendered, displayedFailure;

        private void OnEnable()
        {
            foreach (var other in Object.FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None))
                if (other != this && other.isActiveAndEnabled && other.gameObject.scene == gameObject.scene)
                {
                    gameObject.SetActive(false); Destroy(gameObject); return;
                }
            if (panel == null || segmentArea == null || segmentTemplate == null || segmentLayout == null || counter == null || warning == null)
            {
                Debug.LogError("Assign the Mobility Reserve HUD presentation references.", this);
                enabled = false; return;
            }
            BindCurrentBank();
        }
        private void Update() => BindCurrentBank(); // Identity/availability check only; no per-frame rendering.

        private void BindCurrentBank()
        {
            var current = RunDashBank.Instance;
            if (current != null && !current.isActiveAndEnabled) current = null;
            if (ReferenceEquals(current, bank) && rendered)
            {
                // Environmental failure changes state without a ChargesChanged notification.
                // Keep the HUD read-only and preserve its existing two event subscriptions.
                if (bank != null && bank.IsGameOver != displayedFailure) Render(false);
                return;
            }
            Unsubscribe();
            bank = current;
            if (bank != null)
            {
                bank.ChargesChanged += OnChargesChanged;
                bank.NewRunStarted += OnNewRun;
            }
            RefreshPresentation();
        }
        private void OnChargesChanged(int remaining) => Render(false);
        private void OnNewRun() => Render(false);

        [ContextMenu("Refresh Read-Only Presentation")]
        public void RefreshPresentation() => Render(true);

        private void Render(bool force)
        {
            int remaining = bank != null ? bank.RemainingCharges : -1;
            int allocation = bank != null ? bank.StartingDashCount : 0;
            bool failed = bank != null && bank.IsGameOver;
            if (!force && rendered && remaining == DisplayedCharges && allocation == DisplayedStartingCount && failed == displayedFailure) return;
            DisplayedCharges = remaining; DisplayedStartingCount = allocation; displayedFailure = failed; rendered = true;
            ResizeSegmentPool(allocation);
            if (force) RefreshSegmentLayout();
            counter.text = bank != null ? remaining.ToString("D2") : unavailableCounter;
            counter.color = remaining == 1 ? criticalColor : (bank != null ? counterColor : unavailableColor);
            warning.text = bank == null ? unavailableMessage :
                (failed && bank.FailureCause == RunFailureCause.Environmental ? environmentalFailureMessage :
                (remaining == 1 ? criticalWarning : (remaining == 0 ? exhaustedMessage : "")));
            warning.color = remaining == 1 || remaining == 0 ? criticalColor : unavailableColor;
            for (int i = 0; i < segments.Count; i++)
            {
                bool lit = i < remaining;
                bool critical = lit && remaining == 1;
                segments[i].color = !lit ? inactiveColor : (critical ? criticalColor : activeColor);
                var sprite = !lit ? inactiveSegment : (critical ? criticalSegment : activeSegment);
                segments[i].sprite = sprite != null ? sprite : segmentTemplate.sprite;
                segments[i].raycastTarget = false;
            }
        }
        private void ResizeSegmentPool(int count)
        {
            if (segments.Count == count) return;
            // Rebuild only when the starting allocation changes, not when charges are spent.
            foreach (var image in segments) if (image != null) { image.gameObject.SetActive(false); Destroy(image.gameObject); }
            segments.Clear();
            for (int i = 0; i < count; i++)
            {
                var image = Instantiate(segmentTemplate, segmentArea);
                image.name = "ChargeSegment" + (i + 1).ToString("D2");
                image.raycastTarget = false; image.gameObject.SetActive(true); segments.Add(image);
            }
            RefreshSegmentLayout();
        }
        private void OnRectTransformDimensionsChange() => RefreshSegmentLayout();
        private void RefreshSegmentLayout()
        {
            if (segmentArea == null || segmentLayout == null || segments.Count == 0) return;
            int columns = Mathf.Min(segments.Count, Mathf.Max(1, maximumColumns));
            int rows = Mathf.CeilToInt((float)segments.Count / columns);
            float width = segmentArea.rect.width, height = segmentArea.rect.height;
            if (width <= 0 || height <= 0) return;
            // Reduce spacing as needed so other positive allocations stay inside this compact panel.
            float gapX = Mathf.Min(Mathf.Max(0, segmentGap), width / (3f * Mathf.Max(1, columns - 1)));
            float gapY = Mathf.Min(Mathf.Max(0, segmentGap), height / (3f * Mathf.Max(1, rows - 1)));
            segmentLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            segmentLayout.constraintCount = columns;
            segmentLayout.spacing = new Vector2(gapX, gapY);
            segmentLayout.cellSize = new Vector2((width - gapX * (columns - 1)) / columns, (height - gapY * (rows - 1)) / rows);
        }
        private void Unsubscribe()
        {
            if (bank == null) return;
            bank.ChargesChanged -= OnChargesChanged;
            bank.NewRunStarted -= OnNewRun;
        }
        private void OnDisable()
        {
            Unsubscribe(); bank = null; rendered = false;
        }
    }
}
