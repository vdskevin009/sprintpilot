namespace SprintPilot.Domain;

public enum PlanningHorizon { Current, Next, NextWeek, Backlog, Total }
public enum TagDimension { Application, Initiative }
public enum TagAllocation { SplitEvenly, FullMembership }
public sealed record TagClassification(string Tag, bool Application, bool Initiative);
public sealed record CapacityTarget(double Minimum = 50, double Maximum = 60);
public sealed class PlanningSettings
{
    public string[] Types { get; set; } = ["Product Backlog Item", "User Story", "Bug", "Requirement", "Issue"];
    public List<TagClassification> Tags { get; set; } = [];
    public TagAllocation Allocation { get; set; } = TagAllocation.SplitEvenly;
    public bool EstimatesAreHours { get; set; }
    public Dictionary<string, string> EstimateFields { get; set; } = new();
    public CapacityTarget DefaultTarget { get; set; } = new();
    // Keys are immutable identity ID + iteration ID; absence falls back to the default.
    public Dictionary<string, CapacityTarget> CapacityOverrides { get; set; } = new();
    public static string CapacityKey(string personId, string iterationId) => personId + ":" + iterationId;
}
public sealed record PlanningPeriod(PlanningHorizon Horizon, string Label, string Detail, WorkItem[] Items)
{
    public double KnownEstimate { get; init; }
    public int MissingEstimates { get; init; }
}
public sealed record TagWorkload(string Tag, double Effort, int[] ItemIds, int MissingEstimates, bool Unclassified);
public sealed record PersonPeriod(double Effort, int Count, int MissingEstimates, CapacityTarget Target, string Status, int[] ItemIds);
public sealed record PersonWorkload(string Id, string Name, bool TeamMember, PersonPeriod Current, PersonPeriod Next);
public sealed record PlanningSnapshot(
    PlanningPeriod[] Periods, Iteration? Current, Iteration? Next, DateOnly NextWeekStart,
    PersonWorkload[] People, int ExcludedCompleted, int UnknownStates, int UnscheduledCount,
    DateTimeOffset LoadedAt);
