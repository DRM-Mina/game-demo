using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Drm_Mina;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using StringContent = System.Net.Http.StringContent;

public static class DRMAuthenticator
{
    private static readonly HttpClient Client = new();
    private static int _determinedSessionId = -1;

    public static event EventHandler<DRMStatusCode> OnComplete;
    
    public static void Start()
    {
        OnComplete?.Invoke(null, Run().Result);
    }

    static async Task<DRMStatusCode> Run()
    {
        IdentifierData data = new IdentifierData();

        try
        {
            await data.GetData();
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return DRMStatusCode.DeviceNotCompatible;
        }
        

        string hash = "";
        try
        {
            hash = await GetHash(data);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return DRMStatusCode.DeviceNotCompatible;
        }

        int blockHeight = 0;
        try
        {
            // blockHeight =
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return DRMStatusCode.NodeError;
        }

        int session = -1;
        try
        {
            session = await GetCurrentSession(hash);
            switch (session)
            {
                case -1:
                    return DRMStatusCode.ProverNotReady;
                case < -1:
                    throw new Exception();
            }
        }
        catch (Exception e)
        {
            return DRMStatusCode.GameNotBoughtOrNoConnection;
        }
        
        _determinedSessionId = Random.Range(2, int.MaxValue);
        var newRandomSession = new SessionData
        {
            rawIdentifiers = data,
            currentSession = session.ToString(),
            newSession = _determinedSessionId.ToString(),
            gameId = Constants.GameIDString
        };
        
        var dataS = JsonConvert.SerializeObject(newRandomSession);
        StringContent content = new StringContent(dataS, Encoding.UTF8, "application/json");
        
        
        int maxRetries = 5;
        int retryDelayMs = 1000;

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                HttpResponseMessage response = await Client.PostAsync(Constants.ProverURL, content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned status code.");
                }
                break;
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    if (retry == maxRetries - 1)
                    {
                        return DRMStatusCode.ProverNotReady;
                    }
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    return DRMStatusCode.ProverError;
                }
            }
        }

        var startTime = Time.time;
        while(Time.time - startTime < 300f)
        {
            int id;
            try
            {
                id = await GetCurrentSession(hash);
                Debug.Log("NEW ID:" + id);
                if (_determinedSessionId == id)
                {
                    //success
                    return DRMStatusCode.Success;
                }
                if(id <= 0)
                {
                    //game is not bought or no connection
                    return DRMStatusCode.GameNotBoughtOrNoConnection;
                }
            }
            catch (Exception e)
            {
            }
            await Task.Delay(10000);
        }
        //timeout
        return DRMStatusCode.Timeout;
    }

    public static async Task<int> GetCurrentSession(string hash)
    {
        StringContent content = new StringContent(hash, Encoding.UTF8, "application/json");
        
        int maxRetries = 5;
        int retryDelayMs = 1000;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                HttpResponseMessage response = await Client.PostAsync(Constants.ProverURL, content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned status code.");
                }
                var responseString = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseString);
                return responseJson["session"].Value<int>();
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    if (retry == maxRetries - 1)
                    {
                        return -1;
                    }
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    return -2;
                }
            }
        }

        return -3;
    }

    public static async Task<string> GetHash(IdentifierData data)
    {
        var device = new Device(data);
        var hash = await device.Hash();
        return hash;
    }

}

public enum DRMStatusCode
{
    Success,
    ProverNotReady,
    ProverError,
    NodeError,
    DeviceNotCompatible,
    Timeout,
    GameNotBoughtOrNoConnection
}