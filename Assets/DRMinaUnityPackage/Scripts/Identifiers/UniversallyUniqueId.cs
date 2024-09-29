using System;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;

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