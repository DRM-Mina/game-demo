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
    public string gameTokenAddress;
    public string drmContractAddress;
    public string proverEndpoint;
    public int proverMaxRetries;
    public int proverRetryIntervalSeconds;
    public int authTimeoutMinutes;

    public TextBar textBar;
    private static readonly HttpClient Client = new();

    private int currentSessionId = -1;
    private int determinedSessionId = -1;

    private static int _currentBlockHeight = -1;

    private const int Second = 1000;
    private const int Minute = 60000;
    private const int AnimationDelay = 1 * Second;

    private IdentifierData identifierData;
    [HideInInspector] public bool isDead = false;

    private void Start()
    {
        DRMEnvironment.GAME_TOKEN_ADDRESS = gameTokenAddress;
        DRMEnvironment.DRM_CONTRACT_ADDRESS = drmContractAddress;
        DRMEnvironment.PROVER_ENDPOINT = proverEndpoint;
        DRMEnvironment.PROVER_MAX_RETRIES = proverMaxRetries;
        DRMEnvironment.PROVER_RETRY_INTERVAL_SECONDS = proverRetryIntervalSeconds;
        DRMEnvironment.AUTH_TIMEOUT_MINUTES = authTimeoutMinutes;
        Run();
    }

    public async Task Run()
    {
        Debug.Log(DRMEnvironment.GAME_TOKEN_ADDRESS);
        Debug.Log(DRMEnvironment.DRM_CONTRACT_ADDRESS);
        if (DRMEnvironment.GAME_TOKEN_ADDRESS == null || DRMEnvironment.DRM_CONTRACT_ADDRESS == null ||
            DRMEnvironment.DRM_CONTRACT_ADDRESS == "" || DRMEnvironment.GAME_TOKEN_ADDRESS == "")
        {
            textBar.Terminate(
                "Please set the game token address and DRM contract address in the DRMEnvironment script.");
            return;
        }

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
            textBar.UpdateText("Setting prover address...");
            var result = await SetProverAddress();
            if (!result)
            {
                textBar.Terminate();
                return;
            }

            textBar.UpdateText("Prover address set.");
        }
        catch (Exception e)
        {
            textBar.Terminate();
            return;
        }

        try
        {
            await Task.Delay(AnimationDelay);
            textBar.UpdateText("Getting current block height...");
            _currentBlockHeight = await GetBlockHeight();
            Debug.Log(_currentBlockHeight);
            if (_currentBlockHeight <= 0)
            {
                Debug.Log("Block height is not valid.");
                throw new Exception();
            }

            textBar.UpdateText("Current block height: " + _currentBlockHeight);
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

        while (Time.time - startTime < authTimeoutMinutes * Minute)
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
        var fromBlock = _currentBlockHeight.ToString();
        Debug.Log(fromBlock);

        var queryS = query.Replace("{input1}", DRMEnvironment.DRM_CONTRACT_ADDRESS).Replace("{input2}", fromBlock);

        var contentS = JsonConvert.SerializeObject(new { query = queryS });
        var content = new StringContent(contentS, Encoding.UTF8, "application/json");

        try
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
                    Debug.Log(deviceHash + " " + prevSession + " " + newSession);

                    if (deviceHash == hash && prevSession == prev.ToString() && newSession == current.ToString())
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
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

        var maxRetries = DRMEnvironment.PROVER_MAX_RETRIES;
        var retryIntervalSeconds = DRMEnvironment.PROVER_RETRY_INTERVAL_SECONDS;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                var response = await Client.PostAsync(DRMEnvironment.PROVER_ENDPOINT + "current-session", content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }

                if (response.StatusCode != HttpStatusCode.OK)
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

                    await Task.Delay(retryIntervalSeconds * Second);
                }
                else
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    private static async Task<bool> SetProverAddress()
    {
        var requestBody = new
        {
            drmAddressB58 = DRMEnvironment.DRM_CONTRACT_ADDRESS,
            gameTokenAddressB58 = DRMEnvironment.GAME_TOKEN_ADDRESS
        };

        Debug.Log(JsonConvert.SerializeObject(requestBody));

        var dataString = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(dataString, Encoding.UTF8, "application/json");

        var maxRetries = DRMEnvironment.PROVER_MAX_RETRIES;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                var response = await Client.PostAsync(DRMEnvironment.PROVER_ENDPOINT + "set-address", content);
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new ApplicationException();
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Server returned status code.");
                }

                Debug.Log("Prover address set.");
                return true;
            }
            catch (Exception e)
            {
                if (e is ApplicationException)
                {
                    Debug.Log("Server returned bad request");
                    return false;
                }

                await Task.Delay(2 * Second);
            }
        }

        return false;
    }

    private async Task<bool> SendNewSession()
    {
        var newRandomSession = new SessionData
        {
            rawIdentifiers = identifierData,
            currentSession = currentSessionId.ToString(),
            newSession = determinedSessionId.ToString(),
        };

        var dataS = JsonConvert.SerializeObject(newRandomSession);
        var content = new StringContent(dataS, Encoding.UTF8, "application/json");

        var maxRetries = DRMEnvironment.PROVER_MAX_RETRIES;
        var retryIntervalSeconds = DRMEnvironment.PROVER_RETRY_INTERVAL_SECONDS;


        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                textBar.UpdateText("Sending New Session with ID: " + determinedSessionId);
                var response = await Client.PostAsync(DRMEnvironment.PROVER_ENDPOINT, content);
                if (response.StatusCode == HttpStatusCode.Processing)
                {
                    throw new ApplicationException();
                }

                if (response.StatusCode != HttpStatusCode.OK)
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

                    await Task.Delay(retryIntervalSeconds * Second);
                }
                else
                {
                    textBar.Terminate(
                        "Your device may not be compatible with our prover, or your internet connection is down. Please try again later.");
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