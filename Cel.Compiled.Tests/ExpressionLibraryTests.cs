using Cel.Compiled.Tests.Compat;

namespace Cel.Compiled.Tests;

public class ExpressionLibraryTests
{
    [Fact]
    public void SharedExpressionLibraryMatchesExpectedValues()
    {
        var library = CompatTestData.LoadExpressionLibrary();
        var run = CompatTestData.EvaluateWithCelCompiled(library);
        var failures = new List<string>();

        foreach (var expressionCase in library.Cases)
        {
            var result = run.Results.Single(entry => entry.Id == expressionCase.Id);
            if (expressionCase.ExpectedError != null)
            {
                if (result.Error == null)
                {
                    failures.Add($"{expressionCase.Id}: expected error '{expressionCase.ExpectedError.Category}' but evaluation returned a value.");
                    continue;
                }

                if (!string.Equals(expressionCase.ExpectedError.Category, result.Error.Category, StringComparison.Ordinal))
                {
                    failures.Add($"{expressionCase.Id}: expected error '{expressionCase.ExpectedError.Category}' but got '{result.Error.Category}'.");
                    continue;
                }

                if (expressionCase.ExpectedError.MessageContains != null &&
                    (result.Error.Message == null || !result.Error.Message.Contains(expressionCase.ExpectedError.MessageContains, StringComparison.Ordinal)))
                {
                    failures.Add($"{expressionCase.Id}: expected error message containing '{expressionCase.ExpectedError.MessageContains}' but got '{result.Error.Message}'.");
                }

                continue;
            }

            if (result.Error != null)
            {
                failures.Add($"{expressionCase.Id}: expected value but got error '{result.Error.Category}': {result.Error.Message}");
                continue;
            }

            if (expressionCase.Expected == null || result.Value == null)
            {
                failures.Add($"{expressionCase.Id}: expected and actual values must both be present.");
                continue;
            }

            if (!string.Equals(expressionCase.Expected.Type, result.Value.Type, StringComparison.Ordinal))
            {
                failures.Add($"{expressionCase.Id}: expected type '{expressionCase.Expected.Type}' but got '{result.Value.Type}'.");
                continue;
            }

            if (!string.Equals(expressionCase.Expected.ToCanonicalJson(), result.Value.ToCanonicalJson(), StringComparison.Ordinal))
            {
                failures.Add(
                    $"{expressionCase.Id}: expected {expressionCase.Expected.ToCanonicalJson()} but got {result.Value.ToCanonicalJson()} for expression `{expressionCase.Expression}`.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
