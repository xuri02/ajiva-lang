using System.Collections.ObjectModel;
using System.Text;
using ajivac_lib.AST;

namespace ajivac_lib;

public interface ILexer
{
    Token CurrentToken { get; }

    string LastIdentifier { get; }

    string LastValue { get; }

    int GetTokenPrecedence();

    Token ReadNextToken();
}
public class Lexer : ILexer
{
    /// <inheritdoc />
    public Token CurrentToken { get; set; } 

    /// <inheritdoc />
    public string LastIdentifier { get; set; }

    /// <inheritdoc />
    public string LastValue { get; set; }

    /// <inheritdoc />
    public int GetTokenPrecedence()
    {
        if (CurrentToken is null) return -1;
        return _binOpPrecedence.TryGetValue(CurrentToken.Type, out var precedence) ? precedence : -1;
    }

    /// <inheritdoc />
    public Token ReadNextToken()
    {
        while (LanguageDefinition.IsWhiteSpace(ReadNextChar()))
        {
            _lastTokenPosition++;
        }

        if (LanguageDefinition.IsValidIdentifierBegin(_currentChar))
        {
            _buffer.Append(_currentChar);
            while (LanguageDefinition.IsValidIdentifierContinuation(PeekNextChar()))
                _buffer.Append(ReadNextChar());

            LastIdentifier = _buffer.ToString();
            _buffer.Clear();

            foreach (var (type, str) in LanguageDefinition.KeyWordMap)
            {
                if (string.Equals(LastIdentifier, str, StringComparison.OrdinalIgnoreCase))
                    return NextTokenFromLast(type);
            }

            return NextTokenFromLast(TokenType.Identifier);
        }

        if (LanguageDefinition.IsValidNumberBegin(_currentChar))
        {
            _buffer.Append(_currentChar);
            while (LanguageDefinition.IsValidNumberContinuation(PeekNextChar(), _buffer))
                _buffer.Append(ReadNextChar());

            LastValue = _buffer.ToString();
            _buffer.Clear();
            return NextTokenFromLast(TokenType.Value);
        }
        //todo char and string and bool

        if (LanguageDefinition.IsCommentBegin(_currentChar))
        {
            if (LanguageDefinition.IsSingleLineCommentBegin(_currentChar, PeekNextChar()))
            {
                while (!LanguageDefinition.IsLineBreak(_currentChar) && !EOF)
                {
                    ReadNextChar();
                }

                return ReadNextToken();
            }
            if (LanguageDefinition.IsMultiLineCommentBegin(_currentChar, PeekNextChar()))
            {
                while (!LanguageDefinition.IsMultiLineCommentEnd(_currentChar, PeekNextChar()) && !EOF)
                {
                    ReadNextChar();
                }
                return ReadNextToken();
            }
        }

        if (LanguageDefinition.IsAttributeChar(_currentChar))
        {
            return NextTokenFromLast(TokenType.Attribute);
        }

        if (LanguageDefinition.IsPreprocessorChar(_currentChar))
        {
            return NextTokenFromLast(TokenType.Preprocessor);
        }

        if (LanguageDefinition.TryGetSpecialCharToken(_currentChar, PeekNextChar(), out var op, out var length))
        {
            while (length > 1)
            {
                ReadNextChar();
                length--;
            }
            return NextTokenFromLast(op);
        }

        eof:
        if (EOF)
        {
            return NextTokenFromLast(TokenType.EOF);
        }

        return NextTokenFromLast(TokenType.Unknown);
    }

    private readonly StringBuilder _buffer = new StringBuilder();
    private char _currentChar;
    private uint _currentPosition;
    private uint _lastTokenPosition;
    private bool EOF;
    private readonly TextReader _reader;
    private readonly string _text;
    private readonly Dictionary<TokenType, int> _binOpPrecedence;

    private char ReadNextChar()
    {
        var read = _reader.Read();
        _currentPosition++;

        if (read == -1)
        {
            EOF = true;
            return default;
        }

        return _currentChar = (char)read;
    }

    private char PeekNextChar()
    {
        return (char)_reader.Peek();
    }

    public Lexer(string text, Dictionary<TokenType, int> binOpPrecedence)
    {
        _reader = new StringReader(text);
        _text = text;
        _binOpPrecedence = binOpPrecedence;
    }

    private Token NextTokenFromLast(TokenType type)
    {
        CurrentToken = new Token(type, new SourceSpan {
            Position = _lastTokenPosition,
            Length = _currentPosition - _lastTokenPosition,
            Source = _text
        });
        _lastTokenPosition = _currentPosition;
        return CurrentToken;
    }
}
public class LanguageDefinition
{
    public static bool IsValidIdentifierBegin(char c)
    {
        return char.IsLetter(c) || c == '_';
    }

    public static bool IsValidIdentifierContinuation(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    public static bool IsWhiteSpace(char c)
    {
        return c is ' ' or '\t' or '\n' or '\r';
    }

    public static IDictionary<TokenType, string> KeyWordMap = new ReadOnlyDictionary<TokenType, string>(
        new Dictionary<TokenType, string>() {
            [TokenType.I32] = "i32",
            [TokenType.U32] = "u32",
            [TokenType.I64] = "i64",
            [TokenType.U64] = "u64",
            [TokenType.F32] = "f32",
            [TokenType.F64] = "f64",

            [TokenType.Chr] = "chr",
            [TokenType.Str] = "str",
            [TokenType.Bit] = "bit",

            [TokenType.If] = "if",
            [TokenType.Else] = "else",

            [TokenType.For] = "for",
            [TokenType.While] = "while",
            [TokenType.Break] = "break",
            [TokenType.Continue] = "continue",

            [TokenType.Fn] = "fn",
            [TokenType.Return] = "return",

            [TokenType.Void] = "void",
            [TokenType.True] = "true",
            [TokenType.False] = "false",
            [TokenType.Null] = "null",
        }
    );

    public static bool IsValidNumberBegin(char c)
    {
        return char.IsDigit(c) || c is '.';
    }

    public static bool IsValidNumberContinuation(char c, StringBuilder builder)
    {
        if (c != '.') return char.IsDigit(c);
        return !builder.ToString().Contains('.');
    }

    public static bool IsCommentBegin(char c)
    {
        return c is '/';
    }

    public static bool IsSingleLineCommentBegin(char c, char next)
    {
        return c == '/' && next == '/';
    }

    public static bool IsMultiLineCommentBegin(char c, char next)
    {
        return c is '/' && next is '*';
    }

    public static bool IsMultiLineCommentEnd(char c, char next)
    {
        return c is '*' && next is '/';
    }

    public static bool IsSingleSpecialControlChar(char c)
    {
        return c is '+' or '+' or '+' or '-' or '-' or '-' or '*' or '*' or '/' or '/' or '%' or '%' or '^' or '^' or '^' or '&' or '&' or '&' or '|' or '|' or '|' or '!' or '!' or '=' or '=' or '<' or '<' or '<' or '>' or '>' or '>' or '?' or ':' or '~' or '.' or ',' or ';' or '(' or ')' or '{' or '}' or '[' or ']';
    }

    public static bool TryGetSpecialCharToken(char c, char next, out TokenType type, out int length)
    {
        if (IsSingleSpecialControlChar(c))
        {
            if (IsSingleSpecialControlChar(next))
            {
                type = c switch {
                    '+' when next is '+' => TokenType.Increment,
                    '+' when next is '=' => TokenType.AddAssign,
                    '-' when next is '-' => TokenType.Decrement,
                    '-' when next is '=' => TokenType.SubAssign,
                    '*' when next is '=' => TokenType.MulAssign,
                    '/' when next is '=' => TokenType.DivAssign,
                    '%' when next is '=' => TokenType.ModAssign,
                    '^' when next is '=' => TokenType.XorAssign,
                    '^' when next is '^' => TokenType.Xor,
                    '&' when next is '=' => TokenType.AndAssign,
                    '&' when next is '&' => TokenType.And,
                    '|' when next is '=' => TokenType.OrAssign,
                    '|' when next is '|' => TokenType.Or,
                    '!' when next is '=' => TokenType.NotEqual,
                    '=' when next is '=' => TokenType.DoubleEquals,
                    '<' when next is '=' => TokenType.LessEqual,
                    '<' when next is '<' => TokenType.ShiftLeft,
                    '>' when next is '=' => TokenType.GreaterEqual,
                    '>' when next is '>' => TokenType.ShiftRight,
                    _ => TokenType.Unknown
                };
                length = 2;
                if (type != TokenType.Unknown)
                    return true;
            }

            length = 1;
            type = c switch {
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '%' => TokenType.Percent,
                '^' => TokenType.BitXor,
                '&' => TokenType.BitAnd,
                '|' => TokenType.BitOr,
                '!' => TokenType.Not,
                '=' => TokenType.Assign,
                '<' => TokenType.Less,
                '>' => TokenType.Greater,
                '?' => TokenType.Question,
                ':' => TokenType.Colon,
                '~' => TokenType.Negate,
                '.' => TokenType.Dot,
                ',' => TokenType.Comma,
                ';' => TokenType.Semicolon,
                '(' => TokenType.LParen,
                ')' => TokenType.RParen,
                '{' => TokenType.LBrace,
                '}' => TokenType.RBrace,
                '[' => TokenType.LBracket,
                ']' => TokenType.RBracket,
                _ => TokenType.Unknown
            };
            return type != TokenType.Unknown;
        }
        type = TokenType.Unknown;
        length = 0;
        return false;
    }

    public static bool IsLineBreak(char c)
    {
        return c is '\n' or '\r';
    }

    public static bool IsAttributeChar(char c)
    {
        return c is '@';
    }

    public static bool IsPreprocessorChar(char c)
    {
        return c is '#';
    }

    public static bool IsOpenParenthesis(TokenType type)
    {
        return type is TokenType.LParen or TokenType.LBrace or TokenType.LBracket;
    }

    public static bool IsValidType(TokenType type)
    {
        return type is TokenType.I32 or TokenType.I64 or TokenType.U32 or TokenType.U64 or TokenType.F32 or TokenType.F64 or TokenType.Chr or TokenType.Str or TokenType.Bit;
    }

    public static bool TryGetBuildInType(TokenType type, out BuildInType buildInType)
    {
        buildInType = type switch {
            TokenType.I32 => BuildInType.I32,
            TokenType.I64 => BuildInType.I64,
            TokenType.U32 => BuildInType.U32,
            TokenType.U64 => BuildInType.U64,
            TokenType.F32 => BuildInType.F32,
            TokenType.F64 => BuildInType.F64,
            TokenType.Chr => BuildInType.Chr,
            TokenType.Str => BuildInType.Str,
            TokenType.Bit => BuildInType.Bit,
            TokenType.Void => BuildInType.Void,
            _ => BuildInType.Unknown
        };
        return buildInType != BuildInType.Unknown;
    }
}
