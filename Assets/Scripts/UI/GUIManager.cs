using RooseLabs.Network;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class GUIManager : MonoBehaviour
    {
        public static GUIManager Instance { get; private set; }

        [SerializeField] private TMP_Text joinCodeText;
        [SerializeField] private TMP_Text runeCounterText;

        private NetworkConnector m_networkConnector;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            m_networkConnector = NetworkConnector.Instance;
            if ((bool)m_networkConnector && m_networkConnector.CurrentSessionJoinCode != null)
            {
                joinCodeText.text = m_networkConnector.CurrentSessionJoinCode;
                joinCodeText.gameObject.SetActive(true);
            }
        }

        public void UpdateRuneCounter(int count)
        {
            runeCounterText.text = $"{count} Runes Discovered";
        }
    }
}
