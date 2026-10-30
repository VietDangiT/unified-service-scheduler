namespace ServiceScheduler.Domain.Common;

/// <summary>An expected business failure. <see cref="Code"/> is stable and machine-readable.</summary>
public sealed record DomainError(string Code, string Message, ErrorKind Kind);
