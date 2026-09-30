using Xunit;

namespace Expressions.Tests;

public sealed class CalculatorTests
{
    [Theory]
    [InlineData("7", 7)]
    [InlineData("2+3", 5)]
    [InlineData("2+3*4", 14)]
    [InlineData("2*3+4", 10)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("2*(3+4)-5", 9)]
    [InlineData("((1+2)*(3+4))", 21)]
    public void PrecedenceAndParentheses_AreHonoured(string expression, int expected)
    {
        Assert.Equal(expected, Calculator.Evaluate(expression));
    }

    [Theory]
    [InlineData("10-3-2", 5)]
    [InlineData("10-3+2", 9)]
    [InlineData("1-2-3-4", -8)]
    public void AdditionAndSubtraction_GroupLeftToRight(string expression, int expected)
    {
        Assert.Equal(expected, Calculator.Evaluate(expression));
    }

    [Theory]
    [InlineData("100/5/2", 10)]
    [InlineData("100/5*2", 40)]
    [InlineData("64/2/2/2", 8)]
    public void MultiplicationAndDivision_GroupLeftToRight(string expression, int expected)
    {
        Assert.Equal(expected, Calculator.Evaluate(expression));
    }

    [Theory]
    [InlineData("-3+5", 2)]
    [InlineData("2*-3", -6)]
    [InlineData("-(2+3)", -5)]
    [InlineData("10--3", 13)]
    public void UnaryMinus_IsAcceptedWhereverAnOperandMay(string expression, int expected)
    {
        Assert.Equal(expected, Calculator.Evaluate(expression));
    }

    [Fact]
    public void WhitespaceIsIgnored_IncludingAtTheEnds()
    {
        Assert.Equal(3m, Calculator.Evaluate(" 1 + 2 "));
        Assert.Equal(14m, Calculator.Evaluate("2 + 3 * 4"));
    }

    [Fact]
    public void TheArithmeticIsDecimal_NotBinaryFloatingPoint()
    {
        Assert.Equal(0.3m, Calculator.Evaluate("0.1+0.2"));
        Assert.Equal(3m, Calculator.Evaluate("1.5*2"));
    }

    [Fact]
    public void DivisionByZero_IsReportedAsSuch()
    {
        Assert.Throws<DivideByZeroException>(() => Calculator.Evaluate("1/0"));
        Assert.Throws<DivideByZeroException>(() => Calculator.Evaluate("1/(3-3)"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2+")]
    [InlineData("2+*3")]
    [InlineData("(1+2")]
    [InlineData("1+2)")]
    [InlineData("1 2")]
    [InlineData("two")]
    public void AnExpressionTheGrammarDoesNotAccept_IsRejected(string expression)
    {
        Assert.Throws<FormatException>(() => Calculator.Evaluate(expression));
    }

    [Fact]
    public void AnExpressionThatIsNotThere_IsRejected()
    {
        Assert.Throws<ArgumentNullException>("expression", () => Calculator.Evaluate(null!));
    }
}
