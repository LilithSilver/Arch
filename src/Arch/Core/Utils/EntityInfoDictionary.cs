﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arch.Core;
using Arch.Core.Extensions;

namespace Arch.Core.Utils;

/// <summary>
///     The <see cref="EntityInfoDictionary"/> class
///     represents an jagged array that stores <see cref="EntityInfo"/> for quickly acessing it.
/// </summary>
internal class EntityInfoDictionary
{
    /// <summary>
    ///     How large a chunk should be. This value will be a power of 2.
    /// </summary>
    private static readonly int _chunkSize;
    private static readonly int _chunkSizeMinusOne;

    /// <summary>
    ///     The jagged array storing the <see cref="EntityInfo"/>.
    /// </summary>
    private EntityInfo[][] _entityInfos = Array.Empty<EntityInfo[]>();

    /// <summary>
    ///     The currently largest id inside this <see cref="EntityInfoDictionary"/>, for trimming purposes.
    /// </summary>
    private int _largestId;

    /// <summary>
    ///     Initializes the static values of <see cref="EntityInfoDictionary"/>.
    /// </summary>
    static EntityInfoDictionary()
    {
        var cpuL1CacheSize = 16_000; // In bytes
        var idealSize = cpuL1CacheSize / Unsafe.SizeOf<EntityInfo>();

        _chunkSize = MathExtensions.RoundToPowerOfTwo(idealSize);
        _chunkSizeMinusOne = _chunkSize - 1;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="EntityInfoDictionary"/> class.
    /// </summary>
    public EntityInfoDictionary() : this(256)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="EntityInfoDictionary"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    public EntityInfoDictionary(int capacity)
    {
        EnsureCapacity(capacity);
    }

    /// <summary>
    ///     Adds a new <see cref="EntityInfo"/> to a given index.
    /// </summary>
    /// <param name="id">The index.</param>
    /// <param name="entityInfo">The <see cref="EntityInfo"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int id, EntityInfo entityInfo)
    {
        this[id] = entityInfo;
    }

    /// <summary>
    ///     Removes an <see cref="EntityInfo"/> from a given index.
    ///     Replaces its value with the default one.
    /// </summary>
    /// <param name="id">The index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(int id)
    {
        this[id] = default;
    }

    /// <summary>
    ///     Trys to return an <see cref="EntityInfo"/> from its index, if its set.
    /// </summary>
    /// <param name="id">The index.</param>
    /// <param name="entityInfo">The <see cref="EntityInfo"/>.</param>
    /// <returns>True if it was set, false if not.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(int id, out EntityInfo entityInfo)
    {
        // If the id is negative
        if (id < 0)
        {
            entityInfo = default;
            return false;
        }

        IdToSlot(id, out var outerIndex, out var innerIndex);

        // If the item is outside the array. Then it definetly doesn't exist
        if (outerIndex > _entityInfos.Length)
        {
            entityInfo = default;
            return false;
        }

        ref var item = ref _entityInfos[outerIndex][innerIndex];

        // If the item is the default then the nobody set its value.
        if (item == default)
        {
            entityInfo = default;
            return false;
        }

        entityInfo = item;
        return true;
    }

    /// <summary>
    ///     Converts the passed id to its inner and outer index ( or slot ) inside the <see cref="_entityInfos"/> array.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="outerIndex">The outer index.</param>
    /// <param name="innerIndex">The inner index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IdToSlot(int id, out int outerIndex, out int innerIndex)
    {
        Debug.Assert(id >= 0, "Id cannot be negative.");

        outerIndex = id / _chunkSize;
        /* Instead of the '%' operator we can use logical '&' operator which is faster. But it requires the chunk size to be a power of 2. */
        innerIndex = id & _chunkSizeMinusOne;
    }

    /// <summary>
    ///     Ensures the capacity of this <see cref="EntityInfoDictionary"/> and resizes it correctly.
    /// </summary>
    /// <param name="capacity">The new capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (capacity < _largestId)
        {
            return;
        }

        var currentSize = _entityInfos.Length;
        var desiredSize = (capacity / _chunkSize) + 1;

        Array.Resize(ref _entityInfos, desiredSize);

        // Create the new arrays.
        for (int i = currentSize; i < desiredSize; i++)
        {
            _entityInfos[i] = new EntityInfo[_chunkSize];
        }

        UpdateLargestId();
    }

    /// <summary>
    ///     Trims this <see cref="EntityInfoDictionary"/> and releases unused resources.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess()
    {
        var lastIndexWithNonDefaultValues = _entityInfos.Length - 1;
        for (var i = lastIndexWithNonDefaultValues; i >= 0; i--)
        {
            if (ArrayContainsNonDefaultValues(_entityInfos[i]))
            {
                break;
            }

            lastIndexWithNonDefaultValues = i - 1;
        }

        Array.Resize(ref _entityInfos, lastIndexWithNonDefaultValues + 1);
        UpdateLargestId();
    }

    /// <summary>
    ///     Clears this <see cref="EntityInfoDictionary"/> and sets all values to the default one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        Array.Clear(_entityInfos, 0, _entityInfos.Length);
    }

    /// <summary>
    ///     Updates the largest id.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLargestId()
    {
        _largestId = _entityInfos.Length * _chunkSize;
    }

    /// <summary>
    ///     Checks if the passed <see cref="EntityInfo"/> array contains set values.
    /// </summary>
    /// <param name="array">The <see cref="EntityInfo"/> array to check.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ArrayContainsNonDefaultValues(EntityInfo[] array)
    {
        foreach (var item in array)
        {
            if (item != default)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns a reference to a <see cref="EntityInfo"/> at an given index.
    /// </summary>
    /// <param name="id">The index.</param>
    public ref EntityInfo this[int id]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Debug.Assert(id >= 0, "Id cannot be negative");

            EnsureCapacity(id);
            IdToSlot(id, out var outerIndex, out var innerIndex);
            return ref _entityInfos[outerIndex][innerIndex];
        }
    }
}
