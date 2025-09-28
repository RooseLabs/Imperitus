using RooseLabs.Network;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class GUIManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text joinCodeText;

        private NetworkConnector m_networkConnector;

        private void Start()
        {
            m_networkConnector = NetworkConnector.Instance;
            if ((bool)m_networkConnector && m_networkConnector.CurrentSessionJoinCode != null)
            {
                joinCodeText.text = m_networkConnector.CurrentSessionJoinCode;
                joinCodeText.gameObject.SetActive(true);
            }
        }
    }
}
