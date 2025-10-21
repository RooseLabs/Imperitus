using RooseLabs.Network;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI.UIElements
{
    public class SessionJoinCode : MonoBehaviour
    {
        [SerializeField] private TMP_Text joinCodeText;

        private void OnEnable()
        {
            if (NetworkConnector.Instance.CurrentSessionJoinCode != null)
            {
                joinCodeText.gameObject.SetActive(true);
                joinCodeText.text = NetworkConnector.Instance.CurrentSessionJoinCode;
            }
            else
            {
                joinCodeText.gameObject.SetActive(false);
            }
        }
    }
}
