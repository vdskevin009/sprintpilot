using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SprintPilot.Application;
using SprintPilot.AzureDevOps;
using SprintPilot.Domain;
// Regression: HTML draggable is an enumerated attribute, not a Boolean attribute.
await using(var services=new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddLogging().BuildServiceProvider())
await using(var renderer=new Microsoft.AspNetCore.Components.Web.HtmlRenderer(services,services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>())){
 var example=SprintPilot.Infrastructure.PlanningExample.Create(new DateOnly(2026,9,15));
 foreach(var enabled in new[]{true,false}){
  var html=await renderer.Dispatcher.InvokeAsync(async()=>{
   var rendered=await renderer.RenderComponentAsync<SprintPilot.Web.Components.WorkGrid>(Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string,object?>{
    ["Items"]=example.Items.Take(1).ToList(),["Meta"]=example.Metadata,["Columns"]=new[]{"Title","Tags"},["SuggestedTags"]=(Func<WorkItem,string[]>)(_=>new[]{"First suggestion","Second suggestion","Third suggestion"}),
    ["CanReorder"]=(Func<WorkItem,bool>)(_=>enabled)
   }));
   return rendered.ToHtmlString();
  });
  html=WebUtility.HtmlDecode(html);
  if(!html.Contains("+ First suggestion")||html.Contains("+ Second suggestion")||html.Contains("+ Third suggestion")||!html.Contains("More (2)"))throw new Exception("Render one suggested tag and a More control by default.");
  if(!html.Contains(enabled?"draggable=\"true\"":"draggable=\"false\""))throw new Exception("Drag handle must render an explicit HTML true/false value.");
 }
}
var home=new SprintPilot.Web.Components.Pages.Home();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
typeof(SprintPilot.Web.Components.Pages.Home).GetField("items",flags)!.SetValue(home,new List<WorkItem>{
 new(){Id=1,OwnerId="external-owner",Owner="External colleague",Tags=["Blocked"]},new(){Id=2}
});
var personName=typeof(SprintPilot.Web.Components.Pages.Home).GetMethod("PersonName",flags)!;
if((string)personName.Invoke(home,new object[]{"external-owner"})!="External colleague"||
 (string)personName.Invoke(home,new object[]{""})!="Unassigned"||
 ((string)personName.Invoke(home,new object[]{"missing-owner"})!).Equals("Unassigned"))
 throw new Exception("Only genuinely unassigned work may use the Unassigned label.");
Console.WriteLine("PASS rendered drag attributes and external-owner labels");
await ApprovalChecks.Run();
var fake=new FakeAzure();using var http=new HttpClient(fake);var tracker=new AzureTracker(http,new FakeCredentials(),new PatAuthentication(),NullLogger<AzureTracker>.Instance);
int count=0;void Check(bool x,string name){if(!x)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
var meta=await tracker.MetadataAsync();Check(meta.Types[0].EstimateField=="Microsoft.VSTS.Scheduling.Effort","Process-specific estimate mapping");
Check(fake.SawConnectionDataParameters,"Connection test supplies Azure DevOps synchronization parameters");
var calls=fake.Calls;await tracker.MetadataAsync();Check(fake.Calls==calls,"Metadata cache suppresses repeated requests");
var capacity=await tracker.CapacityAsync("sprint");Check(capacity.Members.Single().CapacityPerDay==6,"Azure DevOps capacity-per-day is retained");Check(capacity.Members.Single().DaysOff.Length==1,"Azure DevOps member days off are retained");
var projects=await tracker.ProjectsAsync();Check(projects.Count==2&&projects[0].Name=="Archive"&&projects[1].Name=="Project","Accessible projects are listed and sorted");
var repositories=await tracker.RepositoriesAsync("Project");Check(repositories.Count==1&&repositories[0].Id=="repo"&&repositories[0].DefaultBranch=="main","Git repositories expose normalized default branches");
var branches=await tracker.BranchesAsync("Project","repo");var oldBranch=branches.Single(b=>b.Name=="feature/old");
Check(branches.Count==3&&branches.Single(b=>b.Name=="main").IsDefault&&branches.Single(b=>b.Name=="feature/live").HasActivePullRequest,"Branch inventory marks default and active-PR refs");
Check(oldBranch.HasCompletedPullRequest&&oldBranch.LastCommitAuthor=="Test Author"&&oldBranch.LastCommitDate is not null,"Branch inventory attaches merged-tip and latest-commit evidence");
var deleted=await tracker.DeleteBranchesAsync("Project","repo",[new("feature/old",oldBranch.ObjectId)]);
Check(deleted.Count==1&&deleted[0].Success&&fake.LastRefDelete is not null&&fake.LastRefDelete[0]?["oldObjectId"]?.ToString()==oldBranch.ObjectId&&fake.LastRefDelete[0]?["newObjectId"]?.ToString()==new string('0',40),"Branch delete uses reviewed object ID and zero target ref");
var pullRequests=await tracker.PullRequestsAsync("Project","repo");var stalePr=pullRequests.Single(p=>p.Id==17);
Check(pullRequests.Count==2&&stalePr.SourceBranch=="feature/stale"&&stalePr.TargetBranch=="main"&&stalePr.IsDraft,"Active pull requests expose normalized refs and draft state");
Check(stalePr.LastCommitAuthor=="Test Author"&&stalePr.LastCommitDate is not null&&stalePr.ReviewerCount==2&&stalePr.ApprovalCount==0&&stalePr.BlockingVoteCount==0,"Pull request cleanup includes source activity and reviewer signals");
var abandoned=await tracker.AbandonPullRequestsAsync("Project","repo",[new(17,stalePr.SourceCommitId)]);
Check(abandoned.Single().Success&&fake.LastPrUpdate?["status"]?.ToString()=="abandoned","Pull request cleanup abandons a reviewed unchanged active PR");
var staleGuard=await tracker.AbandonPullRequestsAsync("Project","repo",[new(17,"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]);
Check(!staleGuard.Single().Success&&fake.PrPatchCalls==1,"Pull request cleanup refuses to abandon when the reviewed source commit changed");
var rows=await tracker.SprintAsync("Project\\Sprint 'A'");Check(rows.Count==201&&fake.BatchSizes.SequenceEqual(new[]{200,1}),"Read batching respects 200-item limit");
Check(rows[0].Id==201&&rows[0].Order==1&&rows[^1].Id==1&&rows[^1].Order==201,"Sprint read follows the iteration backlog order instead of global backlog rank");
await tracker.ReorderSprintAsync("sprint","Project\\Sprint 'A'",200,201,199);
Check(fake.ReorderCalls==1&&fake.LastReorder?["ids"]?[0]?.GetValue<int>()==200&&fake.LastReorder?["previousId"]?.GetValue<int>()==201&&fake.LastReorder?["nextId"]?.GetValue<int>()==199&&fake.LastReorder?["iterationPath"]?.ToString()=="Project\\Sprint 'A'","Sprint reorder uses the iteration-specific workitemsorder API");
Check(rows[0].Parent==5000&&rows[0].Children.SequenceEqual(new[]{6000}),"Batch relations are retained");
Check(rows[0].Url=="https://dev.azure.com/example/Project/_workitems/edit/201","Azure DevOps link is available when the batch omits hyperlinks");
Check(fake.SawExpand,"Batch request expands fields and relationships so backlog order is populated");
var saved=await tracker.UpdateAsync(new(rows[0],[new(ItemField.Estimate,8d)]));Check(saved.Revision==2,"Updated authoritative revision is returned");
Check(fake.LastPatch![0]!["op"]!.ToString()=="test"&&fake.LastPatch[0]!["path"]!.ToString()=="/rev"&&fake.LastPatch[0]!["value"]!.GetValue<int>()==1,"Revision test is first patch operation");
Check(fake.LastPatch[1]!["path"]!.ToString()=="/fields/Microsoft.VSTS.Scheduling.Effort","Patch uses discovered process field");
var tagged=rows[0] with{Tags=["API","Platform"]};
await tracker.UpdateAsync(new(tagged,[new(ItemField.Tags,"API")]));
Check(fake.LastPatch![1]!["op"]!.ToString()=="replace"&&fake.LastPatch[1]!["path"]!.ToString()=="/fields/System.Tags"&&fake.LastPatch[1]!["value"]!.ToString()=="API","Removing one tag replaces System.Tags with the remaining tags");
await tracker.UpdateAsync(new(tagged,[new(ItemField.Tags,"")]));
Check(fake.LastPatch![1]!["op"]!.ToString()=="remove"&&fake.LastPatch[1]!["path"]!.ToString()=="/fields/System.Tags","Removing the last tag removes System.Tags instead of writing an empty value");
var reordered=await tracker.UpdateAsync(new(saved,[new(ItemField.Order,900d)]));Check(reordered.Order==900,"Backlog order is returned after update");
Check(fake.LastPatch![1]!["path"]!.ToString()=="/fields/Microsoft.VSTS.Common.BacklogPriority","Order uses Azure DevOps process configuration field");
var before=fake.PatchCalls;try{await tracker.UpdateAsync(new(rows[0],[new(ItemField.Acceptance,"unsupported")]));throw new Exception("Unsupported field accepted");}catch(TrackerException){Check(fake.PatchCalls==before,"Unsupported field rejected before mutation");}
fake.FailPatch=true;try{await tracker.UpdateAsync(new(rows[0],[new(ItemField.Title,"Changed title")]));throw new Exception("Expected denial");}catch(TrackerException e){Check(e.Message.Contains("Permission denied")&&!e.ToString().Contains(FakeAzure.Canary),"Untrusted error bodies cannot leak into exception messages");}
Check(fake.PatchCalls==before+1,"Failed mutation is not retried automatically");
try{AzureTracker.ValidateConnection(new("https://evil.example","Project"));throw new Exception("Invalid host accepted");}catch(TrackerException){Check(true,"Organization input cannot redirect PAT to another host");}

var planningRows=await tracker.PlanningAsync(["Product Backlog Item","Task"]);
Check(planningRows.Count==201,"Planning reads beyond the selected sprint");
Check(!fake.LastWiql.Contains("System.IterationPath")&&fake.LastWiql.Contains("System.AreaPath")&&fake.LastWiql.Contains("NOT IN ('Done')")&&!fake.LastWiql.Contains("'Task'"),"Planning query spans iterations, respects team scope, excludes done and tasks");
Check(planningRows[0].NumericFields["Microsoft.VSTS.Scheduling.Effort"]==8,"Numeric fields are retained for explicit planning-field selection");
fake.Paging=true;
var paged=await tracker.PlanningAsync(["Product Backlog Item"]);
Check(paged.Count==2003&&paged.Select(w=>w.Id).Distinct().Count()==2003,"Planning keyset pagination retains every item across pages");
fake.Paging=false;fake.OmitOne=true;
try{await tracker.PlanningAsync(["Product Backlog Item"]);throw new Exception("Partial read was accepted");}catch(TrackerException e){Check(e.Message.Contains("No partial dashboard"),"Incomplete batch reads fail rather than undercount workload");}
Console.WriteLine($"{count} integration checks passed. HTTP is simulated; no Azure DevOps writes were made.");
sealed class FakeCredentials:ICredentialStore {
 public ValueTask<Credentials?> GetAsync(CancellationToken ct=default)=>ValueTask.FromResult<Credentials?>(new(new("example","Project","team"),FakeAzure.Canary));
 public ValueTask SaveAsync(Credentials c,CancellationToken ct=default)=>ValueTask.CompletedTask;
}
sealed class FakeAzure:HttpMessageHandler {
 public const string Canary="synthetic-test-value-do-not-log";
 public int Calls,PatchCalls,ReorderCalls,PrPatchCalls;public List<int> BatchSizes=[];public bool SawExpand,SawConnectionDataParameters,FailPatch,Paging,OmitOne;public string LastWiql="";public JsonArray? LastPatch,LastRefDelete;public JsonObject? LastReorder,LastPrUpdate;
 static JsonObject Item(int id,int rev=1)=>new(){["id"]=id,["rev"]=rev,["fields"]=new JsonObject{["System.Title"]="Test item",["System.WorkItemType"]="Product Backlog Item",["System.State"]="New",["System.IterationPath"]="Project\\Sprint 'A'",["System.AreaPath"]="Project",["Microsoft.VSTS.Scheduling.Effort"]=8,["Microsoft.VSTS.Common.StackRank"]=100,["Microsoft.VSTS.Common.BacklogPriority"]=900},["relations"]=new JsonArray(new JsonObject{["rel"]="System.LinkTypes.Hierarchy-Reverse",["url"]="https://dev.azure.com/example/_apis/wit/workItems/5000"},new JsonObject{["rel"]="System.LinkTypes.Hierarchy-Forward",["url"]="https://dev.azure.com/example/_apis/wit/workItems/6000"})};
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Calls++;var p=r.RequestUri!.AbsolutePath;var body=r.Content is null?"":await r.Content.ReadAsStringAsync(ct);JsonNode n;
  if(r.Headers.Authorization?.Scheme!="Basic")throw new Exception("Authentication header missing");
  if(r.Method==HttpMethod.Patch&&p.EndsWith("/workitemsorder")){ReorderCalls++;LastReorder=JsonNode.Parse(body)!.AsObject();n=new JsonObject{["value"]=new JsonArray(new JsonObject{["id"]=LastReorder["ids"]![0]!.GetValue<int>(),["order"]=1200000000})};}
  else if(r.Method==HttpMethod.Patch&&p.EndsWith("/_apis/git/repositories/repo/pullrequests/17")){PrPatchCalls++;LastPrUpdate=JsonNode.Parse(body)!.AsObject();n=JsonNode.Parse("""{"pullRequestId":17,"status":"abandoned","lastMergeSourceCommit":{"commitId":"4444444444444444444444444444444444444444"}}""")!;}
  else if(r.Method==HttpMethod.Patch){PatchCalls++;LastPatch=JsonNode.Parse(body)!.AsArray();if(FailPatch)return new(HttpStatusCode.Forbidden){Content=new StringContent(Canary)};n=Item(1,2);}
  else if(p.EndsWith("/connectionData")){var q=r.RequestUri.Query;SawConnectionDataParameters=q.Contains("connectOptions=1")&&q.Contains("lastChangeId=-1")&&q.Contains("lastChangeId64=-1")&&!q.Contains("api-version");if(!SawConnectionDataParameters)return new(HttpStatusCode.BadRequest);n=JsonNode.Parse("""{"authenticatedUser":{"id":"me","providerDisplayName":"Test user"}}""")!;}
  else if(p.EndsWith("/_apis/projects"))n=JsonNode.Parse("""{"value":[{"id":"project","name":"Project"},{"id":"archive","name":"Archive"}]}""")!;
  else if(p.EndsWith("/_apis/git/repositories")&&r.Method==HttpMethod.Get)n=JsonNode.Parse("""{"value":[{"id":"repo","name":"Repo","defaultBranch":"refs/heads/main","isDisabled":false}]}""")!;
  else if(p.EndsWith("/_apis/git/repositories/repo"))n=JsonNode.Parse("""{"id":"repo","name":"Repo","defaultBranch":"refs/heads/main"}""")!;
  else if(p.EndsWith("/_apis/git/repositories/repo/refs")&&r.Method==HttpMethod.Get)n=JsonNode.Parse("""{"value":[{"name":"refs/heads/main","objectId":"1111111111111111111111111111111111111111","isLocked":false,"creator":{"displayName":"Test User"}},{"name":"refs/heads/feature/live","objectId":"2222222222222222222222222222222222222222","isLocked":false,"creator":{"displayName":"Live User"}},{"name":"refs/heads/feature/old","objectId":"3333333333333333333333333333333333333333","isLocked":false,"creator":{"displayName":"Old User"}}]}""")!;
  else if(p.EndsWith("/_apis/git/repositories/repo/refs")&&r.Method==HttpMethod.Post){LastRefDelete=JsonNode.Parse(body)!.AsArray();n=new JsonArray(new JsonObject{{"name","refs/heads/feature/old"},{"updateStatus","succeeded"},{"success",true}});}
  else if(p.EndsWith("/_apis/git/repositories/repo/pullrequests")){var active=r.RequestUri!.Query.Contains("status=active",StringComparison.OrdinalIgnoreCase);n=active?JsonNode.Parse("""{"value":[{"pullRequestId":16,"title":"Live change","status":"active","sourceRefName":"refs/heads/feature/live","targetRefName":"refs/heads/main","creationDate":"2026-09-20T12:00:00Z","isDraft":false,"createdBy":{"displayName":"Live User"},"lastMergeSourceCommit":{"commitId":"2222222222222222222222222222222222222222"},"reviewers":[{"vote":10}]},{"pullRequestId":17,"title":"Stale draft","status":"active","sourceRefName":"refs/heads/feature/stale","targetRefName":"refs/heads/main","creationDate":"2026-01-01T12:00:00Z","isDraft":true,"mergeStatus":"notSet","createdBy":{"displayName":"Old User"},"lastMergeSourceCommit":{"commitId":"4444444444444444444444444444444444444444"},"reviewers":[{"vote":0},{"vote":0}]}]}""")!:JsonNode.Parse("""{"value":[{"sourceRefName":"refs/heads/feature/old","lastMergeSourceCommit":{"commitId":"3333333333333333333333333333333333333333"}}]}""")!;}
  else if(p.EndsWith("/_apis/git/repositories/repo/pullrequests/17"))n=JsonNode.Parse("""{"pullRequestId":17,"title":"Stale draft","status":"active","sourceRefName":"refs/heads/feature/stale","targetRefName":"refs/heads/main","lastMergeSourceCommit":{"commitId":"4444444444444444444444444444444444444444"}}""")!;
  else if(p.Contains("/_apis/git/repositories/repo/commits/")){var sha=p.Split('/').Last();n=new JsonObject{{"commitId",sha},{"comment","Test commit"},{"author",new JsonObject{{"name","Test Author"},{"date","2026-01-01T12:00:00Z"}}}};}
  else if(p.EndsWith("/members"))n=JsonNode.Parse("""{"value":[{"identity":{"id":"me","displayName":"Test user","uniqueName":"test@example.test"}}]}""")!;
  else if(p.EndsWith("/teams"))n=JsonNode.Parse("""{"value":[{"id":"team","name":"Team"}]}""")!;
  else if(p.EndsWith("/iterations"))n=JsonNode.Parse("""{"value":[{"id":"sprint","name":"Sprint A","path":"Project\\Sprint 'A'","attributes":{}}]}""")!;
  else if(p.EndsWith("/teamfieldvalues"))n=JsonNode.Parse("""{"values":[{"value":"Project","includeChildren":true}]}""")!;
  else if(p.EndsWith("/capacities"))n=JsonNode.Parse("""{"value":[{"teamMember":{"id":"me","displayName":"Test user"},"activities":[{"name":"Development","capacityPerDay":6}],"daysOff":[{"start":"2026-12-24T00:00:00Z","end":"2026-12-31T00:00:00Z"}]}]}""")!;
  else if(p.EndsWith("/teamdaysoff"))n=JsonNode.Parse("""{"daysOff":[]}""")!;
  else if(p.EndsWith("/areas"))n=JsonNode.Parse("""{"name":"Project","children":[]}""")!;
  else if(p.EndsWith("/processconfiguration"))n=JsonNode.Parse("""{"typeFields":{"Order":{"referenceName":"Microsoft.VSTS.Common.BacklogPriority"}}}""")!;
  else if(p.EndsWith("/workitemtypes"))n=JsonNode.Parse("""{"value":[{"name":"Product Backlog Item"}]}""")!;
  else if(p.Contains("/workitemtypes/"))n=JsonNode.Parse("""{"fields":[{"referenceName":"System.Title"},{"referenceName":"System.Tags"},{"referenceName":"Microsoft.VSTS.Scheduling.Effort"},{"referenceName":"Microsoft.VSTS.Common.StackRank"},{"referenceName":"Microsoft.VSTS.Common.BacklogPriority"}],"states":[{"name":"New","category":"Proposed"},{"name":"Done","category":"Completed"}]}""")!;
  else if(p.EndsWith("/iterations/sprint/workitems")){n=new JsonObject{["workItemRelations"]=new JsonArray(Enumerable.Range(1,201).Reverse().Select(i=>(JsonNode)new JsonObject{["rel"]=null,["source"]=null,["target"]=new JsonObject{["id"]=i,["url"]=$"https://dev.azure.com/example/_apis/wit/workItems/{i}"}}).ToArray())};}
  else if(p.EndsWith("/wiql")){LastWiql=JsonNode.Parse(body)!["query"]!.ToString();var start=Paging?int.Parse(System.Text.RegularExpressions.Regex.Match(LastWiql,@"\[System.Id\] > (\d+)").Groups[1].Value)+1:1;var length=Paging?(start==1?2000:3):201;n=new JsonObject{["workItems"]=new JsonArray(Enumerable.Range(start,length).Select(i=>(JsonNode)new JsonObject{["id"]=i}).ToArray())};}
  else if(p.EndsWith("/workitemsbatch")){var b=JsonNode.Parse(body)!;var ids=b["ids"]!.AsArray();BatchSizes.Add(ids.Count);SawExpand=b["$expand"]?.ToString()=="All";n=new JsonObject{["value"]=new JsonArray(ids.Take(OmitOne?Math.Max(0,ids.Count-1):ids.Count).Select(i=>(JsonNode)Item(i!.GetValue<int>())).ToArray())};}
  else n=new JsonObject{["id"]="project"};
  return new(HttpStatusCode.OK){Content=new StringContent(n.ToJsonString(),Encoding.UTF8,"application/json")};
 }
}
