using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Parser;

public enum CelTokenType
{
    None,
    Ident, Int, UInt, Double, String, Bytes, Bool, Null,
    Dot, LParen, RParen, LBracket, RBracket, LBrace, RBrace, Comma, Colon, Question,
    Plus, Minus, Mul, Div, Mod,
    Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual,
    And, Or, Not, In, Reserved,
    EOF
}

public record CelToken(CelTokenType Type, object? Value, int Position);

public class CelLexer
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
        tokens.Add(new CelToken(CelTokenType.EOF, null, _pos));
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
        return new CelToken(CelTokenType.String, sb.ToString(), start);
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
        return new CelToken(CelTokenType.String, sb.ToString(), start);
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
        return new CelToken(CelTokenType.String, sb.ToString(), start);
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
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start);
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
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start);
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
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start);
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
            "true" => new CelToken(CelTokenType.Bool, true, start),
            "false" => new CelToken(CelTokenType.Bool, false, start),
            "null" => new CelToken(CelTokenType.Null, null, start),
            "in" => new CelToken(CelTokenType.In, null, start),
            "as" or "break" or "const" or "continue" or "else" or "for" or "function" or "if" or
            "import" or "let" or "loop" or "package" or "namespace" or "return" or "var" or "void" or "while"
                => new CelToken(CelTokenType.Reserved, text, start),
            _ => new CelToken(CelTokenType.Ident, text, start)
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
                return new CelToken(CelTokenType.UInt, ulong.Parse(hexText, NumberStyles.HexNumber, CultureInfo.InvariantCulture), start);
            }
            return new CelToken(CelTokenType.Int, long.Parse(hexText, NumberStyles.HexNumber, CultureInfo.InvariantCulture), start);
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
             return new CelToken(CelTokenType.Dot, null, start);
        }

        if (_pos < _input.Length && (_input[_pos] == 'u' || _input[_pos] == 'U'))
        {
            _pos++;
            if (isDouble)
            {
                throw new CelParseException("Suffix 'u' or 'U' is not allowed on double literals", start);
            }
            return new CelToken(CelTokenType.UInt, ulong.Parse(text, CultureInfo.InvariantCulture), start);
        }

        if (isDouble)
        {
            return new CelToken(CelTokenType.Double, double.Parse(text, CultureInfo.InvariantCulture), start);
        }
        return new CelToken(CelTokenType.Int, long.Parse(text, CultureInfo.InvariantCulture), start);
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
        return new CelToken(CelTokenType.String, sb.ToString(), start);
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
        return new CelToken(CelTokenType.Bytes, bytes.ToArray(), start);
    }

    private static bool IsHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    private static bool IsOctalDigit(char c) => c >= '0' && c <= '7';

    private CelToken ReadOperatorOrPunctuator(int start)
    {
        char c = _input[_pos++];
        return c switch
        {
            '.' => new CelToken(CelTokenType.Dot, null, start),
            '(' => new CelToken(CelTokenType.LParen, null, start),
            ')' => new CelToken(CelTokenType.RParen, null, start),
            '[' => new CelToken(CelTokenType.LBracket, null, start),
            ']' => new CelToken(CelTokenType.RBracket, null, start),
            '{' => new CelToken(CelTokenType.LBrace, null, start),
            '}' => new CelToken(CelTokenType.RBrace, null, start),
            ',' => new CelToken(CelTokenType.Comma, null, start),
            ':' => new CelToken(CelTokenType.Colon, null, start),
            '?' => new CelToken(CelTokenType.Question, null, start),
            '+' => new CelToken(CelTokenType.Plus, null, start),
            '-' => new CelToken(CelTokenType.Minus, null, start),
            '*' => new CelToken(CelTokenType.Mul, null, start),
            '/' => new CelToken(CelTokenType.Div, null, start),
            '%' => new CelToken(CelTokenType.Mod, null, start),
            '!' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.NotEqual, start) : new CelToken(CelTokenType.Not, null, start),
            '=' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.Equal, start) : throw new CelParseException("Unexpected character '='", start),
            '<' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.LessEqual, start) : new CelToken(CelTokenType.Less, null, start),
            '>' => TryConsume('=') ? ConsumeAndReturn(CelTokenType.GreaterEqual, start) : new CelToken(CelTokenType.Greater, null, start),
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

    private CelToken ConsumeAndReturn(CelTokenType type, int start) => new CelToken(type, null, start);
}

public class CelParser
{
    private readonly List<CelToken> _tokens;
    private int _pos;

    private CelParser(List<CelToken> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    public static CelExpr Parse(string input)
    {
        var lexer = new CelLexer(input);
        var tokens = lexer.Tokenize();
        var parser = new CelParser(tokens);
        var expr = parser.ParseExpression();
        parser.Expect(CelTokenType.EOF);
        return expr;
    }

    private CelExpr ParseExpression() => ParseTernary();

    private CelExpr ParseTernary()
    {
        var cond = ParseOr();
        if (Match(CelTokenType.Question))
        {
            var left = ParseTernary();
            Expect(CelTokenType.Colon);
            var right = ParseTernary();
            return new CelCall("_?_:_", null, new[] { cond, left, right });
        }
        return cond;
    }

    private CelExpr ParseOr()
    {
        var left = ParseAnd();
        while (Match(CelTokenType.Or))
        {
            var right = ParseAnd();
            left = new CelCall("_||_", null, new[] { left, right });
        }
        return left;
    }

    private CelExpr ParseAnd()
    {
        var left = ParseEquality();
        while (Match(CelTokenType.And))
        {
            var right = ParseEquality();
            left = new CelCall("_&&_", null, new[] { left, right });
        }
        return left;
    }

    private CelExpr ParseEquality()
    {
        var left = ParseRelational();
        while (true)
        {
            if (Match(CelTokenType.Equal))
                left = new CelCall("_==_", null, new[] { left, ParseRelational() });
            else if (Match(CelTokenType.NotEqual))
                left = new CelCall("_!=_", null, new[] { left, ParseRelational() });
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
                left = new CelCall("_<_", null, new[] { left, ParseAdditive() });
            else if (Match(CelTokenType.LessEqual))
                left = new CelCall("_<=_", null, new[] { left, ParseAdditive() });
            else if (Match(CelTokenType.Greater))
                left = new CelCall("_>_", null, new[] { left, ParseAdditive() });
            else if (Match(CelTokenType.GreaterEqual))
                left = new CelCall("_>=_", null, new[] { left, ParseAdditive() });
            else if (Match(CelTokenType.In))
                left = new CelCall("@in", null, new[] { left, ParseAdditive() });
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
                left = new CelCall("_+_", null, new[] { left, ParseMultiplicative() });
            else if (Match(CelTokenType.Minus))
                left = new CelCall("_-_", null, new[] { left, ParseMultiplicative() });
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
                left = new CelCall("_*_", null, new[] { left, ParseUnary() });
            else if (Match(CelTokenType.Div))
                left = new CelCall("_/_", null, new[] { left, ParseUnary() });
            else if (Match(CelTokenType.Mod))
                left = new CelCall("_%_", null, new[] { left, ParseUnary() });
            else break;
        }
        return left;
    }

    private CelExpr ParseUnary()
    {
        if (Match(CelTokenType.Not))
            return new CelCall("!_", null, new[] { ParseUnary() });
        if (Match(CelTokenType.Minus))
            return new CelCall("-_", null, new[] { ParseUnary() });
        return ParsePrimary();
    }

    private CelExpr ParsePrimary()
    {
        CelExpr expr;
        var token = Consume();
        switch (token.Type)
        {
            case CelTokenType.Bool: expr = new CelConstant((bool)token.Value!); break;
            case CelTokenType.Int: expr = new CelConstant((long)token.Value!); break;
            case CelTokenType.UInt: expr = new CelConstant((ulong)token.Value!); break;
            case CelTokenType.Double: expr = new CelConstant((double)token.Value!); break;
            case CelTokenType.String: expr = new CelConstant((string)token.Value!); break;
            case CelTokenType.Bytes: expr = new CelConstant((byte[])token.Value!); break;
            case CelTokenType.Null: expr = new CelConstant(CelValue.Null); break;
            case CelTokenType.LBracket:
                expr = ParseList();
                break;
            case CelTokenType.LBrace:
                expr = ParseMap();
                break;
            case CelTokenType.Reserved:
                throw new CelParseException($"Reserved word '{token.Value}' cannot be used as an identifier", token.Position);
            case CelTokenType.Ident:
                string name = (string)token.Value!;
                if (Match(CelTokenType.LParen))
                {
                    var args = ParseArgs();
                    expr = new CelCall(name, null, args);
                }
                else
                {
                    expr = new CelIdent(name);
                }
                break;
            case CelTokenType.LParen:
                expr = ParseExpression();
                Expect(CelTokenType.RParen);
                break;
            default:
                throw new CelParseException($"Unexpected token {token.Type}", token.Position);
        }

        while (true)
        {
            if (Match(CelTokenType.Dot))
            {
                var fieldToken = Expect(CelTokenType.Ident);
                string fieldName = (string)fieldToken.Value!;
                if (Match(CelTokenType.LParen))
                {
                    var args = ParseArgs();
                    expr = new CelCall(fieldName, expr, args);
                }
                else
                {
                    expr = new CelSelect(expr, fieldName);
                }
            }
            else if (Match(CelTokenType.LBracket))
            {
                var indexExpr = ParseExpression();
                Expect(CelTokenType.RBracket);
                expr = new CelIndex(expr, indexExpr);
            }
            else break;
        }

        return expr;
    }

    private CelExpr ParseList()
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
        Expect(CelTokenType.RBracket);
        return new CelList(elements);
    }

    private CelExpr ParseMap()
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
        Expect(CelTokenType.RBrace);
        return new CelMap(entries);
    }

    private List<CelExpr> ParseArgs()
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
        Expect(CelTokenType.RParen);
        return args;
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
            throw new CelParseException($"Expected {type} but got {token.Type}", token.Position);
        return token;
    }

    private CelToken Consume() => _tokens[_pos++];

    private CelToken Peek() => _tokens[_pos];
}
