using Drm_Mina.Identifiers;

namespace Drm_Mina;

public class Device
{
    private CpuId _cpuId;
    private Serial _systemSerial;
    private UniversallyUniqueId _uuid;
    private Serial _baseBoardSerial;
    private MacAddress _ethernetMac;
    private MacAddress _wifiMac;
    private Serial _diskSerial;


    public Device(string cpuId, string systemSerial, string uuid, string baseBoardSerial, string ethernetMac,
        string wifiMac,
        string diskSerial)
    {
        _cpuId = new CpuId(cpuId);
        _systemSerial = new Serial(systemSerial);
        _uuid = new UniversallyUniqueId(uuid);
        _baseBoardSerial = new Serial(baseBoardSerial);
        _ethernetMac = new MacAddress(ethernetMac);
        _wifiMac = new MacAddress(wifiMac);
        _diskSerial = new Serial(diskSerial);
    }

    private Field[] HashInputs()
    {
        var input = new List<Field>();
        input.Add(_cpuId.ToField());
        input.Add(_systemSerial.ToField());
        input.Add(_uuid.ToField());
        input.Add(_baseBoardSerial.ToField());
        input.Add(_ethernetMac.ToField());
        input.Add(_wifiMac.ToField());
        input.Add(_diskSerial.ToField());
        return input.ToArray();
    }

    public string Hash()
    {
        var inputs = HashInputs();
        var hash = Poseidon.Hash(inputs);
        return hash.ToString();
    }
}