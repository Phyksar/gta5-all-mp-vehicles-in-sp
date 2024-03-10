
namespace Utilities
{
    public struct HashCode
    {
        private int Value;

        public void Add<T>(in T value)
        {
            Value = (Value * 397) ^ value.GetHashCode();
        }

        public int ToHashCode()
        {
            return Value;
        }

        public static int Combine<T1, T2>(in T1 valueA, in T2 valueB)
        {
            var hashCode = new HashCode();
            hashCode.Add(valueA);
            hashCode.Add(valueB);
            return hashCode.ToHashCode();
        }

        public static int Combine<T1, T2, T3>(in T1 valueA, in T2 valueB, in T3 valueC)
        {
            var hashCode = new HashCode();
            hashCode.Add(valueA);
            hashCode.Add(valueB);
            hashCode.Add(valueC);
            return hashCode.ToHashCode();
        }
    }
}
