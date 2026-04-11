// Polyfill required for C# 9 records and init-only setters when targeting netstandard2.0.
// The compiler emits references to this type for 'record' and 'init' keywords;
// it is provided by the runtime in .NET 5+, but must be declared manually for netstandard2.0.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
