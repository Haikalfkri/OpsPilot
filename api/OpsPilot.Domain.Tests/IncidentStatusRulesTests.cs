using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Rules;
using Xunit;

namespace OpsPilot.Domain.Tests;

public class IncidentStatusRulesTests
{
    private static readonly (IncidentStatus From, IncidentStatus To)[] AllowedPairs =
    {
        (IncidentStatus.Open, IncidentStatus.Investigating),
        (IncidentStatus.Open, IncidentStatus.Resolved),
        (IncidentStatus.Investigating, IncidentStatus.Resolved),
        (IncidentStatus.Resolved, IncidentStatus.Closed)
    };

    [Fact]
    public void CanTransition_AllowsExactlyTheFourDocumentedTransitions_AndNothingElse()
    {
        // Mencoba SEMUA 16 pasangan (4 status x 4 status)
        foreach (var from in Enum.GetValues<IncidentStatus>())
        {
            foreach (var to in Enum.GetValues<IncidentStatus>())
            {
                var expected = AllowedPairs.Contains((from, to));

                Assert.True(
                    expected == IncidentStatusRules.CanTransition(from, to),
                    $"{from} -> {to} should be {(expected ? "allowed" : "rejected")}");
            }
        }
    }

    [Fact]
    public void Closed_IsTerminal_HasNoNextStatuses() =>
        Assert.Empty(IncidentStatusRules.GetNextStatuses(IncidentStatus.Closed));
}