using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.UI;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class HeistTimer : NetworkBehaviour
    {
        private readonly SyncTimer m_syncTimer = new();
        private bool m_finishedTriggered;

        private void OnEnable()
        {
            m_syncTimer.OnChange += OnTimerChanged;
        }

        private void OnDisable()
        {
            m_syncTimer.OnChange -= OnTimerChanged;
        }

        public void ToggleTimerVisibility(bool visible)
        {
            GUIManager.Instance.SetTimerActive(visible);
        }

        [Server]
        public void StartTimer(float time)
        {
            m_syncTimer.StartTimer(time, sendRemainingOnStop: true);
            m_finishedTriggered = false;
        }

        [Server]
        public void PauseTimer()
        {
            m_syncTimer.PauseTimer(sendRemaining: true);
        }

        [Server]
        public void ResumeTimer()
        {
            m_syncTimer.UnpauseTimer();
        }

        [Server]
        public void StopTimer()
        {
            m_syncTimer.StopTimer(sendRemaining: true);
        }

        private void Update()
        {
            m_syncTimer.Update(Time.deltaTime);
            float remainingTime = Mathf.Max(Mathf.CeilToInt(m_syncTimer.Remaining), 0);
            GUIManager.Instance.UpdateTimer(remainingTime);
        }

        private void OnTimerChanged(SyncTimerOperation op, float prev, float next, bool asServer)
        {
            if (op != SyncTimerOperation.Finished || m_finishedTriggered) return;
            HandleTimerFinished();
        }

        private void HandleTimerFinished()
        {
            if (!IsServerInitialized) return;
            this.LogInfo("Heist timer has reached zero. Ending heist as failed.");
            m_finishedTriggered = true;
            GameManager.Instance.EndHeist(false);
        }
    }
}
