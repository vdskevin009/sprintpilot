using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SprintPilot.Application;
using SprintPilot.AzureDevOps;

static class ApprovalChecks {
 public static async Task Run(){
  var fake=new ApprovalHttp();using var http=new HttpClient(fake);
  var tracker=new AzureTracker(http,new FakeCredentials(),new PatAuthentication(),NullLogger<AzureTracker>.Instance);
  var result=await tracker.PendingPipelineApprovalsAsync("Other Project");
  var row=result.Items.Single();
  if(row.Pipeline!="Deploy API"||row.Stage!="Pre-prod"||row.Run!="42"||!row.CanApprove||fake.Pages!=2)
   throw new Exception("Pending approvals must resolve the pipeline, run and stage across build pages.");
  await tracker.ApprovePipelineAsync("Other Project",ApprovalHttp.Id);
  if(fake.Patches!=1||fake.Body?[0]?["approvalId"]?.ToString()!=ApprovalHttp.Id||fake.Body?[0]?["status"]?.ToString()!="approved")
   throw new Exception("Only the selected approval must be updated.");
  fake.Status="approved";
  await MustReject(()=>tracker.ApprovePipelineAsync("Other Project",ApprovalHttp.Id));
  fake.Status="pending";fake.Permission="view";
  await MustReject(()=>tracker.ApprovePipelineAsync("Other Project",ApprovalHttp.Id));
  if(fake.Patches!=1)throw new Exception("Stale or unauthorized approvals must never be patched.");
  fake.Permission="view, update";fake.FailPatch=true;
  await MustReject(()=>tracker.ApprovePipelineAsync("Other Project",ApprovalHttp.Id));
  if(fake.Patches!=2)throw new Exception("A failed approval mutation must not be retried.");
  Console.WriteLine("PASS pipeline approval mapping, pagination, project scope, stale/permission guards and no mutation retry");
 }
 static async Task MustReject(Func<Task> action){try{await action();}catch(TrackerException){return;}throw new Exception("Expected rejected approval.");}
 sealed class ApprovalHttp:HttpMessageHandler {
  public const string Id="11111111-1111-1111-1111-111111111111";
  public string Status="pending",Permission="view, update";public bool FailPatch;
  public int Patches,Pages;public JsonArray? Body;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){
   if(!request.RequestUri!.AbsolutePath.Contains("/Other%20Project/",StringComparison.OrdinalIgnoreCase)&&!request.RequestUri.AbsolutePath.Contains("/Other Project/"))
    throw new Exception("Approval reads and writes must use selected project.");
   var path=request.RequestUri.AbsolutePath;var query=request.RequestUri.Query;
   string json;string? next=null;
   if(request.Method==HttpMethod.Patch){Patches++;Body=JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsArray();if(FailPatch)return new(HttpStatusCode.ServiceUnavailable);json="""{"count":1,"value":[]}""";}
   else if(path.EndsWith("/approvals/"+Id))json=$$"""{"id":"{{Id}}","status":"{{Status}}","permissions":"{{Permission}}"}""";
   else if(path.EndsWith("/approvals"))json=$$"""{"value":[{"id":"{{Id}}","status":"pending","permissions":"view, update"}]}""";
   else if(path.EndsWith("/builds")){
    Pages++;
    if(query.Contains("continuationToken=page2"))json="""{"value":[{"id":42,"buildNumber":"42","definition":{"name":"Deploy API"}}]}""";
    else{json="""{"value":[]}""";next="page2";}
   }else if(path.EndsWith("/timeline"))json=$$"""{"records":[{"id":"stage","type":"Stage","name":"Pre-prod"},{"id":"checkpoint","type":"Checkpoint","parentId":"stage"},{"id":"{{Id}}","type":"Checkpoint.Approval","parentId":"checkpoint"}]}""";
   else throw new Exception("Unexpected approval endpoint");
   var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
   if(next is not null)response.Headers.Add("x-ms-continuationtoken",next);
   return response;
  }
 }
}
