using SprintPilot.Domain;
namespace SprintPilot.Application;
public interface ICredentialStore {
 ValueTask<Credentials?> GetAsync(CancellationToken ct=default);
 ValueTask SaveAsync(Credentials credentials,CancellationToken ct=default);
}
public interface IPreferencesStore {
 Task<Preferences> LoadAsync(CancellationToken ct=default);
 Task SaveAsync(Preferences preferences,CancellationToken ct=default);
 Task SaveThemeAsync(string theme,CancellationToken ct=default);
}
public interface IWorkTracker {
 Task<Person> TestAsync(Credentials credentials,CancellationToken ct=default);
 Task<Metadata> MetadataAsync(bool refresh=false,CancellationToken ct=default);
 Task<IReadOnlyList<WorkItem>> SprintAsync(string iteration,CancellationToken ct=default);
 Task ReorderSprintAsync(string iterationId,string iterationPath,int id,int previousId,int nextId,CancellationToken ct=default);
 Task<SprintCapacity> CapacityAsync(string iterationId,CancellationToken ct=default);
 Task<IReadOnlyList<AzureProject>> ProjectsAsync(CancellationToken ct=default);
 Task<IReadOnlyList<GitRepository>> RepositoriesAsync(string project,CancellationToken ct=default);
 Task<IReadOnlyList<GitBranch>> BranchesAsync(string project,string repositoryId,CancellationToken ct=default);
 Task<IReadOnlyList<string>> PipelineYamlFilesAsync(string project,string repositoryId,string branch,CancellationToken ct=default);
 Task<PipelineApprovalList> PendingPipelineApprovalsAsync(string project,CancellationToken ct=default);
 Task ApprovePipelineAsync(string project,string approvalId,CancellationToken ct=default);
 Task<IReadOnlyList<PipelineDefinition>> PipelinesAsync(string project,CancellationToken ct=default);
 Task<IReadOnlyList<PipelineCreateResult>> CreatePipelinesAsync(string project,string repositoryId,string branch,IReadOnlyList<PipelineCreateRequest> pipelines,CancellationToken ct=default);
 Task<IReadOnlyList<BranchDeleteResult>> DeleteBranchesAsync(string project,string repositoryId,IReadOnlyList<BranchDeleteRequest> branches,CancellationToken ct=default);
 Task<IReadOnlyList<GitPullRequest>> PullRequestsAsync(string project,string repositoryId,CancellationToken ct=default);
 Task<IReadOnlyList<GitPullRequestSignal>> PullRequestSignalsAsync(string project,string repositoryId,CancellationToken ct=default);
 Task<IReadOnlyList<PullRequestAbandonResult>> AbandonPullRequestsAsync(string project,string repositoryId,IReadOnlyList<PullRequestAbandonRequest> pullRequests,CancellationToken ct=default);
 Task<IReadOnlyList<WorkItem>> PlanningAsync(string[] types,CancellationToken ct=default);
 Task<WorkItem> GetAsync(int id,CancellationToken ct=default);
 Task<IReadOnlyList<WorkItemComment>> CommentsAsync(int id,CancellationToken ct=default);
 Task<WorkItemComment> AddCommentAsync(int id,string text,CancellationToken ct=default);
 Task<WorkItem> UpdateAsync(ItemUpdate update,CancellationToken ct=default);
 Task<WorkItem> CreateAsync(string type,IReadOnlyList<Change> changes,int? parent,CancellationToken ct=default);
}
public sealed class TrackerException(string message):Exception(message);
public sealed class BulkEditor(IWorkTracker tracker) {
 public async Task<IReadOnlyList<UpdateResult>> ApplyAsync(IEnumerable<ItemUpdate> updates,IProgress<UpdateResult>? progress=null,CancellationToken ct=default) {
  var result=new System.Collections.Concurrent.ConcurrentBag<UpdateResult>();
  await Parallel.ForEachAsync(updates,new ParallelOptions{MaxDegreeOfParallelism=4,CancellationToken=ct},async (u,token)=>{
   UpdateResult row;
   try {row=new(u.Original.Id,await tracker.UpdateAsync(u,token),null);}
   catch(TrackerException ex){row=new(u.Original.Id,null,ex.Message);}
   catch(OperationCanceledException){row=new(u.Original.Id,null,"Cancelled or timed out. Refresh before retrying; server outcome may be unknown.");}
   catch {row=new(u.Original.Id,null,"Update could not be confirmed. Refresh before retrying.");}
   result.Add(row);progress?.Report(row);
  });return result.OrderBy(x=>x.Id).ToArray();
 }
}


public sealed record PipelineDefinition(int Id,string Name,string RepositoryId,string YamlPath,string Branch);
public sealed record PipelineCreateRequest(string Name,string YamlPath);
public sealed record PipelineCreateResult(string Name,bool Success,int? Id,string? Error);


public sealed record PipelineApproval(string Id,string Pipeline,string Run,string Stage,string Url,bool CanApprove,string Instructions);
public sealed record PipelineApprovalList(IReadOnlyList<PipelineApproval> Items,string Warning="");
