namespace RooseLabs.Utils
{
    public static class LoggingExtensions
    {
        public static void LogInfo(this UnityEngine.MonoBehaviour self, string message)
        {
            var logger = Core.Logger.GetLogger(self.GetType().Name);
            logger.Info($"{GetContext(self)} {message}");
        }

        public static void LogWarning(this UnityEngine.MonoBehaviour self, string message)
        {
            var logger = Core.Logger.GetLogger(self.GetType().Name);
            logger.Warning($"{GetContext(self)} {message}");
        }

        public static void LogError(this UnityEngine.MonoBehaviour self, string message)
        {
            var logger = Core.Logger.GetLogger(self.GetType().Name);
            logger.Error($"{GetContext(self)} {message}");
        }

        private static string GetContext(UnityEngine.MonoBehaviour self)
        {
            if (self is FishNet.Object.NetworkBehaviour nb)
                return $"[{(nb.IsServerInitialized ? "Server" : "Client")}][{(nb.IsController ? "Controller" : "Observer")}]";
            return string.Empty;
        }
    }
}
