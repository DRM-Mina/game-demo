using DRMinaUnityPackage;
using UnityEngine;

public class DRMAuthenticationStarter : MonoBehaviour
{

    public string gameTokenAddress;
    public string drmContractAddress;
    
    private void Start()
    {
        DRMEnvironment.GAME_TOKEN_ADDRESS = gameTokenAddress;
        DRMEnvironment.DRM_CONTRACT_ADDRESS = drmContractAddress;
        // DRMAuthenticator.OnComplete += DRMAuthenticatorOnOnComplete;
        // DRMAuthenticator.Start();
    }

    private void DRMAuthenticatorOnOnComplete(object sender, DRMStatusCode e)
    {
        Debug.Log(e);
        // Todo: Handle the DRM status code
    }
}

