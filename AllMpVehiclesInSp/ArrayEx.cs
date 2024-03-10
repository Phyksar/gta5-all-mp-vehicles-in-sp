using System;
using System.Collections.Generic;
using System.Linq;

public static class ArrayEx
{
    public static T[] Exclude<T>(T[] elements, ISet<T> excludeSet)
    {
        return elements.Where((element) => !excludeSet.Contains(element)).ToArray();
    }

    public static T Random<T>(Random random, T[] elements)
    {
        if (elements.Length == 0) {
            throw new IndexOutOfRangeException("array is empty");
        }
        return elements[random.Next(elements.Length)];
    }
}
