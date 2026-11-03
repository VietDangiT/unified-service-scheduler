using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Application.Fakes;

// Each save takes the next scripted error, as if a concurrent booking had already taken that resource; once the
// script runs out, saves succeed. Every appointment offered to the save is recorded, failed or not.
internal sealed class ScriptedAppointmentRepository(params DomainError[] failures) : IAppointmentRepository
{
    private readonly Queue<DomainError> _failures = new(failures);
    private readonly List<Appointment> _attempts = [];

    public IReadOnlyList<Appointment> Attempts => _attempts;

    public Task<Result<Appointment>> AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        _attempts.Add(appointment);

        return Task.FromResult(_failures.TryDequeue(out DomainError? failure)
            ? Result.Failure<Appointment>(failure)
            : Result.Success(appointment));
    }
}
