namespace SprintPilot.Domain;
public sealed record WorkItem {
 public Dictionary<string,double> NumericFields {get;init;}=new();
 public int Id {get;init;} public int Revision {get;init;}
 public string Type {get;init;}=""; public string Title {get;init;}="";
 public string Owner {get;init;}=""; public string OwnerId {get;init;}="";
 public string State {get;init;}=""; public string Iteration {get;init;}="";
 public string Area {get;init;}=""; public double? Estimate {get;init;} public double? Order {get;init;}
 public int? Priority {get;init;} public int? Parent {get;init;}
 public int[] Children {get;init;}=[]; public string[] Tags {get;init;}=[];
 public string Description {get;init;}=""; public string Acceptance {get;init;}="";
 public DateTimeOffset Changed {get;init;} public string Url {get;init;}="";
}
public enum ItemField {Title,Owner,State,Iteration,Area,Estimate,Order,Priority,Tags,Description,Acceptance}
public record Change(ItemField Field, object? Value);
public record ItemUpdate(WorkItem Original, IReadOnlyList<Change> Changes);
public record UpdateResult(int Id, WorkItem? Item, string? Error) {public bool Success=>Item is not null;}
public record Iteration(string Id,string Name,string Path,DateTimeOffset? Start,DateTimeOffset? Finish);
public record Person(string Id,string Name,string UniqueName);
public record Team(string Id,string Name);
public record StateDefinition(string Name,string Category);
public record TypeDefinition(string Name,StateDefinition[] States,string[] Fields,string? EstimateField,string? OrderField=null) {
 public bool IsFinished(string state)=>States.Any(s=>s.Name==state && s.Category is "Completed" or "Removed");
}
public record AreaScope(string Path,bool IncludeChildren);
public record Metadata(Team[] Teams,Person[] People,Iteration[] Iterations,string[] Areas,TypeDefinition[] Types,AreaScope[] Scope,Person Me);
public sealed record ConnectionInfo(string Organization,string Project,string Team="");
// Secret is deliberately not a serializable record or printable value object.
public sealed class Credentials(ConnectionInfo connection,string token) {
 public ConnectionInfo Connection {get;}=connection;
 public string Token {get;}=token;
 public override string ToString()=>"[protected credentials]";
}
public record WorkTemplate(string Name,string Description,string Acceptance,string Tags,string Area);
public record SavedView(string Name,Dictionary<string,string> Filters,string[] Columns);
public sealed class Preferences {
 public int Version {get;set;}
 public Dictionary<string,PlanningSettings> PlanningProfiles {get;set;}=new();
 public string Theme {get;set;}="system";
 public string[] Columns {get;set;}=["Order","ID","Type","Title","Owner","State","Iteration","Estimate","Tags"];
 public List<SavedView> Views {get;set;}=[];
 public List<WorkTemplate> Templates {get;set;}=[new("Feature","Goal:\n\nContext:","Given … when … then …\n\nTesting:","", ""),new("Bug","Actual behavior:\n\nExpected behavior:\n\nSteps to reproduce:\n\nEnvironment:","Regression test:","Bug", ""),new("Disaster Recovery","Goal:\n\nRecovery scope:\n\nDependencies:\n\nRollback:","Recovery validation:\n\nTesting:","DR", ""),new("Technical Task","Goal:\n\nImplementation notes:\n\nDependencies:","Done when:\n\nTesting:","Technical", ""),new("Deployment","Target environment:\n\nDeployment steps:\n\nRollback:","Smoke tests:\n\nVerification:","Deployment", ""),new("Database Change","Schema or data change:\n\nCompatibility:\n\nRollback:","Migration validation:\n\nTesting:","Database", ""),new("Migration","Source:\n\nTarget:\n\nMapping:\n\nRecovery:","Reconciliation:\n\nTesting:","Migration", ""),new("API Change","Endpoint:\n\nContract change:\n\nCompatibility:","Contract tests:\n\nError scenarios:","API", "")];
 public string AiPrompt {get;set;}="";
 public Dictionary<string,int> QualityWeights {get;set;}=new(){["Clear title"]=15,["Description"]=15,["Acceptance criteria"]=15,["Sprint"]=10,["Area"]=5,["Estimate"]=10,["Assignee"]=5,["Tags"]=5,["Testing information"]=10,["Distinct title"]=5};
 public int StaleDays {get;set;}=14;
}
