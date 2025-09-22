using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RooseLabs.SceneManagement
{
    public class Bootstrapper : MonoBehaviour
    {
        private void Start()
        {
            _ = LoadMainMenuScene();
        }

        private async Task LoadMainMenuScene()
        {
            await SceneManager.LoadSceneAsync("PersistentManagers", LoadSceneMode.Additive);
            // PersistentManagers currently has a single GameObject with a DontDestroyOnLoad script, so it's safe to
            // unload it right away since the GameObjects within it will persist anyway.
            // This will need to change to LoadSceneMode.Additive if for some reason we change that setup.
            await SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }
    }
}
