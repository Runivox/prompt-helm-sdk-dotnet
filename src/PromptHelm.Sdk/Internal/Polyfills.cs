#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill so that <c>init</c>-only setters and C# 9 records compile on
/// target frameworks that do not ship this type (netstandard2.0).
/// </summary>
internal static class IsExternalInit
{
}
#endif
