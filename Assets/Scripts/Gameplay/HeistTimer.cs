using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Gameplay;
using RooseLabs.UI;
using UnityEngine;

namespace RooseLabs
{
    public class HeistTimer : NetworkBehaviour
    {
        [Header("Timer Settings")]
        public float defaultTime = 300f;

        private readonly SyncTimer syncTimer = new SyncTimer();

        private int lastDisplayedSecond = -1;

        private bool finishedTriggered = false;

        private void Awake()
        {
            syncTimer.OnChange += OnTimerChanged;
        }

        private void OnDestroy()
        {
            syncTimer.OnChange -= OnTimerChanged;
        }

        public void ShowTimer()
        {
            GUIManager.Instance.SetTimerActive(true);
        }

        /// <summary>
        /// Start the timer with a specified duration. Server only.
        /// </summary>
        [Server]
        public void StartTimer(float time)
        {
            syncTimer.StartTimer(time, true);

            finishedTriggered = false;
            lastDisplayedSecond = -1;

            //Debug.Log($"Timer started for {time} seconds.");
        }

        /// <summary>
        /// Pause the timer. Server only.
        /// </summary>
        [Server]
        public void PauseTimer(bool updateClients = false)
        {
            syncTimer.PauseTimer(updateClients);
        }

        /// <summary>
        /// Resume the timer. Server only.
        /// </summary>
        [Server]
        public void ResumeTimer()
        {
            syncTimer.UnpauseTimer();
        }

        private void Update()
        {
            // Update SyncTimer every frame
            syncTimer.Update();

            // Update UI per second
            UpdateTimerUI();
        }

        /// <summary>
        /// Updates TMP_Text UI per whole second, clamped to zero
        /// </summary>
        private void UpdateTimerUI()
        {
            int currentSecond = Mathf.CeilToInt(syncTimer.Remaining);

            if (currentSecond != lastDisplayedSecond)
            {
                lastDisplayedSecond = currentSecond;
                GUIManager.Instance.UpdateTimer(Mathf.Max(currentSecond, 0));
            }
        }

        /// <summary>
        /// Callback for SyncTimer changes
        /// </summary>
        private void OnTimerChanged(SyncTimerOperation op, float prev, float next, bool asServer)
        {
            if (op == SyncTimerOperation.Finished && !finishedTriggered)
            {
                finishedTriggered = true;
                HandleTimerFinished();
            }
        }

        /// <summary>
        /// Handles logic when timer reaches zero
        /// </summary>
        private void HandleTimerFinished()
        {
            Debug.Log("Timer ran out!");
            GameManager.Instance.EndHeist();
        }
    }
}
