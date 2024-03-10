

using GTA.Math;
using Utilities;

public interface ISearchQuery<T> where T : IPosition3
{
    T[] FindInSphere(in Vector3 center);
}
