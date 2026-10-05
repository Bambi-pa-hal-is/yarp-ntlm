namespace Yarp.Sample.Ntlm.ModernApp.Data;

// Hardcoded data for the fictional Ekdala Municipality. All people, addresses and IDs are made up.

public static class PermitStatus
{
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string NeedsCompletion = "NeedsCompletion";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] Decisions = [Approved, Rejected, NeedsCompletion];
}

public sealed record PermitEvent(DateTimeOffset At, string By, string Text);

public sealed record BuildingPermit(
    string Id,
    string Type,
    string Address,
    string Property,
    string Applicant,
    string Description,
    DateOnly Submitted,
    string Status,
    string? Officer,
    decimal Fee,
    IReadOnlyList<PermitEvent> Timeline,
    // Case number in the legacy system, for permits migrated from it.
    string? LegacyCaseNumber = null);

public sealed record PermitDecision(string Decision, string? Comment);

/// <summary>
/// In-memory store for building permits. Decisions are kept until the app restarts.
/// </summary>
public sealed class PermitStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, BuildingPermit> _permits;

    public PermitStore()
    {
        _permits = Seed().ToDictionary(permit => permit.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<BuildingPermit> GetAll()
    {
        lock (_lock)
        {
            return _permits.Values.OrderByDescending(permit => permit.Submitted).ToList();
        }
    }

    public BuildingPermit? Find(string id)
    {
        lock (_lock)
        {
            return _permits.GetValueOrDefault(id);
        }
    }

    public BuildingPermit? Decide(string id, PermitDecision decision, string officer)
    {
        lock (_lock)
        {
            if (!_permits.TryGetValue(id, out var permit))
            {
                return null;
            }

            var text = decision.Decision switch
            {
                PermitStatus.Approved => "Permit approved.",
                PermitStatus.Rejected => "Permit rejected.",
                _ => "Applicant asked to complete the application.",
            };
            if (!string.IsNullOrWhiteSpace(decision.Comment))
            {
                text += " " + decision.Comment.Trim();
            }

            permit = permit with
            {
                Status = decision.Decision,
                Officer = officer,
                Timeline = [.. permit.Timeline, new PermitEvent(DateTimeOffset.Now, officer, text)],
            };
            _permits[id] = permit;
            return permit;
        }
    }

    private static IEnumerable<BuildingPermit> Seed()
    {
        static PermitEvent Event(int month, int day, string by, string text) => new(new DateTimeOffset(2026, month, day, 9, 0, 0, TimeSpan.FromHours(2)), by, text);

        yield return new("BP-2026-0101", "New detached house", "Ängsvägen 12", "Ekdala Ängen 4:12", "Karin & Olof Sjöberg",
            "One-and-a-half storey detached house, 142 m², with attached carport. Within detailed development plan DP-87.",
            new(2026, 7, 15), PermitStatus.UnderReview, "Erik Johansson", 38_400m,
            [
                Event(7, 15, "Case management (legacy)", "Application registered as EKD-2026-0301."),
                Event(9, 1, "System", "Migrated from the legacy case management system."),
                Event(9, 8, "Erik Johansson", "Neighbours notified. Deadline for comments 2026-09-29."),
            ],
            LegacyCaseNumber: "EKD-2026-0301");
        yield return new("BP-2026-0114", "Extension", "Ekbacken 7", "Ekdala Norra 2:31", "Gunnar Åberg",
            "Single-storey extension of 24 m² to the rear of the house for a bedroom and accessible bathroom.",
            new(2026, 9, 3), PermitStatus.Submitted, null, 12_900m,
            [Event(9, 3, "E-service", "Application submitted with drawings and site plan.")]);
        yield return new("BP-2026-0117", "Solar panels", "Skogsstigen 4", "Västerby 1:118", "Per-Erik Lund",
            "Roof-mounted solar panels, 38 m², on a building within the cultural heritage area Västerby by.",
            new(2026, 9, 9), PermitStatus.UnderReview, "Erik Johansson", 4_200m,
            [
                Event(9, 9, "E-service", "Application submitted."),
                Event(9, 14, "Erik Johansson", "Referred to the municipal antiquarian for comments."),
            ]);
        yield return new("BP-2026-0121", "Garage", "Lindvägen 3", "Ekdala Centrum 7:4", "Birgitta Holm",
            "Detached garage, 36 m², 1.2 m from the property boundary. Neighbour consent missing.",
            new(2026, 9, 12), PermitStatus.NeedsCompletion, "Erik Johansson", 7_600m,
            [
                Event(9, 12, "E-service", "Application submitted."),
                Event(9, 18, "Erik Johansson", "Requested written consent from the neighbour at Lindvägen 5."),
            ]);
        yield return new("BP-2026-0123", "Change of use", "Stationsgatan 9", "Ekdala Södra 3:9", "Stationshuset Fastigheter AB",
            "Change of use of ground-floor retail space (210 m²) into a restaurant with outdoor seating.",
            new(2026, 9, 16), PermitStatus.Submitted, null, 21_300m,
            [Event(9, 16, "E-service", "Application submitted.")]);
        yield return new("BP-2026-0098", "Demolition", "Kyrkgatan 22", "Ekdala Centrum 1:52", "Ekdala Bostäder AB",
            "Demolition of a dilapidated storage building (1950s) in the courtyard.",
            new(2026, 8, 20), PermitStatus.Approved, "Erik Johansson", 5_100m,
            [
                Event(8, 20, "E-service", "Application submitted."),
                Event(8, 27, "Erik Johansson", "Inspection plan established."),
                Event(9, 2, "Erik Johansson", "Permit approved. Demolition may start after technical consultation."),
            ]);
        yield return new("BP-2026-0090", "New detached house", "Sjövägen 1", "Västerby 4:2", "Elin Strand",
            "Detached house in a shoreline protection area, 30 m from the lake.",
            new(2026, 8, 11), PermitStatus.Rejected, "Erik Johansson", 9_800m,
            [
                Event(8, 11, "E-service", "Application submitted."),
                Event(9, 4, "Erik Johansson", "Permit rejected: no special reason for exemption from shoreline protection."),
            ]);
    }
}
