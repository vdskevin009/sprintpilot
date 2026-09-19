using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SprintPilot.Application;
using SprintPilot.Domain;
namespace SprintPilot.AzureDevOps;
public interface ITrackerAuthentication {ValueTask AuthenticateAsync(HttpRequestMessage request,Credentials credentials,CancellationToken ct);}
public sealed class PatAuthentication:ITrackerAuthentication {
 public ValueTask AuthenticateAsync(HttpRequestMessage request,Credentials c,CancellationToken ct){request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(":"+c.Token)));return ValueTask.CompletedTask;}
}
public sealed class AzureTracker(HttpClient http,ICredentialStore store,ITrackerAuthentication auth,ILogger<AzureTracker> logger):IWorkTracker {
 private ConnectionInfo? activeConnection;private Metadata? cached;private DateTimeOffset expires;private string cacheKey="";private readonly SemaphoreSlim metadataGate=new(1,1);
 public static void ValidateConnection(ConnectionInfo c){if(!System.Text.RegularExpressions.Regex.IsMatch(c.Organization,@"^[A-Za-z0-9][A-Za-z0-9-]{0,100}$")||string.IsNullOrWhiteSpace(c.Project)||c.Project.Length>200)throw new TrackerException("Enter the organization name (not a URL) and a project name.");}
 static string E(string s)=>Uri.EscapeDataString(s);
 static string S(JsonNode? n)=>n?.ToString()??"";
 static JsonArray Values(JsonNode n)=>(JsonArray?)n["value"]??[];
 async Task<Credentials> Credentials(CancellationToken ct)=>await store.GetAsync(ct)??throw new TrackerException("Azure DevOps connection is required.");
 async Task<JsonNode> Send(Credentials c,string path,HttpMethod method,object? body=null,bool patch=false,CancellationToken ct=default,bool organization=false,bool apiVersion=true){
  ValidateConnection(c.Connection);activeConnection=c.Connection;var baseUrl=$"https://dev.azure.com/{E(c.Connection.Organization)}/"+(organization?"":E(c.Connection.Project)+"/");
  for(int attempt=0;;attempt++){
   using var req=new HttpRequestMessage(method,baseUrl+path+(apiVersion?(path.Contains('?')?"&":"?")+"api-version=7.1":""));await auth.AuthenticateAsync(req,c,ct);
   if(body is not null)req.Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(body),Encoding.UTF8,patch?"application/json-patch+json":"application/json");
   HttpResponseMessage response;
   try{response=await http.SendAsync(req,ct);}catch(HttpRequestException){throw new TrackerException("Cannot reach Azure DevOps. Check network, VPN and proxy settings.");}
   using(response){
    // Retry reads only. Never replay a mutation after an ambiguous network outcome.
    if(method!=HttpMethod.Patch && !path.Contains("workitems/$") && !path.Contains("/comments",StringComparison.OrdinalIgnoreCase) && (response.StatusCode==(HttpStatusCode)429 || response.StatusCode==HttpStatusCode.ServiceUnavailable) && attempt<2){var wait=response.Headers.RetryAfter?.Delta??TimeSpan.FromSeconds(attempt+1);await Task.Delay(wait>TimeSpan.FromSeconds(10)?TimeSpan.FromSeconds(10):wait,ct);continue;}
    if(!response.IsSuccessStatusCode){logger.LogWarning("Azure DevOps operation failed with status {Status}",(int)response.StatusCode);throw new TrackerException(response.StatusCode switch {HttpStatusCode.Unauthorized=>"Authentication failed. Check or renew your PAT.",HttpStatusCode.Forbidden=>"Permission denied. Check project access and token scopes.",HttpStatusCode.NotFound=>"Item or project not found, or access is denied.",HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed=>"Revision conflict. Refresh and review the newer item before retrying.",HttpStatusCode.BadRequest when method==HttpMethod.Get=>"Azure DevOps rejected the connection request. Verify the organization and project names, then try again.",HttpStatusCode.BadRequest=>"Azure DevOps rejected the fields, query, transition, or revision. Refresh and verify the proposed change.",(HttpStatusCode)429=>"Azure DevOps is throttling requests. Wait before retrying.",_=>"Azure DevOps request failed. Refresh to confirm server state before retrying."});}
    try{return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))??throw new TrackerException("Empty Azure DevOps response.");}catch(System.Text.Json.JsonException){throw new TrackerException("Unexpected Azure DevOps response.");}
   }
  }
 }
 public async Task<Person> TestAsync(Credentials credentials,CancellationToken ct=default){
  await Send(credentials,"_apis/projects/"+E(credentials.Connection.Project),HttpMethod.Get,ct:ct,organization:true);
  // Azure DevOps' connection-data endpoint expects synchronization parameters on
  // some organizations. Supplying the documented initial values keeps this
  // authentication-only check compatible across account configurations.
  var n=await Send(credentials,"_apis/connectionData?connectOptions=1&lastChangeId=-1&lastChangeId64=-1",HttpMethod.Get,ct:ct,organization:true,apiVersion:false);var u=n["authenticatedUser"];return new(S(u?["id"]),S(u?["providerDisplayName"]),S(u?["properties"]?["Account"]?["$value"]));
 }
 public async Task<Metadata> MetadataAsync(bool refresh=false,CancellationToken ct=default){await metadataGate.WaitAsync(ct);try{
  var c=await Credentials(ct);var key=c.Connection.ToString();if(!refresh&&cached is not null&&cacheKey==key&&expires>DateTimeOffset.UtcNow)return cached;
  var teamList=new List<Team>();
  for(var skip=0;;skip+=100){var page=Values(await Send(c,$"_apis/projects/{E(c.Connection.Project)}/teams?$top=100&$skip={skip}",HttpMethod.Get,ct:ct,organization:true));teamList.AddRange(page.Select(n=>new Team(S(n?["id"]),S(n?["name"]))));if(page.Count<100)break;}
  var teams=teamList.ToArray();if(teams.Length==0)throw new TrackerException("No accessible teams were found.");
  var team=teams.FirstOrDefault(t=>t.Id==c.Connection.Team||t.Name==c.Connection.Team)??teams[0];
  var iterTask=Send(c,$"{E(team.Id)}/_apis/work/teamsettings/iterations",HttpMethod.Get,ct:ct);
  var areaTask=Send(c,"_apis/wit/classificationnodes/areas?$depth=14",HttpMethod.Get,ct:ct);
  var typesTask=Send(c,"_apis/wit/workitemtypes",HttpMethod.Get,ct:ct);
  var scopeTask=Send(c,$"{E(team.Id)}/_apis/work/teamsettings/teamfieldvalues",HttpMethod.Get,ct:ct);
  var meTask=TestAsync(c,ct);
  string processOrderField="";
  try{var process=await Send(c,"_apis/work/processconfiguration",HttpMethod.Get,ct:ct);processOrderField=S(process["typeFields"]?["Order"]?["referenceName"]);}catch(TrackerException){}
  var people=new List<Person>();for(int skip=0;;skip+=100){var page=Values(await Send(c,$"_apis/projects/{E(c.Connection.Project)}/teams/{E(team.Id)}/members?$top=100&$skip={skip}",HttpMethod.Get,ct:ct,organization:true));people.AddRange(page.Select(n=>n?["identity"]).Select(n=>new Person(S(n?["id"]),S(n?["displayName"]),S(n?["uniqueName"]))));if(page.Count<100)break;}
  DateTimeOffset? Date(JsonNode? n)=>DateTimeOffset.TryParse(S(n),out var d)?d:null;
  var iterations=Values(await iterTask).Select(n=>new Iteration(S(n?["id"]),S(n?["name"]),S(n?["path"]),Date(n?["attributes"]?["startDate"]),Date(n?["attributes"]?["finishDate"]))).OrderBy(i=>i.Start??DateTimeOffset.MaxValue).ThenBy(i=>i.Path,StringComparer.OrdinalIgnoreCase).ToArray();
  var areas=new List<string>();void Walk(JsonNode? n,string parent){if(n is null)return;var path=parent==""?S(n["name"]):parent+"\\"+S(n["name"]);areas.Add(path);foreach(var child in (JsonArray?)n["children"]??[])Walk(child,path);}Walk(await areaTask,"");
  var types=new List<TypeDefinition>();foreach(var n in Values(await typesTask)){
   var name=S(n?["name"]);if(n?["isDisabled"]?.GetValue<bool>()==true)continue;
   var detail=await Send(c,"_apis/wit/workitemtypes/"+E(name),HttpMethod.Get,ct:ct);
   var fields=((JsonArray?)detail["fields"]??[]).Select(f=>S(f?["referenceName"])).ToArray();
   var states=((JsonArray?)detail["states"]??[]).Select(s=>new StateDefinition(S(s?["name"]),S(s?["category"]))).ToArray();
   var estimate=new[]{"Microsoft.VSTS.Scheduling.StoryPoints","Microsoft.VSTS.Scheduling.Effort","Microsoft.VSTS.Scheduling.Size","Microsoft.VSTS.Scheduling.RemainingWork"}.FirstOrDefault(fields.Contains);
   var order=processOrderField!=""&&fields.Contains(processOrderField)?processOrderField:new[]{"Microsoft.VSTS.Common.StackRank","Microsoft.VSTS.Common.BacklogPriority"}.FirstOrDefault(fields.Contains);
   types.Add(new(name,states,fields,estimate,order));
  }
  var scope=((JsonArray?)(await scopeTask)["values"]??[]).Select(n=>new AreaScope(S(n?["value"]),n?["includeChildren"]?.GetValue<bool>()??false)).ToArray();
  cached=new(teams,people.ToArray(),iterations,areas.ToArray(),types.ToArray(),scope,await meTask);cacheKey=key;expires=DateTimeOffset.UtcNow.AddMinutes(15);return cached;
 }finally{metadataGate.Release();}}
 public static string WiqlLiteral(string s)=>s.Replace("'","''");
 public async Task<SprintCapacity> CapacityAsync(string iterationId,CancellationToken ct=default){
  var c=await Credentials(ct);var meta=await MetadataAsync(ct:ct);var team=meta.Teams.FirstOrDefault(t=>t.Id==c.Connection.Team||t.Name==c.Connection.Team)??meta.Teams.First();
  DateRange[] Ranges(JsonNode? node)=>((JsonArray?)node??[]).Select(r=>new{Start=S(r?["start"]),End=S(r?["end"])}).Where(r=>DateTimeOffset.TryParse(r.Start,out _)&&DateTimeOffset.TryParse(r.End,out _)).Select(r=>new DateRange(DateTimeOffset.Parse(r.Start,CultureInfo.InvariantCulture),DateTimeOffset.Parse(r.End,CultureInfo.InvariantCulture))).ToArray();
  // Capacity/days-off endpoints use the legacy 6.0 contract in this environment.
  // Put api-version in the path and disable Send's default 7.1 query parameter.
  var capacity=await Send(c,$"{E(team.Id)}/_apis/work/teamsettings/iterations/{E(iterationId)}/capacities?api-version=6.0",HttpMethod.Get,ct:ct,apiVersion:false);
  var members=((JsonArray?)capacity["value"]??[]).Select(n=>{var person=n?["teamMember"];var daily=((JsonArray?)n?["activities"]??[]).Select(a=>double.TryParse(S(a?["capacityPerDay"]),NumberStyles.Float,CultureInfo.InvariantCulture,out var h)&&double.IsFinite(h)&&h>0?h:0).Sum();return new MemberCapacity(S(person?["id"]),S(person?["displayName"]),Ranges(n?["daysOff"]),daily);}).Where(m=>m.PersonId!="").ToArray();
  var teamDays=await Send(c,$"{E(team.Id)}/_apis/work/teamsettings/iterations/{E(iterationId)}/teamdaysoff?api-version=6.0",HttpMethod.Get,ct:ct,apiVersion:false);
  return new SprintCapacity(members,Ranges(teamDays["daysOff"]));
 }
 public async Task<IReadOnlyList<WorkItem>> SprintAsync(string iteration,CancellationToken ct=default){
  var c=await Credentials(ct);var meta=await MetadataAsync(ct:ct);
  if(meta.Scope.Length==0)throw new TrackerException("The selected team has no configured area paths.");
  var scopes=string.Join(" OR ",meta.Scope.Select(a=>$"[System.AreaPath] {(a.IncludeChildren?"UNDER":"=")} '{WiqlLiteral(a.Path)}'"));
  var orderField=meta.Types.Select(t=>t.OrderField).FirstOrDefault(f=>!string.IsNullOrWhiteSpace(f));
  var orderBy=orderField is null?"[System.Id]":$"[{orderField}], [System.Id]";
  var n=await Send(c,"_apis/wit/wiql",HttpMethod.Post,new{query=$"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.IterationPath] = '{WiqlLiteral(iteration)}' AND ({scopes}) ORDER BY {orderBy}"},ct:ct);
  var ids=((JsonArray?)n["workItems"]??[]).Select(x=>x!["id"]!.GetValue<int>()).ToArray();var items=new List<WorkItem>();
  foreach(var chunk in ids.Chunk(200)){var batch=await Send(c,"_apis/wit/workitemsbatch",HttpMethod.Post,new Dictionary<string,object>{["ids"]=chunk,["$expand"]="All",["errorPolicy"]="Fail"},ct:ct);items.AddRange(Values(batch).Select(Map));}
  return items.OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToArray();
 }

 public async Task<IReadOnlyList<WorkItem>> PlanningAsync(string[] types,CancellationToken ct=default)
 {
  var c=await Credentials(ct);var meta=await MetadataAsync(ct:ct);
  var selectedTypes=meta.Types.Where(t=>types.Contains(t.Name,StringComparer.OrdinalIgnoreCase)&&!t.Name.Equals("Task",StringComparison.OrdinalIgnoreCase)).ToArray();
  if(selectedTypes.Length==0)throw new TrackerException("No selected backlog/bug types are available in this project.");
  if(meta.Scope.Length==0)throw new TrackerException("The selected team has no configured area paths.");
  var scopes=string.Join(" OR ",meta.Scope.Select(a=>$"[System.AreaPath] {(a.IncludeChildren?"UNDER":"=")} '{WiqlLiteral(a.Path)}'"));
  var typeQueries=selectedTypes.Select(t=>{
   var completed=t.States.Where(s=>s.Category is "Completed" or "Removed").Select(s=>$"'{WiqlLiteral(s.Name)}'").ToArray();
   return $"([System.WorkItemType] = '{WiqlLiteral(t.Name)}'"+(completed.Length>0?$" AND [System.State] NOT IN ({string.Join(",",completed)})":"")+")";
  });
  var rows=new List<WorkItem>();var lastId=0;
  while(true){
   ct.ThrowIfCancellationRequested();
   var query=$"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.Id] > {lastId} AND ({scopes}) AND ({string.Join(" OR ",typeQueries)}) ORDER BY [System.Id]";
   var response=await Send(c,"_apis/wit/wiql?$top=2000",HttpMethod.Post,new{query},ct:ct);
   var ids=((JsonArray?)response["workItems"]??[]).Select(n=>n!["id"]!.GetValue<int>()).Distinct().Order().ToArray();
   if(ids.Length==0)break;
   if(ids[0]<=lastId)throw new TrackerException("Planning pagination did not advance. Refresh and try again.");
   if(rows.Count+ids.Length>50000)throw new TrackerException("Planning exceeds 50,000 items. Narrow the team's area scope; no partial dashboard was shown.");
   foreach(var chunk in ids.Chunk(200)){
    var batch=await Send(c,"_apis/wit/workitemsbatch",HttpMethod.Post,new Dictionary<string,object>{["ids"]=chunk,["$expand"]="All",["errorPolicy"]="Fail"},ct:ct);
    var values=Values(batch);if(values.Count!=chunk.Length)throw new TrackerException("Some planning items could not be read. No partial dashboard was shown.");
    rows.AddRange(values.Select(Map));
   }
   lastId=ids[^1];if(ids.Length<2000)break;
  }
  return rows;
 }
 WorkItem Map(JsonNode? n){var f=n?["fields"];string Field(string key)=>S(f?[key]);int? Number(string key)=>int.TryParse(Field(key),out var x)?x:null;
  var type=Field("System.WorkItemType");var estimate=cached?.Types.FirstOrDefault(t=>t.Name==type)?.EstimateField;
  var relations=(JsonArray?)n?["relations"]??[];int? Id(JsonNode? rel)=>int.TryParse(S(rel?["url"]).Split('/').Last(),out var x)?x:null;
  var order=cached?.Types.FirstOrDefault(t=>t.Name==type)?.OrderField;var owner=f?["System.AssignedTo"];return new(){NumericFields=(f as JsonObject)?.Where(kv=>kv.Value is JsonValue v&&v.TryGetValue<double>(out _)).ToDictionary(kv=>kv.Key,kv=>kv.Value!.GetValue<double>())??new(),Id=n?["id"]?.GetValue<int>()??0,Revision=n?["rev"]?.GetValue<int>()??0,CommentCount=Number("System.CommentCount")??0,Type=type,Title=Field("System.Title"),Owner=owner is JsonObject?S(owner["displayName"]):S(owner),OwnerId=owner is JsonObject?S(owner["id"]):"",State=Field("System.State"),Iteration=Field("System.IterationPath"),Area=Field("System.AreaPath"),Estimate=estimate is not null && double.TryParse(Field(estimate),NumberStyles.Float,CultureInfo.InvariantCulture,out var e)?e:null,Order=order is not null&&double.TryParse(Field(order),NumberStyles.Float,CultureInfo.InvariantCulture,out var rank)?rank:null,Priority=Number("Microsoft.VSTS.Common.Priority"),Description=Field("System.Description"),Acceptance=Field("Microsoft.VSTS.Common.AcceptanceCriteria"),Parent=Id(relations.FirstOrDefault(r=>S(r?["rel"])=="System.LinkTypes.Hierarchy-Reverse")),Children=relations.Where(r=>S(r?["rel"])=="System.LinkTypes.Hierarchy-Forward").Select(Id).OfType<int>().ToArray(),Tags=Field("System.Tags").Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries),Changed=DateTimeOffset.TryParse(Field("System.ChangedDate"),out var changed)?changed:default,Url=S(n?["_links"]?["html"]?["href"]) is {Length:>0} link?link:activeConnection is {} connection?$"https://dev.azure.com/{E(connection.Organization)}/{E(connection.Project)}/_workitems/edit/{n?["id"]}":""};
 }
 public async Task<WorkItem> GetAsync(int id,CancellationToken ct=default){var c=await Credentials(ct);await MetadataAsync(ct:ct);return Map(await Send(c,$"_apis/wit/workitems/{id}?$expand=All",HttpMethod.Get,ct:ct));}
 WorkItemComment MapComment(JsonNode? n){DateTimeOffset.TryParse(S(n?["createdDate"]),out var created);return new(n?["id"]?.GetValue<int>()??0,S(n?["text"]),S(n?["createdBy"]?["displayName"]),created);}
 public async Task<IReadOnlyList<WorkItemComment>> CommentsAsync(int id,CancellationToken ct=default){var c=await Credentials(ct);var n=await Send(c,$"_apis/wit/workItems/{id}/comments?$top=100&api-version=7.1-preview.4",HttpMethod.Get,ct:ct,apiVersion:false);return ((JsonArray?)n["comments"]??(JsonArray?)n["value"]??[]).Select(MapComment).OrderBy(x=>x.Created).ToArray();}
 public async Task<WorkItemComment> AddCommentAsync(int id,string text,CancellationToken ct=default){if(string.IsNullOrWhiteSpace(text))throw new TrackerException("Enter a comment first.");var c=await Credentials(ct);var n=await Send(c,$"_apis/wit/workItems/{id}/comments?api-version=7.1-preview.4",HttpMethod.Post,new{text=text.Trim()},ct:ct,apiVersion:false);return MapComment(n);}
 public static string FieldName(ItemField f,TypeDefinition type)=>f switch{ItemField.Title=>"System.Title",ItemField.Owner=>"System.AssignedTo",ItemField.State=>"System.State",ItemField.Iteration=>"System.IterationPath",ItemField.Area=>"System.AreaPath",ItemField.Estimate=>type.EstimateField??throw new TrackerException("This work-item type has no supported estimate field."),ItemField.Order=>type.OrderField??throw new TrackerException("This work-item type has no supported backlog-order field."),ItemField.Priority=>"Microsoft.VSTS.Common.Priority",ItemField.Tags=>"System.Tags",ItemField.Description=>"System.Description",ItemField.Acceptance=>"Microsoft.VSTS.Common.AcceptanceCriteria",_=>throw new TrackerException("Unsupported field.")};
 async Task<List<object>> Patch(string type,IReadOnlyList<Change> changes,CancellationToken ct){var meta=await MetadataAsync(ct:ct);var definition=meta.Types.FirstOrDefault(t=>t.Name==type)??throw new TrackerException("Unsupported work-item type.");var patch=new List<object>();
  foreach(var change in changes){var field=FieldName(change.Field,definition);if(!definition.Fields.Contains(field))throw new TrackerException($"{type} does not support {change.Field}. No update was sent.");patch.Add(new{op="add",path="/fields/"+field,value=change.Value});}return patch;
 }
 public async Task<WorkItem> UpdateAsync(ItemUpdate update,CancellationToken ct=default){var c=await Credentials(ct);var patch=await Patch(update.Original.Type,update.Changes,ct);patch.Insert(0,new{op="test",path="/rev",value=update.Original.Revision});return Map(await Send(c,$"_apis/wit/workitems/{update.Original.Id}?$expand=All",HttpMethod.Patch,patch,true,ct));}
 public async Task<WorkItem> CreateAsync(string type,IReadOnlyList<Change> changes,int? parent,CancellationToken ct=default){var c=await Credentials(ct);var patch=await Patch(type,changes,ct);if(parent is {} id)patch.Add(new{op="add",path="/relations/-",value=new{rel="System.LinkTypes.Hierarchy-Reverse",url=$"https://dev.azure.com/{E(c.Connection.Organization)}/_apis/wit/workItems/{id}"}});return Map(await Send(c,"_apis/wit/workitems/$"+E(type)+"?$expand=All",HttpMethod.Post,patch,true,ct));}
}
