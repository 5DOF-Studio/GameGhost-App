using System.Reflection;

namespace GaimerDesktop.Tests.Helpers;

/// <summary>
/// Invokes private static methods for testing internal logic.
/// </summary>
public static class ReflectionHelper
{
    public static T? InvokePrivateStatic<T>(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new MissingMethodException(type.FullName, methodName);

        return (T?)method.Invoke(null, args);
    }
}
