using System;

namespace Cel.Compiled.Parser;

internal class CelParseException : Exception
{
    public int Position { get; }
    public CelParseException(string message, int position)
        : base(message) => Position = position;
}
