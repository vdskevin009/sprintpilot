using SprintPilot.Application;
using SprintPilot.Domain;
using SprintPilot.AzureDevOps;
using SprintPilot.Infrastructure;
namespace SprintPilot.Web;
public sealed class TrackerSession(AzureTracker azure,DemoTracker demo):IWorkTracker {
 public bool Demo {get;set;}
 IWorkTracker Active=>Demo?demo:azure;
 public Task<Person> TestAsync(Credentials c,CancellationToken ct=default)=>Active.TestAsync(c,ct);
 public Task<Metadata> MetadataAsync(bool refresh=false,CancellationToken ct=default)=>Active.MetadataAsync(refresh,ct);
 public Task<IReadOnlyList<WorkItem>> SprintAsync(string sprint,CancellationToken ct=default)=>Active.SprintAsync(sprint,ct);
 public Task<SprintCapacity> CapacityAsync(string iterationId,CancellationToken ct=default)=>Active.CapacityAsync(iterationId,ct);
 public Task<IReadOnlyList<WorkItem>> PlanningAsync(string[] types,CancellationToken ct=default)=>Active.PlanningAsync(types,ct);
 public Task<WorkItem> GetAsync(int id,CancellationToken ct=default)=>Active.GetAsync(id,ct);
 public Task<WorkItem> UpdateAsync(ItemUpdate u,CancellationToken ct=default)=>Active.UpdateAsync(u,ct);
 public Task<WorkItem> CreateAsync(string type,IReadOnlyList<Change> changes,int? parent,CancellationToken ct=default)=>Active.CreateAsync(type,changes,parent,ct);
}
