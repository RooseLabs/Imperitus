using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace RooseLabs.SceneManagement
{
    public class Bootstrapper : MonoBehaviour
    {
        private void Start()
        {
            _ = Boot();
        }

        private async Task Boot()
        {
            await UnitySceneManager.LoadSceneAsync("PersistentManagers", LoadSceneMode.Single);
        }
    }
}
