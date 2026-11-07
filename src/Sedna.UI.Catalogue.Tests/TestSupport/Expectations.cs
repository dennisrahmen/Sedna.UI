using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// How long an expectation keeps trying before it fails: fifteen seconds, not Playwright's
/// five. An expectation still has to come true; on a loaded runner it is given the time a
/// loaded runner takes, rather than failing something that does happen.
/// </summary>
internal static class Expectations
{
    [ModuleInitializer]
    internal static void Patient() => Assertions.SetDefaultExpectTimeout(15_000);
}
