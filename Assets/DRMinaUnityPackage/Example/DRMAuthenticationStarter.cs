using DRMinaUnityPackage;
using UnityEngine;

public class DRMAuthenticationStarter : MonoBehaviour
{

    public string gameTokenAddress;
    public string drmContractAddress;
    public string proverEndpoint;
    public int proverMaxRetries;
    public int proverRetryIntervalSeconds;
    public int authTimeoutMinutes;
    
    private void Start()
    {
        DRMEnvironment.GAME_TOKEN_ADDRESS = gameTokenAddress;
        DRMEnvironment.DRM_CONTRACT_ADDRESS = drmContractAddress;
        DRMEnvironment.PROVER_ENDPOINT = proverEndpoint;
        DRMEnvironment.PROVER_MAX_RETRIES = proverMaxRetries;
        DRMEnvironment.PROVER_RETRY_INTERVAL_SECONDS = proverRetryIntervalSeconds;
        DRMEnvironment.AUTH_TIMEOUT_MINUTES = authTimeoutMinutes;
        // DRMAuthenticator.OnComplete += DRMAuthenticatorOnOnComplete;
        // DRMAuthenticator.Start();
    }

    private void DRMAuthenticatorOnOnComplete(object sender, DRMStatusCode e)
    {
        Debug.Log(e);
        // Todo: Handle the DRM status code
    }
}

