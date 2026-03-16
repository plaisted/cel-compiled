using System;

namespace Cel.Compiled.Parser;

public class CelParseException : Exception
{
    public int Position { get; }
    public CelParseException(string message, int position)
        : base(message) => Position = position;
}
