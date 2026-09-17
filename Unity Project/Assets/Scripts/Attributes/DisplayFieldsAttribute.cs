using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class DisplayFieldsAttribute : PropertyAttribute
{
    public readonly string[] fields;

    public DisplayFieldsAttribute(params string[] fields)
    {
        this.fields = fields;
    }
}
