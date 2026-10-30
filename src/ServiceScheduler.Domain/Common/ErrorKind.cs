namespace ServiceScheduler.Domain.Common;

/// <summary>Category of an expected business failure; the API maps each kind to one HTTP status.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
}
