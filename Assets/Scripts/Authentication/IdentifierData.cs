using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class IdentifierData
{
    public async Task GetData()
    {
#if UNITY_STANDALONE_WIN

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

#endif

#if UNITY_STANDALONE_OSX
        string command = "system_profiler SPHardwareDataType | grep 'Serial Number (system)' && system_profiler SPHardwareDataType | grep 'Hardware UUID' && ifconfig en0 | grep 'ether' && ifconfig en1 | grep 'ether'";

        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = processInfo;
            try
            {
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    UnityEngine.Debug.Log(output);
                    ParseOutput(output);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    UnityEngine.Debug.LogError($"Error: {error}");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Exception: {e.Message}");
            }
        }
#endif

#if UNITY_STANDALONE_LINUX
        string command = "sudo dmidecode -t processor | grep ID && sudo dmidecode -s system-serial-number && sudo dmidecode -s system-uuid && sudo dmidecode -t baseboard | grep Serial | awk '{print $3}' && ip link show eno1 | grep link/ether | awk '{print $2}' && ip link show wlo1 | grep link/ether | awk '{print $2}'";

        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = processInfo;
            try
            {
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    ParseOutput(output);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    UnityEngine.Debug.LogError($"Error: {error}");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Exception: {e.Message}");
            }
        }
#endif

    }

    private string[] stringsToReplace = { " ", "\n", "\r" };

    public string cpuId = "";
    public string systemSerial = "";
    public string systemUUID = "";
    public string baseboardSerial = "";
    public string[] macAddress = new[] { "", "" };
    public string diskSerial = "";


#if UNITY_STANDALONE_WIN

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

#endif


#if UNITY_STANDALONE_OSX
    private void ParseOutput(string output)
    {
        string[] lines = output.Split('\n');

        if (lines.Length >= 4)
        {
            cpuId = "0000000000000000";
            systemSerial = lines[0].Trim().Replace("Serial Number (system): ", "");
            systemUUID = lines[1].Trim().Replace("Hardware UUID: ", "");
            baseboardSerial = systemSerial;
            macAddress[0] = lines[2].Trim().Replace("ether ", "");
            macAddress[1] = lines[3].Trim().Replace("ether ", ""); ;

        }
        else
        {
            UnityEngine.Debug.LogError("Unexpected output format");
        }
    }

    private string ExtractValue(string line, string prefix)
    {
        int startIndex = line.IndexOf(prefix);
        if (startIndex != -1)
        {
            return line.Substring(startIndex + prefix.Length).Trim().Replace(" ", "");
        }
        return "";
    }
#endif

#if UNITY_STANDALONE_LINUX
private void ParseOutput(string output)
        {
            string[] lines = output.Split('\n');

            if (lines.Length >= 6)
            {
                cpuId = ExtractValue(lines[0], "ID: ");
                systemSerial = lines[1].Trim();
                systemUUID = lines[2].Trim();
                baseboardSerial = lines[3].Trim();
                macAddress[0] = lines[4].Trim();
                macAddress[1] = lines[5].Trim();

            }
            else
            {
                UnityEngine.Debug.LogError("Unexpected output format");
            }
        }

        private string ExtractValue(string line, string prefix)
        {
            int startIndex = line.IndexOf(prefix);
            if (startIndex != -1)
            {
                return line.Substring(startIndex + prefix.Length).Trim().Replace(" ", "");
            }
            return "";
        }
#endif
}