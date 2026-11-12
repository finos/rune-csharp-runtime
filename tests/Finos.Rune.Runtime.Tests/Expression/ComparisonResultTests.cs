using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

public class ComparisonResultTests
{
    [Fact]
    public void Factories_ForSuccessFailureAndEmpty_ProduceThreeDistinctStates()
    {
        Assert.True(ComparisonResult.Success().Get());
        Assert.False(ComparisonResult.Failure("nope").Get());
        Assert.Null(ComparisonResult.OfEmpty().Get());

        Assert.False(ComparisonResult.Success().IsEmptyOperand);
        Assert.False(ComparisonResult.Failure("nope").IsEmptyOperand);
        Assert.True(ComparisonResult.OfEmpty().IsEmptyOperand);
    }

    [Fact]
    public void GetOrDefault_AcrossTheThreeStates_SubstitutesOnlyForEmpty()
    {
        Assert.True(ComparisonResult.Success().GetOrDefault(false));
        Assert.False(ComparisonResult.Failure("nope").GetOrDefault(true));
        Assert.True(ComparisonResult.OfEmpty().GetOrDefault(true));
        Assert.False(ComparisonResult.OfEmpty().GetOrDefault(false));
    }

    [Fact]
    public void ResultCount_AcrossTheThreeStates_IsZeroOnlyForEmpty()
    {
        Assert.Equal(1, ComparisonResult.Success().ResultCount);
        Assert.Equal(1, ComparisonResult.Failure("nope").ResultCount);
        Assert.Equal(0, ComparisonResult.OfEmpty().ResultCount);
    }

    [Fact]
    public void OfNullSafe_ForNoValuesOrAllNulls_IsAnEmptyOperand()
    {
        Assert.True(ComparisonResult.OfNullSafe(new FakeMapper()).IsEmptyOperand);
        Assert.True(ComparisonResult.OfNullSafe(new FakeMapper(null, null)).IsEmptyOperand);
    }

    [Fact]
    public void OfNullSafe_WithNullsAmongTheValues_RequiresEveryNonNullToBeTrue()
    {
        Assert.True(ComparisonResult.OfNullSafe(new FakeMapper(true, null)).Get());
        Assert.True(ComparisonResult.OfNullSafe(new FakeMapper(true, true)).Get());
        Assert.False(ComparisonResult.OfNullSafe(new FakeMapper(true, false)).Get());
        Assert.False(ComparisonResult.OfNullSafe(new FakeMapper(false, null)).Get());
    }

    [Fact]
    public void AndNullSafe_AcrossEmptySideCombinations_IsEmptyOnlyWhenBothAreEmpty()
    {
        Assert.True(ComparisonResult.OfEmpty().AndNullSafe(ComparisonResult.OfEmpty()).IsEmptyOperand);
        Assert.False(ComparisonResult.OfEmpty().AndNullSafe(ComparisonResult.Success()).IsEmptyOperand);
        Assert.False(ComparisonResult.Success().AndNullSafe(ComparisonResult.OfEmpty()).IsEmptyOperand);
    }

    [Fact]
    public void AndNullSafe_WithATrueLeftAndAnEmptyRight_IsFalseAndNamesTheEmptySide()
    {
        ComparisonResult result = ComparisonResult.Success().AndNullSafe(ComparisonResult.OfEmpty());

        Assert.False(result.Get());
        Assert.Equal("right of `and` operation is empty", result.Error);
    }

    [Fact]
    public void AndNullSafe_WhenEitherOrBothSidesAreNotTrue_DescribesEachOfThem()
    {
        Assert.Equal(
            "left of `and` operation is empty",
            ComparisonResult.OfEmpty().AndNullSafe(ComparisonResult.Success()).Error);
        Assert.Equal(
            "right of `and` operation is empty",
            ComparisonResult.Success().AndNullSafe(ComparisonResult.OfEmpty()).Error);
        Assert.Equal(
            "left of `and` operation is false and right of `and` operation is false",
            ComparisonResult.Failure(null).AndNullSafe(ComparisonResult.Failure(null)).Error);
    }

    [Fact]
    public void AndNullSafe_WithANestedFalseResult_PropagatesItsErrorVerbatim()
    {
        ComparisonResult inner = ComparisonResult.OfEmpty().AndNullSafe(ComparisonResult.Success());
        ComparisonResult outer = ComparisonResult.OfEmpty().AndNullSafe(inner);

        Assert.False(outer.Get());
        Assert.Equal(
            "left of `and` operation is empty and left of `and` operation is empty", outer.Error);
    }

    [Fact]
    public void AndNullSafe_WhenBothSidesCarryTheirOwnError_JoinsThoseInsteadOfDescribingTheSides()
    {
        ComparisonResult result = ComparisonResult.Failure("left broke")
            .AndNullSafe(ComparisonResult.Failure("right broke"));

        Assert.False(result.Get());
        Assert.Equal("left broke and right broke", result.Error);
    }

    [Fact]
    public void AndNullSafe_WithTwoSuccesses_IsTrueWithAnEmptyError()
    {
        ComparisonResult result = ComparisonResult.Success().AndNullSafe(ComparisonResult.Success());

        Assert.True(result.Get());
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void OrNullSafe_AcrossEmptySideCombinations_IsEmptyOnlyWhenBothAreEmpty()
    {
        Assert.True(ComparisonResult.OfEmpty().OrNullSafe(ComparisonResult.OfEmpty()).IsEmptyOperand);
        Assert.False(ComparisonResult.OfEmpty().OrNullSafe(ComparisonResult.Failure("x")).IsEmptyOperand);
    }

    [Fact]
    public void OrNullSafe_WhenOneSideSucceeds_IsTrueWithANullError()
    {
        ComparisonResult result = ComparisonResult.Failure("left broke").OrNullSafe(ComparisonResult.Success());

        Assert.True(result.Get());
        Assert.Null(result.Error);
    }

    [Fact]
    public void OrNullSafe_WhenBothSidesFail_JoinsBothErrors()
    {
        ComparisonResult result = ComparisonResult.Failure("left broke")
            .OrNullSafe(ComparisonResult.Failure("right broke"));

        Assert.False(result.Get());
        Assert.Equal("left broke and right broke", result.Error);
    }

    [Fact]
    public void OrNullSafe_WhenAFailingSideCarriesNoError_RendersItAsTheTextNull()
    {
        ComparisonResult result = ComparisonResult.OfEmpty().OrNullSafe(ComparisonResult.OfEmpty()
            .OrNullSafe(ComparisonResult.Failure("only reason")));

        Assert.False(result.Get());
        Assert.Equal("null and null and only reason", result.Error);
    }

    private sealed class FakeMapper(params bool?[] values) : IMapper<bool?>
    {
        public bool? Get() => values.Length == 1 ? values[0] : null;

        public IReadOnlyList<bool?> GetMulti() => values;

        public int ResultCount => values.Count(v => v is not null);

        public IReadOnlyList<IMapperPath> Paths => [];

        public IReadOnlyList<IMapperPath> ErrorPaths => [];

        public IReadOnlyList<string?> Errors => [];
    }
}
