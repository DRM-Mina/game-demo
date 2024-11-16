using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using StringContent = System.Net.Http.StringContent;
using System.Collections.Generic;
using DRMinaUnityPackage;
using DRMinaUnityPackage.Scripts;


public class Authenticator : MonoBehaviour
{
    public TextBar textBar;
    private static readonly HttpClient Client = new();
    private int currentSessionId = -1;
    private int determinedSessionId = -1;

    private const int Second = 1000;
    private const int Minute = 60000;
    private const int AnimationDelay = 1 * Second;

    private IdentifierData identifierData;
    [HideInInspector] public bool isDead = false;

    private void Start()
    {
        Run();
    }

    public async Task Run()
    {
        await Task.Delay(500 + AnimationDelay);

        identifierData = new IdentifierData();

        try
        {
            textBar.UpdateText("Gathering identifier data...");
            await identifierData.GetData();
            textBar.UpdateText("Identifier data collected.");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            textBar.Terminate();
            return;
        }

        // await Task.Delay(500 + animationDelay);
        // textBar.UpdateText("CPUID: " + data.cpuId);
        // await Task.Delay(100 + animationDelay);
        // textBar.UpdateText("Serial: " + data.systemSerial);
        // await Task.Delay(100 + animationDelay);
        // textBar.UpdateText("UUID: " + data.systemUUID);
        // await Task.Delay(100 + animationDelay);
        // textBar.UpdateText("Baseboard: " + data.baseboardSerial);
        // await Task.Delay(100 + animationDelay);
        // textBar.UpdateText("MAC1: " + data.macAddress[0]);
        // await Task.Delay(100 + animationDelay);
        // textBar.UpdateText("MAC2: " + data.macAddress[1]);
        // await Task.Delay(100 + animationDelay);

        var hash = "";
        try
        {
            textBar.UpdateText("Calculating hash...");
            hash = await GetHash(identifierData);
            Debug.Log(hash);
            await Task.Delay(AnimationDelay);
            textBar.UpdateText("Hash calculated.");
            await Task.Delay(AnimationDelay);
            textBar.UpdateText("Hash: " + hash);
        }
        catch (Exception e)
        {
            textBar.Terminate();
            return;
        }


        try
        {
            await Task.Delay(AnimationDelay);
            textBar.UpdateText("Getting current session...");
            
            currentSessionId = await GetCurrentSession(hash);
            if (currentSessionId <= 0)
            {
                Debug.Log("Game is not bought.");
                throw new Exception();
            }
            
            textBar.UpdateText("Current session ID: " + currentSessionId);
        }
        catch (Exception e)
        {
            textBar.Terminate();
            return;
        }
        
        await Task.Delay(AnimationDelay);
        determinedSessionId = Random.Range(2, int.MaxValue);
        textBar.UpdateText("Sending New Session with ID: " + determinedSessionId);

        if (await SendNewSession())
        {
            textBar.UpdateText("New session proof sent to sequencer ^^");
            await Task.Delay(AnimationDelay);
            textBar.UpdateText("Lets wait until sequencer sends to Mina!");
            await Task.Delay(AnimationDelay);
        }
        else
        {
            return;
        }

        textBar.StartTimer(240);
        await Task.Delay(4 * Minute);
        textBar.UpdateText("Start to fetching events from Mina");
        textBar.StartTimer(600);
        var startTime = Time.time;
        while(Time.time - startTime < 10 * Minute)
        {
            textBar.UpdateText("Getting Current Session...");
            try
            {
                var isUpdated = await ControlEventsForSession(
                    hash, currentSessionId, determinedSessionId);
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
            await Task.Delay(AnimationDelay);
            textBar.UpdateText("ID is not same. Retrying...");
            await Task.Delay(Minute);
        }
        textBar.UpdateText("Sowwy we cannot found :(");
        textBar.EndTimer();
    }

    private static async Task<int> GetBlockHeight()
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
        var obj = JsonConvert.DeserializeObject<JObject>(responseString);

        var blockHeight = obj["blocks"][0]["blockHeight"];

        return (int)blockHeight;
    }

    private async Task<bool> ControlEventsForSession(string hash, int prev, int current)
    {
        const string query = @"
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

        var queryS = query.Replace("{input1}", DRMEnvironment.GameIDString).Replace("{input2}", fromBlock);

        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        var content = new StringContent(contentS, Encoding.UTF8, "application/json");

        const int maxRetries = 3;
        const int retryDelayMs = 2000;

        for (var i = 0; i < maxRetries; i++)
        {
            var response = await Client.PostAsync("https://api.minascan.io/archive/devnet/v1/graphql", content);

            var responseString = await response.Content.ReadAsStringAsync();
            Debug.Log(responseString);
        
            var root = JsonConvert.DeserializeObject<RootObject>(responseString);
            
            foreach (var eventItem in root.Data.Events)
            {
                foreach (var eventDataItem in eventItem.EventData)
                {
                    var deviceHash = eventDataItem.Data[0];
                    var prevSession = eventDataItem.Data[1];
                    var newSession = eventDataItem.Data[2];
                    Debug.Log(deviceHash + " " +  prevSession + " " + newSession);
                
                    if (deviceHash == hash && prevSession == prev.ToString() && newSession == current.ToString())
                    {
                        return true;
                    }
                }
            }
            
            await Task.Delay(retryDelayMs);
        }

        return false;
    }
    public class RootObject
    {
        public Data Data { get; set; }
    }

    public class Data
    {
        public List<Event> Events { get; set; }
    }

    public class Event
    {
        public List<EventData> EventData { get; set; }
    }

    public class EventData
    {
        public List<string> Data { get; set; }
    }

    private static async Task<int> GetCurrentSession(string hash)
    {
        var requestBody = new
        {
            deviceHash = hash 
        };
        var dataString = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(dataString, Encoding.UTF8, "application/json");
        
        const int maxRetries = 3;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                var response = await Client.PostAsync(DRMEnvironment.ProverURL + "current-session", content);
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
                    await Task.Delay(Minute);
                }
                else
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    private async Task<bool> SendNewSession()
    {
        var newRandomSession = new SessionData
        {
            rawIdentifiers = identifierData,
            currentSession = currentSessionId.ToString(),
            newSession = determinedSessionId.ToString(),
            gameId = DRMEnvironment.GameIDString
        };
        
        var dataS = JsonConvert.SerializeObject(newRandomSession);
        var content = new StringContent(dataS, Encoding.UTF8, "application/json");
        
        const int maxRetries = 5;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                textBar.UpdateText("Sending New Session with ID: " + determinedSessionId);
                var response = await Client.PostAsync(DRMEnvironment.ProverURL, content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned  status code.");
                }
                return true;
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    textBar.UpdateText("Prover is not ready, steady lads...");
                    if (retry == maxRetries - 1)
                    {
                        textBar.Terminate();
                        return false;
                    }
                    await Task.Delay(20 * Second);
                }
                else
                {
                    textBar.Terminate("Your device may not be compatible with our prover, or your internet connection is down. Please try again later.");
                    return false;
                }
            }
        }

        return false;
    }
    private static async Task<string> GetHash(IdentifierData data)
    {
        var device = new Device(data);
        var hash = await device.Hash();
        return hash;
    }
    
}