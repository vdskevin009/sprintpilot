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
 public Task ReorderSprintAsync(string iterationId,string iterationPath,int id,int previousId,int nextId,CancellationToken ct=default)=>Active.ReorderSprintAsync(iterationId,iterationPath,id,previousId,nextId,ct);
 public Task<SprintCapacity> CapacityAsync(string iterationId,CancellationToken ct=default)=>Active.CapacityAsync(iterationId,ct);
 public Task<IReadOnlyList<AzureProject>> ProjectsAsync(CancellationToken ct=default)=>Active.ProjectsAsync(ct);
 public Task<IReadOnlyList<GitRepository>> RepositoriesAsync(string project,CancellationToken ct=default)=>Active.RepositoriesAsync(project,ct);
 public Task<IReadOnlyList<GitBranch>> BranchesAsync(string project,string repositoryId,CancellationToken ct=default)=>Active.BranchesAsync(project,repositoryId,ct);
 public Task<IReadOnlyList<BranchDeleteResult>> DeleteBranchesAsync(string project,string repositoryId,IReadOnlyList<BranchDeleteRequest> branches,CancellationToken ct=default)=>Active.DeleteBranchesAsync(project,repositoryId,branches,ct);
 public Task<IReadOnlyList<GitPullRequest>> PullRequestsAsync(string project,string repositoryId,CancellationToken ct=default)=>Active.PullRequestsAsync(project,repositoryId,ct);
 public Task<IReadOnlyList<PullRequestAbandonResult>> AbandonPullRequestsAsync(string project,string repositoryId,IReadOnlyList<PullRequestAbandonRequest> pullRequests,CancellationToken ct=default)=>Active.AbandonPullRequestsAsync(project,repositoryId,pullRequests,ct);
 public Task<IReadOnlyList<WorkItem>> PlanningAsync(string[] types,CancellationToken ct=default)=>Active.PlanningAsync(types,ct);
 public Task<WorkItem> GetAsync(int id,CancellationToken ct=default)=>Active.GetAsync(id,ct);
 public Task<IReadOnlyList<WorkItemComment>> CommentsAsync(int id,CancellationToken ct=default)=>Active.CommentsAsync(id,ct);
 public Task<WorkItemComment> AddCommentAsync(int id,string text,CancellationToken ct=default)=>Active.AddCommentAsync(id,text,ct);
 public Task<WorkItem> UpdateAsync(ItemUpdate u,CancellationToken ct=default)=>Active.UpdateAsync(u,ct);
 public Task<WorkItem> CreateAsync(string type,IReadOnlyList<Change> changes,int? parent,CancellationToken ct=default)=>Active.CreateAsync(type,changes,parent,ct);
}
