namespace Drm_Mina;
using System.Numerics;

public class Field
{
    public BigInteger Value { get; }

    private static BigInteger Modulus { get; } =
        BigInteger.Parse("28948022309329048855892746252171976963363056481941560715954676764349967630337");

    public Field(BigInteger value)
    {
        Value = value % Modulus;
        if (Value < 0) Value += Modulus;
    }
    
    public static Field operator +(Field a, Field b) => new Field(a.Value + b.Value);
    public static Field operator -(Field a, Field b) => new Field(a.Value - b.Value);
    public static Field operator *(Field a, Field b) => new Field(a.Value * b.Value);
    public static Field operator /(Field a, Field b) => a * b.Inverse();
    public Field Inverse() => new Field(BigInteger.ModPow(Value, Modulus - 2, Modulus));

    public static Field Power(Field a, BigInteger exp) =>
        new Field(BigInteger.ModPow(a.Value, exp, Modulus));
    
    public static Field Dot(Field[] a, Field[] b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vectors must have the same length");
        Field result = new Field(0);
        for (int i = 0; i < a.Length; i++)
        {
            result += a[i] * b[i];
        }

        return result;
    }

    public override string ToString() => Value.ToString();
}