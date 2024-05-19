namespace Drm_Mina.Identifiers;
using System.Numerics;

public class Serial : IdentifierBase
{
    public Serial(string serial)
    {
        if (!IsValid(serial))
        {
            throw new ArgumentException("Invalid Serial");
        }

        Value = serial;
    }
    
    public sealed override string Value { get; set; }
    
    private static bool IsValid(string serial)
    {
        return true;
    }
    
    private string ToBigNumber()
    {
        return BigInteger.Parse("0" + Value, System.Globalization.NumberStyles.HexNumber).ToString();
    }
    
    private Field[] ToFields()
    {
        var fields = new List<Field>();
        
        for (var i = 0; i < 128; i++)
        {
            fields.Add(new Field(BigInteger.Parse("0")));
        }
        
        for (var i = 0; i < Value.Length; i++)
        {
            fields[i] = new Field((int)Value[i]);
        }
        
        return fields.ToArray();
    }
    
    public override Field ToField()
    {
        return Poseidon.Hash(ToFields());
    }
    
    public override bool IsValid() => IsValid(Value);
}