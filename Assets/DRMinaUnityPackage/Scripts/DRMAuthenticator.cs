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

public static class DRMAuthenticator
{
    private static readonly HttpClient Client = new();
    private static int _currentSessionId = -1;
    private static int _determinedSessionId = -1;
    
    private const int Second = 1000;
    private const int Minute = 60000;
    
    private static IdentifierData _identifierData;

    public static event EventHandler<DRMStatusCode> OnComplete;
    
    public static void Start()
    {
        OnComplete?.Invoke(null, Run().Result);
    }

    static async Task<DRMStatusCode> Run()
    {
        _identifierData = new IdentifierData();

        try
        {
            await _identifierData.GetData();
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return DRMStatusCode.DeviceNotCompatible;
        }

        var hash = "";
        try
        {
            hash = await GetHash(_identifierData);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return DRMStatusCode.DeviceNotCompatible;
        }

        
        var (currentSession, currentSessionStatusCode) = await GetCurrentSession(hash);
        if (currentSession < 1)
        {
            Debug.Log("Current session not found");
            return currentSessionStatusCode;
        }
        
        _currentSessionId = currentSession;
        
        _determinedSessionId = Random.Range(2, int.MaxValue);
        
        var (success, newSessionStatusCode) = await SendNewSession();

        if (!success)
        {
            return newSessionStatusCode;
        }
        
        Debug.Log("Waiting sequencer to include tx");
        await Task.Delay(4 * Minute);
        
        var startTime = Time.time;
        while (Time.time - startTime < 10 * Minute)
        {
            try
            {
                var isUpdated = await ControlEventsForSession(
                    hash, _currentSessionId, _determinedSessionId);
                if (isUpdated)
                {
                    return DRMStatusCode.Success;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            Debug.Log("Ids not same waiting");
            await Task.Delay(Minute);
        }
        
        return DRMStatusCode.Timeout;
    }

    private static async Task<(bool, DRMStatusCode)> SendNewSession()
    {
        var newRandomSession = new SessionData
        {
            rawIdentifiers = _identifierData,
            currentSession = _currentSessionId.ToString(),
            newSession = _determinedSessionId.ToString(),
            gameId = Constants.GameIDString
        };
        
        var dataS = JsonConvert.SerializeObject(newRandomSession);
        var content = new StringContent(dataS, Encoding.UTF8, "application/json");
        
        const int maxRetries = 5;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                Debug.Log("Sending New Session with ID: " + _determinedSessionId);
                var response = await Client.PostAsync(Constants.ProverURL, content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned  status code.");
                }
                return (true, DRMStatusCode.Success);
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    Debug.Log("Prover is not ready, steady lads...");
                    if (retry == maxRetries - 1)
                    {
                        return (false, DRMStatusCode.ProverNotReady);
                    }
                    await Task.Delay(20 * Second);
                }
                else
                {
                    return (false, DRMStatusCode.GameNotBoughtOrNoConnection);
                }
            }
        }

        return (false, DRMStatusCode.Timeout);
    }
    private static async Task<(int, DRMStatusCode)> GetCurrentSession(string hash)
    {
        var requestBody = new
        {
            deviceHash = hash 
        };
        var dataString = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(dataString, Encoding.UTF8, "application/json");
        
        const int maxRetries = 3;
        
        Debug.Log("Getting current session");
        
        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                var response = await Client.PostAsync(Constants.ProverURL + "current-session", content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    return (0, DRMStatusCode.ProverNotReady);
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    return (0, DRMStatusCode.ProverError);
                }
                var responseData = await response.Content.ReadAsStringAsync();
                Debug.Log(responseData);
                var responseJson = JObject.Parse(responseData);
                Debug.Log(responseJson["currentSession"].Value<int>());
                return (responseJson["currentSession"].Value<int>(), DRMStatusCode.Success);
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    if (retry == maxRetries - 1)
                    {
                        return (0, DRMStatusCode.Timeout);
                    }
                    await Task.Delay(Minute);
                }
                else
                {
                    return (0, DRMStatusCode.Timeout);
                }
            }
        }
        return (0, DRMStatusCode.Timeout);
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

    private static async Task<bool> ControlEventsForSession(string hash, int prev, int current)
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
        var fromBlockNumber = (await GetBlockHeight() - 1).ToString();

        var queryS = query.Replace("{input1}", Constants.GameIDString).Replace("{input2}", fromBlockNumber);

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
    
    private static async Task<string> GetHash(IdentifierData data)
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