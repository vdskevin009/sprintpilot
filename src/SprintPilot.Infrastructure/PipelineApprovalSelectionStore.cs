using System.Text.Json;

namespace SprintPilot.Infrastructure;

// Kept outside the published app and separate from the Settings form snapshot.
public sealed class PipelineApprovalSelectionStore {
 readonly string path;
 readonly SemaphoreSlim gate=new(1,1);
 public PipelineApprovalSelectionStore():this(LocalPaths.File("pipeline-approval-projects.json")){}
 public PipelineApprovalSelectionStore(string path){this.path=path;}
 async Task<Dictionary<string,string>> Read(CancellationToken ct){
  if(!File.Exists(path))return new(StringComparer.OrdinalIgnoreCase);
  var values=JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(path,ct))??[];
  return new(values,StringComparer.OrdinalIgnoreCase);
 }
 public async Task<string?> LoadAsync(string organization,CancellationToken ct=default){
  await gate.WaitAsync(ct);try{return (await Read(ct)).GetValueOrDefault(organization);}finally{gate.Release();}
 }
 public async Task SaveAsync(string organization,string projectId,CancellationToken ct=default){
  if(string.IsNullOrWhiteSpace(organization)||string.IsNullOrWhiteSpace(projectId))throw new ArgumentException("Organization and project are required.");
  await gate.WaitAsync(ct);
  var temp=path+"."+Guid.NewGuid()+".tmp";
  try{
   var values=await Read(ct);values[organization]=projectId;
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
   await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(values),ct);
   if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temp,UnixFileMode.UserRead|UnixFileMode.UserWrite);
   File.Move(temp,path,true);
  }finally{if(File.Exists(temp))File.Delete(temp);gate.Release();}
 }
}
