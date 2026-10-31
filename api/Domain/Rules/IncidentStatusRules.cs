using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Rules;

public static class IncidentStatusRules
{
    // Tabel: dari status X, boleh pindah ke status mana saja.
    private static readonly IReadOnlyDictionary<IncidentStatus, IncidentStatus[]> Allowed =
        new Dictionary<IncidentStatus, IncidentStatus[]>
        {
            [IncidentStatus.Open] = new[] { IncidentStatus.Investigating, IncidentStatus.Resolved },
            [IncidentStatus.Investigating] = new[] { IncidentStatus.Resolved },
            [IncidentStatus.Resolved] = new[] { IncidentStatus.Closed },
            [IncidentStatus.Closed] = Array.Empty<IncidentStatus>()
        };

    // daftar status tujuan yang sah dari status saat ini
    public static IReadOnlyList<IncidentStatus> GetNextStatuses(IncidentStatus from) =>
        Allowed.TryGetValue(from, out var next) ? next : Array.Empty<IncidentStatus>();

    // apakah perpindahan from → to diizinkan?
    public static bool CanTransition(IncidentStatus from, IncidentStatus to) =>
        GetNextStatuses(from).Contains(to);
}