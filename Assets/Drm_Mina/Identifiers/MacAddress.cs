using System;
using System.Numerics;
using System.Text.RegularExpressions;

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

            Value = macAddress.Replace(":", "").ToUpper();
            Value = macAddress.Replace("-", "").ToUpper();
        }

        public sealed override string Value { get; set; }

        private static bool IsValid(string macAddress)
        {
            var macAddressRegex = @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$";
            return Regex.IsMatch(macAddress, macAddressRegex);
        }

        private string ToBigNumber()
        {
            return BigInteger.Parse("0" + Value, System.Globalization.NumberStyles.HexNumber).ToString();
        }

        public override Field ToField()
        {
            return new Field(BigInteger.Parse(ToBigNumber()));
        }

        public override bool IsValid() => IsValid(Value);
    }
}