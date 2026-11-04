namespace ServiceScheduler.Domain.Dealerships;

/// <summary>
/// Declared in specialisation order, least specialised first (ADR-0002 #18): best fit spends the least
/// specialised compatible bay first and keeps <see cref="EV"/> for the jobs only it can do.
/// </summary>
public enum BayType
{
    Standard,
    Lift,
    Alignment,
    EV,
}
