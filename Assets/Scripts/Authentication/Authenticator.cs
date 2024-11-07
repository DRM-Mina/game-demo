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
    private int animationDelay = 0;
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

        textBar.StartTimer(2400);
        await Task.Delay(240000); // 4 minutes
        textBar.StartTimer(6000);
        var startTime = Time.time;
        while(Time.time - startTime < 600000) // 10 minutes
        {
            textBar.UpdateText("Getting Current Session...");
            try
            {
                var isUpdated = await ControlEventsForSession(
                    hash, session, _determinedSessionId);
                if (isUpdated)
                {
                    textBar.Success();
                    textBar.EndTimer();
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            await Task.Delay(animationDelay);
            textBar.UpdateText("ID is not same. Retrying...");
            await Task.Delay(60000); // 1 minute
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

    public async Task<bool> ControlEventsForSession(string hash, int prev, int current)
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
        var fromBlock = (await GetBlockHeight() - 1).ToString();

        string queryS = query.Replace("{input1}", Constants.GameIDString).Replace("{input2}", fromBlock);
        Debug.Log(queryS);

        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        StringContent content = new StringContent(contentS, Encoding.UTF8, "application/json");

        int maxRetries = 4;
        int retryDelayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            Debug.Log((i+1) + "th try");
            var response = await Client.PostAsync("https://api.minascan.io/archive/devnet/v1/graphql", content);

            var responseString = await response.Content.ReadAsStringAsync();
            Debug.Log(responseString);
        
            var root = JsonConvert.DeserializeObject<RootObject>(responseString);
            
            foreach (var eventItem in root.data.events)
            {
                foreach (var eventDataItem in eventItem.eventData)
                {
                    var devicehash = eventDataItem.data[0];
                    var prevSession = eventDataItem.data[1];
                    var newSession = eventDataItem.data[2];
                
                    Debug.Log(devicehash + " " +  prevSession + " " + newSession);
                
                    if (devicehash == hash && prevSession == prev.ToString() && newSession == current.ToString())
                    {
                        return true;
                    }
                }
            }


            // foreach (var e in events)
            // {
            //     var devicehash = e.Data
            //     var prevSession = e.Data[1];
            //     var newSession = e.Data[2];
            //
            //     Debug.Log(devicehash + " " +  prevSession + " " + newSession);
            //
            //     if (devicehash == hash && prevSession == prev.ToString() && newSession == current.ToString())
            //     {
            //         return true;
            //     }
            // }
            await Task.Delay(retryDelayMs);
        }

        return false;
    }
    public class RootObject
    {
        public Data data { get; set; }
    }

    public class Data
    {
        public List<Event> events { get; set; }
    }

    public class Event
    {
        public List<EventData> eventData { get; set; }
    }

    public class EventData
    {
        public List<string> data { get; set; }
    }




    public async Task<int> GetCurrentSession(string hash)
    {
        var requestBody = new
        {
            deviceHash = hash 
        };
        var dataString = JsonConvert.SerializeObject(requestBody);
        StringContent content = new StringContent(dataString, Encoding.UTF8, "application/json");
        
        int maxRetries = 3;
        int retryDelayMs = 3333;
        
        Debug.Log("Getting current session...");
        
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                HttpResponseMessage response = await Client.PostAsync(Constants.ProverURL + "current-session", content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned status code.");
                }
                var responseData = await response.Content.ReadAsStringAsync();
                Debug.Log(responseData);
                var responseJson = JObject.Parse(responseData);
                Debug.Log(responseJson);
                Debug.Log(responseJson["currentSession"].Value<int>());
                return responseJson["currentSession"].Value<int>();
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    if (retry == maxRetries - 1)
                    {
                        return 0;
                    }
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    public async Task<string> GetHash(IdentifierData data)
    {
        var device = new Device(data);
        var hash = await device.Hash();
        return hash;
    }


}