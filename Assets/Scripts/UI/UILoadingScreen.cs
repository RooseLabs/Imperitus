using UnityEngine;

namespace RooseLabs.UI
{
    public class UILoadingScreen : MonoBehaviour
    {
        public static UILoadingScreen Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Hide();
        }

        public static void Show()
        {
            Instance.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            Instance.gameObject.SetActive(false);
        }
    }
}
