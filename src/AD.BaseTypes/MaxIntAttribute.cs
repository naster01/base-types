﻿namespace AD.BaseTypes;

/// <summary>
/// Int with a maximal value.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MaxIntAttribute : Attribute, IStaticBaseTypeValidation<int>
{
#pragma warning disable IDE0060 // Remove unused parameter
    /// <param name="max">Maximal value.</param>
    public MaxIntAttribute(int max)
    { }
#pragma warning restore IDE0060 // Remove unused parameter

    /// <param name="value">The value to be validated.</param>
    /// <param name="max">Maximal value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The parameter <paramref name="value"/> is too large.</exception>
    public static void Validate(int value, int max) => IntValidation.Max(max, value);
}
