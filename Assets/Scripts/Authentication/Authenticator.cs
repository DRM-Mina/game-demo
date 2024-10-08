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
using System.Collections.Generic;


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
        
        await GetEvents();

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

    public async Task<int> GetBlockHeight()
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://devnet.api.minaexplorer.com/blocks?limit=1"),
            Headers =
            {

            }
        };

        var response = await Client.SendAsync(request);
        
        var responseString = await response.Content.ReadAsStringAsync();
        JObject obj = JsonConvert.DeserializeObject<JObject>(responseString);

        var blockHeight = obj["blocks"][0]["blockHeight"];

        return (int)blockHeight;
    }

    public async Task<int> GetEvents()
    {
        var query = @"
{
  events(
    input: {address: ""{input1}"", from: {input2}}
  ) {
    eventData {
      data
    }
  }
}
";

        string queryS = query.Replace("{input1}", "B62qn6sovFQ3XUdr98xv4Ex63sZncBSQDSZ7EBKpUGGdiiVCR8oJnpa").Replace("{input2}", "3333");
        Debug.Log(queryS);

        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        StringContent content = new StringContent(contentS, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("https://api.minascan.io/archive/devnet/v1/graphql", content);

        var responseString = await response.Content.ReadAsStringAsync();
        Debug.Log(responseString);
        
        var obj = JsonConvert.DeserializeObject<Root>(responseString);
        var events = obj.Data.Events;

        foreach (var e in events)
        {
            var devicehash = e.EventData[0].Data[0];
            var prevSession = e.EventData[0].Data[1];
            var newSession = e.EventData[0].Data[2];
            
            Debug.Log(devicehash);
            Debug.Log(prevSession);
            Debug.Log(newSession);
        }

        return 0;
    }
    public class EventDataItem
    {
        public List<string> Data { get; set; }
    }

    public class Event
    {
        public List<EventDataItem> EventData { get; set; }
    }

    public class EventsResponse
    {
        public List<Event> Events { get; set; }
    }

    public class Root
    {
        public EventsResponse Data { get; set; }
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

        var innerObj = obj["blocks"][0]["blockHeight"];
        if (!innerObj.HasValues || (int)innerObj["value"] < 1)
        {
            return -1;
        }
        return (int)innerObj["value"];;
        
        return 1;
    }

    public async Task<int> GetTimeoutInterval(string hash)
    {
        string query = @"
        query GetTimeoutInterval {
            runtime {
                GameToken {
                    timeoutInterval(key: {value: ""{input}""}) {
                        value
                    }
                }
            }
        }";

        string queryS = query.Replace("{input}", Constants.GameIDString);
        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        StringContent content = new StringContent(contentS, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync(Constants.SessionURL, content);

        var responseString = await response.Content.ReadAsStringAsync();
        JObject obj = JsonConvert.DeserializeObject<JObject>(responseString);

        var innerObj = obj["data"]["runtime"]["GameToken"]["timeoutInterval"];
        if (!innerObj.HasValues || (int)innerObj["value"] < 120)
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