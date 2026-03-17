using System.Text;
using Cel.Compiled.Compiler;

namespace Cel.Compiled;

public static class CelDiagnosticFormatter
{
    public static string Format(Exception exception, string? sourceText = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            CelCompilationException compilation => FormatCore(compilation.Message, compilation.ErrorCode, compilation.ExpressionText ?? sourceText, compilation.SourceSpan, compilation.Line, compilation.Column),
            CelRuntimeException runtime => FormatCore(runtime.Message, runtime.ErrorCode, runtime.ExpressionText ?? sourceText, runtime.SourceSpan, runtime.Line, runtime.Column),
            _ => exception.Message
        };
    }

    private static string FormatCore(string message, string errorCode, string? sourceText, CelSourceSpan? span, int? line, int? column)
    {
        if (string.IsNullOrEmpty(sourceText) || span is null)
            return $"{errorCode}: {message}";

        var resolved = line is null || column is null
            ? CelDiagnosticUtilities.GetLineColumn(sourceText, span.Value.Start)
            : default;
        var actualLine = line ?? resolved.Line;
        var actualColumn = column ?? resolved.Column;
        var snippet = CelDiagnosticUtilities.GetLineSnippet(sourceText, span.Value.Start);
        var caret = CelDiagnosticUtilities.BuildCaretLine(sourceText, span.Value);

        var builder = new StringBuilder();
        builder.Append(errorCode)
            .Append(" at line ")
            .Append(actualLine)
            .Append(", column ")
            .Append(actualColumn)
            .Append(": ")
            .Append(message)
            .AppendLine()
            .AppendLine(snippet)
            .Append(caret);
        return builder.ToString();
    }
}
