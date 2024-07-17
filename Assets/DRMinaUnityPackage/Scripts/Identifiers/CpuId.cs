using System;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Drm_Mina.Identifiers
{
    public class CpuId : IdentifierBase
    {
        public CpuId(string cpuId)
        {
            if (!IsValid(cpuId))
            {
                throw new ArgumentException("Invalid Cpu ID");
            }

            Value = cpuId.Replace("-", "").ToUpper();
        }

        public sealed override string Value { get; set; }

        private static bool IsValid(string cpuId)
        {
            var cpuIdRegex = @"^[0-9A-F]{16}$";
            return Regex.IsMatch(cpuId, cpuIdRegex);
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