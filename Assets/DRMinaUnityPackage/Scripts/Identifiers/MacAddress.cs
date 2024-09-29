using System;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Drm_Mina.Identifiers
{
    public class MacAddress : IdentifierBase
    {
        public MacAddress(string macAddress)
        {
            if (!IsValid(macAddress))
            {
                throw new ArgumentException("Invalid Mac Address");
            }

            Value = macAddress.Replace(":", "").Replace("-", "").ToUpper();
        }

        public sealed override string Value { get; set; }

        private static bool IsValid(string macAddress)
        {
            var macAddressRegex = @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$";
            return Regex.IsMatch(macAddress, macAddressRegex);
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
}
