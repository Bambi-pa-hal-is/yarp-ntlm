namespace Yarp.Sample.Ntlm.LegacyApp.Data;

// Hardcoded data for the fictional Ekdala Municipality. All people, addresses and IDs are made up.

public sealed record Employee(string Account, string DisplayName, string Department, string Title);

public static class Employees
{
    private static readonly Employee[] All =
    [
        new(@"EKDALA\anna.lindqvist", "Anna Lindqvist", "Social Services", "Social Worker"),
        new(@"EKDALA\erik.johansson", "Erik Johansson", "Building & Planning", "Building Permit Officer"),
        new(@"EKDALA\maria.nilsson", "Maria Nilsson", "Environment & Health", "Health Inspector"),
        new(@"EKDALA\johan.berg", "Johan Berg", "Education", "Placement Coordinator"),
        new(@"EKDALA\sara.ek", "Sara Ek", "Technical Services", "Traffic Engineer"),
    ];

    /// <summary>
    /// Finds the employee for a Windows account. Unknown accounts, such as your own when running the sample,
    /// get a generic entry.
    /// </summary>
    public static Employee Find(string account) =>
        All.FirstOrDefault(employee => string.Equals(employee.Account, account, StringComparison.OrdinalIgnoreCase))
        ?? new Employee(account, account[(account.IndexOf('\\') + 1)..], "Municipal Office", "Case Officer");
}

public static class Departments
{
    public const string SocialServices = "Social Services";
    public const string BuildingAndPlanning = "Building & Planning";
    public const string EnvironmentAndHealth = "Environment & Health";
    public const string Education = "Education";
    public const string TechnicalServices = "Technical Services";

    public static readonly string[] All = [SocialServices, BuildingAndPlanning, EnvironmentAndHealth, Education, TechnicalServices];
}

public static class CaseStatus
{
    public const string Registered = "Registered";
    public const string InProgress = "In progress";
    public const string AwaitingInfo = "Awaiting information";
    public const string Decided = "Decided";
    public const string Closed = "Closed";

    public static readonly string[] All = [Registered, InProgress, AwaitingInfo, Decided, Closed];
}

public sealed record CaseEvent(DateOnly Date, string Who, string What);

public sealed record MunicipalCase(
    string Number,
    string Title,
    string Type,
    string Department,
    string Applicant,
    string Status,
    DateOnly Received,
    string Officer,
    string Description,
    IReadOnlyList<CaseEvent> History,
    // Set for case types that have moved to the modern app.
    string? MigratedTo = null)
{
    public bool IsOpen => Status is not (CaseStatus.Decided or CaseStatus.Closed);
}

public sealed record Resident(string PersonalId, string Name, string Address, string District, int HouseholdSize, string[] CaseNumbers);

public static class CaseRegistry
{
    public static readonly IReadOnlyList<MunicipalCase> All =
    [
        new("EKD-2026-0412", "Application for home care services", "Home care", Departments.SocialServices,
            "Birgitta Holm", CaseStatus.InProgress, new(2026, 9, 2), "Anna Lindqvist",
            "Applicant requests assistance with daily activities after hospital discharge. Needs assessment scheduled.",
            [
                new(new(2026, 9, 2), "Registry", "Case registered from web form."),
                new(new(2026, 9, 4), "Anna Lindqvist", "Assigned. Home visit booked for 2026-09-15."),
                new(new(2026, 9, 15), "Anna Lindqvist", "Home visit completed. Drafting decision."),
            ]),
        new("EKD-2026-0398", "Noise complaint – ventilation unit, Storgatan 14", "Complaint", Departments.EnvironmentAndHealth,
            "Lars Persson", CaseStatus.AwaitingInfo, new(2026, 8, 28), "Maria Nilsson",
            "Residents report a constant low-frequency noise from a rooftop ventilation unit, mainly at night.",
            [
                new(new(2026, 8, 28), "Registry", "Complaint received by phone."),
                new(new(2026, 9, 3), "Maria Nilsson", "Requested measurement report from property owner. Deadline 2026-10-10."),
            ]),
        new("EKD-2026-0377", "Food business registration – Café Linden", "Registration", Departments.EnvironmentAndHealth,
            "Café Linden AB", CaseStatus.Decided, new(2026, 8, 19), "Maria Nilsson",
            "Registration of a new café with a small kitchen. Risk classification: low.",
            [
                new(new(2026, 8, 19), "Registry", "Case registered."),
                new(new(2026, 8, 30), "Maria Nilsson", "Inspection done. Registration approved, fee class 2."),
            ]),
        new("EKD-2026-0365", "School placement – transfer to Ekdala Norra", "School placement", Departments.Education,
            "Guardian of pupil (protected)", CaseStatus.Registered, new(2026, 8, 14), "Johan Berg",
            "Request to transfer pupil in grade 4 from Ekdala Södra to Ekdala Norra from the spring term.",
            [
                new(new(2026, 8, 14), "Registry", "Case registered from e-service."),
            ]),
        new("EKD-2026-0351", "Parking permit for disabled persons", "Parking permit", Departments.TechnicalServices,
            "Gunnar Åberg", CaseStatus.InProgress, new(2026, 8, 7), "Sara Ek",
            "Application for parking permit as driver. Medical certificate attached.",
            [
                new(new(2026, 8, 7), "Registry", "Case registered."),
                new(new(2026, 8, 20), "Sara Ek", "Medical certificate reviewed. Waiting for photo."),
            ]),
        new("EKD-2026-0333", "Financial assistance – September", "Financial assistance", Departments.SocialServices,
            "Household (protected)", CaseStatus.Closed, new(2026, 8, 1), "Anna Lindqvist",
            "Monthly application for financial assistance.",
            [
                new(new(2026, 8, 1), "Registry", "Case registered."),
                new(new(2026, 8, 12), "Anna Lindqvist", "Decision: granted. Payment 2026-08-25."),
                new(new(2026, 8, 25), "System", "Payment completed. Case closed."),
            ]),
        new("EKD-2026-0329", "Street lighting out – Björkvägen", "Fault report", Departments.TechnicalServices,
            "Anonymous", CaseStatus.Closed, new(2026, 7, 29), "Sara Ek",
            "Three street lights out between Björkvägen 2 and 10.",
            [
                new(new(2026, 7, 29), "Registry", "Fault report received."),
                new(new(2026, 8, 5), "Sara Ek", "Contractor replaced lamps. Case closed."),
            ]),
        new("EKD-2026-0301", "Building permit – detached house, Ängen 4:12", "Building permit", Departments.BuildingAndPlanning,
            "Karin & Olof Sjöberg", CaseStatus.InProgress, new(2026, 7, 15), "Erik Johansson",
            "Building permits are handled in the new system since 2026-09-01.",
            [
                new(new(2026, 7, 15), "Registry", "Case registered."),
                new(new(2026, 9, 1), "System", "Case migrated to the new building permit system (BP-2026-0101)."),
            ],
            MigratedTo: "/app/permits/BP-2026-0101"),
        new("EKD-2026-0287", "Excavation permit – district heating, Kyrkgatan", "Excavation permit", Departments.TechnicalServices,
            "Ekdala Energi AB", CaseStatus.Decided, new(2026, 7, 8), "Sara Ek",
            "Excavation in carriageway for district heating connection, week 34–36.",
            [
                new(new(2026, 7, 8), "Registry", "Case registered."),
                new(new(2026, 7, 21), "Sara Ek", "Permit granted with traffic plan TA-114."),
            ]),
        new("EKD-2026-0264", "Preschool placement – Solrosen", "School placement", Departments.Education,
            "Guardian of child (protected)", CaseStatus.Decided, new(2026, 6, 30), "Johan Berg",
            "Placement request for preschool from 2026-10-01.",
            [
                new(new(2026, 6, 30), "Registry", "Case registered from e-service."),
                new(new(2026, 7, 10), "Johan Berg", "Offered placement at Solrosen preschool. Accepted."),
            ]),
    ];

    public static MunicipalCase? Find(string number) =>
        All.FirstOrDefault(c => string.Equals(c.Number, number, StringComparison.OrdinalIgnoreCase));
}

public static class ResidentRegistry
{
    // Personal identity numbers are masked; the legacy system only shows the full number to case officers on request.
    public static readonly IReadOnlyList<Resident> All =
    [
        new("19410305-XXXX", "Birgitta Holm", "Lindvägen 3", "Ekdala Centrum", 1, ["EKD-2026-0412"]),
        new("19780611-XXXX", "Lars Persson", "Storgatan 16", "Ekdala Centrum", 3, ["EKD-2026-0398"]),
        new("19520920-XXXX", "Gunnar Åberg", "Ekbacken 7", "Ekdala Norra", 2, ["EKD-2026-0351"]),
        new("19850214-XXXX", "Karin Sjöberg", "Ängsvägen 12", "Ängen", 4, ["EKD-2026-0301"]),
        new("19830502-XXXX", "Olof Sjöberg", "Ängsvägen 12", "Ängen", 4, ["EKD-2026-0301"]),
        new("19990118-XXXX", "Elin Strand", "Kyrkgatan 22 lgh 1102", "Ekdala Centrum", 1, []),
        new("19670730-XXXX", "Per-Erik Lund", "Skogsstigen 4", "Västerby", 2, []),
        new("20010409-XXXX", "Amir Haddad", "Stationsgatan 9 lgh 1201", "Ekdala Södra", 1, []),
    ];
}
