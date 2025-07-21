using System;
using System.Collections;
using System.Collections.Generic;

namespace BlackMesa.Utilities;

public class SequentialElementMap<K, V> : IEnumerable<(K key, V value)>
{
    private readonly Func<V> constructor;
    private readonly Func<K, int> indexGetter;
    private (K key, V value)[] backingArray;

    public SequentialElementMap(Func<V> elementConstructor, Func<K, int> keyIndexGetter, int capacity)
    {
        constructor = elementConstructor;
        indexGetter = keyIndexGetter;
        backingArray = new (K, V)[capacity];
        Clear();
    }

    private void EnsureSize(int size)
    {
        if (backingArray.Length >= size)
            return;

        var oldCapacity = backingArray.Length;
        var newCapacity = oldCapacity;
        while (newCapacity < size)
            newCapacity *= 2;
        Array.Resize(ref backingArray, newCapacity);

        for (var i = oldCapacity; i < newCapacity; i++)
            backingArray[i].value = constructor();
    }

    public ref V GetItem(K key)
    {
        var index = indexGetter(key);
        EnsureSize(index + 1);
        ref var element = ref backingArray[index];
        element.key = key;
        return ref element.value;
    }

    public ref V this[K key] => ref GetItem(key);

    public int Count => backingArray.Length;

    public void Clear()
    {
        for (var i = 0; i < backingArray.Length; i++)
        {
            ref var element = ref backingArray[i];
            element.key = default;
            element.value = constructor();
        }
    }

    public IEnumerator<(K key, V value)> GetEnumerator()
    {
        return ((IEnumerable<(K, V)>)backingArray).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return backingArray.GetEnumerator();
    }
}
