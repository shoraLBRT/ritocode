using System.Globalization;

namespace Expressions;

public static class Calculator
{
    public static decimal Evaluate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var position = 0;
        var value = Sum(expression, ref position);

        while (position < expression.Length && char.IsWhiteSpace(expression[position]))
        {
            position++;
        }

        if (position != expression.Length)
        {
            throw new FormatException($"Unexpected '{expression[position]}' at {position}.");
        }

        return value;
    }

    private static decimal Sum(string text, ref int position)
    {
        var left = Product(text, ref position);

        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position < text.Length && (text[position] == '+' || text[position] == '-'))
        {
            var op = text[position];
            position++;
            var right = Sum(text, ref position);

            return op == '+' ? left + right : left - right;
        }

        return left;
    }

    private static decimal Product(string text, ref int position)
    {
        var left = Atom(text, ref position);

        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position < text.Length && (text[position] == '*' || text[position] == '/'))
        {
            var op = text[position];
            position++;
            var right = Product(text, ref position);

            if (op == '/')
            {
                if (right == 0m)
                {
                    throw new DivideByZeroException("An expression divided by zero.");
                }

                return left / right;
            }

            return left * right;
        }

        return left;
    }

    private static decimal Atom(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position >= text.Length)
        {
            throw new FormatException("The expression ended where a number was expected.");
        }

        if (text[position] == '-')
        {
            position++;

            return -Atom(text, ref position);
        }

        if (text[position] == '(')
        {
            position++;
            var inner = Sum(text, ref position);

            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            if (position >= text.Length || text[position] != ')')
            {
                throw new FormatException("A '(' was never closed.");
            }

            position++;

            return inner;
        }

        var start = position;

        while (position < text.Length && (char.IsAsciiDigit(text[position]) || text[position] == '.'))
        {
            position++;
        }

        if (start == position)
        {
            throw new FormatException($"Expected a number at {position}.");
        }

        return decimal.Parse(text.AsSpan(start, position - start), CultureInfo.InvariantCulture);
    }
}
