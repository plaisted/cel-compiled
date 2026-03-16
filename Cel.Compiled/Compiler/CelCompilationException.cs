using System;

namespace Cel.Compiled.Compiler;

public class CelCompilationException : Exception
{
    public CelCompilationException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
