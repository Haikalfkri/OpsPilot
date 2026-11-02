using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Sla
{
    public static class SlaEvaluator
    {
        public static SlaState EvaluateResponse(
            DateTimeOffset responseDueAtUtc,
            DateTimeOffset? firstRespondedAtUtc,
            DateTimeOffset? resolvedAtUtc,
            DateTimeOffset nowUtc
        )
        {
            var respondedAt = firstRespondedAtUtc ?? resolvedAtUtc;

            return Evaluate(responseDueAtUtc, respondedAt, nowUtc);
        }

        public static SlaState EvaluateResolution(
            DateTimeOffset resolutionDueAtUtc,
            DateTimeOffset? resolvedAtUtc,
            DateTimeOffset nowUtc
        )
        {
            return Evaluate(resolutionDueAtUtc, resolvedAtUtc, nowUtc);
        }

        private static SlaState Evaluate(
            DateTimeOffset dueAtUtc, 
            DateTimeOffset? completedAtUtc, 
            DateTimeOffset nowUtc
        )
        {
            if (completedAtUtc is { } completedAt)
            {
                return completedAt <= dueAtUtc ? SlaState.Met : SlaState.Breached;
            }

            return nowUtc > dueAtUtc ? SlaState.Breached : SlaState.Pending;
        }
    }
}
