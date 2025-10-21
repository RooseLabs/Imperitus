using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class GUIManager : MonoBehaviour
    {
        public static GUIManager Instance { get; private set; }

        [SerializeField] private GameObject guiRootCanvas;
        [SerializeField] private TMP_Text runeCounterText;

        private void Awake()
        {
            Instance = this;
        }

        public void SetGUIActive(bool isActive)
        {
            guiRootCanvas.SetActive(isActive);
        }

        public void UpdateRuneCounter(int count)
        {
            runeCounterText.text = $"{count} Runes Discovered";
        }
    }
}
