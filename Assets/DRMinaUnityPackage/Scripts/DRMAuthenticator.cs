using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DRMinaUnityPackage.Scripts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DRMinaUnityPackage
{
    public static class DRMAuthenticator
    {
        private static readonly HttpClient Client = new();
        private static int _currentSessionId = -1;
        private static int _determinedSessionId = -1;

        private static int _currentBlockHeight = -1;

        private const int Second = 1000;
        private const int Minute = 60000;

        private static IdentifierData _identifierData;

        public static event EventHandler<DRMStatusCode> OnComplete;

        public static void Start()
        {
            RunWrapper();
        }

        private static async void RunWrapper()
        {
            var result = await Run();
            OnComplete?.Invoke(null, result);
        }

        static async Task<DRMStatusCode> Run()
        {
            if (DRMEnvironment.GAME_TOKEN_ADDRESS == null || DRMEnvironment.DRM_CONTRACT_ADDRESS == null)
            {
                return DRMStatusCode.SetEnvironment;
            }

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

            var (setAddressSuccess, setAddressStatusCode) = await SetProverAddress();

            if (!setAddressSuccess)
            {
                return setAddressStatusCode;
            }

            var (currentBlockHeight, blockHeightStatusCode) = await GetBlockHeight();
            if (currentBlockHeight < 1)
            {
                return blockHeightStatusCode;
            }

            _currentBlockHeight = currentBlockHeight;

            var (currentSession, currentSessionStatusCode) = await GetCurrentSession(hash);
            if (currentSession < 1)
            {
                Debug.Log("Current session not found");
                return currentSessionStatusCode;
            }

            _currentSessionId = currentSession;

            _determinedSessionId = Random.Range(2, int.MaxValue);

            var (sendNewSessionSuccess, newSessionStatusCode) = await SendNewSession();

            if (!sendNewSessionSuccess)
            {
                return newSessionStatusCode;
            }

            Debug.Log("Waiting sequencer to include tx");
            await Task.Delay(4 * Minute);

            var startTime = Time.time;
            var retryCount = 0;

            var authTimeoutMinutes = DRMEnvironment.AUTH_TIMEOUT_MINUTES;

            while (Time.time - startTime < authTimeoutMinutes * Minute)
            {
                try
                {
                    var (isUpdated, controlEventsStatusCode) = await ControlEventsForSession(
                        hash, _currentSessionId, _determinedSessionId);
                    if (isUpdated)
                    {
                        return DRMStatusCode.Success;
                    }

                    if (controlEventsStatusCode == DRMStatusCode.MinaNodeError)
                    {
                        retryCount++;
                        if (retryCount > 3)
                        {
                            return DRMStatusCode.MinaNodeError;
                        }
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

        private static async Task<(bool, DRMStatusCode)> SetProverAddress()
        {
            var requestBody = new
            {
                drmAddressB58 = DRMEnvironment.DRM_CONTRACT_ADDRESS,
                gameTokenAddressB58 = DRMEnvironment.GAME_TOKEN_ADDRESS
            };

            var dataString = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(dataString, Encoding.UTF8, "application/json");

            var maxRetries = DRMEnvironment.PROVER_MAX_RETRIES;

            for (var retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    Debug.Log("Setting Prover Address");
                    var response = await Client.PostAsync(DRMEnvironment.PROVER_ENDPOINT + "set-address", content);
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new ApplicationException();
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Server returned status code.");
                    }

                    return (true, DRMStatusCode.Continue);
                }
                catch (Exception e)
                {
                    if (e is ApplicationException)
                    {
                        Debug.Log("Server returned bad request");
                        return (false, DRMStatusCode.SetEnvironment);
                    }

                    await Task.Delay(2 * Second);
                }
            }

            return (false, DRMStatusCode.ProverError);
        }

        private static async Task<(bool, DRMStatusCode)> SendNewSession()
        {
            var newRandomSession = new SessionData
            {
                rawIdentifiers = _identifierData,
                currentSession = _currentSessionId.ToString(),
                newSession = _determinedSessionId.ToString(),
            };

            var dataS = JsonConvert.SerializeObject(newRandomSession);
            var content = new StringContent(dataS, Encoding.UTF8, "application/json");

            var maxRetries = DRMEnvironment.PROVER_MAX_RETRIES;
            var retryIntervalSeconds = DRMEnvironment.PROVER_RETRY_INTERVAL_SECONDS;

            for (var retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    Debug.Log("Sending New Session with ID: " + _determinedSessionId);
                    var response = await Client.PostAsync(DRMEnvironment.PROVER_ENDPOINT, content);
                    if (response.StatusCode == HttpStatusCode.Processing)
                    {
                        throw new ApplicationException();
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Server returned status code.");
                    }

                    return (true, DRMStatusCode.Continue);
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

                        await Task.Delay(retryIntervalSeconds * Second);
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

            var maxRetries = DRMEnvironment.PROVER_MAX_RETRIES;
            var retryIntervalSeconds = DRMEnvironment.PROVER_RETRY_INTERVAL_SECONDS;

            for (var retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    Debug.Log("Getting Current Session");
                    var response = await Client.PostAsync(DRMEnvironment.PROVER_ENDPOINT + "current-session", content);
                    if (response.StatusCode == HttpStatusCode.Processing) // code 102
                    {
                        throw new ApplicationException(); // Retry
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return (0, DRMStatusCode.ProverError);
                    }

                    var responseData = await response.Content.ReadAsStringAsync();
                    Debug.Log("Response Data: " + responseData);
                    var responseJson = JObject.Parse(responseData);
                    var currentSession = (responseJson["currentSession"] ?? throw new InvalidOperationException())
                        .Value<int>();
                    Debug.Log("Current Session: " + currentSession);
                    return currentSession < 1
                        ? (0, DRMStatusCode.GameNotBoughtOrNoConnection)
                        : (currentSession, DRMStatusCode.Continue);
                }
                catch (Exception e)
                {
                    if (e is ApplicationException)
                    {
                        if (retry == maxRetries - 1)
                        {
                            return (0, DRMStatusCode.Timeout);
                        }

                        Debug.Log("Prover is not ready, steady lads...");
                        await Task.Delay(retryIntervalSeconds * Second);
                    }
                    else
                    {
                        return (0, DRMStatusCode.GameNotBoughtOrNoConnection);
                    }
                }
            }

            return (0, DRMStatusCode.Timeout);
        }

        private static async Task<(int, DRMStatusCode)> GetBlockHeight()
        {
            try
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

                return ((int)blockHeight, DRMStatusCode.Continue);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return (0, DRMStatusCode.MinaNodeError);
            }
        }

        private static async Task<(bool, DRMStatusCode)> ControlEventsForSession(string hash, int prev, int current)
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

            var fromBlockNumber = _currentBlockHeight.ToString();

            var queryS = query.Replace("{input1}", DRMEnvironment.DRM_CONTRACT_ADDRESS)
                .Replace("{input2}", fromBlockNumber);

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

                        if (deviceHash == hash && prevSession == prev.ToString() &&
                            newSession == current.ToString())
                        {
                            return (true, DRMStatusCode.Success);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }

            return (false, DRMStatusCode.Timeout);
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
        DeviceNotCompatible,
        Timeout,
        GameNotBoughtOrNoConnection,
        MinaNodeError,
        SetEnvironment,
        Continue,
    }
}