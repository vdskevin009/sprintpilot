using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SprintPilot.Application;
using SprintPilot.Domain;
using ConnectionInfo = SprintPilot.Domain.ConnectionInfo;
namespace SprintPilot.Web.Components.Pages;
public partial class Home {
 [Inject] public HttpClient Http {get;set;}=default!;
 [Inject] public NavigationManager Navigation {get;set;}=default!;
 PlanningPage? planner;
 Preferences prefs=new();Metadata? meta;ConnectionInfo? connection;Person? testUser;
 string organization="",project="",token="",screen="home",message="",dialog="",dialogError="";
 bool focusDialog;bool initializing=true,connecting,hasError,loading,applying,moreFilters,descending,disposed,showAllDaysOff,dailyLookupBusy,dailyPanelLoading,dailyActiveOnly,smartOrdering,meetingCreating,workspaceActiveOnly,workspaceBlockedOnly,workspaceOwnerMode;
 bool pwaInstallAvailable,pwaInstalled,appUpdateAvailable,appUpdateChecking,appUpdating,appUpdateSupported,branchCleanupLoading,branchDeleting,pullRequestCleanupLoading,pullRequestAbandoning;
 string attentionFilter="",classificationTag="",dailyLookupText="",dailyTagText="",dailyCommentText="",dailyFocusOwner="",filterOptionSearch="",holidayCountryFilter="ALL";string? workspacePriorityOwner;
 string meetingTitle="",meetingNotes="",meetingCopilotText="",meetingError="",meetingWorkType="";
 string smartFixPrompt="",smartFixCopilotText="",magicPrompt="",magicCopilotText="",appUpdateText="";
 string branchProject="",branchProjectSearch="",branchRepositoryId="",branchRepositorySearch="",branchSearch="",branchStatusFilter="all";
 string pullRequestProject="",pullRequestProjectSearch="",pullRequestRepositoryId="",pullRequestRepositorySearch="",pullRequestSearch="",pullRequestStatusFilter="all";
 int sprintIndex,selectionAnchor=-1,templateIndex;
 string search="",ownerSearch="",stateFilter="",typeFilter="",tagFilter="",applicationFilter="",areaFilter="",priorityFilter="",quickView="Team",cleanupFilter="",sort="Order",workspaceMode="List";
 string commandSearch="",iterationSearch="",viewName="",bulkKind="",bulkValue="",aiText="",promptText="";
 int promptItemCount;
 string templateName="",templateDescription="",templateAcceptance="",templateTags="",templateArea="",blockedTagsText="",holidayCalendarError="";
 string newType="",newTitle="",newDescription="",newAcceptance="",newArea="",newTags="",newIteration="";int? newParent;
 readonly string[] AllColumns=["Order","ID","Type","Title","Owner","State","Iteration","Area","Estimate","Priority","Parent","Tags","Changed"];
 readonly string[] QuickViews=["My Work","Team","Unassigned","Carry-over","Bugs","Recently Changed"];
 readonly string[] Commands=["Next sprint","Previous sprint","Show my work","Show unassigned","Select all visible","Clear selection","Move selected to next sprint","Move selected to previous sprint","Assign selected","Add tag","Remove tag","Change state","New PBI","Refresh"];
 List<WorkItem> items=[],previousItems=[],related=[],tagHistory=[],planningItems=[],dailyLookupResults=[];SprintCapacity sprintCapacity=new([],[]);readonly Dictionary<string,List<WorkItem>> sprintCache=new();readonly Dictionary<string,SprintCapacity> capacityByIteration=new(StringComparer.OrdinalIgnoreCase);
 WorkItem? dailyPanelItem;List<WorkItemComment> dailyComments=[];List<CalendarHoliday> calendarHolidays=[];List<SmartOrderRow> smartOrderPlan=[];MeetingImport meetingImport=new();List<MeetingActionDraft> meetingActions=[];List<SmartFixGap> smartFixGaps=[];List<SmartFixSuggestion> smartFixSuggestions=[];List<MagicContext> magicContexts=[];List<MagicSuggestion> magicSuggestions=[];
 List<AzureProject> branchProjects=[];List<GitRepository> branchRepositories=[];List<GitBranch> branches=[],branchDeleteQueue=[];List<BranchDeleteResult> branchDeleteResults=[];
 List<AzureProject> pullRequestProjects=[];List<GitRepository> pullRequestRepositories=[];List<GitPullRequest> pullRequests=[],pullRequestAbandonQueue=[];List<PullRequestAbandonResult> pullRequestAbandonResults=[];
 readonly HashSet<string> ownerFilters=new(StringComparer.OrdinalIgnoreCase);int? draggedId,dailyDragOverId,pendingOrderHighlightId;
 readonly HashSet<string> cleanupTagFilters=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> selectedBranches=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<int> selectedPullRequests=[];
 readonly HashSet<int> selected=[],busy=[];readonly Dictionary<int,ItemUpdate> drafts=new();
 List<ItemUpdate> pending=[];List<UpdateResult> results=[];WorkItem? detail;WorkItem[] aiItems=[];ReviewSection[] reviews=[];
 CancellationTokenSource refreshToken=new();readonly CancellationTokenSource lifetime=new();DotNetObjectReference<Home>? reference;
 string PlanningProfileKey => Tracker.Demo ? "demo" : $"{connection?.Organization}|{connection?.Project}|{connection?.Team}";
 static string TeamPreferenceKey(ConnectionInfo c)=>$"{c.Organization}|{c.Project}";
 Iteration? CurrentSprint=>meta?.Iterations.ElementAtOrDefault(sprintIndex);
 Iteration? NextSprint=>meta?.Iterations.ElementAtOrDefault(sprintIndex+1);
 string Adjacent(int delta)=>meta?.Iterations.ElementAtOrDefault(sprintIndex+delta)?.Name??"No sprint";
 string DateRange=>CurrentSprint?.Start is {} start?$"{start:MMM d} – {CurrentSprint.Finish:MMM d, yyyy}":"Dates not configured";
 IEnumerable<WorkItem> AllLoaded=>items.Concat(previousItems).Concat(related).DistinctBy(w=>w.Id);
 string[] TagSuggestions=>AllLoaded.Concat(tagHistory).SelectMany(w=>w.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
 List<WorkItem> SelectedItems=>Visible.Where(w=>selected.Contains(w.Id)).ToList();
 string[] WorkspaceColumns=>prefs.Columns;
 IEnumerable<string> CommonStates {get {var sets=SelectedItems.Select(w=>meta!.Types.First(t=>t.Name==w.Type).States.Select(s=>s.Name).ToHashSet()).ToArray();if(sets.Length==0)return [];var common=sets[0];foreach(var set in sets.Skip(1))common.IntersectWith(set);return common.Order();}}
 string[] Issues(WorkItem w)=>Quality.Issues(w,meta!,prefs,AllLoaded,meta!.Iterations.FirstOrDefault(i=>i.Path==w.Iteration));
 Dictionary<string,int> CleanupCounts=>items.SelectMany(w=>Issues(w)).GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count()).Concat(new[]{new KeyValuePair<string,int>("Previous-sprint unfinished",previousItems.Count(w=>!Quality.Finished(w,meta!)))}).ToDictionary(x=>x.Key,x=>x.Value);
 IEnumerable<WorkItem> CleanupItems=>meta is null?[]:items.Where(w=>!Quality.Finished(w,meta));
 IEnumerable<(string Id,string Name,int Count)> CleanupPeople=>CleanupItems.GroupBy(w=>w.OwnerId).Select(g=>(Id:g.Key,Name:PersonName(g.Key),Count:g.Count())).OrderBy(x=>x.Name=="Unassigned").ThenBy(x=>x.Name);
 IEnumerable<(string Tag,int Count)> CleanupTags=>CleanupItems.SelectMany(w=>w.Tags).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).Select(g=>(Tag:g.Key,Count:g.Count())).OrderBy(x=>x.Tag);
 IEnumerable<AzureProject> FilteredBranchProjects=>branchProjects.Where(p=>branchProjectSearch==""||p.Name.Contains(branchProjectSearch,StringComparison.OrdinalIgnoreCase)).Take(75);
 IEnumerable<GitRepository> FilteredBranchRepositories=>branchRepositories.Where(r=>branchRepositorySearch==""||r.Name.Contains(branchRepositorySearch,StringComparison.OrdinalIgnoreCase)).Take(75);
 static bool ProtectedBranchConvention(string name){var n=name.Trim('/').ToLowerInvariant();return n is "main" or "master" or "develop" or "development" or "dev"||n.StartsWith("release/")||n.StartsWith("hotfix/");}
 static bool ProtectedBranchConvention(GitBranch b)=>ProtectedBranchConvention(b.Name);
 int BranchAgeDays(GitBranch b)=>b.LastCommitDate is null?int.MaxValue:Math.Max(0,(int)Math.Floor((DateTimeOffset.UtcNow-b.LastCommitDate.Value).TotalDays));
 bool BranchRecommended(GitBranch b){if(b.IsDefault||b.IsLocked||b.HasActivePullRequest||b.LastCommitDate is null||ProtectedBranchConvention(b))return false;var age=BranchAgeDays(b);return age>=prefs.BranchCleanupStaleDays&&(b.HasCompletedPullRequest||age>=Math.Max(180,prefs.BranchCleanupStaleDays*2));}
 string BranchReason(GitBranch b){if(b.IsDefault)return "Default branch — never recommended for deletion";if(b.IsLocked)return "Locked in Azure DevOps";if(b.HasActivePullRequest)return "Active pull request";if(ProtectedBranchConvention(b))return "Long-lived branch naming convention — review manually";if(b.LastCommitDate is null)return "Last commit date unavailable — review manually";var age=BranchAgeDays(b);if(BranchRecommended(b)&&b.HasCompletedPullRequest)return $"Merged pull-request tip · inactive {age} days";if(BranchRecommended(b))return $"No active PR · inactive {age} days";if(b.HasCompletedPullRequest)return $"Merged pull-request tip · changed {age} days ago";return $"Last activity {age} days ago";}
 string BranchAgeText(GitBranch b)=>b.LastCommitDate is null?"Unknown":BranchAgeDays(b)==0?"Today":BranchAgeDays(b)==1?"1 day":$"{BranchAgeDays(b)} days";
 IEnumerable<GitBranch> RecommendedBranches=>branches.Where(BranchRecommended);
 IEnumerable<GitBranch> VisibleBranches=>branches.Where(b=>branchSearch==""||b.Name.Contains(branchSearch,StringComparison.OrdinalIgnoreCase)||b.Creator.Contains(branchSearch,StringComparison.OrdinalIgnoreCase)||b.LastCommitAuthor.Contains(branchSearch,StringComparison.OrdinalIgnoreCase)||b.LastCommitMessage.Contains(branchSearch,StringComparison.OrdinalIgnoreCase)).Where(b=>branchStatusFilter switch{"recommended"=>BranchRecommended(b),"active"=>b.HasActivePullRequest,"protected"=>b.IsDefault||b.IsLocked||ProtectedBranchConvention(b),"review"=>!BranchRecommended(b),_=>true});
 List<GitBranch> SelectedBranchRows=>branches.Where(b=>selectedBranches.Contains(b.Name)).ToList();
 IEnumerable<AzureProject> FilteredPullRequestProjects=>pullRequestProjects.Where(p=>pullRequestProjectSearch==""||p.Name.Contains(pullRequestProjectSearch,StringComparison.OrdinalIgnoreCase)).Take(75);
 IEnumerable<GitRepository> FilteredPullRequestRepositories=>pullRequestRepositories.Where(r=>pullRequestRepositorySearch==""||r.Name.Contains(pullRequestRepositorySearch,StringComparison.OrdinalIgnoreCase)).Take(75);
 int PullRequestAgeDays(GitPullRequest p)=>Math.Max(0,(int)Math.Floor((DateTimeOffset.UtcNow-p.CreatedDate).TotalDays));
 int PullRequestStaleDays(GitPullRequest p)=>p.LastCommitDate is null?int.MaxValue:Math.Max(0,(int)Math.Floor((DateTimeOffset.UtcNow-p.LastCommitDate.Value).TotalDays));
 bool PullRequestRecommended(GitPullRequest p){if(p.LastCommitDate is null||ProtectedBranchConvention(p.SourceBranch)||p.ApprovalCount>0||p.BlockingVoteCount>0)return false;var threshold=prefs.PullRequestCleanupStaleDays;var required=p.IsDraft?threshold:Math.Max(180,threshold*2);return PullRequestAgeDays(p)>=required&&PullRequestStaleDays(p)>=required;}
 string PullRequestReason(GitPullRequest p){if(ProtectedBranchConvention(p.SourceBranch))return "Long-lived source branch — review manually";if(p.ApprovalCount>0)return $"{p.ApprovalCount} approval{(p.ApprovalCount==1?"":"s")} — consider completing instead";if(p.BlockingVoteCount>0)return "Reviewer feedback or blocking vote is still present";if(p.LastCommitDate is null)return "Last source activity unavailable — review manually";var age=PullRequestAgeDays(p);var stale=PullRequestStaleDays(p);if(PullRequestRecommended(p))return p.IsDraft?$"Stale draft · open {age} days · no source activity {stale} days":$"Long-stale PR · open {age} days · no source activity {stale} days";if(p.MergeStatus.Equals("conflicts",StringComparison.OrdinalIgnoreCase))return $"Merge conflicts · source unchanged {stale} days";return $"{(p.IsDraft?"Draft · ":"")}open {age} days · source changed {stale} days ago";}
 string PullRequestAgeText(GitPullRequest p){var age=PullRequestAgeDays(p);return age==0?"Opened today":age==1?"Open 1 day":$"Open {age} days";}
 string PullRequestStaleText(GitPullRequest p)=>p.LastCommitDate is null?"Activity unknown":PullRequestStaleDays(p)==0?"Source changed today":PullRequestStaleDays(p)==1?"Source changed 1 day ago":$"Source changed {PullRequestStaleDays(p)} days ago";
 IEnumerable<GitPullRequest> RecommendedPullRequests=>pullRequests.Where(PullRequestRecommended);
 IEnumerable<GitPullRequest> VisiblePullRequests=>pullRequests.Where(p=>pullRequestSearch==""||p.Id.ToString().Contains(pullRequestSearch)||p.Title.Contains(pullRequestSearch,StringComparison.OrdinalIgnoreCase)||p.SourceBranch.Contains(pullRequestSearch,StringComparison.OrdinalIgnoreCase)||p.TargetBranch.Contains(pullRequestSearch,StringComparison.OrdinalIgnoreCase)||p.Creator.Contains(pullRequestSearch,StringComparison.OrdinalIgnoreCase)||p.LastCommitAuthor.Contains(pullRequestSearch,StringComparison.OrdinalIgnoreCase)).Where(p=>pullRequestStatusFilter switch{"recommended"=>PullRequestRecommended(p),"draft"=>p.IsDraft,"approved"=>p.ApprovalCount>0,"conflicts"=>p.MergeStatus.Equals("conflicts",StringComparison.OrdinalIgnoreCase),"review"=>!PullRequestRecommended(p),_=>true});
 List<GitPullRequest> SelectedPullRequestRows=>pullRequests.Where(p=>selectedPullRequests.Contains(p.Id)).ToList();
 List<WorkItem> Visible {get{
  IEnumerable<WorkItem> rows=(quickView=="Carry-over"||cleanupFilter=="Previous-sprint unfinished")?previousItems.Where(w=>!Quality.Finished(w,meta!)):items;
  if(meta is null)return [];
  rows=rows.Where(w=>(search==""||w.Id.ToString().Contains(search)||w.Title.Contains(search,StringComparison.OrdinalIgnoreCase)||w.Tags.Any(t=>t.Contains(search,StringComparison.OrdinalIgnoreCase)))&&(ownerFilters.Count==0||ownerFilters.Contains(w.OwnerId))&&(typeFilter==""||w.Type.Contains(typeFilter,StringComparison.OrdinalIgnoreCase))&&(applicationFilter==""||w.Tags.Any(t=>t.Equals(applicationFilter,StringComparison.OrdinalIgnoreCase)))&&(tagFilter==""||w.Tags.Any(t=>t.Contains(tagFilter,StringComparison.OrdinalIgnoreCase)))&&(screen=="daily"||!workspaceActiveOnly||!Quality.Finished(w,meta))&&(screen=="daily"||!workspaceBlockedOnly||Blocked(w))&&(cleanupTagFilters.Count==0||w.Tags.Any(t=>cleanupTagFilters.Contains(t)))&&(areaFilter==""||w.Area.Contains(areaFilter,StringComparison.OrdinalIgnoreCase))&&(priorityFilter==""||w.Priority?.ToString().Contains(priorityFilter)==true));
  rows=quickView switch{"My Work"=>rows.Where(w=>w.OwnerId==meta.Me.Id),"Unassigned"=>rows.Where(w=>w.OwnerId==""),"Bugs"=>rows.Where(w=>w.Type.Equals("Bug",StringComparison.OrdinalIgnoreCase)),"Recently Changed"=>rows.Where(w=>w.Changed>DateTimeOffset.UtcNow.AddDays(-3)),_=>rows};
  rows=attentionFilter switch{"blocked"=>rows.Where(Blocked),"unassigned"=>rows.Where(w=>w.OwnerId==""),"missing-app"=>rows.Where(w=>!HasApplicationTag(w)),"missing-estimate"=>rows.Where(MissingEstimateForOpenWork),"stale"=>rows.Where(w=>w.Changed!=default&&w.Changed<DateTimeOffset.UtcNow.AddDays(-prefs.StaleDays)),_=>rows};
  if(screen=="cleanup"&&cleanupFilter!=""&&cleanupFilter!="Previous-sprint unfinished")rows=rows.Where(w=>Issues(w).Contains(cleanupFilter));
  Func<WorkItem,IComparable?> key=sort switch{"Order"=>w=>w.Order,"ID"=>w=>w.Id,"Type"=>w=>w.Type,"Owner"=>w=>w.Owner,"State"=>w=>w.State,"Iteration"=>w=>w.Iteration,"Area"=>w=>w.Area,"Estimate"=>w=>w.Estimate,"Priority"=>w=>w.Priority,"Parent"=>w=>w.Parent,"Tags"=>w=>string.Join(";",w.Tags),"Changed"=>w=>w.Changed,_=>w=>w.Title};return (descending?rows.OrderByDescending(key):rows.OrderBy(key)).ThenBy(w=>w.Id).ToList();
 }}
 string DialogTitle=>dialog switch{"palette"=>"Commands","iterations"=>"Choose sprint","columns"=>"Visible columns","saveview"=>"Save view","bulk"=>"Edit selected items","review"=>"Review changes","ai"=>"AI review","prompt"=>"Copilot prompt","create"=>"New work item","smartorder"=>"Smart order preview","smartfix"=>"Smart Fix","magic"=>"Magic Orchestration","branchdelete"=>"Review branch deletion","prabandon"=>"Review pull request cleanup",_=>"SprintPilot"};
 string BulkLabel=>bulkKind switch{"AddTag"=>"Tag to add","RemoveTag"=>"Tag to remove","Next" or "Previous" or "Iteration"=>"Target sprint",_=>bulkKind};
 protected override async Task OnInitializedAsync(){try{prefs=await Preferences.LoadAsync(lifetime.Token);prefs.QualityWeights.Remove("Parent");blockedTagsText=string.Join("\n",prefs.BlockedTags);LoadTemplate();var c=await Credentials.GetAsync(lifetime.Token);if(c is not null){connection=c.Connection;organization=connection.Organization;project=connection.Project;await LoadWorkspace();}}catch(Exception e){Error(e);}finally{initializing=false;}}
 protected override async Task OnAfterRenderAsync(bool first){if(first){reference=DotNetObjectReference.Create(this);await JS.InvokeVoidAsync("sprintPilot.init",reference);await ApplyTheme();_=CheckForAppUpdate();}if(focusDialog){focusDialog=false;await JS.InvokeVoidAsync("sprintPilot.dialog");}if(pendingOrderHighlightId is {} movedId){pendingOrderHighlightId=null;await JS.InvokeVoidAsync("sprintPilot.orderDropSuccess",movedId);}}
 async Task ApplyTheme()=>await JS.InvokeVoidAsync("sprintPilot.theme",prefs.Theme);
 [JSInvokable] public async Task PwaInstallStateChanged(bool available,bool installed){pwaInstallAvailable=available;pwaInstalled=installed;if(!disposed)await InvokeAsync(StateHasChanged);}
 async Task InstallPwa(){try{var result=await JS.InvokeAsync<string>("sprintPilot.installPwa");switch(result){case "accepted":Notify("SprintPilot installation was accepted. Edge will finish adding the app.");break;case "dismissed":Notify("SprintPilot installation was cancelled.");break;case "installed":pwaInstalled=true;Notify("SprintPilot is already installed as an app.");break;default:Notify("Edge is not offering the PWA install prompt. Check Edge > Apps > Install SprintPilot, or ask your AVD administrator whether app installation is allowed.");break;}}catch(Exception e){Error(e);}}
 static string? FindRepoRoot(){DirectoryInfo? dir=new(AppContext.BaseDirectory);while(dir is not null){if(Directory.Exists(Path.Combine(dir.FullName,".git"))&&File.Exists(Path.Combine(dir.FullName,"scripts","Update-SprintPilot.ps1")))return dir.FullName;dir=dir.Parent;}return null;}
 static bool SamePath(string left,string right){var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),comparison);}
 static bool PublishedInstall(string repo)=>SamePath(AppContext.BaseDirectory,Path.Combine(repo,"artifacts","publish"));
 static async Task<string> RunGitAsync(string repo,CancellationToken ct,params string[] args){var start=new ProcessStartInfo{FileName="git",WorkingDirectory=repo,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var arg in args)start.ArgumentList.Add(arg);using var process=Process.Start(start)??throw new TrackerException("Git could not be started.");var outputTask=process.StandardOutput.ReadToEndAsync(ct);var errorTask=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);var output=await outputTask;_ = await errorTask;if(process.ExitCode!=0)throw new TrackerException("SprintPilot could not check the Git repository.");return output.Trim();}
 async Task CheckForAppUpdate(){if(appUpdateChecking||appUpdating)return;appUpdateChecking=true;try{var repo=FindRepoRoot();appUpdateSupported=repo is not null&&OperatingSystem.IsWindows()&&PublishedInstall(repo);if(repo is null){appUpdateText="";appUpdateAvailable=false;return;}using var check=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);check.CancelAfter(TimeSpan.FromSeconds(7));var branch=(await RunGitAsync(repo,check.Token,"branch","--show-current")).Trim();if(!branch.Equals("main",StringComparison.OrdinalIgnoreCase)){appUpdateSupported=false;appUpdateAvailable=false;appUpdateText=$"Updates are managed through Git while this checkout is on {branch}.";return;}if(!appUpdateSupported){appUpdateAvailable=false;appUpdateText="Automatic update is available from the published SprintPilot installation, not a Visual Studio debug session.";return;}var localCommit="";var versionFile=Path.Combine(AppContext.BaseDirectory,"sprintpilot-version.json");if(File.Exists(versionFile)){using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(versionFile,check.Token));if(doc.RootElement.TryGetProperty("Commit",out var commitNode))localCommit=commitNode.GetString()??"";}if(localCommit=="")localCommit=await RunGitAsync(repo,check.Token,"rev-parse","HEAD");var remoteLine=await RunGitAsync(repo,check.Token,"ls-remote","origin","refs/heads/main");var remoteCommit=remoteLine.Split([' ','\t','\r','\n'],StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";appUpdateAvailable=remoteCommit!=""&&!remoteCommit.Equals(localCommit,StringComparison.OrdinalIgnoreCase);appUpdateText=appUpdateAvailable?"A newer SprintPilot version is available.":"SprintPilot is up to date.";}catch(OperationCanceledException) when(!lifetime.IsCancellationRequested){appUpdateAvailable=false;appUpdateText="Update check timed out.";}catch{appUpdateAvailable=false;appUpdateText="Update check is unavailable right now.";}finally{appUpdateChecking=false;if(!disposed)await InvokeAsync(StateHasChanged);}}
 async Task StartAppUpdate(){if(appUpdating)return;try{var repo=FindRepoRoot();if(repo is null||!OperatingSystem.IsWindows()||!PublishedInstall(repo))throw new TrackerException("Automatic update is only available from the published Windows SprintPilot installation.");if(!appUpdateAvailable){await CheckForAppUpdate();if(!appUpdateAvailable){Notify(appUpdateText==""?"SprintPilot is already up to date.":appUpdateText);return;}}var script=Path.Combine(repo,"scripts","Update-SprintPilot.ps1");var port=new Uri(Navigation.BaseUri).Port;var start=new ProcessStartInfo{FileName="powershell.exe",WorkingDirectory=repo,UseShellExecute=true,WindowStyle=ProcessWindowStyle.Normal};start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-File");start.ArgumentList.Add(script);start.ArgumentList.Add("-Port");start.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));_ = Process.Start(start)??throw new TrackerException("The SprintPilot updater could not be started.");appUpdating=true;appUpdateText="Updating and restarting SprintPilot…";Notify("Updater started. A PowerShell window will rebuild and test SprintPilot, then the updated app will reopen.");await InvokeAsync(StateHasChanged);}catch(Exception e){Error(e);}}
 void Notify(string text){message=text;hasError=false;}
 void Error(Exception e){var text=e is TrackerException?e.Message:e is OperationCanceledException?"Operation cancelled or timed out. Refresh to confirm server state.":"The operation could not be completed. Check the connection and try again.";if(dialog!="")dialogError=text;else{message=text;hasError=true;}}
 void ResetTest()=>testUser=null;
 void TokenInput(ChangeEventArgs e){token=e.Value?.ToString()??"";ResetTest();}
 async Task TestConnection(){connecting=true;try{Tracker.Demo=false;testUser=await Tracker.TestAsync(new(new(organization.Trim(),project.Trim()),token.Trim()),lifetime.Token);Notify("Connected to Azure DevOps as "+testUser.Name);}catch(Exception e){Error(e);}finally{connecting=false;}}
 async Task SaveConnection(){if(connecting)return;connecting=true;try{Tracker.Demo=false;var c=new Credentials(new(organization.Trim(),project.Trim()),token.Trim());testUser=await Tracker.TestAsync(c,lifetime.Token);await Credentials.SaveAsync(c,lifetime.Token);connection=c.Connection;token="";await JS.InvokeVoidAsync("sprintPilot.clearToken");await LoadWorkspace();Notify("Connected as "+testUser.Name);}catch(Exception e){Error(e);}finally{connecting=false;}}
 async Task ExploreDemo(){Tracker.Demo=true;connection=null;await LoadWorkspace();}
 void ExitDemo(){refreshToken.Cancel();Tracker.Demo=false;meta=null;items=[];planningItems=[];selected.Clear();drafts.Clear();sprintCache.Clear();detail=null;dailyPanelItem=null;screen="home";}
 void ChangeConnection(){ExitDemo();connection=null;testUser=null;}
 async Task RememberTeam(ConnectionInfo c){if(string.IsNullOrWhiteSpace(c.Team))return;var key=TeamPreferenceKey(c);if(prefs.LastTeams.TryGetValue(key,out var saved)&&saved.Equals(c.Team,StringComparison.OrdinalIgnoreCase))return;prefs.LastTeams[key]=c.Team;await Preferences.SaveAsync(prefs,lifetime.Token);}
 async Task ChangeTeam(ChangeEventArgs e){try{var c=await Credentials.GetAsync(lifetime.Token);if(c is null)return;connection=c.Connection with{Team=e.Value?.ToString()??""};await Credentials.SaveAsync(new(connection,c.Token),lifetime.Token);await RememberTeam(connection);await LoadWorkspace();}catch(Exception ex){Error(ex);}}
 async Task LoadWorkspace(){loading=true;try{
  if(connection is not null&&prefs.LastTeams.TryGetValue(TeamPreferenceKey(connection),out var remembered)&&remembered!=""&&!remembered.Equals(connection.Team,StringComparison.OrdinalIgnoreCase)){var current=await Credentials.GetAsync(lifetime.Token);if(current is not null){connection=current.Connection with{Team=remembered};await Credentials.SaveAsync(new(connection,current.Token),lifetime.Token);}}
  meta=await Tracker.MetadataAsync(true,lifetime.Token);
  if(connection is not null){var team=meta.Teams.FirstOrDefault(t=>t.Id.Equals(connection.Team,StringComparison.OrdinalIgnoreCase)||t.Name.Equals(connection.Team,StringComparison.OrdinalIgnoreCase))??meta.Teams[0];if(!connection.Team.Equals(team.Id,StringComparison.OrdinalIgnoreCase)){var current=await Credentials.GetAsync(lifetime.Token);connection=connection with{Team=team.Id};if(current is not null)await Credentials.SaveAsync(new(connection,current.Token),lifetime.Token);}await RememberTeam(connection);}
  var now=DateTimeOffset.UtcNow;sprintIndex=Array.FindIndex(meta.Iterations,i=>i.Start<=now&&i.Finish?.AddDays(1)>now);if(sprintIndex<0)sprintIndex=0;screen="home";sprintCache.Clear();capacityByIteration.Clear();selected.Clear();drafts.Clear();previousItems=[];related=[];detail=null;dailyPanelItem=null;await LoadSprint();await LoadFutureCapacities(lifetime.Token);await LoadPlanningItems(lifetime.Token);await LoadCalendarHolidays(lifetime.Token);
 }catch(Exception e){Error(e);}finally{loading=false;}}
 async Task LoadPlanningItems(CancellationToken ct){try{var rows=await Tracker.PlanningAsync(meta!.Types.Where(t=>t.Name!="Task").Select(t=>t.Name).ToArray(),ct);planningItems=rows.ToList();tagHistory=planningItems.Where(w=>w.Changed>=DateTimeOffset.UtcNow.AddMonths(-6)).ToList();}catch(OperationCanceledException){throw;}catch{planningItems=[];tagHistory=[];}}
 async Task LoadSprint(bool refreshMetadata=false){refreshToken.Cancel();refreshToken.Dispose();refreshToken=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var ct=refreshToken.Token;var iteration=CurrentSprint;if(iteration is null){items=[];Notify("This team has no configured iterations. Configure them in Azure DevOps, then refresh.");return;}
  loading=true;if(sprintCache.TryGetValue(iteration.Path,out var hit))items=[..hit];else items=[];previousItems=[];related=[];await InvokeAsync(StateHasChanged);
  try{if(refreshMetadata)meta=await Tracker.MetadataAsync(true,ct);var fetched=await Tracker.SprintAsync(iteration.Path,ct);try{sprintCapacity=await Tracker.CapacityAsync(iteration.Id,ct);capacityByIteration[iteration.Id]=sprintCapacity;}catch(TrackerException){sprintCapacity=new([],[]);}ct.ThrowIfCancellationRequested();items=fetched.ToList();sprintCache[iteration.Path]=[..items];if(quickView=="Carry-over"||screen=="cleanup")await LoadPrevious(ct);if(screen=="cleanup")await LoadRelated(ct);}catch(OperationCanceledException){if(!ct.IsCancellationRequested)Notify("Refresh timed out. Try again.");}catch(Exception e){if(!ct.IsCancellationRequested)Error(e);}finally{if(!ct.IsCancellationRequested)loading=false;}}
 async Task LoadFutureCapacities(CancellationToken ct){if(meta is null)return;var today=DateTimeOffset.Now.Date;var horizon=today.AddMonths(6);var future=meta.Iterations.Where(i=>i.Start is not null&&i.Finish is not null&&i.Finish.Value.Date>=today&&i.Start.Value.Date<=horizon).OrderBy(i=>i.Start).Take(16).ToArray();foreach(var iteration in future){ct.ThrowIfCancellationRequested();if(capacityByIteration.ContainsKey(iteration.Id))continue;try{capacityByIteration[iteration.Id]=await Tracker.CapacityAsync(iteration.Id,ct);}catch(TrackerException){}}}
 async Task LoadPrevious(CancellationToken ct){var previous=meta?.Iterations.ElementAtOrDefault(sprintIndex-1);if(previous is null){previousItems=[];return;}previousItems=(await Tracker.SprintAsync(previous.Path,ct)).ToList();}
 async Task LoadRelated(CancellationToken ct){var ids=items.Where(w=>Quality.Finished(w,meta!)).SelectMany(w=>w.Children).Except(AllLoaded.Select(w=>w.Id)).ToArray();var list=new List<WorkItem>();foreach(var id in ids)list.Add(await Tracker.GetAsync(id,ct));related=list;}
 async Task Navigate(int delta){if(busy.Count>0||applying)return;var next=sprintIndex+delta;if(meta is null||next<0||next>=meta.Iterations.Length)return;sprintIndex=next;selected.Clear();selectionAnchor=-1;detail=null;cleanupFilter="";await LoadSprint();}
 async Task ChooseSprint(Iteration iteration){if(busy.Count>0||applying)return;sprintIndex=Array.IndexOf(meta!.Iterations,iteration);selected.Clear();detail=null;CloseDialog();await LoadSprint();}
 async Task Refresh(){if(screen=="planning"&&planner is not null){await planner.RefreshPlanning();return;}if(screen=="branches"){if(branchRepositoryId=="")await LoadBranchProjects();else await LoadBranches();return;}if(screen=="pullrequests"){if(pullRequestRepositoryId=="")await LoadPullRequestProjects();else await LoadPullRequests();return;}if(busy.Count>0||applying)return;sprintCache.Clear();capacityByIteration.Clear();await LoadSprint(true);await LoadFutureCapacities(lifetime.Token);await LoadPlanningItems(lifetime.Token);await LoadCalendarHolidays(lifetime.Token);if(detail is not null)await OpenById(detail.Id);}
 async Task SetQuickView(string view){quickView=view;cleanupFilter="";if(view=="Carry-over"){loading=true;try{await LoadPrevious(refreshToken.Token);}catch(Exception e){Error(e);}finally{loading=false;}}}
 void ClearFilters(){search=ownerSearch=stateFilter=typeFilter=tagFilter=applicationFilter=areaFilter=priorityFilter=cleanupFilter=attentionFilter=filterOptionSearch="";workspaceActiveOnly=workspaceBlockedOnly=workspaceOwnerMode=false;workspacePriorityOwner=null;ownerFilters.Clear();cleanupTagFilters.Clear();quickView="Team";}
 void ToggleOwner(string id){if(screen=="workspace"&&workspaceOwnerMode){SetWorkspacePriorityOwner(id);return;}if(!ownerFilters.Add(id))ownerFilters.Remove(id);}
 void ToggleCleanupTag(string tag){if(!cleanupTagFilters.Add(tag))cleanupTagFilters.Remove(tag);}
 void SelectVisibleAndMoveNext(){selected.Clear();foreach(var w in Visible)selected.Add(w.Id);StartBulk("Next");}
 sealed record PersonLane(string Id,string Name,IReadOnlyList<WorkItem> Items);
 sealed record DailyPersonOption(string Id,string Name,int Open,int RecentlyClosed);
 DateRange[] DaysOff(string personId){IEnumerable<SprintCapacity> capacities=capacityByIteration.Count>0?capacityByIteration.Values:new[]{sprintCapacity};return capacities.SelectMany(c=>c.TeamDaysOff.Concat(c.Members.FirstOrDefault(m=>m.PersonId==personId)?.DaysOff??[])).GroupBy(r=>(r.Start.Date,r.End.Date)).Select(g=>g.First()).OrderBy(r=>r.Start).ToArray();}
 string RangeText(DateRange r)=>r.Start.Date==r.End.Date?$"{r.Start:MMM d}":$"{r.Start:MMM d}–{r.End:MMM d}";
 string DaysOffText(string personId){var ranges=DaysOff(personId);if(ranges.Length==0)return "";return string.Join(", ",ranges.Select(RangeText));}
 bool OffToday(string personId){var today=DateTimeOffset.Now.Date;return DaysOff(personId).Any(r=>today>=r.Start.Date&&today<=r.End.Date);}
 bool InSelectedSprint(DateRange r)=>CurrentSprint?.Start is {} start&&CurrentSprint.Finish is {} finish&&r.End.Date>=start.Date&&r.Start.Date<=finish.Date;
 DateRange? NextDaysOff(string personId,Iteration? iteration=null){var today=DateTimeOffset.Now.Date;IEnumerable<DateRange> rows=DaysOff(personId).Where(r=>r.End.Date>=today);if(iteration?.Start is {} start&&iteration.Finish is {} finish)rows=rows.Where(r=>r.End.Date>=start.Date&&r.Start.Date<=finish.Date);return rows.OrderBy(r=>r.Start).FirstOrDefault();}
 IReadOnlyList<(string PersonId,string Name,DateRange Range)> UpcomingDaysOff(){if(meta is null)return [];var today=DateTimeOffset.Now.Date;var rows=new List<(string,string,DateRange)>();foreach(var p in meta.People){foreach(var r in DaysOff(p.Id).Where(r=>r.End.Date>=today))rows.Add((p.Id,p.Name,r));}return rows.Distinct().OrderBy(x=>x.Item3.Start).ToList();}
 IReadOnlyList<(string PersonId,string Name,DateRange Range)> VisibleDaysOff(){var rows=UpcomingDaysOff();return showAllDaysOff?rows:rows.Take(1).ToList();}
 sealed record HomeTimeOffDay(DateOnly Date,string Title,string[] Sources);
 IReadOnlyList<HomeTimeOffDay> HomeTimeOffDays(){
  var today=DateOnly.FromDateTime(DateTime.Now);
  var rows=new Dictionary<DateOnly,(HashSet<string> Labels,HashSet<string> Sources)>();
  void Add(DateOnly day,string label,string source){
   if(day<today)return;
   if(!rows.TryGetValue(day,out var row)){row=(new(StringComparer.OrdinalIgnoreCase),new(StringComparer.OrdinalIgnoreCase));rows[day]=row;}
   if(!string.IsNullOrWhiteSpace(label))row.Labels.Add(label.Trim());
   row.Sources.Add(source);
  }
  foreach(var leave in UpcomingDaysOff()){
   var from=DateOnly.FromDateTime(leave.Range.Start.Date);if(from<today)from=today;
   var to=DateOnly.FromDateTime(leave.Range.End.Date);
   for(var day=from;day<=to;day=day.AddDays(1)){if(day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)continue;Add(day,leave.Name,"Day off");}
  }
  foreach(var holiday in UpcomingHolidays()){
   var from=holiday.Start<today?today:holiday.Start;
   for(var day=from;day<=holiday.End;day=day.AddDays(1))Add(day,holiday.Name,holiday.Country);
  }
  var allHolidaySources=HolidayCountries.Select(c=>c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
  return rows.OrderBy(x=>x.Key).Select(x=>{
   var sources=x.Value.Sources;
   string[] pills;
   if(allHolidaySources.All(s=>sources.Contains(s)))pills=sources.Contains("Day off")?["All","Day off"]:["All"];
   else pills=HolidayCountries.Select(c=>c.Name).Where(s=>sources.Contains(s)).Concat(sources.Contains("Day off")?["Day off"]:Array.Empty<string>()).ToArray();
   return new HomeTimeOffDay(x.Key,string.Join(" · ",x.Value.Labels.OrderBy(s=>s,StringComparer.OrdinalIgnoreCase)),pills);
  }).ToList();
 }
 string TimeOffDateText(DateOnly day)=>day.Year==DateTime.Now.Year?day.ToString("MMM d"):day.ToString("MMM d, yyyy");
 sealed record HolidayCountry(string Code,string Name,string ApiCode,string? Subdivision=null);
 static readonly HolidayCountry[] HolidayCountries=[
  new("BE","Belgium","BE"),
  new("CA","Canada","CA"),
  new("CA-QC","Québec","CA","CA-QC"),
  new("PL","Poland","PL"),
  new("CZ","Czechia","CZ"),
  new("DE","Germany","DE")
 ];
 sealed record CalendarHoliday(string Name,DateOnly Start,DateOnly End,string CountryCode,string Country);
 bool holidayFallbackUsed;
 List<CalendarHoliday> BuiltInCountryHolidays(HolidayCountry country,int year)=>PublicHolidays.For(country.Code,year).Select(h=>new CalendarHoliday(h.Name,h.Date,h.Date,country.Code,country.Name)).ToList();
 async Task<List<CalendarHoliday>> LoadCountryHolidays(HolidayCountry country,int year,CancellationToken ct){
  try{
   using var requestCts=CancellationTokenSource.CreateLinkedTokenSource(ct);requestCts.CancelAfter(TimeSpan.FromSeconds(5));
   var json=await Http.GetStringAsync($"https://date.nager.at/api/v3/PublicHolidays/{year}/{country.ApiCode}",requestCts.Token);
   using var doc=JsonDocument.Parse(json);var list=new List<CalendarHoliday>();
   foreach(var h in doc.RootElement.EnumerateArray()){
    var isGlobal=!h.TryGetProperty("global",out var global)||global.ValueKind!=JsonValueKind.False;
    var appliesToSubdivision=false;
    if(country.Subdivision is not null&&h.TryGetProperty("counties",out var counties)&&counties.ValueKind==JsonValueKind.Array)
      appliesToSubdivision=counties.EnumerateArray().Any(c=>string.Equals(c.GetString(),country.Subdivision,StringComparison.OrdinalIgnoreCase));
    if(country.Subdivision is null&&!isGlobal)continue;
    if(country.Subdivision is not null&&!isGlobal&&!appliesToSubdivision)continue;
    if(!h.TryGetProperty("date",out var dateNode)||!DateOnly.TryParse(dateNode.GetString(),out var day))continue;
    var name=h.TryGetProperty("localName",out var local)&&!string.IsNullOrWhiteSpace(local.GetString())?local.GetString()!:h.TryGetProperty("name",out var english)?english.GetString()??"Holiday":"Holiday";
    list.Add(new(name,day,day,country.Code,country.Name));
   }
   if(list.Count>0)return list;
   holidayFallbackUsed=true;return BuiltInCountryHolidays(country,year);
  }catch(OperationCanceledException) when(!ct.IsCancellationRequested){holidayFallbackUsed=true;return BuiltInCountryHolidays(country,year);}
   catch(OperationCanceledException){throw;}
   catch{holidayFallbackUsed=true;return BuiltInCountryHolidays(country,year);}
 }
 async Task LoadCalendarHolidays(CancellationToken ct){
  holidayCalendarError="";holidayFallbackUsed=false;var year=DateTime.Now.Year;
  var requests=HolidayCountries.SelectMany(c=>new[]{year,year+1}.Select(y=>LoadCountryHolidays(c,y,ct))).ToArray();
  var results=await Task.WhenAll(requests);calendarHolidays=results.SelectMany(x=>x).GroupBy(x=>(x.CountryCode,x.Name,x.Start)).Select(g=>g.First()).OrderBy(x=>x.Start).ThenBy(x=>x.Country).ToList();
  if(calendarHolidays.Count==0)holidayCalendarError="Public holiday calendars could not be loaded.";
  else if(holidayFallbackUsed)holidayCalendarError="Using the built-in public-holiday calendar for one or more regions because the online calendar was unavailable.";
 }
 IReadOnlyList<CalendarHoliday> UpcomingHolidays(bool applyFilter=true){var today=DateOnly.FromDateTime(DateTime.Now);IEnumerable<CalendarHoliday> rows=calendarHolidays.Where(h=>h.End>=today);if(applyFilter&&holidayCountryFilter!="ALL")rows=rows.Where(h=>h.CountryCode==holidayCountryFilter);return rows.OrderBy(h=>h.Start).ThenBy(h=>h.Country).ToList();}
 IReadOnlyList<CalendarHoliday> HomeHolidays(){var rows=UpcomingHolidays();if(holidayCountryFilter!="ALL")return rows.Take(12).ToList();return rows.GroupBy(h=>h.CountryCode).SelectMany(g=>g.Take(2)).OrderBy(h=>h.Start).ThenBy(h=>h.Country).ToList();}
 IReadOnlyList<CalendarHoliday> VisibleHolidays(){var rows=UpcomingHolidays();return showAllDaysOff?rows:rows.Take(1).ToList();}
 void SetHolidayCountry(string code){holidayCountryFilter=code;showAllDaysOff=false;}
 bool InSelectedSprint(CalendarHoliday holiday)=>CurrentSprint?.Start is {} start&&CurrentSprint.Finish is {} finish&&holiday.End>=DateOnly.FromDateTime(start.DateTime)&&holiday.Start<=DateOnly.FromDateTime(finish.DateTime);
 int HiddenDaysOffCount()=>Math.Max(0,UpcomingDaysOff().Count+UpcomingHolidays().Count-VisibleDaysOff().Count-VisibleHolidays().Count);
 string HolidayCalendarForPerson(string personId)=>PlanningPrefs.HolidayCalendarByPerson.GetValueOrDefault(personId,"");
 string HolidayCalendarName(string personId){var code=HolidayCalendarForPerson(personId);return HolidayCountries.FirstOrDefault(c=>c.Code==code)?.Name??"";}
 string CapacityBaselineText(string personId,Iteration? iteration){var text=$"{ConfiguredCapacityHours(personId,iteration):0.#}h sprint";var calendar=HolidayCalendarName(personId);return calendar==""?text:$"{text} · {calendar}";}
 HashSet<DateOnly> UnavailableWorkingDates(string personId,Iteration iteration){
  var dates=new HashSet<DateOnly>();
  if(iteration.Start is not {} start||iteration.Finish is not {} finish)return dates;
  var first=DateOnly.FromDateTime(start.Date);
  var last=DateOnly.FromDateTime(finish.Date);
  foreach(var range in DaysOff(personId)){
   var from=DateOnly.FromDateTime(range.Start.Date);
   var to=DateOnly.FromDateTime(range.End.Date);
   for(var day=from;day<=to;day=day.AddDays(1))if(day>=first&&day<=last&&day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)dates.Add(day);
  }
  var calendar=HolidayCalendarForPerson(personId);
  if(calendar!="")foreach(var holiday in calendarHolidays.Where(h=>h.CountryCode==calendar&&h.End>=first&&h.Start<=last))
    for(var day=holiday.Start;day<=holiday.End;day=day.AddDays(1))if(day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)dates.Add(day);
  return dates;
 }
 bool Blocked(WorkItem w)=>w.Tags.Any(t=>prefs.BlockedTags.Contains(t,StringComparer.OrdinalIgnoreCase));
 int WorkGroupRank(WorkItem w)=>meta is not null&&Quality.Finished(w,meta)?0:Blocked(w)?1:2;
 string WorkGroupName(WorkItem w)=>WorkGroupRank(w) switch{0=>"Done",1=>"Blocked",_=>"Active"};
 IReadOnlyList<PersonLane> PeopleLanes(){if(meta is null)return [];IEnumerable<WorkItem> scoped=Visible.Where(w=>w.OwnerId!="");if(screen=="daily"&&dailyActiveOnly)scoped=scoped.Where(w=>!Quality.Finished(w,meta));var rows=scoped.ToList();return meta.People.Select(p=>new PersonLane(p.Id,p.Name,rows.Where(w=>w.OwnerId==p.Id).OrderBy(WorkGroupRank).ThenBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToList())).Where(l=>l.Items.Count>0).ToList();}
 double ConfiguredCapacityHours(string personId,Iteration? iteration){
  var hours=PlanningPrefs.PersonCapacityHours.GetValueOrDefault(personId,PlanningPrefs.DefaultTarget.Maximum);
  if(iteration is not null&&PlanningPrefs.CapacityOverrides.TryGetValue(PlanningSettings.CapacityKey(personId,iteration.Id),out var sprintTarget))hours=sprintTarget.Maximum;
  return Math.Max(0,hours);
 }
 double PlannedCapacityHours(string personId,Iteration? iteration){
  var configured=ConfiguredCapacityHours(personId,iteration);
  if(iteration?.Start is not {} start||iteration.Finish is not {} finish)return configured;
  var first=DateOnly.FromDateTime(start.Date);
  var last=DateOnly.FromDateTime(finish.Date);
  var workingDays=new List<DateOnly>();for(var day=first;day<=last;day=day.AddDays(1))if(day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)workingDays.Add(day);
  if(workingDays.Count==0)return configured;
  var today=DateOnly.FromDateTime(DateTime.Now);var unavailable=UnavailableWorkingDates(personId,iteration);
  var remaining=workingDays.Count(day=>day>=today&&!unavailable.Contains(day));
  return Math.Max(0,configured*remaining/workingDays.Count);
 }
 int DailyActiveCount(PersonLane lane)=>meta is null?lane.Items.Count:lane.Items.Count(w=>!Quality.Finished(w,meta));
 int DailyDoneCount(PersonLane lane)=>meta is null?0:lane.Items.Count(w=>Quality.Finished(w,meta));
 double DailyEffort(PersonLane lane)=>meta is null?lane.Items.Sum(w=>w.Estimate??0):lane.Items.Where(w=>!Quality.Finished(w,meta)).Sum(w=>Planning.Estimate(w,PlanningPrefs)??0);
 int CapacityUsage(PersonLane lane){var max=PlannedCapacityHours(lane.Id,CurrentSprint);return !PlanningPrefs.EstimatesAreHours||max<=0?0:(int)Math.Round(100d*DailyEffort(lane)/max);}
 PlanningSettings PlanningPrefs {get{if(!prefs.PlanningProfiles.TryGetValue(PlanningProfileKey,out var value)){value=Tracker.Demo?SprintPilot.Infrastructure.PlanningExample.Settings():new();prefs.PlanningProfiles[PlanningProfileKey]=value;}return value;}}
 IEnumerable<WorkItem> CurrentOpenItems=>meta is null?[]:items.Where(w=>!Quality.Finished(w,meta));
 IEnumerable<WorkItem> OpenPlanningItems=>meta is null?[]:planningItems.Where(w=>!Quality.Finished(w,meta)&&Planning.IsPlanningType(w.Type,PlanningPrefs));
 string[] ApplicationTags()=>PlanningPrefs.Tags.Where(t=>t.Application).Select(t=>t.Tag).ToArray();
 string[] InitiativeTags()=>PlanningPrefs.Tags.Where(t=>t.Initiative).Select(t=>t.Tag).ToArray();
 bool HasApplicationTag(WorkItem w){var tags=ApplicationTags();return tags.Length>0&&tags.Any(t=>w.Tags.Contains(t,StringComparer.OrdinalIgnoreCase));}
 bool HasInitiativeTag(WorkItem w){var tags=InitiativeTags();return tags.Length>0&&tags.Any(t=>w.Tags.Contains(t,StringComparer.OrdinalIgnoreCase));}
 sealed record AttentionRow(string Key,string Label,int Count,string Hint);
 sealed record HomeGroup(string Name,int Count,double Effort);
 sealed record CapacityRow(string Id,string Name,int Count,double Effort,int MissingEstimates,double CapacityHours,int Percent,string Status,DateRange? NextLeave);
 sealed record InitiativeRow(string Tag,int Count,double Effort,InitiativeMetadata Meta,bool NeedsAttention);
 sealed record SmartOrderRow(int Position,WorkItem Item,string Owner,string Group);
 sealed class MeetingImport {public string[] Summary {get;set;}=[];public string[] Decisions {get;set;}=[];public List<MeetingActionInput> Actions {get;set;}=[];}
 sealed class MeetingActionInput {public string Title {get;set;}="";public string Description {get;set;}="";public string AcceptanceCriteria {get;set;}="";public string Owner {get;set;}="unassigned";public string Sprint {get;set;}="current";public double? SuggestedEstimate {get;set;}public string[] Tags {get;set;}=[];}
 sealed class MeetingActionDraft {public bool Selected {get;set;}=true;public string Title {get;set;}="";public string Description {get;set;}="";public string AcceptanceCriteria {get;set;}="";public string Owner {get;set;}="";public string Sprint {get;set;}="current";public double? SuggestedEstimate {get;set;}public string TagsText {get;set;}="";public int? CreatedId {get;set;}public string CreatedUrl {get;set;}="";}
 sealed record SmartFixGap(WorkItem Item,bool MissingApplication,bool MissingEstimate,bool MissingTags);
 sealed class SmartFixImport {public List<SmartFixSuggestionInput> Items {get;set;}=[];}
 sealed class SmartFixSuggestionInput {public int Id {get;set;}public string? ApplicationTag {get;set;}public double? Estimate {get;set;}public string[] AddTags {get;set;}=[];public string? Reason {get;set;}}
 sealed class SmartFixSuggestion {public int Id {get;set;}public string ApplicationTag {get;set;}="";public double? Estimate {get;set;}public string[] AddTags {get;set;}=[];public string Reason {get;set;}="";public bool ApplyApplication {get;set;}public bool ApplyEstimate {get;set;}public bool ApplyTags {get;set;}}
 sealed record MagicCandidate(string PersonId,string UniqueName,string Name,double CurrentEffort,double CapacityHours,int CurrentPercent,int ProjectedPercent,int SameApplicationItems,DateRange? NextLeave);
 sealed record MagicContext(WorkItem Item,string CurrentOwnerId,string CurrentOwnerName,double Estimate,int CurrentPercent,int SourceProjectedPercent,MagicCandidate[] Candidates);
 sealed record MagicProjectionRow(string Id,string Name,int Before,int After);
 sealed class MagicImport {public List<MagicSuggestionInput> Suggestions {get;set;}=[];}
 sealed class MagicSuggestionInput {public int Id {get;set;}public string SuggestedOwnerId {get;set;}="";public string Reason {get;set;}="";public string Confidence {get;set;}="";}
 sealed class MagicSuggestion {public int Id {get;set;}public string SuggestedOwnerId {get;set;}="";public string Reason {get;set;}="";public string Confidence {get;set;}="Medium";public string Decision {get;set;}="pending";}
 bool MissingEstimateForOpenWork(WorkItem w)=>meta is not null&&!Quality.Finished(w,meta)&&Planning.Estimate(w,PlanningPrefs) is null;
 IReadOnlyList<AttentionRow> HomeAttention(){var rows=CurrentOpenItems.ToArray();var list=new List<AttentionRow>();void Add(string key,string label,int count,string hint){if(count>0)list.Add(new(key,label,count,hint));}Add("blocked","Blocked work",rows.Count(Blocked),"Needs an unblock or dependency decision");Add("unassigned","Unassigned",rows.Count(w=>w.OwnerId==""),"Give the work a clear owner");Add("missing-estimate","Missing estimates",rows.Count(MissingEstimateForOpenWork),"Estimate these items before relying on sprint capacity");if(ApplicationTags().Length>0)Add("missing-app","Missing application tag",rows.Count(w=>!HasApplicationTag(w)),"Classify work so application load stays useful");Add("stale","Stagnating work",rows.Count(w=>w.Changed!=default&&w.Changed<DateTimeOffset.UtcNow.AddDays(-prefs.StaleDays)),$"No change for more than {prefs.StaleDays} days");return list;}
 List<HomeGroup> Remaining(TagDimension dimension){var configured=PlanningPrefs.Tags.Where(t=>dimension==TagDimension.Application?t.Application:t.Initiative).Select(t=>t.Tag).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();return configured.Select(tag=>{var rows=OpenPlanningItems.Where(w=>w.Tags.Contains(tag,StringComparer.OrdinalIgnoreCase)).ToArray();return new HomeGroup(tag,rows.Length,rows.Sum(w=>Planning.Estimate(w,PlanningPrefs)??0));}).Where(x=>x.Count>0).OrderByDescending(x=>x.Count).ThenBy(x=>x.Name).ToList();}
 List<CapacityRow> CapacityRowsFor(Iteration? iteration){if(meta is null||iteration is null)return [];var open=OpenPlanningItems.Where(w=>w.Iteration.Equals(iteration.Path,StringComparison.OrdinalIgnoreCase)).ToArray();return meta.People.Select(p=>{var rows=open.Where(w=>w.OwnerId==p.Id).ToArray();var estimates=rows.Select(w=>Planning.Estimate(w,PlanningPrefs)).ToArray();var effort=estimates.Sum(x=>x??0);var missing=estimates.Count(x=>x is null);var capacity=PlannedCapacityHours(p.Id,iteration);var percent=!PlanningPrefs.EstimatesAreHours?0:capacity<=0?(effort>0?101:0):(int)Math.Round(100*effort/capacity);var status=!PlanningPrefs.EstimatesAreHours?"Enable hour estimates":effort>capacity?"Over capacity":missing>0?"Estimates missing":capacity<=0?"Sprint time elapsed":effort>=capacity*.85?"Nearly full":"Room available";return new CapacityRow(p.Id,p.Name,rows.Length,effort,missing,capacity,percent,status,NextDaysOff(p.Id,iteration));}).Where(x=>x.Count>0).OrderByDescending(x=>x.Percent).ThenBy(x=>x.Name).ToList();}
 string CapacityLoadText(CapacityRow row){if(!PlanningPrefs.EstimatesAreHours)return "Hour comparison off";if(row.CapacityHours<=0)return row.Effort>0?$"{row.Percent}% · no capacity":"0%";var delta=row.Effort-row.CapacityHours;var unknown=row.MissingEstimates>0?" + unknown":"";return delta>0?$"{row.Percent}% · +{delta:0.#}h{unknown}":$"{row.Percent}% · {Math.Max(0,-delta):0.#}h left{unknown}";}
 int CapacityFillPercent(CapacityRow row)=>Math.Clamp(row.Percent,0,100);
 int CapacityOveragePercent(CapacityRow row)=>Math.Clamp(row.Percent-100,0,100);
 string CapacityMeterTitle(CapacityRow row)=>row.Percent>100?$"{row.Percent}% of capacity · {row.Percent-100}% over":$"{row.Percent}% of capacity";
 InitiativeMetadata Initiative(string tag){var key=prefs.InitiativeMetadata.Keys.FirstOrDefault(k=>k.Equals(tag,StringComparison.OrdinalIgnoreCase));if(key is not null)return prefs.InitiativeMetadata[key];var value=new InitiativeMetadata();prefs.InitiativeMetadata[tag]=value;return value;}
 List<InitiativeRow> InitiativeRows(){var today=DateOnly.FromDateTime(DateTime.Now);return Remaining(TagDimension.Initiative).Select(g=>{var m=Initiative(g.Name);var attention=m.Status is "At Risk" or "Blocked"||m.Confidence=="Low"||(m.DueDate is {} due&&due<=today.AddDays(14));return new InitiativeRow(g.Name,g.Count,g.Effort,m,attention);}).OrderByDescending(x=>x.NeedsAttention).ThenBy(x=>x.Meta.DueDate??DateOnly.MaxValue).ThenBy(x=>x.Tag).ToList();}
 IEnumerable<(string Id,string Name,int Count)> FilterPeople=>items.GroupBy(w=>w.OwnerId).Select(g=>(Id:g.Key,Name:PersonName(g.Key),Count:g.Count())).Where(x=>filterOptionSearch==""||x.Name.Contains(filterOptionSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Name=="Unassigned").ThenBy(x=>x.Name);
 IEnumerable<(string Tag,int Count)> FilterApplications=>ApplicationTags().Select(tag=>(Tag:tag,Count:items.Count(w=>w.Tags.Contains(tag,StringComparer.OrdinalIgnoreCase)))).Where(x=>x.Count>0&&(filterOptionSearch==""||x.Tag.Contains(filterOptionSearch,StringComparison.OrdinalIgnoreCase))).OrderBy(x=>x.Tag);
 IEnumerable<(string Tag,int Count)> FilterTags {get{var applications=ApplicationTags().ToHashSet(StringComparer.OrdinalIgnoreCase);var blocked=prefs.BlockedTags.ToHashSet(StringComparer.OrdinalIgnoreCase);return items.SelectMany(w=>w.Tags).Where(t=>!applications.Contains(t)&&!blocked.Contains(t)).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).Select(g=>(Tag:g.Key,Count:g.Count())).Where(x=>filterOptionSearch==""||x.Tag.Contains(filterOptionSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Tag).ToArray();}}
 string[] KnownPlanningTags=>planningItems.Concat(tagHistory).SelectMany(w=>w.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
 string DisplayTitle(WorkItem w){var title=w.Title.Trim();foreach(var tag in w.Tags.OrderByDescending(t=>t.Length)){if(!title.StartsWith(tag,StringComparison.OrdinalIgnoreCase)||title.Length==tag.Length)continue;var tail=title[tag.Length..];if(tail.Length>0&&(char.IsWhiteSpace(tail[0])||"-–—:|/".Contains(tail[0]))){tail=tail.TrimStart(' ','-','–','—',':','|','/');if(tail.Length>0)return tail;}}return title;}
 void ToggleApplicationPill(string value)=>applicationFilter=applicationFilter.Equals(value,StringComparison.OrdinalIgnoreCase)?"":value;
 void ToggleTagPill(string value)=>tagFilter=tagFilter.Equals(value,StringComparison.OrdinalIgnoreCase)?"":value;
 void ToggleWorkspaceActiveOnly()=>workspaceActiveOnly=!workspaceActiveOnly;
 void ToggleWorkspaceBlockedOnly()=>workspaceBlockedOnly=!workspaceBlockedOnly;
 void OpenAttention(string key){ClearFilters();attentionFilter=key;screen="workspace";workspaceMode="List";detail=null;dailyPanelItem=null;}
 void OpenPerson(string id){ClearFilters();ownerFilters.Add(id);screen="workspace";workspaceMode="List";sort="Order";descending=false;detail=null;dailyPanelItem=null;}
 async Task OpenSprintPerson(Iteration iteration,string id){if(meta is null)return;var index=Array.IndexOf(meta.Iterations,iteration);if(index<0)return;sprintIndex=index;ClearFilters();ownerFilters.Add(id);screen="workspace";workspaceMode="List";sort="Order";descending=false;detail=null;dailyPanelItem=null;await LoadSprint();}
 void OpenGroup(string tag){ClearFilters();if(ApplicationTags().Contains(tag,StringComparer.OrdinalIgnoreCase))applicationFilter=tag;else tagFilter=tag;screen="workspace";workspaceMode="List";detail=null;dailyPanelItem=null;}
 string DefaultMeetingType()=>meta?.Types.FirstOrDefault(t=>t.Name is "Product Backlog Item" or "User Story")?.Name??meta?.Types.FirstOrDefault()?.Name??"";
 void EnsureMeetingDefaults(){if(meetingWorkType=="")meetingWorkType=DefaultMeetingType();if(meetingTitle=="")meetingTitle=$"Business meeting · {DateTime.Now:MMM d}";}
 void ResetMeeting(){meetingTitle=$"Business meeting · {DateTime.Now:MMM d}";meetingNotes=meetingCopilotText=meetingError="";meetingImport=new();meetingActions=[];meetingWorkType=DefaultMeetingType();}
 async Task CopyMeetingPrompt(){EnsureMeetingDefaults();if(string.IsNullOrWhiteSpace(meetingNotes)){meetingError="Add meeting notes first.";return;}meetingError="";var knownTags=string.Join(", ",KnownPlanningTags.Take(40));var prompt="""
You are helping structure business meeting notes for SprintPilot. Use only information present in the notes. Do not invent commitments, owners, estimates, acceptance criteria, or technical details. Return raw JSON only.

Current sprint: __CURRENT_SPRINT__
Next sprint: __NEXT_SPRINT__
Known Azure DevOps tags: __KNOWN_TAGS__

Required JSON shape:
{
  "summary": ["short factual summary point"],
  "decisions": ["decision explicitly made in the meeting"],
  "actions": [
    {
      "title": "clear backlog item title",
      "description": "concise context and requested outcome",
      "acceptanceCriteria": "only if supported by the notes; otherwise empty",
      "owner": "me or unassigned",
      "sprint": "current or next",
      "suggestedEstimate": null,
      "tags": ["existing relevant tag"]
    }
  ]
}

Rules:
- New and To Do are simply active work; do not invent workflow states.
- Use owner "me" only when the notes clearly assign the action to me. Otherwise use "unassigned".
- suggestedEstimate is optional and must be null when the notes do not support a reasonable suggestion.
- Prefer known tags and do not invent unnecessary tags.
- Keep actions small enough to become individual backlog items.
- Exclude discussion points that do not require action.

Meeting title: __MEETING_TITLE__

MEETING NOTES:
__MEETING_NOTES__
""".Replace("__CURRENT_SPRINT__",CurrentSprint?.Name??"not available").Replace("__NEXT_SPRINT__",NextSprint?.Name??"not available").Replace("__KNOWN_TAGS__",knownTags).Replace("__MEETING_TITLE__",meetingTitle).Replace("__MEETING_NOTES__",meetingNotes);await JS.InvokeVoidAsync("sprintPilot.copy",prompt);Notify("Copilot meeting prompt copied.");}
 static string StripCodeFence(string text){var value=text.Trim();var fence=new string((char)96,3);if(!value.StartsWith(fence))return value;var first=value.IndexOf('\n');if(first>=0)value=value[(first+1)..];if(value.EndsWith(fence))value=value[..^3];return value.Trim();}
 void ParseMeetingCopilot(){meetingError="";try{var parsed=JsonSerializer.Deserialize<MeetingImport>(StripCodeFence(meetingCopilotText),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException();meetingImport=parsed;meetingActions=parsed.Actions.Where(a=>!string.IsNullOrWhiteSpace(a.Title)).Select(a=>new MeetingActionDraft{Title=a.Title.Trim(),Description=a.Description??"",AcceptanceCriteria=a.AcceptanceCriteria??"",Owner=a.Owner.Equals("me",StringComparison.OrdinalIgnoreCase)?meta?.Me.UniqueName??"":meta?.People.FirstOrDefault(p=>p.Name.Equals(a.Owner,StringComparison.OrdinalIgnoreCase)||p.UniqueName.Equals(a.Owner,StringComparison.OrdinalIgnoreCase))?.UniqueName??"",Sprint=a.Sprint.Equals("next",StringComparison.OrdinalIgnoreCase)?"next":"current",SuggestedEstimate=a.SuggestedEstimate,TagsText=string.Join("; ",a.Tags??[])}).ToList();if(meetingActions.Count==0&&parsed.Summary.Length==0&&parsed.Decisions.Length==0)meetingError="No structured meeting content was found.";}catch{meetingError="The Copilot response is not valid SprintPilot JSON. Copy the generated prompt again and paste Copilot's raw JSON response here.";}}
 async Task CopyMeetingSummary(){var lines=new List<string>{meetingTitle};if(meetingImport.Summary.Length>0){lines.Add("");lines.Add("Summary");lines.AddRange(meetingImport.Summary.Select(x=>"• "+x));}if(meetingImport.Decisions.Length>0){lines.Add("");lines.Add("Decisions");lines.AddRange(meetingImport.Decisions.Select(x=>"• "+x));}var created=meetingActions.Where(a=>a.CreatedId is not null).ToArray();if(created.Length>0){lines.Add("");lines.Add("Created backlog items");lines.AddRange(created.Select(a=>$"• #{a.CreatedId} {a.Title}"));}await JS.InvokeVoidAsync("sprintPilot.copy",string.Join(Environment.NewLine,lines));Notify("Meeting summary copied.");}
 void AssignSelectedMeetingActionsToMe(){if(meta is null)return;foreach(var action in meetingActions.Where(a=>a.Selected&&a.CreatedId is null))action.Owner=meta.Me.UniqueName;}
 void SetSelectedMeetingSprint(string target){if(target=="next"&&NextSprint is null){meetingError="No next sprint is configured.";return;}meetingError="";foreach(var action in meetingActions.Where(a=>a.Selected&&a.CreatedId is null))action.Sprint=target;}
 async Task CreateMeetingActions(){if(meetingCreating||meta is null)return;meetingCreating=true;meetingError="";try{var type=meetingWorkType==""?DefaultMeetingType():meetingWorkType;var definition=meta.Types.FirstOrDefault(t=>t.Name==type)??throw new TrackerException("Choose a valid work-item type.");var area=meta.Scope.FirstOrDefault()?.Path??meta.Areas.FirstOrDefault()??"";var selectedActions=meetingActions.Where(a=>a.Selected&&a.CreatedId is null).ToArray();if(selectedActions.Length==0){meetingError="Select at least one action to create.";return;}foreach(var action in selectedActions){if(string.IsNullOrWhiteSpace(action.Title)){meetingError="Every selected action needs a title.";return;}var iteration=action.Sprint=="next"?NextSprint?.Path:CurrentSprint?.Path;if(string.IsNullOrWhiteSpace(iteration)){meetingError=action.Sprint=="next"?"No next sprint is configured.":"No current sprint is configured.";return;}var title=action.Title.Trim();if(title.Length>255)title=title[..255];var changes=new List<Change>{new(ItemField.Title,title),new(ItemField.Description,ContentText.HtmlEncode(action.Description??"")),new(ItemField.Area,area),new(ItemField.Iteration,iteration),new(ItemField.Tags,action.TagsText??"")};if(action.Owner!="")changes.Add(new(ItemField.Owner,action.Owner));if(action.SuggestedEstimate is {} estimate&&estimate>=0&&definition.EstimateField is not null)changes.Add(new(ItemField.Estimate,estimate));if(!string.IsNullOrWhiteSpace(action.AcceptanceCriteria)){if(definition.Fields.Contains("Microsoft.VSTS.Common.AcceptanceCriteria"))changes.Add(new(ItemField.Acceptance,ContentText.HtmlEncode(action.AcceptanceCriteria)));else{var description=(action.Description??"")+"\n\nACCEPTANCE CRITERIA:\n"+action.AcceptanceCriteria;changes.RemoveAll(c=>c.Field==ItemField.Description);changes.Add(new(ItemField.Description,ContentText.HtmlEncode(description)));}}var created=await Tracker.CreateAsync(type,changes,null,lifetime.Token);action.CreatedId=created.Id;action.CreatedUrl=created.Url;if(created.Iteration==CurrentSprint?.Path)items.Add(created);sprintCache.Clear();}Notify($"{selectedActions.Length} backlog item(s) created from the meeting.");}catch(Exception e){meetingError=e is TrackerException?e.Message:"The backlog items could not be created. Check the Azure DevOps connection and try again.";}finally{meetingCreating=false;}}
 void AddClassification(){var tag=classificationTag.Trim();if(tag=="")return;if(!PlanningPrefs.Tags.Any(t=>t.Tag.Equals(tag,StringComparison.OrdinalIgnoreCase)))PlanningPrefs.Tags.Add(new(tag,false,false));classificationTag="";}
 void SetClassification(string tag,bool application,bool enabled){var i=PlanningPrefs.Tags.FindIndex(t=>t.Tag.Equals(tag,StringComparison.OrdinalIgnoreCase));if(i<0)return;var old=PlanningPrefs.Tags[i];PlanningPrefs.Tags[i]=application?old with{Application=enabled}:old with{Initiative=enabled};}
 void RemoveClassification(string tag){PlanningPrefs.Tags.RemoveAll(t=>t.Tag.Equals(tag,StringComparison.OrdinalIgnoreCase));prefs.InitiativeMetadata.Remove(tag);}
 void SetInitiativeDueDate(string tag,ChangeEventArgs e){var text=e.Value?.ToString();Initiative(tag).DueDate=DateOnly.TryParse(text,out var due)?due:null;}
 void SetInitiativeStatus(string tag,ChangeEventArgs e)=>Initiative(tag).Status=e.Value?.ToString()??"Ongoing";
 void SetInitiativeConfidence(string tag,ChangeEventArgs e)=>Initiative(tag).Confidence=e.Value?.ToString()??"Medium";
 void SetInitiativeComment(string tag,ChangeEventArgs e)=>Initiative(tag).Comment=e.Value?.ToString()??"";
 IReadOnlyList<PersonLane> DailyLanes(){var lanes=PeopleLanes();return dailyFocusOwner==""?lanes:lanes.Where(l=>l.Id==dailyFocusOwner).ToList();}
 IReadOnlyList<(string Id,string Name,int Count)> WorkspacePriorityPeople=>items.GroupBy(w=>w.OwnerId).Select(g=>(Id:g.Key,Name:PersonName(g.Key),Count:g.Count(w=>meta is null||!Quality.Finished(w,meta)))).Where(x=>x.Count>0).OrderBy(x=>x.Name=="Unassigned").ThenBy(x=>x.Name).ToList();
 void ToggleWorkspaceOwnerMode(){workspaceOwnerMode=!workspaceOwnerMode;workspacePriorityOwner=null;ownerFilters.Clear();sort="Order";descending=false;quickView="Team";if(!workspaceOwnerMode)return;var people=WorkspacePriorityPeople;if(people.Count==0)return;var preferred=meta is null?-1:people.ToList().FindIndex(p=>p.Id==meta.Me.Id);SetWorkspacePriorityOwner(people[preferred>=0?preferred:0].Id);}
 void SetWorkspacePriorityOwner(string id){workspaceOwnerMode=true;workspacePriorityOwner=id;ownerFilters.Clear();ownerFilters.Add(id);sort="Order";descending=false;quickView="Team";}
 DateTimeOffset RecentClosedCutoff(){var date=DateTime.Today.AddDays(-1);while(date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)date=date.AddDays(-1);return new DateTimeOffset(date,TimeZoneInfo.Local.GetUtcOffset(date));}
 string RecentClosedSinceLabel=>$"Since {RecentClosedCutoff():ddd, MMM d} 00:00";
 IReadOnlyList<WorkItem> RecentlyClosedFor(string ownerId){if(meta is null)return [];var cutoff=RecentClosedCutoff();return items.Where(w=>w.OwnerId==ownerId&&Quality.Finished(w,meta)&&w.Changed>=cutoff).OrderByDescending(w=>w.Changed).ThenByDescending(w=>w.Id).ToList();}
 IReadOnlyList<WorkItem> DailyOpenFor(string ownerId){if(meta is null)return [];return Visible.Where(w=>w.OwnerId==ownerId&&!Quality.Finished(w,meta)).OrderBy(WorkGroupRank).ThenBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToList();}
 IReadOnlyList<DailyPersonOption> DailyPeople(){if(meta is null)return [];var cutoff=RecentClosedCutoff();return meta.People.Select(p=>{var owned=items.Where(w=>w.OwnerId==p.Id).ToList();return new DailyPersonOption(p.Id,p.Name,owned.Count(w=>!Quality.Finished(w,meta)),owned.Count(w=>Quality.Finished(w,meta)&&w.Changed>=cutoff));}).Where(p=>p.Open>0||p.RecentlyClosed>0).OrderBy(p=>p.Name).ToList();}
 void FocusDaily(string id){dailyFocusOwner=dailyFocusOwner==id?"":id;ownerFilters.Clear();}
 void ClearDailyFocus()=>dailyFocusOwner="";
 void MoveDailyFocus(int delta){var people=DailyPeople();if(people.Count==0){dailyFocusOwner="";return;}var index=people.ToList().FindIndex(p=>p.Id==dailyFocusOwner);if(index<0)index=0;else index=Math.Clamp(index+delta,0,people.Count-1);dailyFocusOwner=people[index].Id;ownerFilters.Clear();}
 void SetPlanningHours(ChangeEventArgs e)=>PlanningPrefs.EstimatesAreHours=e.Value is true;
 static bool TryHours(ChangeEventArgs e,out double hours){var text=e.Value?.ToString()??"";return (double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out hours)||double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out hours))&&double.IsFinite(hours)&&hours>=0;}
 void SetDefaultCapacityHours(ChangeEventArgs e){if(!TryHours(e,out var hours))return;var current=PlanningPrefs.DefaultTarget;PlanningPrefs.DefaultTarget=new(Math.Min(current.Minimum,hours),hours);}
 double PersonCapacityHours(string personId)=>PlanningPrefs.PersonCapacityHours.GetValueOrDefault(personId,PlanningPrefs.DefaultTarget.Maximum);
 bool HasPersonCapacityHours(string personId)=>PlanningPrefs.PersonCapacityHours.ContainsKey(personId);
 void SetPersonCapacityHours(string personId,ChangeEventArgs e){if(TryHours(e,out var hours))PlanningPrefs.PersonCapacityHours[personId]=hours;}
 void ClearPersonCapacityHours(string personId)=>PlanningPrefs.PersonCapacityHours.Remove(personId);
 void SetPersonHolidayCalendar(string personId,ChangeEventArgs e){var value=e.Value?.ToString()??"";if(value=="")PlanningPrefs.HolidayCalendarByPerson.Remove(personId);else if(HolidayCountries.Any(c=>c.Code==value))PlanningPrefs.HolidayCalendarByPerson[personId]=value;}
 async Task SearchDailyWork(){dailyLookupBusy=true;try{var q=dailyLookupText.Trim();dailyLookupResults=[];if(q=="")return;if(int.TryParse(q,out var id)){var found=planningItems.FirstOrDefault(w=>w.Id==id);if(found is null)found=await Tracker.GetAsync(id,lifetime.Token);if(found is not null&&!items.Any(w=>w.Id==found.Id))dailyLookupResults.Add(found);}else{dailyLookupResults=planningItems.Where(w=>!items.Any(x=>x.Id==w.Id)&&(w.Title.Contains(q,StringComparison.OrdinalIgnoreCase)||w.Tags.Any(t=>t.Contains(q,StringComparison.OrdinalIgnoreCase)))).OrderByDescending(w=>w.Changed).Take(8).ToList();}}catch(Exception e){Error(e);}finally{dailyLookupBusy=false;}}
 async Task AddDailyItem(WorkItem source){if(CurrentSprint is null)return;if(busy.Contains(source.Id))return;busy.Add(source.Id);try{var fresh=await Tracker.GetAsync(source.Id,lifetime.Token);var saved=fresh;if(!fresh.Iteration.Equals(CurrentSprint.Path,StringComparison.OrdinalIgnoreCase))saved=await Tracker.UpdateAsync(new(fresh,[new Change(ItemField.Iteration,CurrentSprint.Path)]),lifetime.Token);var index=items.FindIndex(w=>w.Id==saved.Id);if(index>=0)items[index]=saved;else items.Add(saved);dailyLookupResults.RemoveAll(w=>w.Id==saved.Id);sprintCache.Clear();Notify($"#{saved.Id} added to {CurrentSprint.Name}.");}catch(Exception e){Error(e);}finally{busy.Remove(source.Id);}}
 async Task OpenDailyPanel(WorkItem w){detail=null;dailyPanelItem=w;dailyPanelLoading=true;dailyComments=[];dailyTagText=dailyCommentText="";try{dailyComments=(await Tracker.CommentsAsync(w.Id,lifetime.Token)).ToList();}catch(Exception e){Error(e);}finally{dailyPanelLoading=false;}}
 IEnumerable<StateDefinition> DailyStates(WorkItem w)=>meta?.Types.FirstOrDefault(t=>t.Name==w.Type)?.States??[];
 string? DoneState(WorkItem w)=>DailyStates(w).FirstOrDefault(s=>s.Category=="Completed")?.Name??DailyStates(w).FirstOrDefault(s=>s.Name is "Done" or "Closed")?.Name;
 async Task MarkDone(WorkItem w){var done=DoneState(w);if(done is null){Notify($"{w.Type} has no completed state configured.");return;}await InlineEdit((w,ItemField.State,done));dailyPanelItem=items.FirstOrDefault(x=>x.Id==w.Id)??dailyPanelItem;}
 async Task AddDailyTag(){if(dailyPanelItem is null||string.IsNullOrWhiteSpace(dailyTagText))return;var w=dailyPanelItem;var value=string.Join("; ",w.Tags.Append(dailyTagText.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));await InlineEdit((w,ItemField.Tags,value));dailyPanelItem=items.FirstOrDefault(x=>x.Id==w.Id)??w;dailyTagText="";}
 async Task RemoveDailyPanelTag(string tag){if(dailyPanelItem is null)return;var w=dailyPanelItem;await RemoveTag((w,tag));dailyPanelItem=items.FirstOrDefault(x=>x.Id==w.Id)??w;}
 async Task ReassignDailyPanel(ChangeEventArgs e){if(dailyPanelItem is null)return;var w=dailyPanelItem;await InlineEdit((w,ItemField.Owner,e.Value?.ToString()??""));dailyPanelItem=items.FirstOrDefault(x=>x.Id==w.Id)??w;}
 async Task ChangeDailyState(ChangeEventArgs e){if(dailyPanelItem is null)return;var w=dailyPanelItem;await InlineEdit((w,ItemField.State,e.Value?.ToString()??w.State));dailyPanelItem=items.FirstOrDefault(x=>x.Id==w.Id)??w;}
 async Task AddDailyComment(){if(dailyPanelItem is null||string.IsNullOrWhiteSpace(dailyCommentText))return;try{var c=await Tracker.AddCommentAsync(dailyPanelItem.Id,dailyCommentText,lifetime.Token);dailyComments.Add(c);dailyCommentText="";var updated=dailyPanelItem with{CommentCount=dailyPanelItem.CommentCount+1,Changed=DateTimeOffset.UtcNow};Replace(updated);dailyPanelItem=updated;Notify($"Comment added to #{updated.Id}.");}catch(Exception e){Error(e);}}
 string OwnerValue(WorkItem w)=>meta?.People.FirstOrDefault(p=>p.Id==w.OwnerId)?.UniqueName??w.Owner;
 string PersonName(string id)=>meta?.People.FirstOrDefault(p=>p.Id==id)?.Name??"Unassigned";
 string[] SuggestedTags(WorkItem item){var words=item.Title.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(w=>w.Length>=4).ToHashSet(StringComparer.OrdinalIgnoreCase);return tagHistory.Where(w=>w.Id!=item.Id&&w.Tags.Length>0&&w.Title.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Count(words.Contains)>0).SelectMany(w=>w.Tags).Where(t=>!item.Tags.Contains(t,StringComparer.OrdinalIgnoreCase)).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).Take(3).Select(g=>g.Key).ToArray();}
 Task AddSuggestedTag((WorkItem Item,string Tag) value)=>InlineEdit((value.Item,ItemField.Tags,string.Join("; ",value.Item.Tags.Append(value.Tag).Distinct(StringComparer.OrdinalIgnoreCase))));
 Task RemoveTag((WorkItem Item,string Tag) value)=>InlineEdit((value.Item,ItemField.Tags,string.Join("; ",value.Item.Tags.Where(t=>!t.Equals(value.Tag,StringComparison.OrdinalIgnoreCase)))));
 bool CanOrder(WorkItem w)=>CurrentSprint is not null&&w.Iteration.Equals(CurrentSprint.Path,StringComparison.OrdinalIgnoreCase);
 async Task<bool> VerifySprintOrder(IReadOnlyList<int> expected){
  if(CurrentSprint is not {} sprint)return false;
  var refreshed=(await Tracker.SprintAsync(sprint.Path,lifetime.Token)).ToList();
  var actual=refreshed.Where(CanOrder).OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).Select(w=>w.Id).ToArray();
  sort="Order";descending=false;sprintCache.Clear();
  if(!actual.SequenceEqual(expected)){items=refreshed;sprintCache[sprint.Path]=[..items];return false;}
  var positions=expected.Select((id,index)=>(id,order:(double)index+1)).ToDictionary(x=>x.id,x=>x.order);
  for(var i=0;i<items.Count;i++)if(positions.TryGetValue(items[i].Id,out var order))items[i]=items[i] with{Order=order};
  sprintCache[sprint.Path]=[..items];
  return true;
 }
 async Task<bool> MoveSprintItem(IReadOnlyList<WorkItem> ordered,int movedId){
  if(CurrentSprint is not {} sprint)return false;
  var expected=ordered.Select(w=>w.Id).ToArray();var index=Array.IndexOf(expected,movedId);if(index<0)return false;
  var previous=index==0?0:expected[index-1];var next=index==expected.Length-1?0:expected[index+1];
  busy.Add(movedId);try{await Tracker.ReorderSprintAsync(sprint.Id,sprint.Path,movedId,previous,next,lifetime.Token);return await VerifySprintOrder(expected);}finally{busy.Remove(movedId);}
 }
 async Task<bool> PersistSprintOrder(IReadOnlyList<WorkItem> ordered){
  if(CurrentSprint is not {} sprint)return false;var expected=ordered.Select(w=>w.Id).ToArray();foreach(var id in expected)busy.Add(id);
  try{for(var i=0;i<expected.Length;i++){var previous=i==0?0:expected[i-1];var next=i==expected.Length-1?0:expected[i+1];await Tracker.ReorderSprintAsync(sprint.Id,sprint.Path,expected[i],previous,next,lifetime.Token);}return await VerifySprintOrder(expected);}
  finally{foreach(var id in expected)busy.Remove(id);}
 }
 void DragStart(int id){selected.Clear();selectionAnchor=-1;draggedId=id;}
 async Task DropOn(WorkItem target){if(draggedId is not {} sourceId||sourceId==target.Id)return;var ordered=items.Where(CanOrder).OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToList();var sourceIndex=ordered.FindIndex(w=>w.Id==sourceId);var targetIndexBefore=ordered.FindIndex(w=>w.Id==target.Id);if(sourceIndex<0||targetIndexBefore<0||!CanOrder(target)){draggedId=null;return;}var source=ordered[sourceIndex];var insertAfter=sourceIndex<targetIndexBefore;ordered.RemoveAt(sourceIndex);var targetIndex=ordered.FindIndex(w=>w.Id==target.Id);if(targetIndex<0){draggedId=null;return;}ordered.Insert(insertAfter?targetIndex+1:targetIndex,source);draggedId=null;try{var verified=await MoveSprintItem(ordered,sourceId);if(verified){pendingOrderHighlightId=sourceId;await InvokeAsync(StateHasChanged);}Notify(verified?"Sprint order updated in Azure DevOps.":"Azure DevOps returned a different sprint order. The sprint was refreshed; review the current order.");}catch(Exception e){Error(e);sprintCache.Clear();try{await LoadSprint();}catch{}}}
 void DailyDragStart(WorkItem w){if(CanOrder(w)){draggedId=w.Id;dailyDragOverId=null;}}
 void DailyDragOver(WorkItem w){if(draggedId is not null&&draggedId!=w.Id)dailyDragOverId=w.Id;}
 void DailyDragEnd(){draggedId=null;dailyDragOverId=null;}
 string DailyDropClass(WorkItem w){if(dailyDragOverId!=w.Id||draggedId is not {} sourceId)return "";var source=items.FirstOrDefault(x=>x.Id==sourceId);if(source is null)return "";return (source.Order??double.MaxValue)<(w.Order??double.MaxValue)?"drop-target drop-after":"drop-target drop-before";}
 async Task DailyDropOn(WorkItem target){if(draggedId is not {} id||id==target.Id){dailyDragOverId=null;return;}var source=items.FirstOrDefault(w=>w.Id==id);if(source is null){draggedId=null;dailyDragOverId=null;return;}if(source.OwnerId!=target.OwnerId){draggedId=null;dailyDragOverId=null;Notify("Reorder within the same person. Reassign the item first to move it to another person.");return;}if(WorkGroupRank(source)!=WorkGroupRank(target)){draggedId=null;dailyDragOverId=null;Notify("Done, blocked and active work stay in separate groups. Reorder within the same group.");return;}try{await DropOn(target);}finally{dailyDragOverId=null;}}
 void SetScreen(string target){screen=target;detail=null;dailyPanelItem=null;if(target=="daily"){workspaceMode="People";quickView="Team";attentionFilter="";workspaceOwnerMode=false;workspacePriorityOwner=null;}else if(target=="workspace"){workspaceMode="List";sort="Order";descending=false;workspaceOwnerMode=false;workspacePriorityOwner=null;}else if(target=="cleanup"){workspaceMode="List";workspaceOwnerMode=false;workspacePriorityOwner=null;}else if(target is "branches" or "pullrequests"){workspaceOwnerMode=false;workspacePriorityOwner=null;}else if(target=="meeting")EnsureMeetingDefaults();}
 List<SmartOrderRow> BuildSmartOrderPlan(){var ordered=items.Where(CanOrder).OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToList();var owners=ordered.Select(w=>w.OwnerId).Distinct().OrderBy(id=>id==""?1:0).ThenBy(id=>ordered.FindIndex(w=>w.OwnerId==id)).ToArray();var result=new List<SmartOrderRow>();foreach(var owner in owners){foreach(var w in ordered.Where(w=>w.OwnerId==owner).OrderBy(WorkGroupRank).ThenBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id))result.Add(new(result.Count+1,w,PersonName(owner),WorkGroupName(w)));}return result;}
 void PrepareSmartOrder(){smartOrderPlan=BuildSmartOrderPlan();if(smartOrderPlan.Count==0){Notify("No orderable work items were found in this sprint.");return;}dialog="smartorder";dialogError="";focusDialog=true;}
 async Task ApplySmartOrder(){if(smartOrdering)return;smartOrdering=true;dialogError="";try{var verified=await PersistSprintOrder(smartOrderPlan.Select(x=>x.Item).ToArray());if(!verified){dialogError="Azure DevOps returned a different order after the update. The sprint was refreshed; review the current order before retrying.";return;}dialog="";Notify("Smart order applied and verified in Azure DevOps.");}catch(Exception e){sprintCache.Clear();try{await LoadSprint();}catch{}dialogError=e is TrackerException?e.Message:"Smart order could not be fully applied. The sprint was refreshed; review the current Azure DevOps order before retrying.";}finally{smartOrdering=false;}}
 void ToggleDailyActiveOnly()=>dailyActiveOnly=!dailyActiveOnly;
 async Task OpenCleanup(){screen="cleanup";workspaceMode="List";ClearFilters();await LoadSprint();}
 async Task OpenBranchCleanup(){SetScreen("branches");if(branchProjects.Count==0)await LoadBranchProjects();else if(branchRepositoryId!=""&&branches.Count==0)await LoadBranches();}
 async Task LoadBranchProjects(){if(branchCleanupLoading)return;branchCleanupLoading=true;try{branchProjects=(await Tracker.ProjectsAsync(lifetime.Token)).ToList();if(branchProjects.Count==0)throw new TrackerException("No accessible Azure DevOps projects were found.");var preferred=connection?.Project??"";branchProject=branchProjects.FirstOrDefault(p=>p.Name.Equals(preferred,StringComparison.OrdinalIgnoreCase))?.Name??branchProjects[0].Name;await LoadBranchRepositoriesCore();}catch(Exception e){Error(e);}finally{branchCleanupLoading=false;}}
 async Task ChangeBranchProject(ChangeEventArgs e){branchProject=e.Value?.ToString()??"";branchProjectSearch="";branchCleanupLoading=true;try{await LoadBranchRepositoriesCore();}catch(Exception ex){Error(ex);}finally{branchCleanupLoading=false;}}
 async Task LoadBranchRepositoriesCore(){branchRepositories=[];branches=[];selectedBranches.Clear();branchDeleteResults=[];branchRepositoryId="";if(branchProject=="")return;branchRepositories=(await Tracker.RepositoriesAsync(branchProject,lifetime.Token)).ToList();if(branchRepositories.Count>0){branchRepositoryId=branchRepositories[0].Id;await LoadBranchesCore();}}
 async Task ChangeBranchRepository(ChangeEventArgs e){branchRepositoryId=e.Value?.ToString()??"";branchRepositorySearch="";branchCleanupLoading=true;try{branches=[];selectedBranches.Clear();branchDeleteResults=[];if(branchRepositoryId!="")await LoadBranchesCore();}catch(Exception ex){Error(ex);}finally{branchCleanupLoading=false;}}
 async Task LoadBranches(){if(branchCleanupLoading||branchRepositoryId=="")return;branchCleanupLoading=true;try{await LoadBranchesCore();}catch(Exception e){Error(e);}finally{branchCleanupLoading=false;}}
 async Task LoadBranchesCore(){branches=(await Tracker.BranchesAsync(branchProject,branchRepositoryId,lifetime.Token)).ToList();selectedBranches.IntersectWith(branches.Select(b=>b.Name));branchDeleteResults=[];}
 async Task SetBranchStaleDays(ChangeEventArgs e){if(!int.TryParse(e.Value?.ToString(),out var days))return;prefs.BranchCleanupStaleDays=Math.Clamp(days,30,365);try{await Preferences.SaveAsync(prefs,lifetime.Token);}catch(Exception ex){Error(ex);}}
 void ToggleBranch(string name){if(!selectedBranches.Add(name))selectedBranches.Remove(name);}
 void ToggleVisibleBranches(){var visible=VisibleBranches.Where(b=>!b.IsDefault&&!b.IsLocked&&!b.HasActivePullRequest).ToArray();if(visible.Length>0&&visible.All(b=>selectedBranches.Contains(b.Name)))foreach(var b in visible)selectedBranches.Remove(b.Name);else foreach(var b in visible)selectedBranches.Add(b.Name);}
 void SelectRecommendedBranches(){selectedBranches.Clear();foreach(var b in RecommendedBranches)selectedBranches.Add(b.Name);}
 void PrepareBranchDelete(IEnumerable<GitBranch> source){var requested=source.DistinctBy(b=>b.Name,StringComparer.OrdinalIgnoreCase).ToArray();branchDeleteQueue=requested.Where(b=>!b.IsDefault&&!b.IsLocked&&!b.HasActivePullRequest).ToList();branchDeleteResults=[];if(branchDeleteQueue.Count==0){Notify("No deletable branches are selected. Default, locked, and active-PR branches are protected.");return;}dialog="branchdelete";dialogError=requested.Length==branchDeleteQueue.Count?"":"Default, locked, or active-PR branches were excluded from this deletion.";focusDialog=true;}
 void ReviewSelectedBranches()=>PrepareBranchDelete(SelectedBranchRows);
 void ReviewRecommendedBranches()=>PrepareBranchDelete(RecommendedBranches);
 async Task DeleteBranchQueue(){if(branchDeleting||branchDeleteQueue.Count==0)return;branchDeleting=true;dialogError="";try{branchDeleteResults=(await Tracker.DeleteBranchesAsync(branchProject,branchRepositoryId,branchDeleteQueue.Select(b=>new BranchDeleteRequest(b.Name,b.ObjectId)).ToArray(),lifetime.Token)).ToList();var deleted=branchDeleteResults.Where(r=>r.Success).Select(r=>r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);branches.RemoveAll(b=>deleted.Contains(b.Name));foreach(var name in deleted)selectedBranches.Remove(name);if(branchDeleteResults.All(r=>r.Success)){var count=deleted.Count;dialog="";branchDeleteQueue=[];Notify($"{count} branch{(count==1?"":"es")} deleted in Azure DevOps.");}else dialogError=$"{branchDeleteResults.Count(r=>!r.Success)} branch deletion(s) were not confirmed. Refresh and review before retrying.";}catch(Exception e){Error(e);}finally{branchDeleting=false;}}
 async Task OpenPullRequestCleanup(){SetScreen("pullrequests");if(pullRequestProjects.Count==0)await LoadPullRequestProjects();else if(pullRequestRepositoryId!=""&&pullRequests.Count==0)await LoadPullRequests();}
 async Task LoadPullRequestProjects(){if(pullRequestCleanupLoading)return;pullRequestCleanupLoading=true;try{pullRequestProjects=(await Tracker.ProjectsAsync(lifetime.Token)).ToList();if(pullRequestProjects.Count==0)throw new TrackerException("No accessible Azure DevOps projects were found.");var preferred=connection?.Project??"";pullRequestProject=pullRequestProjects.FirstOrDefault(p=>p.Name.Equals(preferred,StringComparison.OrdinalIgnoreCase))?.Name??pullRequestProjects[0].Name;await LoadPullRequestRepositoriesCore();}catch(Exception e){Error(e);}finally{pullRequestCleanupLoading=false;}}
 async Task ChangePullRequestProject(ChangeEventArgs e){pullRequestProject=e.Value?.ToString()??"";pullRequestProjectSearch="";pullRequestCleanupLoading=true;try{await LoadPullRequestRepositoriesCore();}catch(Exception ex){Error(ex);}finally{pullRequestCleanupLoading=false;}}
 async Task LoadPullRequestRepositoriesCore(){pullRequestRepositories=[];pullRequests=[];selectedPullRequests.Clear();pullRequestAbandonResults=[];pullRequestRepositoryId="";if(pullRequestProject=="")return;pullRequestRepositories=(await Tracker.RepositoriesAsync(pullRequestProject,lifetime.Token)).ToList();if(pullRequestRepositories.Count>0){pullRequestRepositoryId=pullRequestRepositories[0].Id;await LoadPullRequestsCore();}}
 async Task ChangePullRequestRepository(ChangeEventArgs e){pullRequestRepositoryId=e.Value?.ToString()??"";pullRequestRepositorySearch="";pullRequestCleanupLoading=true;try{pullRequests=[];selectedPullRequests.Clear();pullRequestAbandonResults=[];if(pullRequestRepositoryId!="")await LoadPullRequestsCore();}catch(Exception ex){Error(ex);}finally{pullRequestCleanupLoading=false;}}
 async Task LoadPullRequests(){if(pullRequestCleanupLoading||pullRequestRepositoryId=="")return;pullRequestCleanupLoading=true;try{await LoadPullRequestsCore();}catch(Exception e){Error(e);}finally{pullRequestCleanupLoading=false;}}
 async Task LoadPullRequestsCore(){pullRequests=(await Tracker.PullRequestsAsync(pullRequestProject,pullRequestRepositoryId,lifetime.Token)).ToList();selectedPullRequests.IntersectWith(pullRequests.Select(p=>p.Id));pullRequestAbandonResults=[];}
 async Task SetPullRequestStaleDays(ChangeEventArgs e){if(!int.TryParse(e.Value?.ToString(),out var days))return;prefs.PullRequestCleanupStaleDays=Math.Clamp(days,30,365);try{await Preferences.SaveAsync(prefs,lifetime.Token);}catch(Exception ex){Error(ex);}}
 void TogglePullRequest(int id){if(!selectedPullRequests.Add(id))selectedPullRequests.Remove(id);}
 void ToggleVisiblePullRequests(){var visible=VisiblePullRequests.ToArray();if(visible.Length>0&&visible.All(p=>selectedPullRequests.Contains(p.Id)))foreach(var p in visible)selectedPullRequests.Remove(p.Id);else foreach(var p in visible)selectedPullRequests.Add(p.Id);}
 void SelectRecommendedPullRequests(){selectedPullRequests.Clear();foreach(var p in RecommendedPullRequests)selectedPullRequests.Add(p.Id);}
 void PreparePullRequestAbandon(IEnumerable<GitPullRequest> source){pullRequestAbandonQueue=source.DistinctBy(p=>p.Id).ToList();pullRequestAbandonResults=[];if(pullRequestAbandonQueue.Count==0){Notify("Select at least one active pull request first.");return;}dialog="prabandon";dialogError="";focusDialog=true;}
 void ReviewSelectedPullRequests()=>PreparePullRequestAbandon(SelectedPullRequestRows);
 void ReviewRecommendedPullRequests()=>PreparePullRequestAbandon(RecommendedPullRequests);
 async Task AbandonPullRequestQueue(){if(pullRequestAbandoning||pullRequestAbandonQueue.Count==0)return;pullRequestAbandoning=true;dialogError="";try{pullRequestAbandonResults=(await Tracker.AbandonPullRequestsAsync(pullRequestProject,pullRequestRepositoryId,pullRequestAbandonQueue.Select(p=>new PullRequestAbandonRequest(p.Id,p.SourceCommitId)).ToArray(),lifetime.Token)).ToList();var abandoned=pullRequestAbandonResults.Where(r=>r.Success).Select(r=>r.Id).ToHashSet();pullRequests.RemoveAll(p=>abandoned.Contains(p.Id));foreach(var id in abandoned)selectedPullRequests.Remove(id);if(pullRequestAbandonResults.All(r=>r.Success)){var count=abandoned.Count;dialog="";pullRequestAbandonQueue=[];Notify($"{count} pull request{(count==1?"":"s")} abandoned in Azure DevOps.");}else dialogError=$"{pullRequestAbandonResults.Count(r=>!r.Success)} pull request cleanup action(s) were not confirmed. Refresh and review before retrying.";}catch(Exception e){Error(e);}finally{pullRequestAbandoning=false;}}
 void Sort(string column){if(sort==column)descending=!descending;else{sort=column;descending=false;}}
 void Select((int Id,bool Shift) e){var visible=Visible;var index=visible.FindIndex(w=>w.Id==e.Id);if(e.Shift&&selectionAnchor>=0){var anchor=visible.FindIndex(w=>w.Id==selectionAnchor);if(anchor>=0){for(int i=Math.Min(anchor,index);i<=Math.Max(anchor,index);i++)selected.Add(visible[i].Id);return;}}if(!selected.Add(e.Id))selected.Remove(e.Id);selectionAnchor=e.Id;}
 void ToggleAll(){var visible=Visible;if(visible.All(w=>selected.Contains(w.Id)))foreach(var w in visible)selected.Remove(w.Id);else foreach(var w in visible)selected.Add(w.Id);}
 void OpenDetail(WorkItem w){dailyPanelItem=null;detail=w;}
 async Task OpenById(int id){try{detail=await Tracker.GetAsync(id,lifetime.Token);}catch(Exception e){Error(e);}}
 void Replace(WorkItem w){var i=items.FindIndex(x=>x.Id==w.Id);if(i>=0){var sprintOrder=items[i].Order;items[i]=w with{Order=sprintOrder};}var p=previousItems.FindIndex(x=>x.Id==w.Id);if(p>=0){var sprintOrder=previousItems[p].Order;previousItems[p]=w with{Order=sprintOrder};}if(detail?.Id==w.Id)detail=w;}
 static string Display(WorkItem w,ItemField f)=>f switch{ItemField.Title=>w.Title,ItemField.Owner=>w.Owner,ItemField.State=>w.State,ItemField.Iteration=>w.Iteration,ItemField.Area=>w.Area,ItemField.Estimate=>w.Estimate?.ToString(CultureInfo.InvariantCulture)??"",ItemField.Order=>w.Order?.ToString(CultureInfo.InvariantCulture)??"",ItemField.Priority=>w.Priority?.ToString()??"",ItemField.Tags=>string.Join("; ",w.Tags),ItemField.Description=>ContentText.Plain(w.Description),ItemField.Acceptance=>ContentText.Plain(w.Acceptance),_=>""};
 async Task InlineEdit((WorkItem Item,ItemField Field,string Value) edit){var w=edit.Item;if(!busy.Add(w.Id))return;try{
  if(drafts.ContainsKey(w.Id))throw new TrackerException("Apply or discard the local AI draft before editing this item.");
  object? value=edit.Value;if(edit.Field is ItemField.Estimate or ItemField.Order){if(edit.Value=="")value=null;else if(double.TryParse(edit.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)&&n>=0)value=n;else throw new TrackerException(edit.Field==ItemField.Order?"Order must be a non-negative number.":"Estimate must be a non-negative number.");}
  if(edit.Field==ItemField.Priority){if(int.TryParse(edit.Value,out var n)&&n>=1&&n<=4)value=n;else throw new TrackerException("Priority must be 1–4.");}
  var changes=new[]{new Change(edit.Field,value)};Replace(ItemChanges.Apply(w,changes,meta!));await InvokeAsync(StateHasChanged);var saved=await Tracker.UpdateAsync(new(w,changes),lifetime.Token);Replace(saved);sprintCache.Clear();
  if(saved.Iteration!=CurrentSprint?.Path)items.RemoveAll(x=>x.Id==saved.Id);if(saved.Iteration!=meta?.Iterations.ElementAtOrDefault(sprintIndex-1)?.Path)previousItems.RemoveAll(x=>x.Id==saved.Id);Notify($"#{w.Id} saved.");
 }catch(Exception e){Replace(w);Error(e);}finally{busy.Remove(w.Id);}}
 Task Show(string value){dialog=value;dialogError="";commandSearch="";focusDialog=true;return Task.CompletedTask;}
 void CloseDialog(){if(applying||branchDeleting||pullRequestAbandoning)return;dialog="";dialogError="";_=JS.InvokeVoidAsync("sprintPilot.restoreFocus");}

 async Task PrepareSmartFix(){
  try{
   if(SelectedItems.Count==0)throw new TrackerException("Select visible work items first.");
   if(SelectedItems.Any(w=>busy.Contains(w.Id)||drafts.ContainsKey(w.Id)))throw new TrackerException("Wait for saves and apply or discard local drafts before using Smart Fix.");
   var apps=ApplicationTags();
   var canSuggestTags=KnownPlanningTags.Length>0;
   smartFixGaps=SelectedItems.Select(w=>new SmartFixGap(w,apps.Length>0&&!HasApplicationTag(w),!Quality.Finished(w,meta!)&&(w.Estimate is null or <=0),canSuggestTags&&w.Tags.Length==0)).Where(g=>g.MissingApplication||g.MissingEstimate||g.MissingTags).ToList();
   if(smartFixGaps.Count==0)throw new TrackerException("The selected items have no missing application tag, estimate, or tags that Smart Fix can address.");
   smartFixSuggestions=[];smartFixCopilotText="";smartFixPrompt=BuildSmartFixPrompt();await Show("smartfix");
  }catch(Exception e){Error(e);}
 }
 string SmartFixNeeds(SmartFixGap gap)=>string.Join(", ",new[]{gap.MissingApplication?"application tag":"",gap.MissingEstimate?"estimate":"",gap.MissingTags?"tags":""}.Where(x=>x!=""));
 static string ClipSmartFixText(string value,int max=1600){var text=ContentText.Plain(value??"").Trim();return text.Length<=max?text:text[..max]+"…";}
 string BuildSmartFixPrompt(){
  var applications=string.Join(", ",ApplicationTags());
  var initiatives=string.Join(", ",InitiativeTags());
  var known=string.Join(", ",KnownPlanningTags.Take(80));
  var details=string.Join("\n\n",smartFixGaps.Select(g=>$"# {g.Item.Id}\nTitle: {g.Item.Title}\nType: {g.Item.Type}\nArea: {g.Item.Area}\nNeeds: {SmartFixNeeds(g)}\nCurrent estimate: {(g.Item.Estimate?.ToString("0.##",CultureInfo.InvariantCulture)??"(missing)")}\nExisting tags: {(g.Item.Tags.Length==0?"(none)":string.Join("; ",g.Item.Tags))}\nDescription: {ClipSmartFixText(g.Item.Description,1600)}\nAcceptance criteria / testing context: {ClipSmartFixText(g.Item.Acceptance,1000)}"));
  return """
You are helping clean up selected Azure DevOps work items for SprintPilot.
Use only the work-item information and the known tags below. Do not invent business scope or tags. For estimates, make a reasonable best-effort estimate from the title, description, acceptance criteria, work-item type, area and existing tags.
The initiative tag is OPTIONAL. Never add an initiative merely because an item does not have one.
Only propose values for fields listed in "Needs". Do not replace or remove existing tags.
For applicationTag, use one of the configured application tags or null if you cannot determine it.
For addTags, use only tags from Known Azure DevOps tags. Return only tags that should be ADDED.
For estimate, when an estimate is missing you MUST return a positive number. Derive the best estimate you can from the available context. If the detail is still insufficient to derive a confident estimate, use exactly 3 hours as the default. Never return null for an item whose Needs includes estimate.
Return raw JSON only, without Markdown fences.

Configured application tags: __APPLICATION_TAGS__
Configured initiative tags (optional): __INITIATIVE_TAGS__
Known Azure DevOps tags: __KNOWN_TAGS__

Required JSON shape:
{
  "items": [
    {
      "id": 123,
      "applicationTag": "Existing application tag or null",
      "estimate": 5,
      "addTags": ["Existing known tag"],
      "reason": "Short explanation grounded in the work item"
    }
  ]
}

WORK ITEMS
__WORK_ITEMS__
""".Replace("__APPLICATION_TAGS__",applications==""?"(none configured)":applications).Replace("__INITIATIVE_TAGS__",initiatives==""?"(none configured)":initiatives).Replace("__KNOWN_TAGS__",known==""?"(none known)":known).Replace("__WORK_ITEMS__",details);
 }
 async Task CopySmartFixPrompt(){try{if(string.IsNullOrWhiteSpace(smartFixPrompt))throw new TrackerException("Prepare Smart Fix first.");await JS.InvokeVoidAsync("sprintPilot.copy",smartFixPrompt);Notify($"Copied Smart Fix prompt for {smartFixGaps.Count} item(s).");}catch(Exception e){Error(e);}}
 void ParseSmartFixCopilot(){
  try{
   var parsed=JsonSerializer.Deserialize<SmartFixImport>(StripCodeFence(smartFixCopilotText),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException();
   var duplicates=parsed.Items.GroupBy(x=>x.Id).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();if(duplicates.Length>0)throw new TrackerException("Copilot returned duplicate work-item IDs: "+string.Join(", ",duplicates));
   var gaps=smartFixGaps.ToDictionary(g=>g.Item.Id);
   var allowedTags=KnownPlanningTags.Concat(ApplicationTags()).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
   var applicationTags=ApplicationTags().ToHashSet(StringComparer.OrdinalIgnoreCase);
   var suggestions=new List<SmartFixSuggestion>();
   foreach(var input in parsed.Items){
    if(!gaps.TryGetValue(input.Id,out var gap))throw new TrackerException($"Copilot returned #{input.Id}, which was not part of this Smart Fix batch.");
    var application=(input.ApplicationTag??"").Trim();
    if(!gap.MissingApplication)application="";
    else if(application!=""&&!applicationTags.Contains(application))throw new TrackerException($"#{input.Id}: application tag '{application}' is not one of the configured application tags.");
    var estimate=input.Estimate;var defaultedEstimate=false;
    if(estimate is {} estimateValue&&(!double.IsFinite(estimateValue)||estimateValue<=0))throw new TrackerException($"#{input.Id}: estimate must be a positive number or null.");
    if(!gap.MissingEstimate)estimate=null;
    else if(estimate is null){estimate=3d;defaultedEstimate=true;}
    var addTags=(input.AddTags??[]).Where(t=>!string.IsNullOrWhiteSpace(t)).Select(t=>t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    foreach(var tag in addTags)if(!allowedTags.Contains(tag))throw new TrackerException($"#{input.Id}: tag '{tag}' is not a known Azure DevOps tag.");
    addTags=addTags.Where(t=>!gap.Item.Tags.Contains(t,StringComparer.OrdinalIgnoreCase)&&!t.Equals(application,StringComparison.OrdinalIgnoreCase)).ToArray();
    var reason=(input.Reason??"").Trim();if(defaultedEstimate)reason=reason==""?"Estimate defaulted to 3h because Copilot returned no estimate.":reason+" · Estimate defaulted to 3h.";
    var suggestion=new SmartFixSuggestion{Id=input.Id,ApplicationTag=application,Estimate=estimate,AddTags=addTags,Reason=reason,ApplyApplication=application!="",ApplyEstimate=estimate is not null,ApplyTags=addTags.Length>0};
    if(suggestion.ApplyApplication||suggestion.ApplyEstimate||suggestion.ApplyTags)suggestions.Add(suggestion);
   }
   smartFixSuggestions=suggestions;if(smartFixSuggestions.Count==0)throw new TrackerException("Copilot did not return any applicable Smart Fix suggestions.");
   dialogError="";
  }catch(Exception e){smartFixSuggestions=[];Error(e);}
 }
 void ReviewSmartFix(){
  try{
   var loaded=SelectedItems.ToDictionary(w=>w.Id);
   pending=[];
   foreach(var suggestion in smartFixSuggestions){
    if(!loaded.TryGetValue(suggestion.Id,out var original))throw new TrackerException($"#{suggestion.Id} is no longer loaded. Close Smart Fix and select the items again.");
    var changes=new List<Change>();var tags=original.Tags.ToList();var tagChanged=false;
    if(suggestion.ApplyApplication&&suggestion.ApplicationTag!=""&&!tags.Contains(suggestion.ApplicationTag,StringComparer.OrdinalIgnoreCase)){tags.Add(suggestion.ApplicationTag);tagChanged=true;}
    if(suggestion.ApplyTags)foreach(var tag in suggestion.AddTags)if(!tags.Contains(tag,StringComparer.OrdinalIgnoreCase)){tags.Add(tag);tagChanged=true;}
    if(tagChanged)changes.Add(new(ItemField.Tags,string.Join("; ",tags.Distinct(StringComparer.OrdinalIgnoreCase))));
    if(suggestion.ApplyEstimate&&suggestion.Estimate is {} estimate&&original.Estimate is null or <=0)changes.Add(new(ItemField.Estimate,estimate));
    if(changes.Count>0)pending.Add(new(original,changes));
   }
   if(pending.Count==0)throw new TrackerException("Choose at least one Smart Fix suggestion before continuing.");
   results=[];dialog="review";dialogError="";
  }catch(Exception e){Error(e);}
 }

 int MagicPercent(string personId,double effort){var capacity=PlannedCapacityHours(personId,CurrentSprint);return capacity<=0?(effort>0?999:0):(int)Math.Round(100d*effort/capacity);}
 string[] ItemApplications(WorkItem item)=>ApplicationTags().Where(tag=>item.Tags.Contains(tag,StringComparer.OrdinalIgnoreCase)).ToArray();
 List<MagicContext> BuildMagicContexts(){
  if(meta is null||CurrentSprint is null)return [];
  var current=items.Where(w=>!Quality.Finished(w,meta)&&Planning.IsPlanningType(w.Type,PlanningPrefs)&&w.Iteration.Equals(CurrentSprint.Path,StringComparison.OrdinalIgnoreCase)).ToArray();
  var effortByOwner=meta.People.ToDictionary(p=>p.Id,p=>current.Where(w=>w.OwnerId==p.Id).Sum(w=>Planning.Estimate(w,PlanningPrefs)??0),StringComparer.OrdinalIgnoreCase);
  var missingByOwner=meta.People.ToDictionary(p=>p.Id,p=>current.Count(w=>w.OwnerId==p.Id&&Planning.Estimate(w,PlanningPrefs) is null),StringComparer.OrdinalIgnoreCase);
  var contexts=new List<MagicContext>();
  foreach(var item in current.Where(w=>w.OwnerId!=""&&!Blocked(w))){
   var estimate=Planning.Estimate(item,PlanningPrefs);if(estimate is null or <=0)continue;
   var source=meta.People.FirstOrDefault(p=>p.Id==item.OwnerId);if(source is null||missingByOwner.GetValueOrDefault(source.Id)>0)continue;
   var sourceEffort=effortByOwner.GetValueOrDefault(source.Id);var sourcePercent=MagicPercent(source.Id,sourceEffort);if(sourcePercent<85)continue;
   var applications=ItemApplications(item);
   var candidates=meta.People.Where(p=>p.Id!=source.Id&&!OffToday(p.Id)&&missingByOwner.GetValueOrDefault(p.Id)==0).Select(p=>{
    var currentEffort=effortByOwner.GetValueOrDefault(p.Id);var capacity=PlannedCapacityHours(p.Id,CurrentSprint);var currentPercent=MagicPercent(p.Id,currentEffort);var projected=MagicPercent(p.Id,currentEffort+estimate.Value);
    var sameApplication=applications.Length==0?0:current.Count(w=>w.OwnerId==p.Id&&applications.Any(a=>w.Tags.Contains(a,StringComparer.OrdinalIgnoreCase)));
    return new MagicCandidate(p.Id,p.UniqueName,p.Name,currentEffort,capacity,currentPercent,projected,sameApplication,NextDaysOff(p.Id,CurrentSprint));
   }).Where(candidate=>candidate.CapacityHours>0&&candidate.CurrentPercent+10<=sourcePercent&&candidate.ProjectedPercent<sourcePercent)
     .OrderByDescending(candidate=>candidate.SameApplicationItems).ThenBy(candidate=>candidate.ProjectedPercent).ThenBy(candidate=>candidate.Name).Take(4).ToArray();
   if(candidates.Length==0)continue;
   contexts.Add(new(item,source.Id,source.Name,estimate.Value,sourcePercent,MagicPercent(source.Id,Math.Max(0,sourceEffort-estimate.Value)),candidates));
  }
  return contexts.OrderByDescending(x=>x.CurrentPercent).ThenByDescending(x=>x.Estimate).Take(24).ToList();
 }
 string MagicLeaveText(DateRange? leave)=>leave is null?"none":RangeText(leave);
 string BuildMagicPrompt(){
  var details=string.Join("\n\n",magicContexts.Select(context=>{
   var applications=ItemApplications(context.Item);
   var candidates=string.Join("\n",context.Candidates.Select(candidate=>$"- candidateId={candidate.PersonId}; name={candidate.Name}; load={candidate.CurrentPercent}% -> {candidate.ProjectedPercent}%; sameApplicationItems={candidate.SameApplicationItems}; nextOff={MagicLeaveText(candidate.NextLeave)}"));
   return $"WORK ITEM #{context.Item.Id}\nTitle: {context.Item.Title}\nCurrent owner: {context.CurrentOwnerName} ({context.CurrentOwnerId})\nCurrent owner load: {context.CurrentPercent}% -> {context.SourceProjectedPercent}% if moved\nEstimate: {context.Estimate:0.##}h\nApplications: {(applications.Length==0?"(none classified)":string.Join(", ",applications))}\nTags: {(context.Item.Tags.Length==0?"(none)":string.Join("; ",context.Item.Tags))}\nDescription: {ClipSmartFixText(context.Item.Description,1400)}\nAcceptance/testing: {ClipSmartFixText(context.Item.Acceptance,800)}\nAllowed candidates:\n{candidates}";
  }));
  return """
You are helping orchestrate a software delivery sprint. Recommend only useful work-item reassignments that improve workload balance while preserving context.

Important constraints:
- SprintPilot already filtered out Done, blocked, unestimated and unsafe candidate combinations.
- You may ONLY recommend a work item listed below and ONLY one of its exact allowed candidateId values.
- Do not infer performance, seniority, competence, personality, availability, or productivity beyond the supplied facts.
- Treat same-application work as continuity/context, not proof of skill.
- Prefer moves that reduce an overloaded or nearly-full current owner and keep the recipient at a reasonable projected load, ideally <=100%.
- Consider known upcoming time off when choosing between otherwise similar candidates.
- Do not recommend movement merely to make percentages look equal. If there is no meaningful operational benefit, omit the item.
- Give a concise factual reason grounded in capacity, application continuity, or time-off data.
- Confidence must be High, Medium, or Low.
- Return raw JSON only, without Markdown fences.

Required JSON shape:
{
  "suggestions": [
    {
      "id": 123,
      "suggestedOwnerId": "exact-candidate-id",
      "reason": "Why this reassignment helps orchestration",
      "confidence": "High"
    }
  ]
}

ORCHESTRATION CONTEXT
__CONTEXT__
""".Replace("__CONTEXT__",details);
 }
 async Task PrepareMagicOrchestration(){
  try{
   if(meta is null||CurrentSprint is null)throw new TrackerException("Load a current sprint first.");
   if(!PlanningPrefs.EstimatesAreHours)throw new TrackerException("Enable 'Treat configured estimates as hours' in Settings before running Magic Orchestration.");
   magicContexts=BuildMagicContexts();magicSuggestions=[];magicCopilotText="";
   if(magicContexts.Count==0){Notify("No safe reassignment candidates were found. Complete active estimates/capacity first, or the sprint may already be balanced.");return;}
   magicPrompt=BuildMagicPrompt();await Show("magic");
  }catch(Exception e){Error(e);}
 }
 async Task CopyMagicPrompt(){try{if(string.IsNullOrWhiteSpace(magicPrompt))throw new TrackerException("Prepare Magic Orchestration first.");await JS.InvokeVoidAsync("sprintPilot.copy",magicPrompt);Notify($"Copied orchestration prompt for {magicContexts.Count} candidate work item(s).");}catch(Exception e){Error(e);}}
 void ParseMagicCopilot(){
  try{
   var parsed=JsonSerializer.Deserialize<MagicImport>(StripCodeFence(magicCopilotText),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException();
   var duplicates=parsed.Suggestions.GroupBy(x=>x.Id).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();if(duplicates.Length>0)throw new TrackerException("Copilot returned duplicate work-item IDs: "+string.Join(", ",duplicates));
   var contexts=magicContexts.ToDictionary(x=>x.Item.Id);var suggestions=new List<MagicSuggestion>();
   foreach(var input in parsed.Suggestions){
    if(!contexts.TryGetValue(input.Id,out var context))throw new TrackerException($"Copilot returned #{input.Id}, which was not part of this orchestration analysis.");
    var ownerId=(input.SuggestedOwnerId??"").Trim();var target=context.Candidates.FirstOrDefault(c=>c.PersonId.Equals(ownerId,StringComparison.OrdinalIgnoreCase));
    if(target is null)throw new TrackerException($"#{input.Id}: suggested owner is not one of SprintPilot's allowed candidates.");
    var confidence=(input.Confidence??"").Trim();confidence=confidence.Equals("High",StringComparison.OrdinalIgnoreCase)?"High":confidence.Equals("Low",StringComparison.OrdinalIgnoreCase)?"Low":"Medium";
    suggestions.Add(new(){Id=input.Id,SuggestedOwnerId=target.PersonId,Reason=(input.Reason??"").Trim(),Confidence=confidence,Decision="pending"});
   }
   magicSuggestions=suggestions;dialogError=magicSuggestions.Count==0?"Copilot did not recommend any reassignment. That is a valid orchestration result.":"";
  }catch(Exception e){magicSuggestions=[];Error(e);}
 }
 MagicContext MagicContextFor(int id)=>magicContexts.First(x=>x.Item.Id==id);
 MagicCandidate MagicTarget(MagicSuggestion suggestion)=>MagicContextFor(suggestion.Id).Candidates.First(x=>x.PersonId.Equals(suggestion.SuggestedOwnerId,StringComparison.OrdinalIgnoreCase));
 void DecideMagic(MagicSuggestion suggestion,string decision)=>suggestion.Decision=decision;
 List<MagicProjectionRow> MagicProjection(){
  if(meta is null||CurrentSprint is null)return [];
  var current=items.Where(w=>!Quality.Finished(w,meta)&&Planning.IsPlanningType(w.Type,PlanningPrefs)&&w.Iteration.Equals(CurrentSprint.Path,StringComparison.OrdinalIgnoreCase)).ToArray();
  var effort=meta.People.ToDictionary(p=>p.Id,p=>current.Where(w=>w.OwnerId==p.Id).Sum(w=>Planning.Estimate(w,PlanningPrefs)??0),StringComparer.OrdinalIgnoreCase);
  var before=effort.ToDictionary(kv=>kv.Key,kv=>MagicPercent(kv.Key,kv.Value),StringComparer.OrdinalIgnoreCase);var changed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var suggestion in magicSuggestions.Where(s=>s.Decision=="accepted")){
   var context=MagicContextFor(suggestion.Id);effort[context.CurrentOwnerId]=Math.Max(0,effort.GetValueOrDefault(context.CurrentOwnerId)-context.Estimate);effort[suggestion.SuggestedOwnerId]=effort.GetValueOrDefault(suggestion.SuggestedOwnerId)+context.Estimate;changed.Add(context.CurrentOwnerId);changed.Add(suggestion.SuggestedOwnerId);
  }
  return meta.People.Where(p=>changed.Contains(p.Id)).Select(p=>new MagicProjectionRow(p.Id,p.Name,before.GetValueOrDefault(p.Id),MagicPercent(p.Id,effort.GetValueOrDefault(p.Id)))).OrderByDescending(x=>x.Before).ThenBy(x=>x.Name).ToList();
 }
 void ReviewMagicOrchestration(){
  try{
   if(meta is null)throw new TrackerException("Reload SprintPilot first.");
   if(magicSuggestions.Any(s=>s.Decision=="pending"))throw new TrackerException("Accept or ignore every orchestration recommendation before continuing.");
   var accepted=magicSuggestions.Where(s=>s.Decision=="accepted").ToArray();if(accepted.Length==0)throw new TrackerException("Accept at least one reassignment before continuing.");
   pending=[];
   foreach(var suggestion in accepted){
    var context=MagicContextFor(suggestion.Id);var target=MagicTarget(suggestion);var original=items.FirstOrDefault(w=>w.Id==suggestion.Id)??throw new TrackerException($"#{suggestion.Id} is no longer in the current sprint.");
    if(Quality.Finished(original,meta))throw new TrackerException($"#{suggestion.Id} is already completed and will not be reassigned.");
    if(!original.OwnerId.Equals(context.CurrentOwnerId,StringComparison.OrdinalIgnoreCase))throw new TrackerException($"#{suggestion.Id} changed owner since the orchestration analysis. Run Magic Orchestration again.");
    pending.Add(new(original,[new Change(ItemField.Owner,target.UniqueName)]));
   }
   results=[];dialog="review";dialogError="";
  }catch(Exception e){Error(e);}
 }
 void StartBulk(string kind){if(SelectedItems.Count==0){Notify("Select visible work items first.");return;}bulkKind=kind;bulkValue=kind switch{"Next"=>meta?.Iterations.ElementAtOrDefault(sprintIndex+1)?.Path??"","Previous"=>meta?.Iterations.ElementAtOrDefault(sprintIndex-1)?.Path??"",_=>""};_=Show("bulk");}
 void PreviewBulk(){try{if(SelectedItems.Count==0)throw new TrackerException("Select visible work items first.");if(bulkKind=="State"&&!CommonStates.Contains(bulkValue))throw new TrackerException("Choose a state supported by all selected work-item types.");if(SelectedItems.Any(w=>busy.Contains(w.Id)||drafts.ContainsKey(w.Id)))throw new TrackerException("Wait for saves and apply or discard local drafts before bulk editing.");
  if(bulkValue==""&&bulkKind!="Owner")throw new TrackerException("Choose a value first.");if(bulkKind=="Priority"&&(!int.TryParse(bulkValue,out var p)||p<1||p>4))throw new TrackerException("Priority must be 1–4.");
  pending=SelectedItems.Select(w=>{Change c;if(bulkKind is "AddTag" or "RemoveTag"){var tags=w.Tags.ToList();if(bulkKind=="AddTag")tags.Add(bulkValue.Trim());else tags.RemoveAll(t=>t.Equals(bulkValue.Trim(),StringComparison.OrdinalIgnoreCase));c=new(ItemField.Tags,string.Join("; ",tags.Distinct(StringComparer.OrdinalIgnoreCase)));}else{var f=bulkKind is "Next" or "Previous"?ItemField.Iteration:Enum.Parse<ItemField>(bulkKind);c=new(f,bulkKind=="Priority"?int.Parse(bulkValue):bulkValue);}return new ItemUpdate(w,[c]);}).ToList();results=[];dialog="review";dialogError="";
 }catch(Exception e){Error(e);}}
 async Task ApplyPending(){if(applying)return;applying=true;dialogError="";results=[];foreach(var p in pending)busy.Add(p.Original.Id);try{
  var progress=new Progress<UpdateResult>(r=>{if(disposed)return;_=InvokeAsync(()=>{results.RemoveAll(x=>x.Id==r.Id);results.Add(r);if(r.Item is not null){Replace(r.Item);drafts.Remove(r.Id);}StateHasChanged();});});
  results=(await Bulk.ApplyAsync(pending,progress,lifetime.Token)).ToList();foreach(var r in results.Where(r=>r.Success)){Replace(r.Item!);drafts.Remove(r.Id);}sprintCache.Clear();items.RemoveAll(w=>w.Iteration!=CurrentSprint?.Path);previousItems.RemoveAll(w=>w.Iteration!=meta?.Iterations.ElementAtOrDefault(sprintIndex-1)?.Path);Notify($"{results.Count(r=>r.Success)} updated; {results.Count(r=>!r.Success)} failed.");
 }catch(Exception e){Error(e);}finally{foreach(var p in pending)busy.Remove(p.Original.Id);applying=false;}}
 async Task RetryFailed(){applying=true;try{var failed=results.Where(r=>!r.Success).Select(r=>r.Id).ToHashSet();var next=new List<ItemUpdate>();foreach(var p in pending.Where(p=>failed.Contains(p.Original.Id))){var fresh=await Tracker.GetAsync(p.Original.Id,lifetime.Token);Replace(fresh);next.Add(new(fresh,p.Changes));}pending=next;results=[];dialogError="Review the refreshed current values before applying the retry.";}catch(Exception e){Error(e);}finally{applying=false;}}
 void PrepareNext(){if(loading||busy.Count>0){Notify("Wait for the current refresh or save to finish.");return;}if(meta?.Iterations.ElementAtOrDefault(sprintIndex+1)is null){Notify("Configure the next team sprint in Azure DevOps first.");return;}var visible=Visible.Where(w=>!Quality.Finished(w,meta)).ToArray();selected.Clear();foreach(var w in visible)selected.Add(w.Id);Notify($"{selected.Count} visible unfinished items selected. Adjust checkboxes, then use Sprint → to review the move.");}
 async Task OpenPrompt(IEnumerable<WorkItem> source){try{var rows=source.ToArray();if(rows.Length==0)throw new TrackerException("Select work items first.");promptItemCount=rows.Length;promptText=AiReview.Export(rows,prefs.AiPrompt);await Show("prompt");}catch(Exception e){Error(e);}}
 async Task CopyPrompt(){try{if(string.IsNullOrWhiteSpace(promptText))throw new TrackerException("The prompt is empty.");await JS.InvokeVoidAsync("sprintPilot.copy",promptText);Notify($"Copied Copilot prompt for {promptItemCount} item(s).");}catch(Exception e){Error(e);}}
 void PasteSelected()=>PasteItems(SelectedItems);
 void PasteItems(IEnumerable<WorkItem> source){aiItems=source.ToArray();if(aiItems.Length==0){Notify("Select work items first.");return;}aiText="";reviews=[];_=Show("ai");}
 void ParseAi(){try{reviews=AiReview.Parse(aiText,aiItems.Select(w=>w.Id).ToArray());dialogError="";}catch(Exception e){Error(e);}}
 IReadOnlyList<Change> ReviewChanges(ReviewSection review){var changes=AiReview.Changes(review).ToList();var original=aiItems.Single(w=>w.Id==review.Id);if(!meta!.Types.First(t=>t.Name==original.Type).Fields.Contains("Microsoft.VSTS.Common.AcceptanceCriteria")){var acceptance=changes.Single(c=>c.Field==ItemField.Acceptance);changes.Remove(acceptance);var desc=changes.Single(c=>c.Field==ItemField.Description);changes.Remove(desc);changes.Add(new(ItemField.Description,desc.Value+ContentText.HtmlEncode("\n\nACCEPTANCE CRITERIA:\n"+review.Sections["ACCEPTANCE CRITERIA"])));}return changes;}
 void ApplyAiLocally(){foreach(var r in reviews)drafts[r.Id]=new(aiItems.Single(w=>w.Id==r.Id),ReviewChanges(r));CloseDialog();Notify($"{reviews.Length} local draft(s) prepared. Open an item to review and save. Drafts are lost when the session ends.");}
 void ReviewAiServer(){pending=reviews.Select(r=>new ItemUpdate(aiItems.Single(w=>w.Id==r.Id),ReviewChanges(r))).ToList();results=[];dialog="review";dialogError="";}
 void ReviewDraft(int id){pending=[drafts[id]];results=[];_=Show("review");}
 void NewItem(){if(meta is null){Notify("Connect to Azure DevOps or open demo mode first.");return;}newType=meta.Types.FirstOrDefault(t=>t.Name is "Product Backlog Item" or "User Story")?.Name??meta.Types.First().Name;newTitle=newDescription=newAcceptance=newTags="";newArea=meta.Scope.FirstOrDefault()?.Path??meta.Areas.FirstOrDefault()??"";newIteration=CurrentSprint?.Path??"";newParent=null;_=Show("create");}
 void ApplyCreateTemplate(ChangeEventArgs e){if(int.TryParse(e.Value?.ToString(),out var index)&&prefs.Templates.ElementAtOrDefault(index)is {} t){newDescription=t.Description;newAcceptance=t.Acceptance;newTags=t.Tags;if(t.Area!="")newArea=t.Area;}}
 async Task CreateItem(){if(applying)return;applying=true;try{if(string.IsNullOrWhiteSpace(newTitle)||newTitle.Length>255)throw new TrackerException("Enter a title between 1 and 255 characters.");var changes=new List<Change>{new(ItemField.Title,newTitle.Trim()),new(ItemField.Description,ContentText.HtmlEncode(newDescription)),new(ItemField.Area,newArea),new(ItemField.Iteration,newIteration),new(ItemField.Tags,newTags)};
  if(newAcceptance!=""){if(meta!.Types.First(t=>t.Name==newType).Fields.Contains("Microsoft.VSTS.Common.AcceptanceCriteria"))changes.Add(new(ItemField.Acceptance,ContentText.HtmlEncode(newAcceptance)));else{changes.RemoveAll(c=>c.Field==ItemField.Description);changes.Add(new(ItemField.Description,ContentText.HtmlEncode(newDescription+"\n\nACCEPTANCE CRITERIA:\n"+newAcceptance)));}}var w=await Tracker.CreateAsync(newType,changes,newParent,lifetime.Token);if(w.Iteration==CurrentSprint?.Path)items.Add(w);sprintCache.Clear();detail=w;dialog="";Notify($"Created #{w.Id}.");
 }catch(Exception e){Error(e);}finally{applying=false;}}
 async Task ToggleColumn(string c){prefs.Columns=prefs.Columns.Contains(c)?prefs.Columns.Where(x=>x!=c).ToArray():AllColumns.Where(x=>prefs.Columns.Contains(x)||x==c).ToArray();await SaveSettings();}
 Dictionary<string,string> Filters()=>new(){["search"]=search,["owners"]=string.Join('|',ownerFilters),["type"]=typeFilter,["application"]=applicationFilter,["tag"]=tagFilter,["activeOnly"]=workspaceActiveOnly.ToString(),["blockedOnly"]=workspaceBlockedOnly.ToString(),["area"]=areaFilter,["priority"]=priorityFilter,["quick"]=quickView,["sort"]=sort,["descending"]=descending.ToString()};
 async Task SaveView(){if(string.IsNullOrWhiteSpace(viewName)){dialogError="Enter a view name.";return;}prefs.Views.RemoveAll(v=>v.Name.Equals(viewName.Trim(),StringComparison.OrdinalIgnoreCase));prefs.Views.Add(new(viewName.Trim(),Filters(),prefs.Columns.ToArray()));await SaveSettings();CloseDialog();viewName="";}
 async Task LoadView(SavedView v){var f=v.Filters;search=f.GetValueOrDefault("search","");ownerFilters.Clear();foreach(var id in f.GetValueOrDefault("owners",f.GetValueOrDefault("owner","")).Split('|',StringSplitOptions.RemoveEmptyEntries))ownerFilters.Add(id);stateFilter="";typeFilter=f.GetValueOrDefault("type","");applicationFilter=f.GetValueOrDefault("application","");tagFilter=f.GetValueOrDefault("tag","");workspaceActiveOnly=f.GetValueOrDefault("activeOnly")=="True";workspaceBlockedOnly=f.GetValueOrDefault("blockedOnly")=="True";areaFilter=f.GetValueOrDefault("area","");priorityFilter=f.GetValueOrDefault("priority","");sort=f.GetValueOrDefault("sort","Order");descending=f.GetValueOrDefault("descending")=="True";prefs.Columns=v.Columns;screen="workspace";await SetQuickView(f.GetValueOrDefault("quick","Team"));}
 async Task DeleteView(SavedView v){prefs.Views.Remove(v);await SaveSettings();}
 async Task SaveSettings(){try{prefs.BlockedTags=blockedTagsText.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();await Preferences.SaveAsync(prefs,lifetime.Token);await ApplyTheme();Notify("Settings saved.");}catch(Exception e){Error(e);}}
 void LoadTemplate(){if(prefs.Templates.ElementAtOrDefault(templateIndex)is not {} t)return;templateName=t.Name;templateDescription=t.Description;templateAcceptance=t.Acceptance;templateTags=t.Tags;templateArea=t.Area;}
 void NewTemplate(){prefs.Templates.Add(new("New template","","","",""));templateIndex=prefs.Templates.Count-1;LoadTemplate();}
 async Task DeleteTemplate(){if(templateIndex>=0&&templateIndex<prefs.Templates.Count)prefs.Templates.RemoveAt(templateIndex);templateIndex=0;LoadTemplate();await SaveSettings();}
 async Task SaveTemplate(){if(templateName.Trim()==""){Notify("Enter a template name.");return;}var t=new WorkTemplate(templateName.Trim(),templateDescription,templateAcceptance,templateTags,templateArea);if(templateIndex<prefs.Templates.Count)prefs.Templates[templateIndex]=t;else prefs.Templates.Add(t);await SaveSettings();}
 static bool SafeLink(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&(uri.Host=="dev.azure.com"||uri.Host.EndsWith(".visualstudio.com",StringComparison.OrdinalIgnoreCase));
 async Task RunCommand(string command){CloseDialog();switch(command){case "Next sprint":await Navigate(1);break;case "Previous sprint":await Navigate(-1);break;case "Show my work":await SetQuickView("My Work");break;case "Show unassigned":await SetQuickView("Unassigned");break;case "Select all visible":foreach(var w in Visible)selected.Add(w.Id);break;case "Clear selection":selected.Clear();break;case "Move selected to next sprint":StartBulk("Next");break;case "Move selected to previous sprint":StartBulk("Previous");break;case "Assign selected":StartBulk("Owner");break;case "Add tag":StartBulk("AddTag");break;case "Remove tag":StartBulk("RemoveTag");break;case "Change state":StartBulk("State");break;case "New PBI":NewItem();break;case "Refresh":await Refresh();break;}}
 [JSInvokable]public async Task Shortcut(string command){if(applying)return;if(command=="escape"){if(dialog!="")CloseDialog();else detail=null;}else if(dialog==""){switch(command){case "palette":await Show("palette");break;case "search":await JS.InvokeVoidAsync("sprintPilot.focusSearch");break;case "next":await Navigate(1);break;case "previous":await Navigate(-1);break;case "refresh":if(meta is not null)await Refresh();break;case "select":foreach(var w in Visible)selected.Add(w.Id);break;}}await InvokeAsync(StateHasChanged);}
 public async ValueTask DisposeAsync(){disposed=true;lifetime.Cancel();refreshToken.Cancel();try{await JS.InvokeVoidAsync("sprintPilot.dispose");}catch(JSDisconnectedException){}reference?.Dispose();refreshToken.Dispose();lifetime.Dispose();}
}
