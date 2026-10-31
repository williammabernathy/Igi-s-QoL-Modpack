using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace IgiQoL.Core;

public static class PatchWarmup
{
    private const BindingFlags Members = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static void Prepare(Type t)
    {
        foreach (var m in t.GetMethods(Members))
        {
            if (m.IsAbstract || m.ContainsGenericParameters) continue;
            RuntimeHelpers.PrepareMethod(m.MethodHandle);
        }
        foreach (var c in t.GetConstructors(Members))
        {
            if (c.IsAbstract || c.ContainsGenericParameters) continue;
            RuntimeHelpers.PrepareMethod(c.MethodHandle);
        }
        foreach (var nested in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (nested.ContainsGenericParameters) continue;
            Prepare(nested);
        }
    }
}
