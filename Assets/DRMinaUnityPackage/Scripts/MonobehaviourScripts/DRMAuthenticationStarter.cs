using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DRMAuthenticationStarter : MonoBehaviour
{
    private void Awake()
    {
        DRMAuthenticator.Start();
    }
}
