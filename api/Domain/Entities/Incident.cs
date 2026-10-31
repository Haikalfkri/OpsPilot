using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Rules;
using OpsPilot.Domain.Sla;

namespace OpsPilot.Domain.Entities;

public class Incident
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    public Guid CreatedById { get; set; }
    public Guid? AssigneeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? FirstRespondedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public DateTimeOffset ResponseDueAtUtc { get; set; }
    public DateTimeOffset ResolutionDueAtUtc { get; set; }

    public ICollection<IncidentComment> Comments { get; set; } = new List<IncidentComment>();
    public ICollection<IncidentActivity> Activities { get; set; } = new List<IncidentActivity>();
    public ICollection<IncidentSlaBreach> SlaBreaches { get; set; } = new List<IncidentSlaBreach>();
    public Postmortem? Postmortem { get; set; }

    public static Incident Create(
        Guid teamId, Guid createdById, string title, string description,
        IncidentSeverity severity, Guid? assigneeId, SlaPolicy sla, DateTimeOffset now)
    {
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            CreatedById = createdById,
            Title = title,
            Description = description,
            Severity = severity,
            Status = IncidentStatus.Open,
            AssigneeId = assigneeId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        incident.RecalculateSlaTargets(sla);
        return incident;
    }

    public void UpdateDetails(string title, string description, DateTimeOffset now)
    {
        EnsureNotClosed();

        Title = title;
        Description = description;
        UpdatedAtUtc = now;
    }

    public void ChangeSeverity(IncidentSeverity newSeverity, SlaPolicy sla, DateTimeOffset now)
    {
        EnsureNotClosed();

        Severity = newSeverity;
        RecalculateSlaTargets(sla);
        UpdatedAtUtc = now;
    }

    public void AssignTo(Guid? assigneeId, DateTimeOffset now)
    {
        EnsureNotClosed();

        AssigneeId = assigneeId;
        UpdatedAtUtc = now;
    }

    public void ChangeStatus(IncidentStatus newStatus, DateTimeOffset now)
    {
        if (!IncidentStatusRules.CanTransition(Status, newStatus))
            throw new DomainRuleException($"Cannot change status from {Status} to {newStatus}.");

        switch (newStatus)
        {
            case IncidentStatus.Investigating:
                // "??=" artinya: isi hanya kalau masih kosong (respons pertama dicatat sekali)
                FirstRespondedAtUtc ??= now;
                break;

            case IncidentStatus.Resolved:
                ResolvedAtUtc = now;
                break;

            case IncidentStatus.Closed:
                ClosedAtUtc = now;
                break;
        }

        Status = newStatus;
        UpdatedAtUtc = now;
    }

    private void RecalculateSlaTargets(SlaPolicy sla)
    {
        var (response, resolution) = sla.CalculateDueDates(CreatedAtUtc, Severity);
        ResponseDueAtUtc = response;
        ResolutionDueAtUtc = resolution;
    }

    private void EnsureNotClosed()
    {
        if (Status == IncidentStatus.Closed)
            throw new DomainRuleException("A closed incident cannot be modified.");
    }
}