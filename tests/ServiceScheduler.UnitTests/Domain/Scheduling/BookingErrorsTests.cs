using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Domain.Scheduling;

public sealed class BookingErrorsTests
{
    // EC-84: the kind decides the HTTP status (ADR-0002 #10): Validation 422, Conflict 409, NotFound 404.
    // BeyondBookingHorizon (rule 5) is not built yet, so it has no row here.
    public static TheoryData<DomainError, ErrorKind> DocumentedKinds => new()
    {
        { BookingErrors.VehicleNotOwnedByCustomer, ErrorKind.Validation },
        { BookingErrors.ServiceNotOfferedAtDealership, ErrorKind.Validation },
        { BookingErrors.StartInPast, ErrorKind.Validation },
        { BookingErrors.StartNotOnGrid, ErrorKind.Validation },
        { BookingErrors.OutsideOpeningHours, ErrorKind.Validation },
        { BookingErrors.NoQualifiedTechnicianAvailable, ErrorKind.Conflict },
        { BookingErrors.NoCompatibleBayAvailable, ErrorKind.Conflict },
        { BookingErrors.VehicleAlreadyBooked, ErrorKind.Conflict },
        { BookingErrors.NotFound, ErrorKind.NotFound },
    };

    [Theory]
    [MemberData(nameof(DocumentedKinds))]
    public void Kind_OfEachBookingError_IsItsDocumentedKind(DomainError error, ErrorKind expected)
    {
        ErrorKind kind = error.Kind;

        Assert.Equal(expected, kind);
    }
}
