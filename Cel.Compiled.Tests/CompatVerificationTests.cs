using System.Text.Json;
using Cel.Compiled.Tests.Compat;
using Xunit;

namespace Cel.Compiled.Tests;

public class CompatVerificationTests
{
    [Fact]
    public void AllLibraryCasesMatchExpected()
    {
        var library = CompatTestData.LoadExpressionLibrary();
        var allowed = CompatTestData.LoadAllowedDivergences().AllowedDivergences.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var compiledResults = CompatTestData.EvaluateWithCelCompiled(library).Results.ToDictionary(result => result.Id, StringComparer.Ordinal);

        var failures = new List<string>();

        foreach (var expressionCase in library.Cases)
        {
            if (allowed.ContainsKey(expressionCase.Id))
                continue;

            var result = compiledResults[expressionCase.Id];

            if (expressionCase.ExpectedError != null)
            {
                if (result.Error == null)
                {
                    failures.Add($"{expressionCase.Id}: Expected error category '{expressionCase.ExpectedError.Category}', but got success value {FormatValue(result.Value)}");
                }
                else if (!string.Equals(result.Error.Category, expressionCase.ExpectedError.Category, StringComparison.Ordinal))
                {
                    failures.Add($"{expressionCase.Id}: Expected error category '{expressionCase.ExpectedError.Category}', but got '{result.Error.Category}' ({result.Error.Message})");
                }
                continue;
            }

            if (result.Error != null)
            {
                failures.Add($"{expressionCase.Id}: Expected success, but got error '{result.Error.Category}': {result.Error.Message}");
                continue;
            }

            if (result.Value == null)
            {
                failures.Add($"{expressionCase.Id}: Result value was null.");
                continue;
            }

            if (!string.Equals(result.Value.Type, expressionCase.Expected!.Type, StringComparison.Ordinal))
            {
                failures.Add($"{expressionCase.Id}: Expected type '{expressionCase.Expected.Type}', but got '{result.Value.Type}'");
                continue;
            }

            var actualJson = result.Value.ToCanonicalJson();
            var expectedJson = expressionCase.Expected.ToCanonicalJson();

            if (!string.Equals(actualJson, expectedJson, StringComparison.Ordinal))
            {
                failures.Add($"{expressionCase.Id}: Expected value {expectedJson}, but got {actualJson}");
            }
        }

        Assert.True(failures.Count == 0, $"Compatibility verification failed with {failures.Count} failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static string FormatValue(CompatValue? value)
    {
        return value?.ToCanonicalJson() ?? "null";
    }
}
