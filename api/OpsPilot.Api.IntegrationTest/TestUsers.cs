namespace OpsPilot.Api.IntegrationTests;

public static class TestUsers
{
    public const string Password = "DemoPassword123!";
    public const string OtherTeamAdmin = "other.admin@opspilot.local";

    public const string Admin = "admin@opspilot.local";
    public const string Responder = "responder@opspilot.local";
    public const string Viewer = "viewer@opspilot.local";
    public const string Inactive = "inactive@opspilot.local";
    public const string OtherTeamResponder = "other.responder@opspilot.local";
    // Id tetap dari seeder, dipakai untuk menguji akses lintas tim
    public static readonly Guid OtherTeamResponderId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    // Id tetap dari seeder (dipakai untuk meng-assign incident)
    public static readonly Guid AdminId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid ResponderId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid ViewerId = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    public static readonly Guid InactiveId = Guid.Parse("a0000000-0000-0000-0000-000000000004");
}