using SprintPilot.Domain;
namespace SprintPilot.Application;

public static class Planning
{
    public static bool IsPlanningType(string type, PlanningSettings settings) =>
        !type.Equals("Task", StringComparison.OrdinalIgnoreCase) && settings.Types.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static double? Estimate(WorkItem item, PlanningSettings settings)
    {
        double? value = settings.EstimateFields.TryGetValue(item.Type, out var field) && !string.IsNullOrWhiteSpace(field)
            ? item.NumericFields.TryGetValue(field, out var raw) ? raw : null
            : item.Estimate;
        return value is {} n && double.IsFinite(n) && n >= 0 ? n : null;
    }

    public static void Validate(PlanningSettings settings)
    {
        if (settings.Types.Length == 0 || settings.Types.Any(t => t.Equals("Task", StringComparison.OrdinalIgnoreCase)))
            throw new TrackerException("Select at least one backlog/bug type. Tasks are excluded from planning.");
        static bool Valid(CapacityTarget t) => double.IsFinite(t.Minimum) && double.IsFinite(t.Maximum) && t.Minimum >= 0 && t.Maximum >= t.Minimum;
        if (!Valid(settings.DefaultTarget) || settings.CapacityOverrides.Values.Any(t => !Valid(t)))
            throw new TrackerException("Capacity targets need non-negative hours and a maximum at least as large as the minimum.");
        if (settings.Tags.Any(t => string.IsNullOrWhiteSpace(t.Tag)))
            throw new TrackerException("Tag names cannot be empty.");
        if (settings.Tags.Select(t => t.Tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.Tags.Count)
            throw new TrackerException("Each tag can be classified only once. A tag may belong to both dimensions.");
    }

    public static PlanningSnapshot Build(IEnumerable<WorkItem> source, Metadata meta, PlanningSettings settings, DateOnly today, DateTimeOffset loadedAt)
    {
        Validate(settings);
        var eligible = source.GroupBy(w => w.Id).Select(g => g.OrderByDescending(w => w.Revision).First())
            .Where(w => IsPlanningType(w.Type, settings)).ToArray();
        var open = eligible.Where(w => !Quality.Finished(w, meta)).ToArray();
        static DateOnly Day(DateTimeOffset date) => DateOnly.FromDateTime(date.UtcDateTime);
        // Iteration timestamps represent date-only schedule boundaries, not per-item bookings.
        var iterations = meta.Iterations.OrderBy(i => i.Start).ThenBy(i => i.Path).ToArray();
        var current = iterations.Where(i => i.Start is {} s && i.Finish is {} f && Day(s) <= today && Day(f) >= today)
            .OrderByDescending(i => i.Start).ThenBy(i => i.Path).FirstOrDefault();
        var next = iterations.Where(i => i.Start is {} s && Day(s) > (current?.Finish is {} f ? Day(f) : today))
            .OrderBy(i => i.Start).ThenBy(i => i.Path).FirstOrDefault();
        var monday = today.AddDays(7 - ((int)today.DayOfWeek + 6) % 7);
        var sunday = monday.AddDays(6);
        var overlapping = iterations.Where(i => i.Start is {} s && i.Finish is {} f && Day(s) <= sunday && Day(f) >= monday)
            .Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Backlog includes unscheduled and overdue work, but not work booked into a current/future dated sprint.
        var scheduled = iterations.Where(i => i.Start is not null && i.Finish is {} f && Day(f) >= today)
            .Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allKnownPaths = iterations.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        WorkItem[] For(Iteration? iteration) => iteration is null ? [] : open.Where(w => w.Iteration.Equals(iteration.Path, StringComparison.OrdinalIgnoreCase)).ToArray();
        PlanningPeriod Period(PlanningHorizon horizon, string label, string detail, WorkItem[] rows) => new(horizon, label, detail, rows)
        {
            KnownEstimate = rows.Sum(w => Estimate(w, settings) ?? 0),
            MissingEstimates = rows.Count(w => Estimate(w, settings) is null)
        };
        var periods = new[] {
            Period(PlanningHorizon.Current, "Current sprint", current?.Name ?? "No active dated sprint", For(current)),
            Period(PlanningHorizon.Next, "Next sprint", next?.Name ?? "No next dated sprint", For(next)),
            Period(PlanningHorizon.NextWeek, "Next week", $"{monday:MMM d}–{sunday:MMM d} · overlapping sprints", open.Where(w => overlapping.Contains(w.Iteration)).ToArray()),
            Period(PlanningHorizon.Backlog, "Backlog", "Unscheduled + overdue sprint work", open.Where(w => !scheduled.Contains(w.Iteration)).ToArray()),
            Period(PlanningHorizon.Total, "Total", "All open team backlog items + bugs", open)
        };
        CapacityTarget Target(string id, Iteration? iteration) => iteration is null ? settings.DefaultTarget : settings.CapacityOverrides.GetValueOrDefault(PlanningSettings.CapacityKey(id, iteration.Id), settings.DefaultTarget);
        PersonPeriod PersonPeriod(string id, PlanningPeriod period, Iteration? iteration)
        {
            var rows = period.Items.Where(w => Identity(w) == id).ToArray();
            var effort = rows.Sum(w => Estimate(w, settings) ?? 0);
            var missing = rows.Count(w => Estimate(w, settings) is null);
            var target = Target(id, iteration);
            var status = iteration is null ? "No sprint" : id == "" ? "Unassigned" : !settings.EstimatesAreHours ? "Confirm hour units" :
                effort > target.Maximum ? "Above target" : missing > 0 ? "Estimates missing" : effort < target.Minimum ? "Room for work" : "Within target";
            return new(effort, rows.Length, missing, target, status, rows.Select(w => w.Id).ToArray());
        }
        var identities = meta.People.Select(p => p.Id).Concat(open.Select(Identity)).Distinct().ToArray();
        var people = identities.Select(id => new PersonWorkload(id,
            id == "" ? "Unassigned" : meta.People.FirstOrDefault(p => p.Id == id)?.Name ?? open.First(w => Identity(w) == id).Owner,
            meta.People.Any(p => p.Id == id), PersonPeriod(id, periods[0], current), PersonPeriod(id, periods[1], next)))
            .OrderBy(p => p.Id == "").ThenBy(p => p.Name).ToArray();
        var unknown = open.Count(w => meta.Types.FirstOrDefault(t => t.Name == w.Type)?.States.All(s => s.Name != w.State) != false);
        return new(periods, current, next, monday, people, eligible.Length - open.Length, unknown,
            open.Count(w => !allKnownPaths.Contains(w.Iteration)), loadedAt);
    }

    public static string Identity(WorkItem item) => item.OwnerId != "" ? item.OwnerId : item.Owner != "" ? "unresolved:" + item.Owner : "";

    public static TagWorkload[] Distribution(PlanningPeriod period, PlanningSettings settings, TagDimension dimension)
    {
        var configured = settings.Tags.Where(t => dimension == TagDimension.Application ? t.Application : t.Initiative)
            .Select(t => t.Tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var rows = configured.ToDictionary(t => t, _ => new List<(WorkItem Item, double Fraction)>(), StringComparer.OrdinalIgnoreCase);
        var unclassified = new List<(WorkItem Item, double Fraction)>();
        foreach (var item in period.Items)
        {
            var matches = configured.Where(t => item.Tags.Any(tag => tag.Trim().Equals(t, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (matches.Length == 0) unclassified.Add((item, 1));
            else foreach (var tag in matches) rows[tag].Add((item, settings.Allocation == TagAllocation.SplitEvenly ? 1d / matches.Length : 1));
        }
        TagWorkload BuildRow(string name, List<(WorkItem Item, double Fraction)> entries, bool other) => new(name,
            entries.Sum(e => (Estimate(e.Item, settings) ?? 0) * e.Fraction), entries.Select(e => e.Item.Id).Distinct().ToArray(),
            entries.Count(e => Estimate(e.Item, settings) is null), other);
        return rows.Select(r => BuildRow(r.Key, r.Value, false)).Append(BuildRow("Unclassified", unclassified, true))
            .OrderBy(r => r.Unclassified).ThenByDescending(r => r.Effort).ThenBy(r => r.Tag).ToArray();
    }
}
