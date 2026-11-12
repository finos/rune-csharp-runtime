using System.Globalization;

namespace Finos.Rune.Runtime.Tests;

/// <summary>Shorthand so parsing in tests never depends on the machine's locale.</summary>
internal static class Culture
{
    internal static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
}
