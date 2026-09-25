using System.Text.Json.Nodes;
using SprintPilot.Application;

namespace SprintPilot.AzureDevOps;

public sealed partial class AzureTracker {
 static bool ApprovalPending(JsonNode? approval)=>S(approval?["status"]).Equals("pending",StringComparison.OrdinalIgnoreCase);
 static bool CanUpdateApproval(JsonNode? approval){
  var permissions=S(approval?["permissions"]);
  return permissions.Split(',',StringSplitOptions.TrimEntries).Contains("update",StringComparer.OrdinalIgnoreCase)
   ||(int.TryParse(permissions,out var flags)&&(flags&2)!=0);
 }
 public async Task<PipelineApprovalList> PendingPipelineApprovalsAsync(string project,CancellationToken ct=default){
  if(string.IsNullOrWhiteSpace(project))throw new TrackerException("Choose a project first.");
  var c=await Credentials(ct);
  var pending=Values(await Send(c,"_apis/pipelines/approvals?state=pending&$expand=steps,permissions&top=1000",HttpMethod.Get,ct:ct,projectOverride:project))
   .Where(ApprovalPending).Where(a=>Guid.TryParse(S(a?["id"]),out _)).ToDictionary(a=>S(a!["id"]),a=>a!,StringComparer.OrdinalIgnoreCase);
  if(pending.Count==0)return new([]);
  var contexts=new Dictionary<string,(string Pipeline,string Run,string Stage,int Build)>(StringComparer.OrdinalIgnoreCase);
  var warning=pending.Count>=1000?"Showing the first 1,000 pending approvals.":"";
  var continuation="";var seen=new HashSet<string>();
  do {
   var next="";
   var builds=Values(await Send(c,"_apis/build/builds?statusFilter=inProgress,notStarted&$top=100"+(continuation==""?"":"&continuationToken="+E(continuation)),HttpMethod.Get,ct:ct,projectOverride:project,continuation:value=>next=value));
   foreach(var build in builds){
    var id=build?["id"]?.GetValue<int>()??0;if(id<=0)continue;
    JsonArray records;
    try{records=(JsonArray?)(await Send(c,$"_apis/build/builds/{id}/timeline",HttpMethod.Get,ct:ct,projectOverride:project))["records"]??[];}
    catch(TrackerException){warning="Some pipeline stages could not be loaded. Refresh or review them in Azure DevOps.";continue;}
    var byId=records.Where(r=>S(r?["id"])!="").GroupBy(r=>S(r!["id"]),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last()!,StringComparer.OrdinalIgnoreCase);
    foreach(var record in records.Where(r=>S(r?["type"]).Equals("Checkpoint.Approval",StringComparison.OrdinalIgnoreCase))){
     var approvalId=S(record?["id"]);if(!pending.ContainsKey(approvalId))continue;
     var parent=S(record?["parentId"]);var visited=new HashSet<string>();string stage="";
     while(parent!=""&&visited.Add(parent)&&byId.TryGetValue(parent,out var ancestor)){
      if(S(ancestor["type"]).Equals("Stage",StringComparison.OrdinalIgnoreCase)){stage=S(ancestor["name"]);break;}
      parent=S(ancestor["parentId"]);
     }
     if(stage!="")contexts[approvalId]=(S(build?["definition"]?["name"]),S(build?["buildNumber"]),stage,id);
    }
   }
   continuation=next;
   if(continuation!=""&&!seen.Add(continuation))throw new TrackerException("Azure DevOps repeated a page of builds. Refresh to try again.");
  }while(continuation!="");
  var rows=pending.Select(pair=>{
   var found=contexts.TryGetValue(pair.Key,out var context);
   return new PipelineApproval(pair.Key,found?context.Pipeline:"Pipeline details unavailable",found?context.Run:"",found?context.Stage:"Stage unavailable",
    $"https://dev.azure.com/{E(c.Connection.Organization)}/{E(project)}/_build"+(found?$"/results?buildId={context.Build}&view=results":""),
    found&&CanUpdateApproval(pair.Value),S(pair.Value["instructions"]));
  }).OrderBy(a=>a.Pipeline).ThenBy(a=>a.Run).ThenBy(a=>a.Stage).ToArray();
  if(rows.Any(r=>r.Stage=="Stage unavailable"))warning="Some approvals could not be linked to a stage. Open Azure DevOps to review those approvals.";
  return new(rows,warning);
 }
 public async Task ApprovePipelineAsync(string project,string approvalId,CancellationToken ct=default){
  if(string.IsNullOrWhiteSpace(project)||!Guid.TryParse(approvalId,out _))throw new TrackerException("Refresh and select a valid pipeline approval.");
  var c=await Credentials(ct);
  var current=await Send(c,$"_apis/pipelines/approvals/{E(approvalId)}?$expand=steps,permissions",HttpMethod.Get,ct:ct,projectOverride:project);
  if(!ApprovalPending(current))throw new TrackerException("This approval is no longer pending. Refresh the list.");
  if(!CanUpdateApproval(current))throw new TrackerException("Your Azure DevOps account cannot approve this stage.");
  await Send(c,"_apis/pipelines/approvals",HttpMethod.Patch,new[]{new{approvalId,status="approved",comment="Approved from SprintPilot"}},ct:ct,projectOverride:project);
 }
}
