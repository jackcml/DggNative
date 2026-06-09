using System;

namespace DggNative.Serialization;

[AttributeUsage(AttributeTargets.Property)]
public class JsonFlattenAttribute : Attribute { }