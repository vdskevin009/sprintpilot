using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SprintPilot.Application;
using SprintPilot.AzureDevOps;
using SprintPilot.Domain;
var fake=new FakeAzure();using var http=new HttpClient(fake);var tracker=new AzureTracker(http,new FakeCredentials(),new PatAuthentication(),NullLogger<AzureTracker>.Instance);
int count=0;void Check(bool x,string name){if(!x)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
var meta=await tracker.MetadataAsync();Check(meta.Types[0].EstimateField=="Microsoft.VSTS.Scheduling.Effort","Process-specific estimate mapping");
Check(fake.SawConnectionDataParameters,"Connection test supplies Azure DevOps synchronization parameters");
var calls=fake.Calls;await tracker.MetadataAsync();Check(fake.Calls==calls,"Metadata cache suppresses repeated requests");
var capacity=await tracker.CapacityAsync("sprint");Check(capacity.Members.Single().CapacityPerDay==6,"Azure DevOps capacity-per-day is retained");Check(capacity.Members.Single().DaysOff.Length==1,"Azure DevOps member days off are retained");
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
 public int Calls,PatchCalls,ReorderCalls;public List<int> BatchSizes=[];public bool SawExpand,SawConnectionDataParameters,FailPatch,Paging,OmitOne;public string LastWiql="";public JsonArray? LastPatch;public JsonObject? LastReorder;
 static JsonObject Item(int id,int rev=1)=>new(){["id"]=id,["rev"]=rev,["fields"]=new JsonObject{["System.Title"]="Test item",["System.WorkItemType"]="Product Backlog Item",["System.State"]="New",["System.IterationPath"]="Project\\Sprint 'A'",["System.AreaPath"]="Project",["Microsoft.VSTS.Scheduling.Effort"]=8,["Microsoft.VSTS.Common.StackRank"]=100,["Microsoft.VSTS.Common.BacklogPriority"]=900},["relations"]=new JsonArray(new JsonObject{["rel"]="System.LinkTypes.Hierarchy-Reverse",["url"]="https://dev.azure.com/example/_apis/wit/workItems/5000"},new JsonObject{["rel"]="System.LinkTypes.Hierarchy-Forward",["url"]="https://dev.azure.com/example/_apis/wit/workItems/6000"})};
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Calls++;var p=r.RequestUri!.AbsolutePath;var body=r.Content is null?"":await r.Content.ReadAsStringAsync(ct);JsonNode n;
  if(r.Headers.Authorization?.Scheme!="Basic")throw new Exception("Authentication header missing");
  if(r.Method==HttpMethod.Patch&&p.EndsWith("/workitemsorder")){ReorderCalls++;LastReorder=JsonNode.Parse(body)!.AsObject();n=new JsonObject{["value"]=new JsonArray(new JsonObject{["id"]=LastReorder["ids"]![0]!.GetValue<int>(),["order"]=1200000000})};}
  else if(r.Method==HttpMethod.Patch){PatchCalls++;LastPatch=JsonNode.Parse(body)!.AsArray();if(FailPatch)return new(HttpStatusCode.Forbidden){Content=new StringContent(Canary)};n=Item(1,2);}
  else if(p.EndsWith("/connectionData")){var q=r.RequestUri.Query;SawConnectionDataParameters=q.Contains("connectOptions=1")&&q.Contains("lastChangeId=-1")&&q.Contains("lastChangeId64=-1")&&!q.Contains("api-version");if(!SawConnectionDataParameters)return new(HttpStatusCode.BadRequest);n=JsonNode.Parse("""{"authenticatedUser":{"id":"me","providerDisplayName":"Test user"}}""")!;}
  else if(p.EndsWith("/members"))n=JsonNode.Parse("""{"value":[{"identity":{"id":"me","displayName":"Test user","uniqueName":"test@example.test"}}]}""")!;
  else if(p.EndsWith("/teams"))n=JsonNode.Parse("""{"value":[{"id":"team","name":"Team"}]}""")!;
  else if(p.EndsWith("/iterations"))n=JsonNode.Parse("""{"value":[{"id":"sprint","name":"Sprint A","path":"Project\\Sprint 'A'","attributes":{}}]}""")!;
  else if(p.EndsWith("/teamfieldvalues"))n=JsonNode.Parse("""{"values":[{"value":"Project","includeChildren":true}]}""")!;
  else if(p.EndsWith("/capacities"))n=JsonNode.Parse("""{"value":[{"teamMember":{"id":"me","displayName":"Test user"},"activities":[{"name":"Development","capacityPerDay":6}],"daysOff":[{"start":"2026-12-24T00:00:00Z","end":"2026-12-31T00:00:00Z"}]}]}""")!;
  else if(p.EndsWith("/teamdaysoff"))n=JsonNode.Parse("""{"daysOff":[]}""")!;
  else if(p.EndsWith("/areas"))n=JsonNode.Parse("""{"name":"Project","children":[]}""")!;
  else if(p.EndsWith("/processconfiguration"))n=JsonNode.Parse("""{"typeFields":{"Order":{"referenceName":"Microsoft.VSTS.Common.BacklogPriority"}}}""")!;
  else if(p.EndsWith("/workitemtypes"))n=JsonNode.Parse("""{"value":[{"name":"Product Backlog Item"}]}""")!;
  else if(p.Contains("/workitemtypes/"))n=JsonNode.Parse("""{"fields":[{"referenceName":"System.Title"},{"referenceName":"Microsoft.VSTS.Scheduling.Effort"},{"referenceName":"Microsoft.VSTS.Common.StackRank"},{"referenceName":"Microsoft.VSTS.Common.BacklogPriority"}],"states":[{"name":"New","category":"Proposed"},{"name":"Done","category":"Completed"}]}""")!;
  else if(p.EndsWith("/iterations/sprint/workitems")){n=new JsonObject{["workItemRelations"]=new JsonArray(Enumerable.Range(1,201).Reverse().Select(i=>(JsonNode)new JsonObject{["rel"]=null,["source"]=null,["target"]=new JsonObject{["id"]=i,["url"]=$"https://dev.azure.com/example/_apis/wit/workItems/{i}"}}).ToArray())};}
  else if(p.EndsWith("/wiql")){LastWiql=JsonNode.Parse(body)!["query"]!.ToString();var start=Paging?int.Parse(System.Text.RegularExpressions.Regex.Match(LastWiql,@"\[System.Id\] > (\d+)").Groups[1].Value)+1:1;var length=Paging?(start==1?2000:3):201;n=new JsonObject{["workItems"]=new JsonArray(Enumerable.Range(start,length).Select(i=>(JsonNode)new JsonObject{["id"]=i}).ToArray())};}
  else if(p.EndsWith("/workitemsbatch")){var b=JsonNode.Parse(body)!;var ids=b["ids"]!.AsArray();BatchSizes.Add(ids.Count);SawExpand=b["$expand"]?.ToString()=="All";n=new JsonObject{["value"]=new JsonArray(ids.Take(OmitOne?Math.Max(0,ids.Count-1):ids.Count).Select(i=>(JsonNode)Item(i!.GetValue<int>())).ToArray())};}
  else n=new JsonObject{["id"]="project"};
  return new(HttpStatusCode.OK){Content=new StringContent(n.ToJsonString(),Encoding.UTF8,"application/json")};
 }
}
