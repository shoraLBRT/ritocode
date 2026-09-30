using System.Globalization;

namespace Expressions;

/// <summary>
/// Recursive descent over one cursor. Each grammar rule is a method, and every rule that takes more
/// than one operand gathers them in a loop — which is what makes the grouping left to right.
/// </summary>
public static class Calculator
{
    public static decimal Evaluate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var cursor = new Cursor(expression);
        var value = ReadSum(ref cursor);

        cursor.SkipWhitespace();

        if (!cursor.AtEnd)
        {
            throw new FormatException($"Unexpected '{cursor.Current}' at {cursor.Position}.");
        }

        return value;
    }

    private static decimal ReadSum(ref Cursor cursor)
    {
        var negated = cursor.TryTake('-');
        var value = ReadProduct(ref cursor);

        if (negated)
        {
            value = -value;
        }

        while (cursor.TryTakeEither('+', '-', out var op))
        {
            var right = ReadProduct(ref cursor);
            value = op == '+' ? value + right : value - right;
        }

        return value;
    }

    private static decimal ReadProduct(ref Cursor cursor)
    {
        var value = ReadFactor(ref cursor);

        while (cursor.TryTakeEither('*', '/', out var op))
        {
            var right = ReadFactor(ref cursor);

            if (op == '/' && right == 0m)
            {
                throw new DivideByZeroException("An expression divided by zero.");
            }

            value = op == '*' ? value * right : value / right;
        }

        return value;
    }

    private static decimal ReadFactor(ref Cursor cursor)
    {
        if (cursor.TryTake('('))
        {
            var value = ReadSum(ref cursor);

            if (!cursor.TryTake(')'))
            {
                throw new FormatException("A '(' was never closed.");
            }

            return value;
        }

        return cursor.TakeNumber();
    }

    /// <summary>
    /// A position in the expression. Whitespace is skipped here and nowhere else, so no rule has to
    /// remember to.
    /// </summary>
    private ref struct Cursor
    {
        private readonly string _text;

        public Cursor(string text)
        {
            _text = text;
            Position = 0;
        }

        public int Position { get; private set; }

        public bool AtEnd => Position >= _text.Length;

        public char Current => _text[Position];

        public void SkipWhitespace()
        {
            while (Position < _text.Length && char.IsWhiteSpace(_text[Position]))
            {
                Position++;
            }
        }

        public bool TryTake(char expected)
        {
            SkipWhitespace();

            if (AtEnd || Current != expected)
            {
                return false;
            }

            Position++;

            return true;
        }

        public bool TryTakeEither(char first, char second, out char taken)
        {
            SkipWhitespace();

            if (!AtEnd && (Current == first || Current == second))
            {
                taken = Current;
                Position++;

                return true;
            }

            taken = default;

            return false;
        }

        public decimal TakeNumber()
        {
            SkipWhitespace();

            var start = Position;

            while (!AtEnd && (char.IsAsciiDigit(Current) || Current == '.'))
            {
                Position++;
            }

            if (start == Position)
            {
                throw new FormatException($"Expected a number at {start}.");
            }

            return decimal.Parse(_text.AsSpan(start, Position - start), CultureInfo.InvariantCulture);
        }
    }
}
