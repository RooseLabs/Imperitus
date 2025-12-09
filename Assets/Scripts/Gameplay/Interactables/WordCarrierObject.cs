using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class WordCarrierObject : Item
    {
        [SerializeField, Tooltip("Text component to display the carried word.")]
        private TMP_Text wordText;

        private readonly SyncVar<string> m_word = new();
        private readonly SyncVar<int> m_visibleToClientId = new(-1);

        public override void OnStartClient()
        {
            // Subscribe to word changes to update visual
            m_word.OnChange += OnWordChanged;
            m_visibleToClientId.OnChange += OnVisibilityChanged;
            UpdateWordDisplay(m_word.Value);
            UpdateVisibility();
        }

        private void OnDestroy()
        {
            m_word.OnChange -= OnWordChanged;
            m_visibleToClientId.OnChange -= OnVisibilityChanged;
        }

        public void SetWord(string word)
        {
            m_word.Value = word;
            UpdateWordDisplay(word);
        }

        public string GetWord()
        {
            return m_word.Value;
        }

        public void SetVisibleToClientId(int clientId)
        {
            m_visibleToClientId.Value = clientId;
        }

        private void OnWordChanged(string prev, string next, bool asServer)
        {
            UpdateWordDisplay(next);
        }

        private void OnVisibilityChanged(int prev, int next, bool asServer)
        {
            UpdateVisibility();
        }

        private void UpdateWordDisplay(string word)
        {
            if (wordText != null)
            {
                wordText.text = word;
            }
        }

        private void UpdateVisibility()
        {
            // If visibleToClientId is -1, the object is visible to everyone
            // Otherwise, only visible to the specified client
            bool isVisible = m_visibleToClientId.Value == -1 ||
                           (LocalConnection != null && LocalConnection.ClientId == m_visibleToClientId.Value);

            wordText.gameObject.SetActive(isVisible);
        }
    }
}
