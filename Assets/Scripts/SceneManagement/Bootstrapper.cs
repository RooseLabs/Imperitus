using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            await SceneManager.LoadSceneAsync("PersistentManagers", LoadSceneMode.Single);
        }
    }
}
