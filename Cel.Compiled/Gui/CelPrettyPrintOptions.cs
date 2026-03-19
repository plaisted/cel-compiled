namespace Cel.Compiled.Gui;

/// <summary>
/// Options for the CEL pretty printer.
/// </summary>
/// <param name="MaxWidth">The maximum width of a line before wrapping (default 100).</param>
/// <param name="IndentSize">The number of spaces per indent level (default 2).</param>
/// <param name="MaxParams">The number of parameters a function call has before folding assuming 
/// line wrapping does not trigger (default 2).</param>
/// <param name="MaxDepth">The number depth of object traversal (eg. field.one.two.three) until access 
/// fields are wrapped assuming line wrapping does not trigger (default 3).</param>
internal record CelPrettyPrintOptions(
    int MaxWidth = 100,
    int IndentSize = 2,
    int MaxParams = 2,
    int MaxDepth = 3
    );
