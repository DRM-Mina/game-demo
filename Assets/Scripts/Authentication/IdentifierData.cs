using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class IdentifierData
{
    public async Task GetData()
    {
        List<string> cmds = new List<string>()
        {
            "wmic cpu get ProcessorId",
            "wmic bios get serialnumber",
            "wmic csproduct get uuid",
            "wmic baseboard get serialnumber",
            "getmac /v"
        };
        List<string> results = new();
        foreach (var cmd in cmds)
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var res = await ProcessRunner.RunProcessAsync(processInfo);
            results.Add(res.StandardOutput);
        }

        for(int i=0;i<results.Count; i++)
        {
            var res = results[i];
            switch (i)
            {
                case 0:
                    cpuId = ParseProcessorId(res);
                    Debug.Log("CPUID: " + cpuId);
                    break;
                case 1:
                    systemSerial = ParseSystemSerial(res);
                    Debug.Log("Serial: " + systemSerial);
                    break;
                case 2:
                    systemUUID = ParseSystemUUID(res);
                    Debug.Log("UUID: " + systemUUID);
                    break;
                case 3:
                    baseboardSerial = ParseSystemSerial(res);
                    Debug.Log("Baseboard: " + baseboardSerial);
                    break;
                case 4:
                    macAddress = ParseMacAddress(res);
                    Debug.Log("MAC1: " + macAddress[0]);
                    Debug.Log("MAC2: " + macAddress[1]);
                    break;
            }
        }
    }

    private string[] stringsToReplace = { " ", "\n", "\r" };

    public string cpuId = "52060A00FFFBEBBF";
    public string systemSerial = "5CD0273QXP";
    public string systemUUID = "30444335-3732-5133-5850-bce92f8b2e35";
    public string baseboardSerial = "PKEAE028JDW0D7";
    public string[] macAddress = new[] { "bc:e9:2f:8b:2e:35", "cc:d9:ac:b6:28:0d" };
    public string diskSerial = "";
    
    private string ParseProcessorId(string input)
    {
        foreach (var str in stringsToReplace)
        {
            input = input.Replace(str, "");
        }

        input = input.Replace("ProcessorId", "");
        
        return input;
    }
    private string ParseSystemSerial(string input)
    {
        foreach (var str in stringsToReplace)
        {
            input = input.Replace(str, "");
        }

        input = input.Replace("SerialNumber", "");
        
        return input;
    }
    private string ParseSystemUUID(string input)
    {
        foreach (var str in stringsToReplace)
        {
            input = input.Replace(str, "");
        }

        input = input.Replace("UUID", "");
        
        return input;
    }
    private string[] ParseMacAddress(string input)
    {
        string[] lines = input.Split('\n');
        string ethernetAddress = string.Empty;
        string wifiAddress = string.Empty;

        foreach (string line in lines)
        {
            if (line.Contains("Ethernet") && !line.Contains("vEthernet"))
            {
                Match match = Regex.Match(line, @"([0-9A-F]{2}-){5}[0-9A-F]{2}", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    ethernetAddress = match.Value;
                }
            }
            else if (line.Contains("Wi-Fi"))
            {
                Match match = Regex.Match(line, @"([0-9A-F]{2}-){5}[0-9A-F]{2}", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    wifiAddress = match.Value;
                }
            }
        }

        return new[] { ethernetAddress, wifiAddress };
    }
}