using System.Globalization;
using System.Text.Json.Nodes;
using SprintPilot.Application;
using SprintPilot.Domain;

namespace SprintPilot.AzureDevOps;

public sealed partial class AzureTracker
{
 public async Task SetDaysOffAsync(string iterationId,string personId,IReadOnlyList<DateRange> daysOff,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(iterationId)||string.IsNullOrWhiteSpace(personId))throw new TrackerException("Choose a sprint and person first.");
  var c=await Credentials(ct);var meta=await MetadataAsync(ct:ct);
  var team=meta.Teams.FirstOrDefault(t=>t.Id==c.Connection.Team||t.Name==c.Connection.Team)??meta.Teams.First();
  var basePath=$"{E(team.Id)}/_apis/work/teamsettings/iterations/{E(iterationId)}/capacities";
  var current=await Send(c,basePath+"?api-version=6.0",HttpMethod.Get,ct:ct,apiVersion:false);
  var row=((JsonArray?)current["value"]??[]).FirstOrDefault(n=>S(n?["teamMember"]?["id"]).Equals(personId,StringComparison.OrdinalIgnoreCase))
      ??throw new TrackerException("This person has no capacity row for the selected sprint.");
  var activities=((JsonArray?)row?["activities"]??[]).Select(a=>new {
   name=S(a?["name"]),
   capacityPerDay=double.TryParse(S(a?["capacityPerDay"]),NumberStyles.Float,CultureInfo.InvariantCulture,out var h)&&double.IsFinite(h)?h:0
  }).ToArray();
  var body=new {
   activities,
   daysOff=daysOff.OrderBy(x=>x.Start).Select(x=>new {start=x.Start.ToString("O"),end=x.End.ToString("O")}).ToArray()
  };
  await Send(c,basePath+"/"+E(personId)+"?api-version=6.0",HttpMethod.Patch,body,ct:ct,apiVersion:false);
 }

 public async Task<PipelineActivitySnapshot> PipelineActivityAsync(string project,int failedDays=5,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(project))throw new TrackerException("Choose a project first.");
  failedDays=Math.Clamp(failedDays,1,30);
  var c=await Credentials(ct);var min=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-failedDays).ToString("O",CultureInfo.InvariantCulture));
  var builds=Values(await Send(c,$"_apis/build/builds?queryOrder=queueTimeDescending&minTime={min}&$top=100",HttpMethod.Get,ct:ct,projectOverride:project));
  DateTimeOffset? D(JsonNode? n)=>DateTimeOffset.TryParse(S(n),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var d)?d:null;
  async Task<PipelineRunSummary> Map(JsonNode n)
  {
   var id=n["id"]?.GetValue<int>()??0;var status=S(n["status"]);var result=S(n["result"]);var currentStage="";var failedStage="";
   if(id>0&&(status.Equals("inProgress",StringComparison.OrdinalIgnoreCase)||status.Equals("notStarted",StringComparison.OrdinalIgnoreCase)||result.Equals("failed",StringComparison.OrdinalIgnoreCase)))
   {
    try
    {
     var timeline=Values(await Send(c,$"_apis/build/builds/{id}/timeline",HttpMethod.Get,ct:ct,projectOverride:project));
     var stages=timeline.Where(r=>S(r?["type"]).Equals("Stage",StringComparison.OrdinalIgnoreCase)).ToArray();
     currentStage=S(stages.FirstOrDefault(r=>S(r?["state"]).Equals("inProgress",StringComparison.OrdinalIgnoreCase))?["name"]);
     failedStage=S(stages.FirstOrDefault(r=>S(r?["result"]).Equals("failed",StringComparison.OrdinalIgnoreCase))?["name"]);
     if(failedStage==""&&result.Equals("failed",StringComparison.OrdinalIgnoreCase))
      failedStage=S(timeline.FirstOrDefault(r=>S(r?["result"]).Equals("failed",StringComparison.OrdinalIgnoreCase))?["name"]);
    }catch(TrackerException){}
   }
   var url=S(n["_links"]?["web"]?["href"]);
   return new PipelineRunSummary(id,S(n["definition"]?["name"]),S(n["buildNumber"]),project,StripHead(S(n["sourceBranch"])),status,result,currentStage,failedStage,D(n["startTime"]),D(n["finishTime"]),url);
  }
  var candidates=builds.Where(n=>n is not null).Cast<JsonNode>().ToArray();
  var mapped=new List<PipelineRunSummary>();
  foreach(var n in candidates.Where(n=>S(n["status"]).Equals("inProgress",StringComparison.OrdinalIgnoreCase)||S(n["status"]).Equals("notStarted",StringComparison.OrdinalIgnoreCase)||S(n["result"]).Equals("failed",StringComparison.OrdinalIgnoreCase)).Take(30))
   mapped.Add(await Map(n));
  var running=mapped.Where(x=>x.Status.Equals("inProgress",StringComparison.OrdinalIgnoreCase)||x.Status.Equals("notStarted",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.StartTime).ToArray();
  var failed=mapped.Where(x=>x.Result.Equals("failed",StringComparison.OrdinalIgnoreCase)&&(x.FinishTime??x.StartTime)>=DateTimeOffset.UtcNow.AddDays(-failedDays)).OrderByDescending(x=>x.FinishTime??x.StartTime).ToArray();
  return new(running,failed);
 }

 public async Task<IReadOnlyList<AzureEnvironment>> EnvironmentsAsync(string project,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(project))throw new TrackerException("Choose a project first.");
  var c=await Credentials(ct);
  var response=await Send(c,"_apis/distributedtask/environments?$top=1000&api-version=7.1-preview.1",HttpMethod.Get,ct:ct,apiVersion:false,projectOverride:project);
  DateTimeOffset? D(JsonNode? n)=>DateTimeOffset.TryParse(S(n),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var d)?d:null;
  return Values(response).Select(n=>new AzureEnvironment(
   n?["id"]?.GetValue<int>()??0,
   S(n?["name"]),
   S(n?["description"]),
   S(n?["createdBy"]?["displayName"]),
   D(n?["lastModifiedOn"]),
   S(n?["_links"]?["web"]?["href"])
  )).Where(x=>x.Id>0).OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
 }

 public async Task<IReadOnlyList<EnvironmentDeployment>> EnvironmentDeploymentsAsync(string project,int environmentId,int days=14,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(project)||environmentId<=0)throw new TrackerException("Choose a project and environment first.");
  days=Math.Clamp(days,1,90);
  var c=await Credentials(ct);
  var response=await Send(c,$"_apis/distributedtask/environments/{environmentId}/environmentdeploymentrecords?$top=200&api-version=7.1-preview.1",HttpMethod.Get,ct:ct,apiVersion:false,projectOverride:project);
  DateTimeOffset? D(JsonNode? n)=>DateTimeOffset.TryParse(S(n),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var d)?d:null;
  var cutoff=DateTimeOffset.UtcNow.AddDays(-days);
  return Values(response).Select(n=>{
   var start=D(n?["startTime"])??D(n?["queueTime"]);var finish=D(n?["finishTime"]);
   var definition=S(n?["definition"]?["name"]);if(definition=="")definition=S(n?["owner"]?["name"]);
   var run=S(n?["run"]?["name"]);if(run=="")run=S(n?["release"]?["name"]);if(run=="")run=S(n?["id"]);
   var branch=StripHead(S(n?["sourceBranch"]));if(branch=="")branch=StripHead(S(n?["run"]?["sourceBranch"]));
   var status=S(n?["status"]);var result=S(n?["result"]);
   var url=S(n?["_links"]?["web"]?["href"]);if(url=="")url=S(n?["run"]?["_links"]?["web"]?["href"]);
   return new EnvironmentDeployment(n?["id"]?.GetValue<int>()??0,environmentId,definition,run,branch,status,result,start,finish,url);
  }).Where(x=>(x.FinishTime??x.StartTime??DateTimeOffset.MinValue)>=cutoff).OrderByDescending(x=>x.FinishTime??x.StartTime).ToArray();
 }
}
