using System.Net;
using System.Security.Cryptography;
using System.Text;
using SprintPilot.Application;
using SprintPilot.AzureDevOps;
using SprintPilot.Infrastructure;
using SprintPilot.Web;
using SprintPilot.Web.Components;
var builder=WebApplication.CreateBuilder(args);
var portText=Environment.GetEnvironmentVariable("SPRINTPILOT_PORT")??"5271";
if(!int.TryParse(portText,out var port)||port<1024||port>65535)throw new InvalidOperationException("SPRINTPILOT_PORT must be between 1024 and 65535.");
builder.WebHost.ConfigureKestrel(k=>k.ListenLocalhost(port));
builder.Logging.ClearProviders();builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore",LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http",LogLevel.None);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(o=>{o.DetailedErrors=false;o.JSInteropDefaultCallTimeout=TimeSpan.FromSeconds(30);});
builder.Services.AddScoped<ICredentialStore,CredentialStore>();builder.Services.AddSingleton<IPreferencesStore,PreferencesStore>();
builder.Services.AddSingleton<ITrackerAuthentication,PatAuthentication>();
builder.Services.AddScoped(_=>new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(45)});
builder.Services.AddSingleton(_=>new PipelineApprovalSelectionStore());
builder.Services.AddScoped<AzureTracker>();builder.Services.AddScoped<DemoTracker>();builder.Services.AddScoped<TrackerSession>();builder.Services.AddScoped<IWorkTracker>(s=>s.GetRequiredService<TrackerSession>());builder.Services.AddScoped<BulkEditor>();
var app=builder.Build();
var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var session=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var cookie="SprintPilotSession"+port;
var origin=$"http://localhost:{port}";
static bool Equal(string? a,string b)=>a is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a),Encoding.UTF8.GetBytes(b));
app.Use(async(context,next)=>{
 if(context.Connection.RemoteIpAddress is not {} ip||!IPAddress.IsLoopback(ip)||context.Request.Host.Host!="localhost"||context.Request.Host.Port!=port){context.Response.StatusCode=403;return;}
 var requestOrigin=context.Request.Headers.Origin.ToString();if(requestOrigin!=""&&requestOrigin!=origin){context.Response.StatusCode=403;return;}
 context.Response.Headers["X-Content-Type-Options"]="nosniff";context.Response.Headers["Referrer-Policy"]="no-referrer";context.Response.Headers["X-Frame-Options"]="DENY";
 context.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' ws://localhost:"+port+"; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
 context.Response.Headers.CacheControl="no-store";
 var path=context.Request.Path.Value??"/";
 if(path=="/health"){await context.Response.WriteAsJsonAsync(new{application="SprintPilot",processId=Environment.ProcessId,baseDirectory=AppContext.BaseDirectory});return;}
 if(path=="/session"&&context.Request.Method=="POST"){
  if(requestOrigin!=origin||!Equal(context.Request.Headers["X-SprintPilot-Key"],key)){context.Response.StatusCode=403;return;}
  context.Response.Cookies.Append(cookie,session,new CookieOptions{HttpOnly=true,SameSite=SameSiteMode.Strict,IsEssential=true,Path="/",MaxAge=TimeSpan.FromDays(7)});context.Response.StatusCode=204;return;
 }
 if(path=="/launch"){context.Response.ContentType="text/html";await context.Response.WriteAsync("<!doctype html><html lang='en'><head><title>SprintPilot</title><script src='/launch.js' defer></script></head><body><p id='message'>Opening SprintPilot…</p></body></html>");return;}
 if(path=="/launch.js"){await next();return;}
 if(!Equal(context.Request.Cookies[cookie],session)){
  if(path=="/"){context.Response.ContentType="text/html";await context.Response.WriteAsync("<!doctype html><html lang='en'><head><title>SprintPilot</title></head><body><h1>SprintPilot</h1><p>Open SprintPilot using your desktop shortcut or Start-SprintPilot.ps1 to unlock this local session.</p></body></html>");return;}
  context.Response.StatusCode=401;return;
 }
 await next();
});
app.UseExceptionHandler(a=>a.Run(async ctx=>{ctx.Response.StatusCode=500;await ctx.Response.WriteAsync("SprintPilot could not complete the request. Restart and try again.");}));
app.UseStaticFiles();app.UseAntiforgery();app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Lifetime.ApplicationStarted.Register(()=>{
 // Written only after the listener owns the port. Never emitted to logs.
 LocalPaths.WriteAsync($"session-{port}.json",new{Key=key,ProcessId=Environment.ProcessId,StartTime=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(),BaseDirectory=AppContext.BaseDirectory,Port=port}).GetAwaiter().GetResult();
 // Visual Studio owns this process; open the existing private session flow
 // after binding succeeds, without requiring a separate PowerShell launcher.
 if(app.Environment.IsDevelopment() &&
    Environment.GetEnvironmentVariable("SPRINTPILOT_OPEN_BROWSER") == "1"){
  try{
   System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(origin+"/launch#"+key){UseShellExecute=true});
  }catch{
   // Do not log the launch URL: it contains the local session capability.
   app.Logger.LogWarning("Could not open the browser. Check the default browser configuration and restart debugging.");
  }
 }
});
await app.RunAsync();
