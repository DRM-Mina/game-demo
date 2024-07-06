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

public class Authenticator : MonoBehaviour
{
    public TextBar textBar;
    private static readonly HttpClient Client = new();
    private int _determinedSessionId = -1;
    private int animationDelay = 400;
    [HideInInspector] public bool isDead = false;

    private void Start()
    {
        Run();
    }

    public async Task Run()
    {
        await Task.Delay(500 + animationDelay);

        IdentifierData data = new IdentifierData();

        try
        {
            textBar.UpdateText("Gathering identifier data...");
            await data.GetData();
            textBar.UpdateText("Identifier data collected.");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            textBar.Terminate();
            return;
        }

        await Task.Delay(500 + animationDelay);
        textBar.UpdateText("CPUID: " + data.cpuId);
        await Task.Delay(100 + animationDelay);
        textBar.UpdateText("Serial: " + data.systemSerial);
        await Task.Delay(100 + animationDelay);
        textBar.UpdateText("UUID: " + data.systemUUID);
        await Task.Delay(100 + animationDelay);
        textBar.UpdateText("Baseboard: " + data.baseboardSerial);
        await Task.Delay(100 + animationDelay);
        textBar.UpdateText("MAC1: " + data.macAddress[0]);
        await Task.Delay(100 + animationDelay);
        textBar.UpdateText("MAC2: " + data.macAddress[1]);
        await Task.Delay(100 + animationDelay);

        string hash = "";
        try
        {
            textBar.UpdateText("Calculating hash...");
            hash = await GetHash(data);
            Debug.Log(hash);
            await Task.Delay(100 + animationDelay);
            textBar.UpdateText("Hash calculated.");
            await Task.Delay(200 + animationDelay);
            textBar.UpdateText("Hash: " + hash);
        }
        catch (Exception e)
        {
            textBar.Terminate();
            return;
        }

        int session = -1;
        try
        {
            await Task.Delay(500 + animationDelay);
            textBar.UpdateText("Getting current session...");
            session = await GetCurrentSession(hash);
            if (session <= 0)
            {
                Debug.Log("Game is not bought.");
                throw new Exception();
            }

            textBar.UpdateText("Current session here.");
            await Task.Delay(200 + animationDelay);
            textBar.UpdateText("Session ID: " + session);
        }
        catch (Exception e)
        {
            textBar.Terminate();
            return;
        }
        
        await Task.Delay(200 + animationDelay);
        _determinedSessionId = Random.Range(2, int.MaxValue);
        textBar.UpdateText("Sending New Session with ID: " + _determinedSessionId);
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
                    throw new Exception("Server returned  status code.");
                }
                textBar.UpdateText("New session sent.");
                break;
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    textBar.UpdateText("Prover is not ready, steady lads...");
                    if (retry == maxRetries - 1)
                    {
                        textBar.Terminate();
                        return;
                    }
                    await Task.Delay(retryDelayMs + animationDelay);
                    textBar.UpdateText("Sending New Session with ID: " + _determinedSessionId);
                }
                else
                {
                    textBar.Terminate("Your device may not be compatible with our prover, or your internet connection is down. Please try again later.");
                    return;
                }
            }
        }

        
        await Task.Delay(200 + animationDelay);
        textBar.StartTimer(300);
        var startTime = Time.time;
        while(Time.time - startTime < 300f)
        {
            textBar.UpdateText("Getting Current Session...");
            int id;
            try
            {
                id = await GetCurrentSession(hash);
                Debug.Log("NEW ID:" + id);
                if (_determinedSessionId == id)
                {
                    textBar.Success();
                    textBar.EndTimer();
                    return;
                }
                if(id <= 0)
                {
                    textBar.Terminate();
                    return;
                }
            }
            catch (Exception e)
            {
            }
            await Task.Delay(animationDelay);
            textBar.UpdateText("ID is not same. Retrying...");
            await Task.Delay(10000);
        }
        textBar.UpdateText("FAIL");
        textBar.EndTimer();
    }

    public async Task<int> GetCurrentSession(string hash)
    {
        string query = @"
        query GetCurrentSession {
          runtime {
            DRM {
              sessions(
                key: {gameId: {value: ""{input1}""}, identifierHash: ""{input2}""}
              ) {
                value
              }
            }
          }
        }";

        string queryS = query.Replace("{input1}", Constants.GameIDString).Replace("{input2}", hash);
        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        StringContent content = new StringContent(contentS, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync(Constants.SessionURL, content);

        var responseString = await response.Content.ReadAsStringAsync();
        JObject obj = JsonConvert.DeserializeObject<JObject>(responseString);

        var innerObj = obj["data"]["runtime"]["DRM"]["sessions"];
        if (!innerObj.HasValues || (int)innerObj["value"] < 1)
        {
            return -1;
        }
        return (int)innerObj["value"];;
    }

    public async Task<string> GetHash(IdentifierData data)
    {
        var device = new Device(data);
        var hash = await device.Hash();
        return hash;
    }

}