using ServiceScheduler.UnitTests.Builders;

namespace ServiceScheduler.UnitTests.Domain.Dealerships;

// EC-64: a resource of another dealership is bad reference data, rejected when the aggregate is built
// (ADR-0002 #21), so it can never be counted by rule 3 or chosen by the allocator.
public sealed class DealershipTests
{
    [Fact]
    public void Constructor_WhenATechnicianBelongsToAnotherDealership_ThrowsArgumentException()
    {
        var builder = DealershipBuilder.Default()
            .WithTechnicians(
                TechnicianBuilder.Default().WithId(1).Build(),
                TechnicianBuilder.Default().WithId(2).AtAnotherDealership().Build());

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void Constructor_WhenABayBelongsToAnotherDealership_ThrowsArgumentException()
    {
        var builder = DealershipBuilder.Default()
            .WithBays(
                ServiceBayBuilder.Default().WithId(1).Build(),
                ServiceBayBuilder.Default().WithId(2).AtAnotherDealership().Build());

        Assert.Throws<ArgumentException>(() => builder.Build());
    }
}
