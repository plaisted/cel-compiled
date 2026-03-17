using System;

namespace Cel.Compiled.Parser;

internal class CelParseException : Exception
{
    public int Position { get; }
    public int EndPosition { get; }

    public CelParseException(string message, int position, int? endPosition = null)
        : base(message)
    {
        Position = position;
        EndPosition = endPosition ?? position + 1;
    }
}
