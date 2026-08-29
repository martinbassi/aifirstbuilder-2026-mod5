namespace Paretto.Api.Tests;

/// <summary>
/// Bloque de verificación (Block 3) de FEAT-009.
///
/// Verifica de forma automatizada — no solo a mano, como se había hecho antes en VERIFY — que
/// `docs/adr/adr-005-nearby-murals-haversine-sin-geography.md` quedó reflejando la decisión revisada
/// (Option B, `geography` + NetTopologySuite) y no solo la decisión original (Option A). Valida
/// AC-08/FR-08 de `docs/daw/prd/prd-FEAT-009.md` y la sección "Required tests" del Block 3 de
/// `docs/daw/specs/spec-FEAT-009.md`, que pedía exactamente este chequeo como test versionado.
/// </summary>
public class AdrDocumentationTests
{
    private const string RelativeAdrPath = "docs/adr/adr-005-nearby-murals-haversine-sin-geography.md";

    [Fact]
    public void Adr005_reflects_the_revised_decision_to_adopt_geography()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var adrPath = Path.Combine(repoRoot, "docs", "adr", "adr-005-nearby-murals-haversine-sin-geography.md");

        if (!File.Exists(adrPath))
        {
            Assert.Fail(
                $"Expected to find the ADR at '{adrPath}' (resolved from repo root '{repoRoot}'), " +
                $"but it does not exist. Verify that '{RelativeAdrPath}' still exists relative to the repo root.");
        }

        var content = File.ReadAllText(adrPath);

        var optionBOccurrences = CountOccurrences(content, "Option B");
        Assert.True(
            optionBOccurrences >= 1,
            $"Expected at least 1 occurrence of 'Option B' in '{adrPath}', found {optionBOccurrences}.");

        var statusLine = content
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("| Status |", StringComparison.Ordinal));

        Assert.False(
            string.IsNullOrEmpty(statusLine),
            $"Expected a '| Status |' row in the header table of '{adrPath}', but none was found.");

        Assert.Contains("revisado", statusLine, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string content, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += term.Length;
        }

        return count;
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "docs", "adr"))
                && Directory.Exists(Path.Combine(current.FullName, "backend")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repo root (a directory containing both 'docs/adr' and 'backend') walking up from '{startDirectory}'.");
    }
}
