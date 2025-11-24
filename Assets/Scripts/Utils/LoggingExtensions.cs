namespace RooseLabs.Utils
{
    public static class LoggingExtensions
    {
        [UnityEngine.HideInCallstack]
        public static void LogInfo(this UnityEngine.MonoBehaviour self, string message)
        {
            var logger = Core.Logger.GetLogger(self.GetType().Name);
            logger.Info($"{GetContext(self)} {message}");
        }

        [UnityEngine.HideInCallstack]
        public static void LogWarning(this UnityEngine.MonoBehaviour self, string message)
        {
            var logger = Core.Logger.GetLogger(self.GetType().Name);
            logger.Warning($"{GetContext(self)} {message}");
        }

        [UnityEngine.HideInCallstack]
        public static void LogError(this UnityEngine.MonoBehaviour self, string message)
        {
            var logger = Core.Logger.GetLogger(self.GetType().Name);
            logger.Error($"{GetContext(self)} {message}");
        }

        [UnityEngine.HideInCallstack]
        private static string GetContext(UnityEngine.MonoBehaviour self)
        {
            if (self is FishNet.Object.NetworkBehaviour nb)
            {
                var player = RooseLabs.Network.PlayerHandler.GetPlayer(nb.Owner);
                string ownerId = player && !string.IsNullOrEmpty(player.PlayerName)
                    ? $"{player.PlayerName} (ID: {nb.Owner.ClientId})"
                    : nb.Owner.ClientId.ToString();
                return $"[{(nb.IsServerInitialized ? "Server" : "Client")}][{(nb.IsController ? "Controller" : "Observer")}][Owner: {ownerId}]";
            }
            return string.Empty;
        }
    }
}
