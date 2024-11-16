using DRMinaUnityPackage;
using UnityEngine;

public class DRMAuthenticationStarter : MonoBehaviour
{

    public string gameIDString;
    private void Start()
    {
        DRMEnvironment.GameIDString = gameIDString;
        DRMAuthenticator.OnComplete += DRMAuthenticatorOnOnComplete;
        DRMAuthenticator.Start();
    }

    private void DRMAuthenticatorOnOnComplete(object sender, DRMStatusCode e)
    {
        Debug.Log(e);
        // Todo: Handle the DRM status code
    }
}

