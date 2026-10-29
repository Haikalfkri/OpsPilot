namespace OpsPilot.Api.Auth
{
    public static class Policies
    {
        public const string AdminOnly = nameof(AdminOnly);
        public const string CanManageIncidents = nameof(CanManageIncidents);
        public const string AnyTeamMember = nameof(AnyTeamMember);
    }
}
