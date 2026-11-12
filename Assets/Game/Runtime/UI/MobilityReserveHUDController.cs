using System.Globalization;
using GameJam.Levels;
using GameJam.Resources;
using TMPro;
using UnityEngine;

namespace GameJam.UI
{
    /// <summary>Displays the current bank and timer without owning either state.</summary>
    public sealed class MobilityReserveHUDController : MonoBehaviour
    {
        public RectTransform panel;
        public UnityEngine.UI.Image dashIcon;
        public TMP_Text counter, timer;
        public Color activeColor = new Color(.65f, .95f, .91f);
        public Color criticalColor = new Color(1f, .73f, .33f);
        public Color exhaustedColor = new Color(1f, .44f, .48f);
        public Color unavailableColor = new Color(.60f, .69f, .72f);

        public RunDashBank BoundBank => bank;
        public int DisplayedCharges { get; private set; } = -1;
        public int DisplayedStartingCount => bank != null ? bank.StartingDashCount : 0;
        public bool IsCritical => DisplayedCharges == 1;

        private RunDashBank bank;
        private long displayedCentiseconds = -1;

        private void OnEnable()
        {
            foreach (var other in Object.FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None))
                if (other != this && other.isActiveAndEnabled && other.gameObject.scene == gameObject.scene)
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                    return;
                }

            if (panel == null || dashIcon == null || counter == null || timer == null)
            {
                Debug.LogError("Assign the compact gameplay HUD references.", this);
                enabled = false;
                return;
            }

            BindCurrentBank();
            RefreshPresentation();
            displayedCentiseconds = -1;
            RefreshTimer();
        }

        private void LateUpdate()
        {
            BindCurrentBank();
            RefreshTimer();
        }

        private void BindCurrentBank()
        {
            var current = RunDashBank.Instance;
            if (current != null && !current.isActiveAndEnabled) current = null;
            if (ReferenceEquals(current, bank)) return;
            Unsubscribe();
            bank = current;
            if (bank != null)
            {
                bank.ChargesChanged += OnChargesChanged;
                bank.NewRunStarted += RefreshPresentation;
            }
            RefreshPresentation();
        }

        private void OnChargesChanged(int remaining)
        {
            if (remaining != DisplayedCharges) RefreshPresentation();
        }

        [ContextMenu("Refresh Read-Only Presentation")]
        public void RefreshPresentation()
        {
            DisplayedCharges = bank != null ? bank.RemainingCharges : -1;
            counter.text = DisplayedCharges < 0 ? "--" : DisplayedCharges.ToString(CultureInfo.InvariantCulture);
            Color ink = DisplayedCharges == 1 ? criticalColor : DisplayedCharges == 0 ? exhaustedColor :
                DisplayedCharges < 0 ? unavailableColor : activeColor;
            dashIcon.color = counter.color = ink;
        }

        private void RefreshTimer()
        {
            long milliseconds = RunTimer.Instance != null ? RunTimer.Instance.ElapsedMilliseconds : 0;
            long centiseconds = milliseconds / 10;
            if (centiseconds == displayedCentiseconds) return;
            displayedCentiseconds = centiseconds;
            timer.text = "RUN TIME  " + RunBestTime.Format(milliseconds);
        }

        private void Unsubscribe()
        {
            if (ReferenceEquals(bank, null)) return;
            bank.ChargesChanged -= OnChargesChanged;
            bank.NewRunStarted -= RefreshPresentation;
        }

        private void OnDisable()
        {
            Unsubscribe();
            bank = null;
        }
    }
}
