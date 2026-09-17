using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SprintPilot.Application;
using SprintPilot.Domain;
namespace SprintPilot.Infrastructure;
public static class LocalPaths {
 public static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SprintPilot");
 public static string File(string name){Directory.CreateDirectory(Root);return Path.Combine(Root,name);}
 public static async Task WriteAsync<T>(string name,T value,CancellationToken ct=default){var path=File(name);var temp=path+"."+Guid.NewGuid()+".tmp";try{await System.IO.File.WriteAllTextAsync(temp,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}),ct);if(!OperatingSystem.IsWindows())System.IO.File.SetUnixFileMode(temp,UnixFileMode.UserRead|UnixFileMode.UserWrite);System.IO.File.Move(temp,path,true);}finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}}
}
public sealed class PreferencesStore:IPreferencesStore {
 private readonly SemaphoreSlim gate=new(1,1);
 public async Task<Preferences> LoadAsync(CancellationToken ct=default){await gate.WaitAsync(ct);try{var path=LocalPaths.File("preferences.json");var p=System.IO.File.Exists(path)?JsonSerializer.Deserialize<Preferences>(await System.IO.File.ReadAllTextAsync(path,ct))??new():new();if(p.AiPrompt=="")p.AiPrompt=AiReview.DefaultPrompt;return p;}catch(JsonException){throw new TrackerException("Local preferences are invalid. Rename preferences.json in your local SprintPilot folder and restart.");}finally{gate.Release();}}
 public async Task SaveAsync(Preferences p,CancellationToken ct=default){await gate.WaitAsync(ct);try{await LocalPaths.WriteAsync("preferences.json",p,ct);}finally{gate.Release();}}
}
public sealed class CredentialStore:ICredentialStore {
 private Credentials? session;
 public async ValueTask<Credentials?> GetAsync(CancellationToken ct=default){
  if(session is not null)return session;
  var org=Environment.GetEnvironmentVariable("SPRINTPILOT_AZDO_ORGANIZATION");var project=Environment.GetEnvironmentVariable("SPRINTPILOT_AZDO_PROJECT");var pat=Environment.GetEnvironmentVariable("SPRINTPILOT_AZDO_PAT");
  if(!string.IsNullOrWhiteSpace(org)&&!string.IsNullOrWhiteSpace(project)&&!string.IsNullOrWhiteSpace(pat))return session=new(new(org,project),pat);
  var path=LocalPaths.File("connection.json");if(!System.IO.File.Exists(path))return null;
  try {var info=JsonSerializer.Deserialize<ConnectionInfo>(await System.IO.File.ReadAllTextAsync(path,ct));if(info is null)return null;var token=OperatingSystem.IsWindows()?WindowsCredential.Read(Target(info)):null;return string.IsNullOrEmpty(token)?null:session=new(info,token);}catch(JsonException){return null;}
 }
 private static string Target(ConnectionInfo c)=>$"SprintPilot:{c.Organization}:{c.Project}";
 public async ValueTask SaveAsync(Credentials c,CancellationToken ct=default){
  if(OperatingSystem.IsWindows()){WindowsCredential.Write(Target(c.Connection),c.Token);await LocalPaths.WriteAsync("connection.json",c.Connection,ct);}session=c;
 }
}
internal static class WindowsCredential {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Credential {public uint Flags,Type;public string TargetName;public string? Comment;public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;public uint CredentialBlobSize;public IntPtr CredentialBlob;public uint Persist,AttributeCount;public IntPtr Attributes;public string? TargetAlias,UserName;}
 [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential c,uint flags);
 [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target,uint type,uint flags,out IntPtr ptr);
 [DllImport("advapi32.dll")]private static extern void CredFree(IntPtr ptr);
 public static void Write(string target,string token){var bytes=Encoding.Unicode.GetBytes(token);if(bytes.Length>2560)throw new TrackerException("Token exceeds Windows credential capacity.");var ptr=Marshal.AllocHGlobal(bytes.Length);try{Marshal.Copy(bytes,0,ptr,bytes.Length);var c=new Credential{Type=1,TargetName=target,CredentialBlob=ptr,CredentialBlobSize=(uint)bytes.Length,Persist=2,UserName=Environment.UserName};if(!CredWrite(ref c,0))throw new TrackerException("Windows Credential Manager could not save the token. Use session environment variables instead.");}finally{for(int i=0;i<bytes.Length;i++)Marshal.WriteByte(ptr,i,0);Marshal.FreeHGlobal(ptr);Array.Clear(bytes);}}
 public static string? Read(string target){if(!CredRead(target,1,0,out var ptr))return null;try{var c=Marshal.PtrToStructure<Credential>(ptr);return Marshal.PtrToStringUni(c.CredentialBlob,(int)c.CredentialBlobSize/2);}finally{CredFree(ptr);}}
}
