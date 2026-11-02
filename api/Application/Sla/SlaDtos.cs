using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Sla;

public sealed record SlaTargetDto(IncidentSeverity Severity, int ResponseMinutes, int ResolutionMinutes);

public sealed record SlaPolicyDto(IReadOnlyList<SlaTargetDto> Targets, string Note);