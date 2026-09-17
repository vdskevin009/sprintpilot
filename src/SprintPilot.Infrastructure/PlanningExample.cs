using SprintPilot.Domain;
namespace SprintPilot.Infrastructure;

public sealed record PlanningExampleData(Metadata Metadata, WorkItem[] Items, PlanningSettings Settings, DateOnly Today);
public static class PlanningExample
{
    public static PlanningSettings Settings() => new()
    {
        Types = ["Product Backlog Item", "Bug"], EstimatesAreHours = true,
        Tags = [new("Pablo API", true, false), new("FrontCare", true, false), new("WorkflowTPA", true, false),
            new("Disaster recovery", false, true), new("Reliability", false, true), new("Migration", false, true)]
    };
    public static PlanningExampleData Create(DateOnly today)
    {
        var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        DateTimeOffset Stamp(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var iterations = Enumerable.Range(-1, 3).Select(n => new Iteration((n + 2).ToString(), $"Sprint {18 + n}",
            $"SprintPilot\\Sprint {18 + n}", Stamp(monday.AddDays(n * 14)), Stamp(monday.AddDays(n * 14 + 13)))).ToArray();
        Person[] people = [new("kevin", "Kevin", "kevin@example.test"), new("johan", "Johan", "johan@example.test"), new("piotr", "Piotr", "piotr@example.test"), new("bea", "Bea", "bea@example.test")];
        string[] fields = ["System.Title", "System.State", "System.AssignedTo", "System.IterationPath", "System.AreaPath", "System.Tags", "System.Description", "Microsoft.VSTS.Scheduling.Effort"];
        var types = new[] { "Product Backlog Item", "Bug", "Task" }.Select(n => new TypeDefinition(n,
            [new("New", "Proposed"), new("Active", "InProgress"), new("Done", "Completed")], fields, "Microsoft.VSTS.Scheduling.Effort")).ToArray();
        var metadata = new Metadata([new("demo", "Platform team")], people, iterations, ["SprintPilot\\Platform"], types, [new("SprintPilot", true)], people[0]);
        var items = new List<WorkItem>();
        void Add(string owner, int period, double? estimate, string title, params string[] tags)
        {
            var person = people.FirstOrDefault(p => p.Id == owner);
            items.Add(new WorkItem { Id = 7000 + items.Count, Revision = 1, Type = items.Count % 4 == 0 ? "Bug" : "Product Backlog Item", Title = title,
                OwnerId = owner, Owner = person?.Name ?? "", State = period == 1 ? "Active" : "New",
                Iteration = period < 0 ? "SprintPilot" : iterations[period].Path, Area = "SprintPilot\\Platform", Estimate = estimate,
                NumericFields = estimate is {} value ? new() { ["Microsoft.VSTS.Scheduling.Effort"] = value } : new(), Tags = tags,
                Description = "Demonstration planning item. These are invented estimates, not live work data.", Changed = Stamp(today) });
        }
        Add("kevin",1,24,"Verify DR deployment routing","Pablo API","Disaster recovery");
        Add("kevin",1,18,"Improve timeout diagnostics","Pablo API","Reliability");
        Add("kevin",1,12,"Validate recovery entry points","FrontCare","Disaster recovery");
        Add("johan",1,24,"Reconcile migrated claims","WorkflowTPA","Migration");
        Add("johan",1,20,"Improve migration audit trail","WorkflowTPA","Reliability","Migration");
        Add("johan",1,24,"Validate customer import mapping","FrontCare","Migration");
        Add("piotr",1,16,"Verify API failover contracts","Pablo API","FrontCare","Disaster recovery");
        Add("piotr",1,16,"Harden queue retry behavior","Pablo API","Reliability");
        Add("bea",1,30,"Resolve sign-in recovery errors","FrontCare","Reliability");
        Add("bea",1,22,"Review customer error messages","FrontCare","Reliability");
        Add("",1,12,"Document DR operational checks","WorkflowTPA","Disaster recovery");
        Add("",1,8,"Triage integration incidents");
        Add("kevin",2,20,"Add rollback verification","Pablo API","Disaster recovery");
        Add("kevin",2,12,"Improve response contract tests","Pablo API","Reliability");
        Add("johan",2,30,"Validate historical claim import","WorkflowTPA","Migration");
        Add("johan",2,24,"Prepare migration reconciliation","WorkflowTPA","Migration");
        Add("piotr",2,24,"Review API throttling policy","Pablo API","Reliability");
        Add("piotr",2,20,"Exercise DR restore process","Pablo API","Disaster recovery");
        Add("piotr",2,14,"Verify new identity mapping","FrontCare","Migration");
        Add("bea",2,20,"Review account recovery journey","FrontCare","Reliability");
        Add("",2,null,"Scope shared dependency changes","Pablo API","WorkflowTPA","Reliability");
        Add("",2,12,"Document release verification","WorkflowTPA","Disaster recovery");
        Add("kevin",-1,40,"Automate regional recovery exercise","Pablo API","Disaster recovery");
        Add("johan",-1,32,"Extend migration exception handling","WorkflowTPA","Migration");
        Add("piotr",-1,24,"Reduce slow API requests","Pablo API","Reliability");
        Add("bea",-1,16,"Improve recovery notifications","FrontCare","Reliability");
        Add("",-1,48,"Plan cross-application migration","FrontCare","WorkflowTPA","Migration");
        Add("",-1,null,"Clarify next integration milestone");
        Add("",0,10,"Finish previous-sprint verification","Pablo API","Reliability");
        return new(metadata, items.ToArray(), Settings(), today);
    }
}
