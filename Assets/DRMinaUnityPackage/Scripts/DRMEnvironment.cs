using UnityEngine;

namespace DRMinaUnityPackage
{
    public class DRMEnvironment : MonoBehaviour
    {
        public static string GAME_TOKEN_ADDRESS = "";
        public static string DRM_CONTRACT_ADDRESS = "";
        public static string PROVER_ENDPOINT = "http://127.0.0.1:4444/";
        public static int PROVER_MAX_RETRIES = 5;
        public static int PROVER_RETRY_INTERVAL_SECONDS = 20;
        public static int AUTH_TIMEOUT_MINUTES = 15;
    }
}
