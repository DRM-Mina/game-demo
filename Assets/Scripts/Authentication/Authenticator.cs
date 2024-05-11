using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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

    private void Start()
    {
        Run();
    }

    public async Task Run()
    {
        await Task.Delay(500 + animationDelay);
        
        textBar.UpdateText("Gathering identifier data...");
        IdentifierData data = new IdentifierData();
        await data.GetData();
        textBar.UpdateText("Identifier data collected.");
        
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
        
        textBar.UpdateText("Calculating hash...");
        string hash = await GetHash(data);
        textBar.UpdateText("Hash calculated.");
        await Task.Delay(200 + animationDelay);
        textBar.UpdateText("Hash: " + hash);
        
        await Task.Delay(500 + animationDelay);
        textBar.UpdateText("Getting current session...");
        var session = await GetCurrentSession(hash);
        textBar.UpdateText("Current session here.");
        await Task.Delay(200 + animationDelay);
        textBar.UpdateText("Session ID: " + session);
        
        await Task.Delay(200 + animationDelay);
        _determinedSessionId = Random.Range(2, int.MaxValue);
        textBar.UpdateText("Sending New Session with ID: " + _determinedSessionId);
        var newRandomSession = new SessionData
        {
            rawIdentifiers = data,
            currentSession = session.ToString(),
            newSession = _determinedSessionId.ToString(),
            gameId = "1"
        };
        
        var dataS = JsonConvert.SerializeObject(newRandomSession);
        StringContent content = new StringContent(dataS, Encoding.UTF8, "application/json");

        await Client.PostAsync(Constants.ProverURL, content);
        textBar.UpdateText("New session sent.");
        
        await Task.Delay(200 + animationDelay);
        textBar.StartTimer(300);
        var startTime = Time.time;
        while(Time.time - startTime < 300f)
        {
            textBar.UpdateText("Getting Current Session...");
            var id = await GetCurrentSession(hash);
            Debug.Log("NEW ID:" + id);
            if (_determinedSessionId == id)
            {
                textBar.UpdateText("SUCCESS");
                textBar.EndTimer();
                return;
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

        string gameId = "1";

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
        var dataS = JsonConvert.SerializeObject(data);
        var contentS = JsonConvert.SerializeObject( new {rawIdentifiers=dataS });
        StringContent content = new StringContent(contentS, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync(Constants.ProverURLHash, content);

        var responseString = await response.Content.ReadAsStringAsync();
        return responseString;
    }
}