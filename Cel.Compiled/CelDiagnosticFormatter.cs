using System.Text;
using Cel.Compiled.Compiler;

namespace Cel.Compiled;

/// <summary>
/// Controls the presentation style used by <see cref="CelDiagnosticFormatter"/>.
/// </summary>
public enum CelDiagnosticStyle
{
    /// <summary>
    /// The default structured format with explicit "line N, column N" labels.
    /// </summary>
    Default,

    /// <summary>
    /// A compact CEL-style format matching the presentation used by cel-go,
    /// suitable for CLIs, logs, and developer-facing UI surfaces.
    /// <para>Example:</para>
    /// <code>
    /// ERROR: &lt;input&gt;:1:5: Unexpected token )
    ///  | 1 + )
    ///  | ....^
    /// </code>
    /// </summary>
    CelStyle
}

public static class CelDiagnosticFormatter
{
    /// <summary>
    /// Formats a CEL compilation or runtime exception into a human-readable diagnostic string
    /// using the <see cref="CelDiagnosticStyle.Default"/> style.
    /// </summary>
    public static string Format(Exception exception, string? sourceText = null) =>
        Format(exception, CelDiagnosticStyle.Default, sourceText);

    /// <summary>
    /// Formats a CEL compilation or runtime exception into a human-readable diagnostic string
    /// using the specified presentation style.
    /// </summary>
    public static string Format(Exception exception, CelDiagnosticStyle style, string? sourceText = null, string inputName = "<input>")
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            CelCompilationException compilation => FormatCore(compilation.Message, compilation.ErrorCode, compilation.ExpressionText ?? sourceText, compilation.SourceSpan, compilation.Line, compilation.Column, style, inputName),
            CelRuntimeException runtime => FormatCore(runtime.Message, runtime.ErrorCode, runtime.ExpressionText ?? sourceText, runtime.SourceSpan, runtime.Line, runtime.Column, style, inputName),
            _ => exception.Message
        };
    }

    private static string FormatCore(string message, string errorCode, string? sourceText, CelSourceSpan? span, int? line, int? column, CelDiagnosticStyle style, string inputName)
    {
        if (string.IsNullOrEmpty(sourceText) || span is null)
        {
            return style == CelDiagnosticStyle.CelStyle
                ? $"ERROR: {message}"
                : $"{errorCode}: {message}";
        }

        var resolved = line is null || column is null
            ? CelDiagnosticUtilities.GetLineColumn(sourceText, span.Value.Start)
            : default;
        var actualLine = line ?? resolved.Line;
        var actualColumn = column ?? resolved.Column;
        var snippet = CelDiagnosticUtilities.GetLineSnippet(sourceText, span.Value.Start);
        var caret = CelDiagnosticUtilities.BuildCaretLine(sourceText, span.Value);

        var builder = new StringBuilder();

        if (style == CelDiagnosticStyle.CelStyle)
        {
            builder.Append("ERROR: ")
                .Append(inputName)
                .Append(':')
                .Append(actualLine)
                .Append(':')
                .Append(actualColumn)
                .Append(": ")
                .Append(message)
                .AppendLine()
                .Append(" | ")
                .AppendLine(snippet)
                .Append(" | ")
                .Append(BuildCelStyleCaretLine(sourceText, span.Value));
        }
        else
        {
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
        }

        return builder.ToString();
    }

    private static string BuildCelStyleCaretLine(string sourceText, CelSourceSpan span)
    {
        var startColumn = CelDiagnosticUtilities.GetColumnWithinLine(sourceText, span.Start);
        var width = Math.Max(1, span.End - span.Start);
        return new string('.', Math.Max(0, startColumn - 1)) + new string('^', width);
    }
}
