using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Parser;

internal enum CelTokenType
{
    None,
    Ident, Int, UInt, Double, String, Bytes, Bool, Null,
    Dot, LParen, RParen, LBracket, RBracket, LBrace, RBrace, Comma, Colon, Question,
    Plus, Minus, Mul, Div, Mod,
    Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual,
    And, Or, Not, In, Reserved,
    EOF
}

internal record CelToken(CelTokenType Type, object? Value, int Position, int EndPosition);

internal class CelLexer
{
    private readonly string _input;
    private int _pos;

    public CelLexer(string input)
    {
        _input = input;
        _pos = 0;
    }

    public List<CelToken> Tokenize()
    {
        var tokens = new List<CelToken>();
        while (_pos < _input.Length)
        {
            char c = _input[_pos];
            if (char.IsWhiteSpace(c))
            {
                _pos++;
                continue;
            }

            int start = _pos;

            if (c == '/' && _pos + 1 < _input.Length && _input[_pos + 1] == '/')
            {
                while (_pos < _input.Length && _input[_pos] != '\n')
                {
                    _pos++;
                }
                continue;
            }

            // Check for string/bytes prefixes: r, b, rb, br (case-insensitive)
            bool isRaw = false;
            bool isBytes = false;

            if ((c == 'r' || c == 'R' || c == 'b' || c == 'B') && _pos + 1 < _input.Length)
            {
                char next = _input[_pos + 1];
                if ((c == 'r' || c == 'R') && (next == '"' || next == '\''))
                {
                    isRaw = true;
                    _pos++; // skip 'r'
                }
                else if ((c == 'b' || c == 'B') && (next == '"' || next == '\''))
                {
                    isBytes = true;
                    _pos++; // skip 'b'
                }
                else if ((c == 'r' || c == 'R') && (next == 'b' || next == 'B') && _pos + 2 < _input.Length && (_input[_pos + 2] == '"' || _input[_pos + 2] == '\''))
                {
                    isRaw = true;
                    isBytes = true;
                    _pos += 2; // skip 'rb'
                }
                else if ((c == 'b' || c == 'B') && (next == 'r' || next == 'R') && _pos + 2 < _input.Length && (_input[_pos + 2] == '"' || _input[_pos + 2] == '\''))
                {
                    isRaw = true;
                    isBytes = true;
                    _pos += 2; // skip 'br'
                }
            }

            if (isRaw || isBytes)
            {
                // _pos now points to the opening quote character
                char q = _input[_pos];
                bool isTriple = _pos + 2 < _input.Length && _input[_pos + 1] == q && _input[_pos + 2] == q;

                if (isRaw && isBytes)
                {
                    tokens.Add(isTriple ? ReadTripleQuotedRawBytes(start, q) : ReadRawBytes(start, q));
                }
                else if (isRaw)
                {
                    tokens.Add(isTriple ? ReadTripleQuotedRawString(start, q) : ReadRawString(start, q));
                }
                else // isBytes only
                {
                    tokens.Add(isTriple ? ReadTripleQuotedBytes(start, q) : ReadBytes(start, q));
                }
            }
            else if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentOrKeyword(start));
            }
            else if (char.IsDigit(c) || (c == '.' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumber(start));
            }
            else if (c == '"' || c == '\'')
            {
                if (_pos + 2 < _input.Length && _input[_pos + 1] == c && _input[_pos + 2] == c)
                {
                    tokens.Add(ReadTripleQuotedString(start, c));
                }
                else
                {
                    tokens.Add(ReadString(start, c));
                }
            }
            else
            {
                tokens.Add(ReadOperatorOrPunctuator(start));
            }
        }
        tokens.Add(new CelToken(CelTokenType.EOF, null, _pos, _pos));
        return tokens;
    }

    private CelToken ReadTripleQuotedString(int start, char quote)
    {
        _pos += 3; // skip opening quotes
        var sb = new StringBuilder();
        while (_pos + 2 < _input.Length && !(_input[_pos] == quote && _input[_pos + 1] == quote && _input[_pos + 2] == quote))
        {
            if (_input[_pos] == '\\')
            {
                _pos++;
                if (_pos < _input.Length)
                {
                    sb.Append(_input[_pos] switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        _ => _input[_pos]
                    });
                }
            }
            else
            {
                sb.Append(_input[_pos]);
            }
            _pos++;
        }
        if (_pos + 2 >= _input.Length)
        {
            throw new CelParseException("Unterminated triple-quoted string literal", start);
        }
        _pos += 3; // skip closing quotes
        return new CelToken(CelTokenType.String, sb.ToString(), start, _pos);
    }

    private CelToken ReadRawString(int start, char quote)
    {
        _pos++; // skip opening quote
        var sb = new StringBuilder();
        while (_pos < _input.Length && _input[_pos] != quote)
        {
            sb.Append(_input[_pos]);
            _pos++;
        }
        if (_pos >= _input.Length)
        {
            throw new CelParseException("Unterminated raw string literal", start);
        }
        _pos++; // skip closing quote
        return new CelToken(CelTokenType.String, sb.ToString(), start, _pos);
    }

    private CelToken ReadTripleQuotedRawString(int start, char quote)
    {
        _pos += 3; // skip opening triple quotes
        var sb = new StringBuilder();
        while (_pos + 2 < _input.Length && !(_input[_pos] == quote && _input[_pos + 1] == quote && _input[_pos + 2] == quote))
        {
            sb.Append(_input[_pos]);
            _pos++;
        }
        if (_pos + 2 >= _input.Length)
        {
            throw new CelParseException("Unterminated triple-quoted raw string literal", start);
        }
        _pos += 3; // skip closing triple quotes
        return new CelToken(CelTokenType.String, sb.ToString(), start, _pos);
    }

    private CelToken ReadRawBytes(int start, char quote)
    {
        _pos++; // skip opening quote
        var bytes = new List<byte>();
        while (_pos < _input.Length && _input[_pos] != quote)
        {
            // Encode each character as UTF-8
            var charBytes = Encoding.UTF8.GetBytes(_input, _pos, 1);
            bytes.AddRange(charBytes);
            _pos++;
        }
        if (_pos >= _input.Length)
        {
            throw new CelParseException("Unterminated raw bytes literal", start);
        }
        _pos++; // skip closing quote
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start, _pos);
    }

    private CelToken ReadTripleQuotedRawBytes(int start, char quote)
    {
        _pos += 3; // skip opening triple quotes
        var bytes = new List<byte>();
        while (_pos + 2 < _input.Length && !(_input[_pos] == quote && _input[_pos + 1] == quote && _input[_pos + 2] == quote))
        {
            var charBytes = Encoding.UTF8.GetBytes(_input, _pos, 1);
            bytes.AddRange(charBytes);
            _pos++;
        }
        if (_pos + 2 >= _input.Length)
        {
            throw new CelParseException("Unterminated triple-quoted raw bytes literal", start);
        }
        _pos += 3; // skip closing triple quotes
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start, _pos);
    }

    private CelToken ReadTripleQuotedBytes(int start, char quote)
    {
        _pos += 3; // skip opening triple quotes
        var bytes = new List<byte>();
        while (_pos + 2 < _input.Length && !(_input[_pos] == quote && _input[_pos + 1] == quote && _input[_pos + 2] == quote))
        {
            if (_input[_pos] == '\\')
            {
                _pos++;
                if (_pos >= _input.Length) throw new CelParseException("Unterminated bytes literal", start);
                char ec = _input[_pos];
                switch (ec)
                {
                    case 'n': bytes.Add((byte)'\n'); _pos++; break;
                    case 'r': bytes.Add((byte)'\r'); _pos++; break;
                    case 't': bytes.Add((byte)'\t'); _pos++; break;
                    case 'b': bytes.Add((byte)'\b'); _pos++; break;
                    case 'f': bytes.Add((byte)'\f'); _pos++; break;
                    case 'v': bytes.Add((byte)'\v'); _pos++; break;
                    case 'a': bytes.Add((byte)'\a'); _pos++; break;
                    case '\\': bytes.Add((byte)'\\'); _pos++; break;
                    case '\'': bytes.Add((byte)'\''); _pos++; break;
                    case '"': bytes.Add((byte)'"'); _pos++; break;
                    case '?': bytes.Add((byte)'?'); _pos++; break;
                    case 'x':
                    case 'X':
                        _pos++;
                        if (_pos + 1 < _input.Length && IsHexDigit(_input[_pos]) && IsHexDigit(_input[_pos + 1]))
                        {
                            string hex = _input.Substring(_pos, 2);
                            bytes.Add(Convert.ToByte(hex, 16));
                            _pos += 2;
                        }
                        else throw new CelParseException("Invalid hex escape sequence", _pos - 2);
                        break;
                    default:
                        if (ec >= '0' && ec <= '3' && _pos + 2 < _input.Length && IsOctalDigit(_input[_pos + 1]) && IsOctalDigit(_input[_pos + 2]))
                        {
                            string octal = _input.Substring(_pos, 3);
                            bytes.Add(Convert.ToByte(octal, 8));
                            _pos += 3;
                        }
                        else throw new CelParseException($"Unknown escape sequence \\{ec}", _pos);
                        break;
                }
            }
            else
            {
                var charBytes = Encoding.UTF8.GetBytes(_input, _pos, 1);
                bytes.AddRange(charBytes);
                _pos++;
            }
        }
        if (_pos + 2 >= _input.Length)
        {
            throw new CelParseException("Unterminated triple-quoted bytes literal", start);
        }
        _pos += 3; // skip closing triple quotes
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start, _pos);
    }

    private CelToken ReadIdentOrKeyword(int start)
    {
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
        {
            _pos++;
        }
        string text = _input[start.._pos];
        return text switch
        {
            "true" => new CelToken(CelTokenType.Bool, true, start, _pos),
            "false" => new CelToken(CelTokenType.Bool, false, start, _pos),
            "null" => new CelToken(CelTokenType.Null, null, start, _pos),
            "in" => new CelToken(CelTokenType.In, null, start, _pos),
            "as" or "break" or "const" or "continue" or "else" or "for" or "function" or "if" or
            "import" or "let" or "loop" or "package" or "namespace" or "return" or "var" or "void" or "while"
                => new CelToken(CelTokenType.Reserved, text, start, _pos),
            _ => new CelToken(CelTokenType.Ident, text, start, _pos)
        };
    }

    private CelToken ReadNumber(int start)
    {
        // Check for hex prefix: 0x or 0X
        if (_input[_pos] == '0' && _pos + 1 < _input.Length && (_input[_pos + 1] == 'x' || _input[_pos + 1] == 'X'))
        {
            _pos += 2; // skip "0x"
            int hexStart = _pos;
            while (_pos < _input.Length && IsHexDigit(_input[_pos]))
            {
                _pos++;
            }
            if (_pos == hexStart)
            {
                throw new CelParseException("Expected hexadecimal digits after '0x'", start);
            }
            string hexText = _input[hexStart.._pos];

            // Check for uint suffix
            if (_pos < _input.Length && (_input[_pos] == 'u' || _input[_pos] == 'U'))
            {
                _pos++;
                return new CelToken(CelTokenType.UInt, ulong.Parse(hexText, NumberStyles.HexNumber, CultureInfo.InvariantCulture), start, _pos);
            }
            return new CelToken(CelTokenType.Int, long.Parse(hexText, NumberStyles.HexNumber, CultureInfo.InvariantCulture), start, _pos);
        }

        bool isDouble = false;
        while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
        {
            if (_input[_pos] == '.')
            {
                if (isDouble) break;
                isDouble = true;
            }
            _pos++;
        }

        // A single dot is not a number
        if (_pos == start + 1 && _input[start] == '.')
        {
            // This case should be handled by Tokenize identifying the dot as a punctuator 
            // but we'll be safe here.
        }

        // Handle exponent: e/E [+-]? digits
        if (_pos < _input.Length && (_input[_pos] == 'e' || _input[_pos] == 'E'))
        {
            isDouble = true;
            _pos++;
            if (_pos < _input.Length && (_input[_pos] == '+' || _input[_pos] == '-'))
            {
                _pos++;
            }
            int exponentStart = _pos;
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
            {
                _pos++;
            }
            if (_pos == exponentStart)
            {
                throw new CelParseException("Expected digits after exponent indicator", start);
            }
        }

        string text = _input[start.._pos];

        if (text == ".")
        {
             // Fallback for when dot is not followed by a digit (already checked in Tokenize but for robustness)
             _pos = start + 1;
             return new CelToken(CelTokenType.Dot, null, start, _pos);
        }

        if (_pos < _input.Length && (_input[_pos] == 'u' || _input[_pos] == 'U'))
        {
            _pos++;
            if (isDouble)
            {
                throw new CelParseException("Suffix 'u' or 'U' is not allowed on double literals", start);
            }
            return new CelToken(CelTokenType.UInt, ulong.Parse(text, CultureInfo.InvariantCulture), start, _pos);
        }

        if (isDouble)
        {
            return new CelToken(CelTokenType.Double, double.Parse(text, CultureInfo.InvariantCulture), start, _pos);
        }
        return new CelToken(CelTokenType.Int, long.Parse(text, CultureInfo.InvariantCulture), start, _pos);
    }

    private CelToken ReadString(int start, char quote)
    {
        _pos++; // skip opening quote
        var sb = new StringBuilder();
        while (_pos < _input.Length && _input[_pos] != quote)
        {
            if (_input[_pos] == '\\')
            {
                _pos++;
                if (_pos < _input.Length)
                {
                    sb.Append(_input[_pos] switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        _ => _input[_pos]
                    });
                }
            }
            else
            {
                sb.Append(_input[_pos]);
            }
            _pos++;
        }
        if (_pos >= _input.Length)
        {
            throw new CelParseException("Unterminated string literal", start);
        }
        _pos++; // skip closing quote
        return new CelToken(CelTokenType.String, sb.ToString(), start, _pos);
    }

    private CelToken ReadBytes(int start, char quote)
    {
        _pos++; // skip opening quote
        var bytes = new List<byte>();
        while (_pos < _input.Length && _input[_pos] != quote)
        {
            if (_input[_pos] == '\\')
            {
                _pos++;
                if (_pos >= _input.Length) throw new CelParseException("Unterminated bytes literal", start);

                char c = _input[_pos];
                switch (c)
                {
                    case 'n': bytes.Add((byte)'\n'); _pos++; break;
                    case 'r': bytes.Add((byte)'\r'); _pos++; break;
                    case 't': bytes.Add((byte)'\t'); _pos++; break;
                    case 'b': bytes.Add((byte)'\b'); _pos++; break;
                    case 'f': bytes.Add((byte)'\f'); _pos++; break;
                    case 'v': bytes.Add((byte)'\v'); _pos++; break;
                    case 'a': bytes.Add((byte)'\a'); _pos++; break;
                    case '\\': bytes.Add((byte)'\\'); _pos++; break;
                    case '\'': bytes.Add((byte)'\''); _pos++; break;
                    case '"': bytes.Add((byte)'"'); _pos++; break;
                    case '?': bytes.Add((byte)'?'); _pos++; break;
                    case 'x':
                    case 'X':
                        _pos++;
                        if (_pos + 1 < _input.Length && IsHexDigit(_input[_pos]) && IsHexDigit(_input[_pos + 1]))
                        {
                            string hex = _input.Substring(_pos, 2);
                            bytes.Add(Convert.ToByte(hex, 16));
                            _pos += 2;
                        }
                        else
                        {
                            throw new CelParseException("Invalid hex escape sequence", _pos - 2);
                        }
                        break;
                    default:
                        if (c >= '0' && c <= '3')
                        {
                            if (_pos + 2 < _input.Length && IsOctalDigit(_input[_pos + 1]) && IsOctalDigit(_input[_pos + 2]))
                            {
                                string octal = _input.Substring(_pos, 3);
                                bytes.Add(Convert.ToByte(octal, 8));
                                _pos += 3;
                            }
                            else
                            {
                                throw new CelParseException("Invalid octal escape sequence", _pos);
                            }
                        }
                        else
                        {
                            throw new CelParseException($"Unknown escape sequence \\{c}", _pos);
                        }
                        break;
                }
            }
            else
            {
                var charBytes = Encoding.UTF8.GetBytes(_input, _pos, 1);
                bytes.AddRange(charBytes);
                _pos++;
            }
        }
        if (_pos >= _input.Length)
        {
            throw new CelParseException("Unterminated bytes literal", start);
        }
        _pos++; // skip closing quote
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start, _pos);
    }

    private static bool IsHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    private static bool IsOctalDigit(char c) => c >= '0' && c <= '7';

    private CelToken ReadOperatorOrPunctuator(int start)
    {
        char c = _input[_pos++];
        return c switch
        {
            '.' => new CelToken(CelTokenType.Dot, null, start, _pos),
            '(' => new CelToken(CelTokenType.LParen, null, start, _pos),
            ')' => new CelToken(CelTokenType.RParen, null, start, _pos),
            '[' => new CelToken(CelTokenType.LBracket, null, start, _pos),
            ']' => new CelToken(CelTokenType.RBracket, null, start, _pos),
            '{' => new CelToken(CelTokenType.LBrace, null, start, _pos),
            '}' => new CelToken(CelTokenType.RBrace, null, start, _pos),
            ',' => new CelToken(CelTokenType.Comma, null, start, _pos),
            ':' => new CelToken(CelTokenType.Colon, null, start, _pos),
            '?' => new CelToken(CelTokenType.Question, null, start, _pos),
            '+' => new CelToken(CelTokenType.Plus, null, start, _pos),
            '-' => new CelToken(CelTokenType.Minus, null, start, _pos),
            '*' => new CelToken(CelTokenType.Mul, null, start, _pos),
            '/' => new CelToken(CelTokenType.Div, null, start, _pos),
            '%' => new CelToken(CelTokenType.Mod, null, start, _pos),
            '!' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.NotEqual, start) : new CelToken(CelTokenType.Not, null, start, _pos),
            '=' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.Equal, start) : throw new CelParseException("Unexpected character '='", start),
            '<' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.LessEqual, start) : new CelToken(CelTokenType.Less, null, start, _pos),
            '>' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.GreaterEqual, start) : new CelToken(CelTokenType.Greater, null, start, _pos),
            '&' => TryConsume('&') ? ConsumeAndReturn(CelTokenType.And, start) : throw new CelParseException("Unexpected character '&'", start),
            '|' => TryConsume('|') ? ConsumeAndReturn(CelTokenType.Or, start) : throw new CelParseException("Unexpected character '|'", start),
            _ => throw new CelParseException($"Unexpected character '{c}'", start)
        };
    }

    private bool TryConsume(char expected)
    {
        if (_pos < _input.Length && _input[_pos] == expected)
        {
            _pos++;
            return true;
        }
        return false;
    }

    private CelToken ConsumeAndReturn(CelTokenType type, int start) => new CelToken(type, null, start, _pos);
}

internal class CelParser
{
    private readonly List<CelToken> _tokens;
    private readonly CelSourceMap _sourceMap;
    private int _pos;

    private CelParser(List<CelToken> tokens, string sourceText)
    {
        _tokens = tokens;
        _sourceMap = new CelSourceMap(sourceText);
        _pos = 0;
    }

    public static CelExpr Parse(string input)
    {
        var lexer = new CelLexer(input);
        var tokens = lexer.Tokenize();
        var parser = new CelParser(tokens, input);
        var expr = parser.ParseExpression();
        parser.Expect(CelTokenType.EOF);
        CelSourceMapRegistry.Attach(expr, parser._sourceMap);
        return expr;
    }

    private CelExpr ParseExpression() => ParseTernary();

    private CelExpr ParseTernary()
    {
        var cond = ParseOr();
        if (Match(CelTokenType.Question))
        {
            var left = ParseTernary();
            var colon = Expect(CelTokenType.Colon);
            var right = ParseTernary();
            return Track(new CelCall("_?_:_", null, new[] { cond, left, right }), GetStart(cond), GetEnd(right));
        }
        return cond;
    }

    private CelExpr ParseOr()
    {
        var left = ParseAnd();
        while (Match(CelTokenType.Or))
        {
            var right = ParseAnd();
            left = Track(new CelCall("_||_", null, new[] { left, right }), GetStart(left), GetEnd(right));
        }
        return left;
    }

    private CelExpr ParseAnd()
    {
        var left = ParseEquality();
        while (Match(CelTokenType.And))
        {
            var right = ParseEquality();
            left = Track(new CelCall("_&&_", null, new[] { left, right }), GetStart(left), GetEnd(right));
        }
        return left;
    }

    private CelExpr ParseEquality()
    {
        var left = ParseRelational();
        while (true)
        {
            if (Match(CelTokenType.Equal))
            {
                var right = ParseRelational();
                left = Track(new CelCall("_==_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.NotEqual))
            {
                var right = ParseRelational();
                left = Track(new CelCall("_!=_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else break;
        }
        return left;
    }

    private CelExpr ParseRelational()
    {
        var left = ParseAdditive();
        while (true)
        {
            if (Match(CelTokenType.Less))
            {
                var right = ParseAdditive();
                left = Track(new CelCall("_<_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.LessEqual))
            {
                var right = ParseAdditive();
                left = Track(new CelCall("_<=_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.Greater))
            {
                var right = ParseAdditive();
                left = Track(new CelCall("_>_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.GreaterEqual))
            {
                var right = ParseAdditive();
                left = Track(new CelCall("_>=_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.In))
            {
                var right = ParseAdditive();
                left = Track(new CelCall("@in", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else break;
        }
        return left;
    }

    private CelExpr ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (true)
        {
            if (Match(CelTokenType.Plus))
            {
                var right = ParseMultiplicative();
                left = Track(new CelCall("_+_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.Minus))
            {
                var right = ParseMultiplicative();
                left = Track(new CelCall("_-_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else break;
        }
        return left;
    }

    private CelExpr ParseMultiplicative()
    {
        var left = ParseUnary();
        while (true)
        {
            if (Match(CelTokenType.Mul))
            {
                var right = ParseUnary();
                left = Track(new CelCall("_*_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.Div))
            {
                var right = ParseUnary();
                left = Track(new CelCall("_/_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else if (Match(CelTokenType.Mod))
            {
                var right = ParseUnary();
                left = Track(new CelCall("_%_", null, new[] { left, right }), GetStart(left), GetEnd(right));
            }
            else break;
        }
        return left;
    }

    private CelExpr ParseUnary()
    {
        if (Peek().Type == CelTokenType.Not)
        {
            var opToken = Consume();
            var operand = ParseUnary();
            return Track(new CelCall("!_", null, new[] { operand }), opToken.Position, GetEnd(operand));
        }
        if (Peek().Type == CelTokenType.Minus)
        {
            var opToken = Consume();
            var operand = ParseUnary();
            return Track(new CelCall("-_", null, new[] { operand }), opToken.Position, GetEnd(operand));
        }
        return ParsePrimary();
    }

    private CelExpr ParsePrimary()
    {
        CelExpr expr;
        var token = Consume();
        switch (token.Type)
        {
            case CelTokenType.EOF when token.Position == 0:
                throw new CelParseException("Expression is empty", token.Position, token.EndPosition);
            case CelTokenType.EOF:
                throw new CelParseException("Unexpected end of input", token.Position, token.EndPosition);
            case CelTokenType.Bool: expr = Track(new CelConstant((bool)token.Value!), token.Position, token.EndPosition); break;
            case CelTokenType.Int: expr = Track(new CelConstant((long)token.Value!), token.Position, token.EndPosition); break;
            case CelTokenType.UInt: expr = Track(new CelConstant((ulong)token.Value!), token.Position, token.EndPosition); break;
            case CelTokenType.Double: expr = Track(new CelConstant((double)token.Value!), token.Position, token.EndPosition); break;
            case CelTokenType.String: expr = Track(new CelConstant((string)token.Value!), token.Position, token.EndPosition); break;
            case CelTokenType.Bytes: expr = Track(new CelConstant((byte[])token.Value!), token.Position, token.EndPosition); break;
            case CelTokenType.Null: expr = Track(new CelConstant(CelValue.Null), token.Position, token.EndPosition); break;
            case CelTokenType.LBracket:
                expr = ParseList(token);
                break;
            case CelTokenType.LBrace:
                expr = ParseMap(token);
                break;
            case CelTokenType.Reserved:
                throw new CelParseException($"Reserved word '{token.Value}' cannot be used as an identifier", token.Position, token.EndPosition);
            case CelTokenType.Ident:
                string name = (string)token.Value!;
                if (Match(CelTokenType.LParen))
                {
                    var (args, endToken) = ParseArgs();
                    expr = Track(new CelCall(name, null, args), token.Position, endToken.EndPosition);
                }
                else
                {
                    expr = Track(new CelIdent(name), token.Position, token.EndPosition);
                }
                break;
            case CelTokenType.LParen:
                expr = ParseExpression();
                Expect(CelTokenType.RParen);
                break;
            default:
                throw new CelParseException($"Unexpected token {DescribeActualToken(token)}", token.Position, token.EndPosition);
        }

        while (true)
        {
            if (Match(CelTokenType.Dot))
            {
                var isOptional = Match(CelTokenType.Question);
                var fieldToken = Expect(CelTokenType.Ident);
                string fieldName = (string)fieldToken.Value!;
                if (Match(CelTokenType.LParen))
                {
                    if (isOptional)
                        throw new CelParseException("Optional-safe syntax is not supported for method calls.", fieldToken.Position, fieldToken.EndPosition);

                    var (args, endToken) = ParseArgs();
                    expr = Track(new CelCall(fieldName, expr, args), GetStart(expr), endToken.EndPosition);
                }
                else
                {
                    expr = Track(new CelSelect(expr, fieldName, isOptional), GetStart(expr), fieldToken.EndPosition);
                }
            }
            else if (Match(CelTokenType.LBracket))
            {
                var isOptional = Match(CelTokenType.Question);
                var indexExpr = ParseExpression();
                var closeBracket = Expect(CelTokenType.RBracket);
                expr = Track(new CelIndex(expr, indexExpr, isOptional), GetStart(expr), closeBracket.EndPosition);
            }
            else break;
        }

        return expr;
    }

    private CelExpr ParseList(CelToken openToken)
    {
        var elements = new List<CelExpr>();
        if (Peek().Type != CelTokenType.RBracket)
        {
            while (true)
            {
                elements.Add(ParseExpression());
                if (!Match(CelTokenType.Comma)) break;
                if (Peek().Type == CelTokenType.RBracket) break; // handle trailing comma
            }
        }
        var close = Expect(CelTokenType.RBracket);
        return Track(new CelList(elements), openToken.Position, close.EndPosition);
    }

    private CelExpr ParseMap(CelToken openToken)
    {
        var entries = new List<CelMapEntry>();
        if (Peek().Type != CelTokenType.RBrace)
        {
            while (true)
            {
                var key = ParseExpression();
                Expect(CelTokenType.Colon);
                var value = ParseExpression();
                entries.Add(new CelMapEntry(key, value));
                if (!Match(CelTokenType.Comma)) break;
                if (Peek().Type == CelTokenType.RBrace) break; // handle trailing comma
            }
        }
        var close = Expect(CelTokenType.RBrace);
        return Track(new CelMap(entries), openToken.Position, close.EndPosition);
    }

    private (List<CelExpr> Args, CelToken CloseToken) ParseArgs()
    {
        var args = new List<CelExpr>();
        if (Peek().Type != CelTokenType.RParen)
        {
            while (true)
            {
                args.Add(ParseExpression());
                if (!Match(CelTokenType.Comma)) break;
            }
        }
        var close = Expect(CelTokenType.RParen);
        return (args, close);
    }

    private bool Match(CelTokenType type)
    {
        if (Peek().Type == type)
        {
            _pos++;
            return true;
        }
        return false;
    }

    public CelToken Expect(CelTokenType type)
    {
        var token = Consume();
        if (token.Type != type)
            throw new CelParseException(BuildExpectedTokenMessage(type, token), token.Position, token.EndPosition);
        return token;
    }

    private static string BuildExpectedTokenMessage(CelTokenType expectedType, CelToken actualToken)
    {
        if (expectedType == CelTokenType.EOF)
        {
            return $"Unexpected {DescribeActualToken(actualToken)} after expression";
        }

        if (actualToken.Type == CelTokenType.EOF)
        {
            return $"Expected {DescribeExpectedToken(expectedType)} but reached end of input";
        }

        return $"Expected {DescribeExpectedToken(expectedType)} but got {DescribeActualToken(actualToken)}";
    }

    private static string DescribeExpectedToken(CelTokenType type) => type switch
    {
        CelTokenType.EOF => "end of input",
        _ => DescribeTokenType(type)
    };

    private static string DescribeActualToken(CelToken token) => token.Type switch
    {
        CelTokenType.Ident => "identifier",
        CelTokenType.Int => "integer literal",
        CelTokenType.UInt => "unsigned integer literal",
        CelTokenType.Double => "double literal",
        CelTokenType.String => "string literal",
        CelTokenType.Bytes => "bytes literal",
        CelTokenType.Bool => "boolean literal",
        CelTokenType.Null => "'null'",
        CelTokenType.EOF => "end of input",
        _ => DescribeTokenType(token.Type)
    };

    private static string DescribeTokenType(CelTokenType type) => type switch
    {
        CelTokenType.Dot => "'.'",
        CelTokenType.LParen => "'('",
        CelTokenType.RParen => "')'",
        CelTokenType.LBracket => "'['",
        CelTokenType.RBracket => "']'",
        CelTokenType.LBrace => "'{'",
        CelTokenType.RBrace => "'}'",
        CelTokenType.Comma => "','",
        CelTokenType.Colon => "':'",
        CelTokenType.Question => "'?'",
        CelTokenType.Plus => "'+'",
        CelTokenType.Minus => "'-'",
        CelTokenType.Mul => "'*'",
        CelTokenType.Div => "'/'",
        CelTokenType.Mod => "'%'",
        CelTokenType.Equal => "'=='",
        CelTokenType.NotEqual => "'!='",
        CelTokenType.Less => "'<'",
        CelTokenType.LessEqual => "'<='",
        CelTokenType.Greater => "'>'",
        CelTokenType.GreaterEqual => "'>='",
        CelTokenType.And => "'&&'",
        CelTokenType.Or => "'||'",
        CelTokenType.Not => "'!'",
        CelTokenType.In => "'in'",
        CelTokenType.Reserved => "reserved word",
        CelTokenType.None => "token",
        CelTokenType.Ident => "identifier",
        CelTokenType.Int => "integer literal",
        CelTokenType.UInt => "unsigned integer literal",
        CelTokenType.Double => "double literal",
        CelTokenType.String => "string literal",
        CelTokenType.Bytes => "bytes literal",
        CelTokenType.Bool => "boolean literal",
        CelTokenType.Null => "'null'",
        CelTokenType.EOF => "end of input",
        _ => type.ToString()
    };

    private CelToken Consume() => _tokens[_pos++];

    private CelToken Peek() => _tokens[_pos];

    private T Track<T>(T expr, int start, int end)
        where T : CelExpr
    {
        _sourceMap.Register(expr, start, end);
        return expr;
    }

    private int GetStart(CelExpr expr) => _sourceMap.TryGetSpan(expr, out var span) ? span.Start : 0;

    private int GetEnd(CelExpr expr) => _sourceMap.TryGetSpan(expr, out var span) ? span.End : 0;
}
