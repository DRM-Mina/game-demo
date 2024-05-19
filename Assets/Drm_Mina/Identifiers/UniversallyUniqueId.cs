using System;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Drm_Mina.Identifiers
{
    public class UniversallyUniqueId : IdentifierBase
    {
        public UniversallyUniqueId(string uuid)
        {
            if (!IsValid(uuid))
            {
                throw new ArgumentException("Invalid UUID");
            }

            Value = uuid.Replace("-", "").ToUpper();
        }

        public sealed override string Value { get; set; }

        private static bool IsValid(string uuid)
        {
            var uuidRegex = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";
            return Regex.IsMatch(uuid, uuidRegex);
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