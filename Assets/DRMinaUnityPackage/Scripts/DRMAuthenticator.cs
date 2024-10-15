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
            try
            {
                var isUpdated = await ControlEventsForSession(
                    hash, session, _determinedSessionId);
                if (isUpdated)
                {
                    //success
                    return DRMStatusCode.Success;
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
        var requestBody = new
        {
            deviceHash = hash 
        };
        var dataString = JsonConvert.SerializeObject(requestBody);
        StringContent content = new StringContent(dataString, Encoding.UTF8, "application/json");
        
        int maxRetries = 5;
        int retryDelayMs = 3333;
        
        Debug.Log("Getting current session...");
        
        for (int retry = 0; retry < maxRetries; retry++)
        {
            Debug.Log("Trying to get session" + (retry + 1).ToString() + "th time");
            try
            {
                HttpResponseMessage response = await Client.PostAsync(Constants.ProverURL + "current-session", content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned status not OK.");
                }
                var responseData = await response.Content.ReadAsStringAsync();
                Debug.Log(responseData);
                var responseJson = JObject.Parse(responseData);
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

    static async Task<int> GetBlockHeight()
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
    
    static async Task<bool> ControlEventsForSession(string hash, int prev, int current)
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
        var fromBlockNumber = (await GetBlockHeight() - 5).ToString();

        string queryS = query.Replace("{input1}", Constants.GameIDString).Replace("{input2}", fromBlockNumber);
        Debug.Log(queryS);

        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        StringContent content = new StringContent(contentS, Encoding.UTF8, "application/json");

        int maxRetries = 20;
        int retryDelayMs = 30000;

        for (int i = 0; i < maxRetries; i++)
        {
            Debug.Log((i+1) + "th try");
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
            
                Debug.Log(devicehash + " " +  prevSession + " " + newSession);
            
                if (devicehash == hash && prevSession == prev.ToString() && newSession == current.ToString())
                {
                    return true;
                }
            }
            await Task.Delay(retryDelayMs);
        }

        return false;
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