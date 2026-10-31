using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Sla;

public sealed record SlaTargets(TimeSpan ResponseTarget, TimeSpan ResolutionTarget);

public sealed class SlaPolicy
{
    private readonly IReadOnlyDictionary<IncidentSeverity, SlaTargets> _targets;

    public SlaPolicy(IReadOnlyDictionary<IncidentSeverity, SlaTargets> targets)
    {
        foreach (var severity in Enum.GetValues<IncidentSeverity>())
        {
            if (!targets.ContainsKey(severity))
                throw new ArgumentException($"No SLA targets configured for severity {severity}.", nameof(targets));
        }

        _targets = targets;
    }

    public static SlaPolicy Default() => new(new Dictionary<IncidentSeverity, SlaTargets>
    {
        [IncidentSeverity.Critical] = new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(4)),
        [IncidentSeverity.High] = new(TimeSpan.FromHours(1), TimeSpan.FromHours(8)),
        [IncidentSeverity.Medium] = new(TimeSpan.FromHours(4), TimeSpan.FromHours(24)),
        [IncidentSeverity.Low] = new(TimeSpan.FromHours(8), TimeSpan.FromHours(72))
    });

    public SlaTargets GetTargets(IncidentSeverity severity) => _targets[severity];

    public (DateTimeOffset ResponseDueAtUtc, DateTimeOffset ResolutionDueAtUtc) CalculateDueDates(
        DateTimeOffset createdAtUtc, IncidentSeverity severity)
    {
        var targets = GetTargets(severity);
        return (createdAtUtc + targets.ResponseTarget, createdAtUtc + targets.ResolutionTarget);
    }
}