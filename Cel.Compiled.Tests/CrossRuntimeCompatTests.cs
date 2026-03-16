using System.Diagnostics;
using System.Text.Json;
using Cel.Compiled.Tests.Compat;

namespace Cel.Compiled.Tests;

[Trait("Category", "CrossRuntimeCompat")]
public class CrossRuntimeCompatTests
{
    [RequiresGoFact]
    public void CelGoHarnessProducesResultsJson()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"cel-go-output-{Guid.NewGuid():N}.json");
        try
        {
            RunGoHarness(outputPath);
            Assert.True(File.Exists(outputPath), "The cel-go harness did not produce an output file.");

            var output = JsonSerializer.Deserialize<CompatRunOutput>(File.ReadAllText(outputPath), CompatTestData.SerializerOptions);
            Assert.NotNull(output);
            Assert.NotEmpty(output!.Results);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [RequiresGoFact]
    public void CrossRuntimeComparisonOnlyAllowsDocumentedDivergences()
    {
        var library = CompatTestData.LoadExpressionLibrary();
        var allowed = CompatTestData.LoadAllowedDivergences().AllowedDivergences.ToDictionary(item => item.Id, item => item.Reason, StringComparer.Ordinal);
        var compiledResults = CompatTestData.EvaluateWithCelCompiled(library).Results.ToDictionary(result => result.Id, StringComparer.Ordinal);

        var outputPath = Path.Combine(Path.GetTempPath(), $"cel-go-output-{Guid.NewGuid():N}.json");
        try
        {
            RunGoHarness(outputPath);
            var goResults = JsonSerializer.Deserialize<CompatRunOutput>(File.ReadAllText(outputPath), CompatTestData.SerializerOptions)
                ?? throw new InvalidOperationException("Unable to read cel-go results.");

            var failures = new List<string>();
            var allowedDifferences = new List<string>();

            foreach (var expressionCase in library.Cases)
            {
                var compiled = compiledResults[expressionCase.Id];
                var goResult = goResults.Results.Single(result => result.Id == expressionCase.Id);

                var matches = ResultsMatch(compiled, goResult);
                if (matches)
                    continue;

                if (allowed.TryGetValue(expressionCase.Id, out var reason))
                {
                    allowedDifferences.Add($"{expressionCase.Id}: {reason}");
                    continue;
                }

                failures.Add(
                    $"{expressionCase.Id}: expression `{expressionCase.Expression}` diverged.{Environment.NewLine}" +
                    $"Cel.Compiled: {FormatResult(compiled)}{Environment.NewLine}" +
                    $"cel-go: {FormatResult(goResult)}{Environment.NewLine}" +
                    $"Expected: {(expressionCase.Expected != null ? expressionCase.Expected.ToCanonicalJson() : expressionCase.ExpectedError!.Category)}");
            }

            Assert.True(failures.Count == 0,
                string.Join(Environment.NewLine + Environment.NewLine, failures.Concat(
                    allowedDifferences.Count == 0
                        ? []
                        : [$"Allowed divergences:{Environment.NewLine}{string.Join(Environment.NewLine, allowedDifferences)}"])));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    private static void RunGoHarness(string outputPath)
    {
        var process = Process.Start(new ProcessStartInfo("go",
                $"run . --library \"{CompatTestData.ExpressionLibraryPath}\" --output \"{outputPath}\"")
        {
            WorkingDirectory = CompatTestData.CelGoHarnessDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Unable to start the cel-go harness process.");

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill();
            throw new TimeoutException("cel-go harness did not complete within 30 seconds.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"cel-go harness failed.{Environment.NewLine}stdout:{Environment.NewLine}{stdout.Result}{Environment.NewLine}stderr:{Environment.NewLine}{stderr.Result}");
        }
    }

    private static bool ResultsMatch(CompatCaseResult left, CompatCaseResult right)
    {
        if (left.Error != null || right.Error != null)
        {
            return string.Equals(left.Error?.Category, right.Error?.Category, StringComparison.Ordinal);
        }

        if (left.Value == null || right.Value == null)
            return false;

        return string.Equals(left.Value.Type, right.Value.Type, StringComparison.Ordinal) &&
               string.Equals(left.Value.ToCanonicalJson(), right.Value.ToCanonicalJson(), StringComparison.Ordinal);
    }

    private static string FormatResult(CompatCaseResult result)
    {
        return result.Error != null
            ? $"error[{result.Error.Category}]: {result.Error.Message}"
            : result.Value?.ToCanonicalJson() ?? "<null>";
    }
}

public sealed class RequiresGoFactAttribute : FactAttribute
{
    public RequiresGoFactAttribute()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CEL_GO_COMPAT_SKIP"), "1", StringComparison.Ordinal))
        {
            Skip = "Skipped by CEL_GO_COMPAT_SKIP=1.";
            return;
        }

        if (!IsGoAvailableOnPath())
            Skip = "Go is not installed; set CEL_GO_COMPAT_SKIP=1 to silence this skip.";
    }

    private static bool IsGoAvailableOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var executableName = OperatingSystem.IsWindows() ? "go.exe" : "go";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
                return true;
        }

        return false;
    }
}
