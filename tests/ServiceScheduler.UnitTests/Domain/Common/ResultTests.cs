using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.UnitTests.Domain.Common;

public sealed class ResultTests
{
    private static readonly DomainError SomeError = new("SomeError", "Something failed.", ErrorKind.Validation);

    [Fact]
    public void Success_ExposesValueAndNoError()
    {
        var result = Result.Success("booked");

        Assert.True(result.IsSuccess);
        Assert.Equal("booked", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ExposesErrorAndRefusesValue()
    {
        var result = Result.Failure<string>(SomeError);

        Assert.False(result.IsSuccess);
        Assert.Equal(SomeError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Success_WithNullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Fact]
    public void Failure_WithNullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure<string>(null!));
    }
}
