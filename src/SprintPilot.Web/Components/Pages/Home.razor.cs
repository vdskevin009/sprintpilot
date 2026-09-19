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
 PlanningPage? planner;
 Preferences prefs=new();Metadata? meta;ConnectionInfo? connection;Person? testUser;
 string organization="",project="",token="",screen="home",message="",dialog="",dialogError="";
 bool focusDialog;bool initializing=true,connecting,hasError,loading,applying,moreFilters,descending,disposed,showAllDaysOff,dailyLookupBusy,dailyPanelLoading,dailyActiveOnly,orderReview,smartOrdering,meetingCreating;
 string attentionFilter="",classificationTag="",dailyLookupText="",dailyTagText="",dailyCommentText="",dailyFocusOwner="",filterOptionSearch="",holidayCountryFilter="ALL";
 string meetingTitle="",meetingNotes="",meetingCopilotText="",meetingError="",meetingWorkType="";
 int sprintIndex,selectionAnchor=-1,templateIndex;
 string search="",ownerSearch="",stateFilter="",typeFilter="",tagFilter="",areaFilter="",priorityFilter="",quickView="Team",cleanupFilter="",sort="Order",workspaceMode="List";
 string commandSearch="",iterationSearch="",viewName="",bulkKind="",bulkValue="",aiText="",promptText="";
 int promptItemCount;
 string templateName="",templateDescription="",templateAcceptance="",templateTags="",templateArea="",blockedTagsText="",holidayCalendarError="";
 string newType="",newTitle="",newDescription="",newAcceptance="",newArea="",newTags="",newIteration="";int? newParent;
 readonly string[] AllColumns=["Order","ID","Type","Title","Owner","State","Iteration","Area","Estimate","Priority","Parent","Tags","Changed"];
 readonly string[] QuickViews=["My Work","Team","Unassigned","Carry-over","Bugs","Recently Changed"];
 readonly string[] Commands=["Next sprint","Previous sprint","Show my work","Show unassigned","Select all visible","Clear selection","Move selected to next sprint","Move selected to previous sprint","Assign selected","Add tag","Remove tag","Change state","New PBI","Refresh"];
 List<WorkItem> items=[],previousItems=[],related=[],tagHistory=[],planningItems=[],dailyLookupResults=[];SprintCapacity sprintCapacity=new([],[]);readonly Dictionary<string,List<WorkItem>> sprintCache=new();readonly Dictionary<string,SprintCapacity> capacityByIteration=new(StringComparer.OrdinalIgnoreCase);
 WorkItem? dailyPanelItem;List<WorkItemComment> dailyComments=[];List<CalendarHoliday> calendarHolidays=[];List<SmartOrderRow> smartOrderPlan=[];MeetingImport meetingImport=new();List<MeetingActionDraft> meetingActions=[];
 readonly HashSet<string> ownerFilters=new(StringComparer.OrdinalIgnoreCase);int? draggedId;
 readonly HashSet<string> cleanupTagFilters=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<int> selected=[],busy=[];readonly Dictionary<int,ItemUpdate> drafts=new();
 List<ItemUpdate> pending=[];List<UpdateResult> results=[];WorkItem? detail;WorkItem[] aiItems=[];ReviewSection[] reviews=[];
 CancellationTokenSource refreshToken=new();readonly CancellationTokenSource lifetime=new();DotNetObjectReference<Home>? reference;
 string PlanningProfileKey => Tracker.Demo ? "demo" : $"{connection?.Organization}|{connection?.Project}|{connection?.Team}";
 Iteration? CurrentSprint=>meta?.Iterations.ElementAtOrDefault(sprintIndex);
 Iteration? NextSprint=>meta?.Iterations.ElementAtOrDefault(sprintIndex+1);
 string Adjacent(int delta)=>meta?.Iterations.ElementAtOrDefault(sprintIndex+delta)?.Name??"No sprint";
 string DateRange=>CurrentSprint?.Start is {} start?$"{start:MMM d} – {CurrentSprint.Finish:MMM d, yyyy}":"Dates not configured";
 IEnumerable<WorkItem> AllLoaded=>items.Concat(previousItems).Concat(related).DistinctBy(w=>w.Id);
 string[] TagSuggestions=>AllLoaded.Concat(tagHistory).SelectMany(w=>w.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
 List<WorkItem> SelectedItems=>AllLoaded.Where(w=>selected.Contains(w.Id)).ToList();
 string[] WorkspaceColumns=>orderReview?["Order",..prefs.Columns.Where(c=>c!="Order")]:prefs.Columns;
 IEnumerable<string> CommonStates {get {var sets=SelectedItems.Select(w=>meta!.Types.First(t=>t.Name==w.Type).States.Select(s=>s.Name).ToHashSet()).ToArray();if(sets.Length==0)return [];var common=sets[0];foreach(var set in sets.Skip(1))common.IntersectWith(set);return common.Order();}}
 string[] Issues(WorkItem w)=>Quality.Issues(w,meta!,prefs,AllLoaded,meta!.Iterations.FirstOrDefault(i=>i.Path==w.Iteration));
 Dictionary<string,int> CleanupCounts=>items.SelectMany(w=>Issues(w)).GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count()).Concat(new[]{new KeyValuePair<string,int>("Previous-sprint unfinished",previousItems.Count(w=>!Quality.Finished(w,meta!)))}).ToDictionary(x=>x.Key,x=>x.Value);
 IEnumerable<WorkItem> CleanupItems=>meta is null?[]:items.Where(w=>!Quality.Finished(w,meta));
 IEnumerable<(string Id,string Name,int Count)> CleanupPeople=>CleanupItems.GroupBy(w=>w.OwnerId).Select(g=>(Id:g.Key,Name:PersonName(g.Key),Count:g.Count())).OrderBy(x=>x.Name=="Unassigned").ThenBy(x=>x.Name);
 IEnumerable<(string Tag,int Count)> CleanupTags=>CleanupItems.SelectMany(w=>w.Tags).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).Select(g=>(Tag:g.Key,Count:g.Count())).OrderBy(x=>x.Tag);
 List<WorkItem> Visible {get{
  IEnumerable<WorkItem> rows=(quickView=="Carry-over"||cleanupFilter=="Previous-sprint unfinished")?previousItems.Where(w=>!Quality.Finished(w,meta!)):items;
  if(meta is null)return [];
  rows=rows.Where(w=>(search==""||w.Id.ToString().Contains(search)||w.Title.Contains(search,StringComparison.OrdinalIgnoreCase)||w.Tags.Any(t=>t.Contains(search,StringComparison.OrdinalIgnoreCase)))&&(ownerFilters.Count==0||ownerFilters.Contains(w.OwnerId))&&(stateFilter==""||w.State.Contains(stateFilter,StringComparison.OrdinalIgnoreCase))&&(typeFilter==""||w.Type.Contains(typeFilter,StringComparison.OrdinalIgnoreCase))&&(tagFilter==""||w.Tags.Any(t=>t.Contains(tagFilter,StringComparison.OrdinalIgnoreCase)))&&(cleanupTagFilters.Count==0||w.Tags.Any(t=>cleanupTagFilters.Contains(t)))&&(areaFilter==""||w.Area.Contains(areaFilter,StringComparison.OrdinalIgnoreCase))&&(priorityFilter==""||w.Priority?.ToString().Contains(priorityFilter)==true));
  rows=quickView switch{"My Work"=>rows.Where(w=>w.OwnerId==meta.Me.Id),"Unassigned"=>rows.Where(w=>w.OwnerId==""),"Bugs"=>rows.Where(w=>w.Type.Equals("Bug",StringComparison.OrdinalIgnoreCase)),"Recently Changed"=>rows.Where(w=>w.Changed>DateTimeOffset.UtcNow.AddDays(-3)),_=>rows};
  rows=attentionFilter switch{"blocked"=>rows.Where(Blocked),"unassigned"=>rows.Where(w=>w.OwnerId==""),"missing-app"=>rows.Where(w=>!HasApplicationTag(w)),"missing-initiative"=>rows.Where(w=>!HasInitiativeTag(w)),"stale"=>rows.Where(w=>w.Changed!=default&&w.Changed<DateTimeOffset.UtcNow.AddDays(-prefs.StaleDays)),_=>rows};
  if(screen=="cleanup"&&cleanupFilter!=""&&cleanupFilter!="Previous-sprint unfinished")rows=rows.Where(w=>Issues(w).Contains(cleanupFilter));
  Func<WorkItem,IComparable?> key=sort switch{"Order"=>w=>w.Order,"ID"=>w=>w.Id,"Type"=>w=>w.Type,"Owner"=>w=>w.Owner,"State"=>w=>w.State,"Iteration"=>w=>w.Iteration,"Area"=>w=>w.Area,"Estimate"=>w=>w.Estimate,"Priority"=>w=>w.Priority,"Parent"=>w=>w.Parent,"Tags"=>w=>string.Join(";",w.Tags),"Changed"=>w=>w.Changed,_=>w=>w.Title};return (descending?rows.OrderByDescending(key):rows.OrderBy(key)).ThenBy(w=>w.Id).ToList();
 }}
 string DialogTitle=>dialog switch{"palette"=>"Commands","iterations"=>"Choose sprint","columns"=>"Visible columns","saveview"=>"Save view","bulk"=>"Edit selected items","review"=>"Review changes","ai"=>"AI review","prompt"=>"Copilot prompt","create"=>"New work item","smartorder"=>"Smart order preview",_=>"SprintPilot"};
 string BulkLabel=>bulkKind switch{"AddTag"=>"Tag to add","RemoveTag"=>"Tag to remove","Next" or "Previous" or "Iteration"=>"Target sprint",_=>bulkKind};
 protected override async Task OnInitializedAsync(){try{prefs=await Preferences.LoadAsync(lifetime.Token);prefs.QualityWeights.Remove("Parent");blockedTagsText=string.Join("\n",prefs.BlockedTags);LoadTemplate();var c=await Credentials.GetAsync(lifetime.Token);if(c is not null){connection=c.Connection;organization=connection.Organization;project=connection.Project;await LoadWorkspace();}}catch(Exception e){Error(e);}finally{initializing=false;}}
 protected override async Task OnAfterRenderAsync(bool first){if(first){reference=DotNetObjectReference.Create(this);await JS.InvokeVoidAsync("sprintPilot.init",reference);await ApplyTheme();}if(focusDialog){focusDialog=false;await JS.InvokeVoidAsync("sprintPilot.dialog");}}
 async Task ApplyTheme()=>await JS.InvokeVoidAsync("sprintPilot.theme",prefs.Theme);
 void Notify(string text){message=text;hasError=false;}
 void Error(Exception e){var text=e is TrackerException?e.Message:e is OperationCanceledException?"Operation cancelled or timed out. Refresh to confirm server state.":"The operation could not be completed. Check the connection and try again.";if(dialog!="")dialogError=text;else{message=text;hasError=true;}}
 void ResetTest()=>testUser=null;
 void TokenInput(ChangeEventArgs e){token=e.Value?.ToString()??"";ResetTest();}
 async Task TestConnection(){connecting=true;try{Tracker.Demo=false;testUser=await Tracker.TestAsync(new(new(organization.Trim(),project.Trim()),token.Trim()),lifetime.Token);Notify("Connected to Azure DevOps as "+testUser.Name);}catch(Exception e){Error(e);}finally{connecting=false;}}
 async Task SaveConnection(){if(connecting)return;connecting=true;try{Tracker.Demo=false;var c=new Credentials(new(organization.Trim(),project.Trim()),token.Trim());testUser=await Tracker.TestAsync(c,lifetime.Token);await Credentials.SaveAsync(c,lifetime.Token);connection=c.Connection;token="";await JS.InvokeVoidAsync("sprintPilot.clearToken");await LoadWorkspace();Notify("Connected as "+testUser.Name);}catch(Exception e){Error(e);}finally{connecting=false;}}
 async Task ExploreDemo(){Tracker.Demo=true;connection=null;await LoadWorkspace();}
 void ExitDemo(){refreshToken.Cancel();Tracker.Demo=false;meta=null;items=[];planningItems=[];selected.Clear();drafts.Clear();sprintCache.Clear();detail=null;dailyPanelItem=null;screen="home";}
 void ChangeConnection(){ExitDemo();connection=null;testUser=null;}
 async Task ChangeTeam(ChangeEventArgs e){try{var c=await Credentials.GetAsync(lifetime.Token);if(c is null)return;connection=c.Connection with{Team=e.Value?.ToString()??""};await Credentials.SaveAsync(new(connection,c.Token),lifetime.Token);await LoadWorkspace();}catch(Exception ex){Error(ex);}}
 async Task LoadWorkspace(){loading=true;try{meta=await Tracker.MetadataAsync(true,lifetime.Token);if(connection is not null&&connection.Team=="")connection=connection with{Team=meta.Teams[0].Id};var now=DateTimeOffset.UtcNow;sprintIndex=Array.FindIndex(meta.Iterations,i=>i.Start<=now&&i.Finish?.AddDays(1)>now);if(sprintIndex<0)sprintIndex=0;screen="home";sprintCache.Clear();capacityByIteration.Clear();selected.Clear();drafts.Clear();previousItems=[];related=[];detail=null;dailyPanelItem=null;await LoadSprint();await LoadFutureCapacities(lifetime.Token);await LoadPlanningItems(lifetime.Token);await LoadCalendarHolidays(lifetime.Token);}catch(Exception e){Error(e);}finally{loading=false;}}
 async Task LoadPlanningItems(CancellationToken ct){try{var rows=await Tracker.PlanningAsync(meta!.Types.Where(t=>t.Name!="Task").Select(t=>t.Name).ToArray(),ct);planningItems=rows.ToList();tagHistory=planningItems.Where(w=>w.Changed>=DateTimeOffset.UtcNow.AddMonths(-6)).ToList();}catch(OperationCanceledException){throw;}catch{planningItems=[];tagHistory=[];}}
 async Task LoadSprint(bool refreshMetadata=false){refreshToken.Cancel();refreshToken.Dispose();refreshToken=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var ct=refreshToken.Token;var iteration=CurrentSprint;if(iteration is null){items=[];Notify("This team has no configured iterations. Configure them in Azure DevOps, then refresh.");return;}
  loading=true;if(sprintCache.TryGetValue(iteration.Path,out var hit))items=[..hit];else items=[];previousItems=[];related=[];await InvokeAsync(StateHasChanged);
  try{if(refreshMetadata)meta=await Tracker.MetadataAsync(true,ct);var fetched=await Tracker.SprintAsync(iteration.Path,ct);try{sprintCapacity=await Tracker.CapacityAsync(iteration.Id,ct);capacityByIteration[iteration.Id]=sprintCapacity;}catch(TrackerException){sprintCapacity=new([],[]);}ct.ThrowIfCancellationRequested();items=fetched.ToList();sprintCache[iteration.Path]=[..items];if(quickView=="Carry-over"||screen=="cleanup")await LoadPrevious(ct);if(screen=="cleanup")await LoadRelated(ct);}catch(OperationCanceledException){if(!ct.IsCancellationRequested)Notify("Refresh timed out. Try again.");}catch(Exception e){if(!ct.IsCancellationRequested)Error(e);}finally{if(!ct.IsCancellationRequested)loading=false;}}
 async Task LoadFutureCapacities(CancellationToken ct){if(meta is null)return;var today=DateTimeOffset.Now.Date;var horizon=today.AddMonths(6);var future=meta.Iterations.Where(i=>i.Start is not null&&i.Finish is not null&&i.Finish.Value.Date>=today&&i.Start.Value.Date<=horizon).OrderBy(i=>i.Start).Take(16).ToArray();foreach(var iteration in future){ct.ThrowIfCancellationRequested();if(capacityByIteration.ContainsKey(iteration.Id))continue;try{capacityByIteration[iteration.Id]=await Tracker.CapacityAsync(iteration.Id,ct);}catch(TrackerException){}}}
 async Task LoadPrevious(CancellationToken ct){var previous=meta?.Iterations.ElementAtOrDefault(sprintIndex-1);if(previous is null){previousItems=[];return;}previousItems=(await Tracker.SprintAsync(previous.Path,ct)).ToList();}
 async Task LoadRelated(CancellationToken ct){var ids=items.Where(w=>Quality.Finished(w,meta!)).SelectMany(w=>w.Children).Except(AllLoaded.Select(w=>w.Id)).ToArray();var list=new List<WorkItem>();foreach(var id in ids)list.Add(await Tracker.GetAsync(id,ct));related=list;}
 async Task Navigate(int delta){if(busy.Count>0||applying)return;var next=sprintIndex+delta;if(meta is null||next<0||next>=meta.Iterations.Length)return;sprintIndex=next;selected.Clear();selectionAnchor=-1;detail=null;cleanupFilter="";await LoadSprint();}
 async Task ChooseSprint(Iteration iteration){if(busy.Count>0||applying)return;sprintIndex=Array.IndexOf(meta!.Iterations,iteration);selected.Clear();detail=null;CloseDialog();await LoadSprint();}
 async Task Refresh(){if(screen=="planning"&&planner is not null){await planner.RefreshPlanning();return;}if(busy.Count>0||applying)return;sprintCache.Clear();capacityByIteration.Clear();await LoadSprint(true);await LoadFutureCapacities(lifetime.Token);await LoadPlanningItems(lifetime.Token);await LoadCalendarHolidays(lifetime.Token);if(detail is not null)await OpenById(detail.Id);}
 async Task SetQuickView(string view){quickView=view;cleanupFilter="";if(view=="Carry-over"){loading=true;try{await LoadPrevious(refreshToken.Token);}catch(Exception e){Error(e);}finally{loading=false;}}}
 void ClearFilters(){search=ownerSearch=stateFilter=typeFilter=tagFilter=areaFilter=priorityFilter=cleanupFilter=attentionFilter=filterOptionSearch="";ownerFilters.Clear();cleanupTagFilters.Clear();quickView="Team";}
 void ToggleOwner(string id){if(!ownerFilters.Add(id))ownerFilters.Remove(id);}
 void ToggleCleanupTag(string tag){if(!cleanupTagFilters.Add(tag))cleanupTagFilters.Remove(tag);}
 void SelectVisibleAndMoveNext(){selected.Clear();foreach(var w in Visible)selected.Add(w.Id);StartBulk("Next");}
 sealed record PersonLane(string Id,string Name,IReadOnlyList<WorkItem> Items);
 DateRange[] DaysOff(string personId){IEnumerable<SprintCapacity> capacities=capacityByIteration.Count>0?capacityByIteration.Values:new[]{sprintCapacity};return capacities.SelectMany(c=>c.TeamDaysOff.Concat(c.Members.FirstOrDefault(m=>m.PersonId==personId)?.DaysOff??[])).GroupBy(r=>(r.Start.Date,r.End.Date)).Select(g=>g.First()).OrderBy(r=>r.Start).ToArray();}
 string RangeText(DateRange r)=>r.Start.Date==r.End.Date?$"{r.Start:MMM d}":$"{r.Start:MMM d}–{r.End:MMM d}";
 string DaysOffText(string personId){var ranges=DaysOff(personId);if(ranges.Length==0)return "";return string.Join(", ",ranges.Select(RangeText));}
 bool OffToday(string personId){var today=DateTimeOffset.Now.Date;return DaysOff(personId).Any(r=>today>=r.Start.Date&&today<=r.End.Date);}
 bool InSelectedSprint(DateRange r)=>CurrentSprint?.Start is {} start&&CurrentSprint.Finish is {} finish&&r.End.Date>=start.Date&&r.Start.Date<=finish.Date;
 DateRange? NextDaysOff(string personId,Iteration? iteration=null){var today=DateTimeOffset.Now.Date;IEnumerable<DateRange> rows=DaysOff(personId).Where(r=>r.End.Date>=today);if(iteration?.Start is {} start&&iteration.Finish is {} finish)rows=rows.Where(r=>r.End.Date>=start.Date&&r.Start.Date<=finish.Date);return rows.OrderBy(r=>r.Start).FirstOrDefault();}
 IReadOnlyList<(string PersonId,string Name,DateRange Range)> UpcomingDaysOff(){if(meta is null)return [];var today=DateTimeOffset.Now.Date;var rows=new List<(string,string,DateRange)>();foreach(var p in meta.People){foreach(var r in DaysOff(p.Id).Where(r=>r.End.Date>=today))rows.Add((p.Id,p.Name,r));}return rows.Distinct().OrderBy(x=>x.Item3.Start).ToList();}
 IReadOnlyList<(string PersonId,string Name,DateRange Range)> HomeDaysOff(){var rows=UpcomingDaysOff().GroupBy(x=>x.PersonId).Select(g=>g.OrderBy(x=>x.Range.Start).First());return rows.OrderBy(x=>x.PersonId==meta?.Me.Id?0:1).ThenBy(x=>x.Range.Start).ToList();}
 IReadOnlyList<(string PersonId,string Name,DateRange Range)> VisibleDaysOff(){var rows=UpcomingDaysOff();return showAllDaysOff?rows:rows.Take(1).ToList();}
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
 HashSet<DateOnly> UnavailableWorkingDates(string personId,Iteration iteration){
  var dates=new HashSet<DateOnly>();
  if(iteration.Start is not {} start||iteration.Finish is not {} finish)return dates;
  var first=DateOnly.FromDateTime(start.Date),last=DateOnly.FromDateTime(finish.Date);
  foreach(var range in DaysOff(personId)){
   var from=DateOnly.FromDateTime(range.Start.Date),to=DateOnly.FromDateTime(range.End.Date);
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
  var first=DateOnly.FromDateTime(start.Date),last=DateOnly.FromDateTime(finish.Date);
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
 IReadOnlyList<AttentionRow> HomeAttention(){var rows=CurrentOpenItems.ToArray();var list=new List<AttentionRow>();void Add(string key,string label,int count,string hint){if(count>0)list.Add(new(key,label,count,hint));}Add("blocked","Blocked work",rows.Count(Blocked),"Needs an unblock or dependency decision");Add("unassigned","Unassigned",rows.Count(w=>w.OwnerId==""),"Give the work a clear owner");if(ApplicationTags().Length>0)Add("missing-app","Missing application tag",rows.Count(w=>!HasApplicationTag(w)),"Classify work so application load stays useful");if(InitiativeTags().Length>0)Add("missing-initiative","Missing initiative tag",rows.Count(w=>!HasInitiativeTag(w)),"Keep initiative scope visible");Add("stale","Stagnating work",rows.Count(w=>w.Changed!=default&&w.Changed<DateTimeOffset.UtcNow.AddDays(-prefs.StaleDays)),$"No change for more than {prefs.StaleDays} days");return list;}
 List<HomeGroup> Remaining(TagDimension dimension){var configured=PlanningPrefs.Tags.Where(t=>dimension==TagDimension.Application?t.Application:t.Initiative).Select(t=>t.Tag).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();return configured.Select(tag=>{var rows=OpenPlanningItems.Where(w=>w.Tags.Contains(tag,StringComparer.OrdinalIgnoreCase)).ToArray();return new HomeGroup(tag,rows.Length,rows.Sum(w=>Planning.Estimate(w,PlanningPrefs)??0));}).Where(x=>x.Count>0).OrderByDescending(x=>x.Count).ThenBy(x=>x.Name).ToList();}
 List<CapacityRow> CapacityRowsFor(Iteration? iteration){if(meta is null||iteration is null)return [];var open=OpenPlanningItems.Where(w=>w.Iteration.Equals(iteration.Path,StringComparison.OrdinalIgnoreCase)).ToArray();return meta.People.Select(p=>{var rows=open.Where(w=>w.OwnerId==p.Id).ToArray();var estimates=rows.Select(w=>Planning.Estimate(w,PlanningPrefs)).ToArray();var effort=estimates.Sum(x=>x??0);var missing=estimates.Count(x=>x is null);var capacity=PlannedCapacityHours(p.Id,iteration);var percent=!PlanningPrefs.EstimatesAreHours?0:capacity<=0?(effort>0?101:0):(int)Math.Round(100*effort/capacity);var status=!PlanningPrefs.EstimatesAreHours?"Enable hour estimates":missing>0?"Estimates missing":effort>capacity?"Over capacity":capacity<=0?"Sprint time elapsed":effort>=capacity*.85?"Nearly full":"Room available";return new CapacityRow(p.Id,p.Name,rows.Length,effort,missing,capacity,percent,status,NextDaysOff(p.Id,iteration));}).OrderByDescending(x=>x.Percent).ThenBy(x=>x.Name).ToList();}
 InitiativeMetadata Initiative(string tag){var key=prefs.InitiativeMetadata.Keys.FirstOrDefault(k=>k.Equals(tag,StringComparison.OrdinalIgnoreCase));if(key is not null)return prefs.InitiativeMetadata[key];var value=new InitiativeMetadata();prefs.InitiativeMetadata[tag]=value;return value;}
 List<InitiativeRow> InitiativeRows(){var today=DateOnly.FromDateTime(DateTime.Now);return Remaining(TagDimension.Initiative).Select(g=>{var m=Initiative(g.Name);var attention=m.Status is "At Risk" or "Blocked"||m.Confidence=="Low"||(m.DueDate is {} due&&due<=today.AddDays(14));return new InitiativeRow(g.Name,g.Count,g.Effort,m,attention);}).OrderByDescending(x=>x.NeedsAttention).ThenBy(x=>x.Meta.DueDate??DateOnly.MaxValue).ThenBy(x=>x.Tag).ToList();}
 IEnumerable<(string Id,string Name,int Count)> FilterPeople=>items.GroupBy(w=>w.OwnerId).Select(g=>(Id:g.Key,Name:PersonName(g.Key),Count:g.Count())).Where(x=>filterOptionSearch==""||x.Name.Contains(filterOptionSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Name=="Unassigned").ThenBy(x=>x.Name);
 IEnumerable<(string Name,int Count)> FilterStates=>items.GroupBy(w=>w.State,StringComparer.OrdinalIgnoreCase).Select(g=>(Name:g.Key,Count:g.Count())).Where(x=>filterOptionSearch==""||x.Name.Contains(filterOptionSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Name);
 IEnumerable<(string Tag,int Count)> FilterTags=>items.SelectMany(w=>w.Tags).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).Select(g=>(Tag:g.Key,Count:g.Count())).Where(x=>filterOptionSearch==""||x.Tag.Contains(filterOptionSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Tag);
 string[] KnownPlanningTags=>planningItems.Concat(tagHistory).SelectMany(w=>w.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
 string DisplayTitle(WorkItem w){var title=w.Title.Trim();foreach(var tag in w.Tags.OrderByDescending(t=>t.Length)){if(!title.StartsWith(tag,StringComparison.OrdinalIgnoreCase)||title.Length==tag.Length)continue;var tail=title[tag.Length..];if(tail.Length>0&&(char.IsWhiteSpace(tail[0])||"-–—:|/".Contains(tail[0]))){tail=tail.TrimStart(' ','-','–','—',':','|','/');if(tail.Length>0)return tail;}}return title;}
 void ToggleStatePill(string value)=>stateFilter=stateFilter.Equals(value,StringComparison.OrdinalIgnoreCase)?"":value;
 void ToggleTagPill(string value)=>tagFilter=tagFilter.Equals(value,StringComparison.OrdinalIgnoreCase)?"":value;
 void OpenAttention(string key){ClearFilters();attentionFilter=key;screen="workspace";workspaceMode="List";detail=null;dailyPanelItem=null;}
 void OpenPerson(string id){ClearFilters();ownerFilters.Add(id);screen="daily";workspaceMode="People";detail=null;dailyPanelItem=null;}
 async Task OpenSprintPerson(Iteration iteration,string id){if(meta is null)return;var index=Array.IndexOf(meta.Iterations,iteration);if(index<0)return;sprintIndex=index;ClearFilters();ownerFilters.Add(id);screen="workspace";workspaceMode="List";sort="Order";descending=false;detail=null;dailyPanelItem=null;await LoadSprint();}
 void OpenGroup(string tag){ClearFilters();tagFilter=tag;screen="workspace";workspaceMode="List";detail=null;dailyPanelItem=null;}
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
 void FocusDaily(string id)=>dailyFocusOwner=dailyFocusOwner==id?"":id;
 void ClearDailyFocus()=>dailyFocusOwner="";
 void MoveDailyFocus(int delta){var lanes=PeopleLanes();if(lanes.Count==0){dailyFocusOwner="";return;}var index=lanes.ToList().FindIndex(l=>l.Id==dailyFocusOwner);if(index<0)index=0;else index=Math.Clamp(index+delta,0,lanes.Count-1);dailyFocusOwner=lanes[index].Id;}
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
 bool CanOrder(WorkItem w)=>meta?.Types.FirstOrDefault(t=>t.Name==w.Type)?.OrderField is not null;
 async Task<bool> PersistAzureOrder(IReadOnlyList<WorkItem> ordered){var expected=ordered.Select(w=>w.Id).ToArray();var touched=ordered.Where((w,i)=>w.Order!=(i+1)*1000d).Select(w=>w.Id).ToArray();foreach(var id in touched)busy.Add(id);try{for(var i=0;i<ordered.Count;i++){var desired=(i+1)*1000d;if(ordered[i].Order==desired)continue;var fresh=await Tracker.GetAsync(ordered[i].Id,lifetime.Token);var saved=await Tracker.UpdateAsync(new(fresh,[new Change(ItemField.Order,desired)]),lifetime.Token);Replace(saved);}sprintCache.Clear();await LoadSprint();sort="Order";descending=false;var actual=items.Where(CanOrder).OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).Select(w=>w.Id).ToArray();return actual.SequenceEqual(expected);}finally{foreach(var id in touched)busy.Remove(id);}}
 void DragStart(int id)=>draggedId=id;
 async Task DropOn(WorkItem target){if(draggedId is not {} sourceId||sourceId==target.Id)return;var ordered=items.Where(CanOrder).OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToList();var source=ordered.FirstOrDefault(w=>w.Id==sourceId);if(source is null||!CanOrder(target)){draggedId=null;return;}ordered.Remove(source);var targetIndex=ordered.IndexOf(target);if(targetIndex<0){draggedId=null;return;}ordered.Insert(targetIndex,source);draggedId=null;try{var verified=await PersistAzureOrder(ordered);Notify(verified?"Sprint order updated in Azure DevOps.":"Azure DevOps returned a different order. The sprint was refreshed; review the current order.");}catch(Exception e){Error(e);sprintCache.Clear();try{await LoadSprint();}catch{}}}
 void DailyDragStart(WorkItem w){if(CanOrder(w))draggedId=w.Id;}
 async Task DailyDropOn(WorkItem target){if(draggedId is not {} id||id==target.Id)return;var source=items.FirstOrDefault(w=>w.Id==id);if(source is null){draggedId=null;return;}if(source.OwnerId!=target.OwnerId){draggedId=null;Notify("Reorder within the same person. Reassign the item first to move it to another person.");return;}if(WorkGroupRank(source)!=WorkGroupRank(target)){draggedId=null;Notify("Done, blocked and active work stay in separate groups. Reorder within the same group.");return;}await DropOn(target);}
 void SetScreen(string target){screen=target;detail=null;dailyPanelItem=null;if(target=="daily"){workspaceMode="People";quickView="Team";attentionFilter="";orderReview=false;}else if(target=="workspace"){workspaceMode="List";sort="Order";descending=false;}else if(target=="cleanup")workspaceMode="List";else if(target=="meeting")EnsureMeetingDefaults();if(target!="workspace")orderReview=false;}
 void ToggleOrderReview(){orderReview=!orderReview;if(orderReview){ClearFilters();screen="workspace";workspaceMode="List";sort="Order";descending=false;selected.Clear();}}
 List<SmartOrderRow> BuildSmartOrderPlan(){var ordered=items.Where(CanOrder).OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id).ToList();var owners=ordered.Select(w=>w.OwnerId).Distinct().OrderBy(id=>id==""?1:0).ThenBy(id=>ordered.FindIndex(w=>w.OwnerId==id)).ToArray();var result=new List<SmartOrderRow>();foreach(var owner in owners){foreach(var w in ordered.Where(w=>w.OwnerId==owner).OrderBy(WorkGroupRank).ThenBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id))result.Add(new(result.Count+1,w,PersonName(owner),WorkGroupName(w)));}return result;}
 void PrepareSmartOrder(){smartOrderPlan=BuildSmartOrderPlan();if(smartOrderPlan.Count==0){Notify("No orderable work items were found in this sprint.");return;}dialog="smartorder";dialogError="";focusDialog=true;}
 async Task ApplySmartOrder(){if(smartOrdering)return;smartOrdering=true;dialogError="";try{var verified=await PersistAzureOrder(smartOrderPlan.Select(x=>x.Item).ToArray());if(!verified){dialogError="Azure DevOps returned a different order after the update. The sprint was refreshed; review the current order before retrying.";return;}dialog="";Notify("Smart order applied and verified in Azure DevOps.");}catch(Exception e){sprintCache.Clear();try{await LoadSprint();}catch{}dialogError=e is TrackerException?e.Message:"Smart order could not be fully applied. The sprint was refreshed; review the current Azure DevOps order before retrying.";}finally{smartOrdering=false;}}
 void ToggleDailyActiveOnly()=>dailyActiveOnly=!dailyActiveOnly;
 async Task OpenCleanup(){screen="cleanup";workspaceMode="List";ClearFilters();await LoadSprint();}
 void Sort(string column){if(orderReview&&column!="Order")orderReview=false;if(sort==column)descending=!descending;else{sort=column;descending=false;}}
 void Select((int Id,bool Shift) e){var visible=Visible;var index=visible.FindIndex(w=>w.Id==e.Id);if(e.Shift&&selectionAnchor>=0){var anchor=visible.FindIndex(w=>w.Id==selectionAnchor);if(anchor>=0){for(int i=Math.Min(anchor,index);i<=Math.Max(anchor,index);i++)selected.Add(visible[i].Id);return;}}if(!selected.Add(e.Id))selected.Remove(e.Id);selectionAnchor=e.Id;}
 void ToggleAll(){var visible=Visible;if(visible.All(w=>selected.Contains(w.Id)))foreach(var w in visible)selected.Remove(w.Id);else foreach(var w in visible)selected.Add(w.Id);}
 void OpenDetail(WorkItem w){dailyPanelItem=null;detail=w;}
 async Task OpenById(int id){try{detail=await Tracker.GetAsync(id,lifetime.Token);}catch(Exception e){Error(e);}}
 void Replace(WorkItem w){var i=items.FindIndex(x=>x.Id==w.Id);if(i>=0)items[i]=w;var p=previousItems.FindIndex(x=>x.Id==w.Id);if(p>=0)previousItems[p]=w;if(detail?.Id==w.Id)detail=w;}
 static string Display(WorkItem w,ItemField f)=>f switch{ItemField.Title=>w.Title,ItemField.Owner=>w.Owner,ItemField.State=>w.State,ItemField.Iteration=>w.Iteration,ItemField.Area=>w.Area,ItemField.Estimate=>w.Estimate?.ToString(CultureInfo.InvariantCulture)??"",ItemField.Order=>w.Order?.ToString(CultureInfo.InvariantCulture)??"",ItemField.Priority=>w.Priority?.ToString()??"",ItemField.Tags=>string.Join("; ",w.Tags),ItemField.Description=>ContentText.Plain(w.Description),ItemField.Acceptance=>ContentText.Plain(w.Acceptance),_=>""};
 async Task InlineEdit((WorkItem Item,ItemField Field,string Value) edit){var w=edit.Item;if(!busy.Add(w.Id))return;try{
  if(drafts.ContainsKey(w.Id))throw new TrackerException("Apply or discard the local AI draft before editing this item.");
  object? value=edit.Value;if(edit.Field is ItemField.Estimate or ItemField.Order){if(edit.Value=="")value=null;else if(double.TryParse(edit.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)&&n>=0)value=n;else throw new TrackerException(edit.Field==ItemField.Order?"Order must be a non-negative number.":"Estimate must be a non-negative number.");}
  if(edit.Field==ItemField.Priority){if(int.TryParse(edit.Value,out var n)&&n>=1&&n<=4)value=n;else throw new TrackerException("Priority must be 1–4.");}
  var changes=new[]{new Change(edit.Field,value)};Replace(ItemChanges.Apply(w,changes,meta!));await InvokeAsync(StateHasChanged);var saved=await Tracker.UpdateAsync(new(w,changes),lifetime.Token);Replace(saved);sprintCache.Clear();
  if(saved.Iteration!=CurrentSprint?.Path)items.RemoveAll(x=>x.Id==saved.Id);if(saved.Iteration!=meta?.Iterations.ElementAtOrDefault(sprintIndex-1)?.Path)previousItems.RemoveAll(x=>x.Id==saved.Id);Notify($"#{w.Id} saved.");
 }catch(Exception e){Replace(w);Error(e);}finally{busy.Remove(w.Id);}}
 Task Show(string value){dialog=value;dialogError="";commandSearch="";focusDialog=true;return Task.CompletedTask;}
 void CloseDialog(){if(applying)return;dialog="";dialogError="";_=JS.InvokeVoidAsync("sprintPilot.restoreFocus");}
 void StartBulk(string kind){if(selected.Count==0){Notify("Select work items first.");return;}bulkKind=kind;bulkValue=kind switch{"Next"=>meta?.Iterations.ElementAtOrDefault(sprintIndex+1)?.Path??"","Previous"=>meta?.Iterations.ElementAtOrDefault(sprintIndex-1)?.Path??"",_=>""};_=Show("bulk");}
 void PreviewBulk(){try{if(bulkKind=="State"&&!CommonStates.Contains(bulkValue))throw new TrackerException("Choose a state supported by all selected work-item types.");if(SelectedItems.Count!=selected.Count)throw new TrackerException("Some selected items are no longer loaded. Clear selection and select again.");if(SelectedItems.Any(w=>busy.Contains(w.Id)||drafts.ContainsKey(w.Id)))throw new TrackerException("Wait for saves and apply or discard local drafts before bulk editing.");
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
 Dictionary<string,string> Filters()=>new(){["search"]=search,["owners"]=string.Join('|',ownerFilters),["state"]=stateFilter,["type"]=typeFilter,["tag"]=tagFilter,["area"]=areaFilter,["priority"]=priorityFilter,["quick"]=quickView,["sort"]=sort,["descending"]=descending.ToString()};
 async Task SaveView(){if(string.IsNullOrWhiteSpace(viewName)){dialogError="Enter a view name.";return;}prefs.Views.RemoveAll(v=>v.Name.Equals(viewName.Trim(),StringComparison.OrdinalIgnoreCase));prefs.Views.Add(new(viewName.Trim(),Filters(),prefs.Columns.ToArray()));await SaveSettings();CloseDialog();viewName="";}
 async Task LoadView(SavedView v){var f=v.Filters;search=f.GetValueOrDefault("search","");ownerFilters.Clear();foreach(var id in f.GetValueOrDefault("owners",f.GetValueOrDefault("owner","")).Split('|',StringSplitOptions.RemoveEmptyEntries))ownerFilters.Add(id);stateFilter=f.GetValueOrDefault("state","");typeFilter=f.GetValueOrDefault("type","");tagFilter=f.GetValueOrDefault("tag","");areaFilter=f.GetValueOrDefault("area","");priorityFilter=f.GetValueOrDefault("priority","");sort=f.GetValueOrDefault("sort","Order");descending=f.GetValueOrDefault("descending")=="True";prefs.Columns=v.Columns;screen="workspace";await SetQuickView(f.GetValueOrDefault("quick","Team"));}
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
