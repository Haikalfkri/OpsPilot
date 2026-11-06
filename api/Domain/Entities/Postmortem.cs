namespace OpsPilot.Domain.Entities;

public class Postmortem
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public Guid AuthorId { get; set; }                      
    public string Summary { get; set; } = string.Empty;           
    public string RootCause { get; set; } = string.Empty;        
    public string Impact { get; set; } = string.Empty;          
    public string CorrectiveActions { get; set; } = string.Empty; 
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static Postmortem Create(
        Guid incidentId, Guid authorId, string summary, string rootCause,
        string impact, string correctiveActions, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            AuthorId = authorId,
            Summary = summary,
            RootCause = rootCause,
            Impact = impact,
            CorrectiveActions = correctiveActions,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    public bool Update(string summary, string rootCause, string impact, string correctiveActions, DateTimeOffset now)
    {
        if (Summary == summary && RootCause == rootCause && Impact == impact && CorrectiveActions == correctiveActions)
            return false;

        Summary = summary;
        RootCause = rootCause;
        Impact = impact;
        CorrectiveActions = correctiveActions;
        UpdatedAtUtc = now;
        return true;
    }
}