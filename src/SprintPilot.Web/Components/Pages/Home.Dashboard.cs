using Microsoft.AspNetCore.Components;
using SprintPilot.Application;
using SprintPilot.Domain;

namespace SprintPilot.Web.Components.Pages;

public partial class Home
{
 string homeModal="",homeModalTitle="",homeModalIterationPath="",homeModalOwnerId="",homeModalApplicationFilter="",homeModalOwnerFilter="",homeModalError="";
 string homeAddTitle="",homeAddOwner="",homeAddApplication="",homeAddError="";
 string quickPbiOwner="",quickPbiApplication="";bool quickPbiApplicationPinned;
 bool homeModalLoading,homeModalApplying,homeAddCreating,homeAddApplicationPinned;
 readonly HashSet<int> homeModalSelection=[];
 List<WorkItem> homeModalItems=[];

 string leavePersonId="",leaveStart="",leaveEnd="",leaveNote="",editingManualLeaveId="",editingDevOpsIterationId="";
 DateTimeOffset? editingDevOpsOriginalStart,editingDevOpsOriginalEnd;
 bool leaveSaving;

 bool HomeModalOpen=>homeModal!="";
 bool HomeWorkModal=>homeModal is "attention" or "person" or "move-next";
 IEnumerable<WorkItem> HomeModalVisibleItems {
  get {
   IEnumerable<WorkItem> rows=homeModalItems;
   if(homeModalApplicationFilter!="")rows=rows.Where(w=>w.Tags.Contains(homeModalApplicationFilter,StringComparer.OrdinalIgnoreCase));
   if(homeModalOwnerFilter!="")rows=rows.Where(w=>w.OwnerId.Equals(homeModalOwnerFilter,StringComparison.OrdinalIgnoreCase));
   return rows.OrderBy(w=>w.Order??double.MaxValue).ThenBy(w=>w.Id);
  }
 }
 string HomeModalPersonName=>meta?.People.FirstOrDefault(p=>p.Id==homeModalOwnerId)?.Name??"";
 string[] MyScopeApplications()=>PlanningPrefs.Tags.Where(t=>t.Application&&t.ApplicationMyScope).Select(t=>t.Tag).ToArray();
 string[] MyScopeInitiatives()=>PlanningPrefs.Tags.Where(t=>t.Initiative&&t.InitiativeMyScope).Select(t=>t.Tag).ToArray();

 void OpenAttentionModal(string key)
 {
  var rows=CurrentOpenItems;
  homeModalItems=(key switch {
   "blocked"=>rows.Where(Blocked),
   "unassigned"=>rows.Where(w=>w.OwnerId==""),
   "missing-app"=>rows.Where(w=>!HasApplicationTag(w)),
   "missing-estimate"=>rows.Where(MissingEstimateForOpenWork),
   "stale"=>rows.Where(w=>w.Changed!=default&&w.Changed<DateTimeOffset.UtcNow.AddDays(-prefs.StaleDays)),
   _=>[]
  }).ToList();
  var label=HomeAttention().FirstOrDefault(x=>x.Key==key)?.Label??"Needs attention";
  OpenHomeWorkModal("attention",label,CurrentSprint?.Path??"","");
 }
 void OpenCurrentPersonModal(string id)=>OpenHomeWorkModal("person",PersonName(id),CurrentSprint?.Path??"",id,items.Where(w=>w.OwnerId==id).ToList());
 async Task OpenSprintPersonModal(Iteration iteration,string id)
 {
  homeModalLoading=true;homeModalError="";
  try{
   var rows=iteration.Path.Equals(CurrentSprint?.Path,StringComparison.OrdinalIgnoreCase)?items:(await Tracker.SprintAsync(iteration.Path,lifetime.Token)).ToList();
   OpenHomeWorkModal("person",PersonName(id),iteration.Path,id,rows.Where(w=>w.OwnerId==id).ToList());
  }catch(Exception e){homeModalError=e is TrackerException?e.Message:"Work items could not be loaded.";}
  finally{homeModalLoading=false;}
 }
 void OpenHomeWorkModal(string kind,string title,string iteration,string ownerId,IReadOnlyList<WorkItem>? source=null)
 {
  homeModal=kind;homeModalTitle=title;homeModalIterationPath=iteration;homeModalOwnerId=ownerId;homeModalApplicationFilter="";homeModalOwnerFilter="";homeModalError="";
  homeModalItems=(source??homeModalItems).ToList();homeModalSelection.Clear();dailyPanelItem=null;detail=null;
  ResetHomeAdd();
  if(homeModalItems.FirstOrDefault() is {} first)StartDetailEdit(first);
 }
 void OpenMoveNextModal()
 {
  if(NextSprint is null){Notify("No next sprint is configured.");return;}
  var rows=CurrentOpenItems.Where(w=>Planning.IsPlanningType(w.Type,PlanningPrefs)).ToList();
  OpenHomeWorkModal("move-next","Move work to next sprint",CurrentSprint?.Path??"","",rows);
 }
 void CloseHomeModal()
 {
  if(homeModalApplying||homeAddCreating||detailSaving||leaveSaving)return;
  homeModal="";homeModalTitle=homeModalIterationPath=homeModalOwnerId=homeModalApplicationFilter=homeModalOwnerFilter=homeModalError="";
  homeModalItems=[];homeModalSelection.Clear();detail=null;ResetHomeAdd();ResetLeaveEditor();
 }
 void ToggleHomeModalItem(int id){if(!homeModalSelection.Add(id))homeModalSelection.Remove(id);}
 void ToggleHomeModalVisible()
 {
  var rows=HomeModalVisibleItems.ToArray();
  if(rows.Length>0&&rows.All(w=>homeModalSelection.Contains(w.Id)))foreach(var w in rows)homeModalSelection.Remove(w.Id);
  else foreach(var w in rows)homeModalSelection.Add(w.Id);
 }
 async Task MoveSelectedHomeItems()
 {
  if(homeModalApplying||NextSprint is null)return;
  var ids=homeModalSelection.ToArray();if(ids.Length==0){homeModalError="Select at least one work item.";return;}
  homeModalApplying=true;homeModalError="";var success=0;var failures=new List<string>();
  foreach(var id in ids){
   try{
    var fresh=await Tracker.GetAsync(id,lifetime.Token);
    var saved=await Tracker.UpdateAsync(new(fresh,[new Change(ItemField.Iteration,NextSprint.Path)]),lifetime.Token);
    items.RemoveAll(x=>x.Id==id);var p=planningItems.FindIndex(x=>x.Id==id);if(p>=0)planningItems[p]=saved;
    homeModalItems.RemoveAll(x=>x.Id==id);homeModalSelection.Remove(id);success++;
   }catch(Exception e){failures.Add($"#{id}: {(e is TrackerException?e.Message:"update failed")}");}
  }
  sprintCache.Clear();
  if(detail is not null&&!homeModalItems.Any(x=>x.Id==detail.Id)){detail=null;if(homeModalItems.FirstOrDefault() is {} first)StartDetailEdit(first);}
  if(failures.Count>0)homeModalError=$"{success} moved; {failures.Count} failed. {string.Join(" ",failures.Take(2))}";
  else Notify($"{success} work item{(success==1?"":"s")} moved to {NextSprint.Name}.");
  homeModalApplying=false;
 }

 void ResetHomeAdd()
 {
  homeAddTitle="";homeAddError="";homeAddApplication="";homeAddApplicationPinned=false;
  homeAddOwner=homeModalOwnerId==""?prefs.LastBacklogOwner:meta?.People.FirstOrDefault(p=>p.Id==homeModalOwnerId)?.UniqueName??prefs.LastBacklogOwner;
 }
 void HomeAddTitleChanged(ChangeEventArgs e)
 {
  homeAddTitle=e.Value?.ToString()??"";
  if(!homeAddApplicationPinned)homeAddApplication=SuggestApplication(homeAddTitle);
 }
 void HomeAddApplicationChanged(ChangeEventArgs e){homeAddApplication=e.Value?.ToString()??"";homeAddApplicationPinned=homeAddApplication!="";}
 void QuickPbiTitleChanged(ChangeEventArgs e){quickPbiTitle=e.Value?.ToString()??"";if(!quickPbiApplicationPinned)quickPbiApplication=SuggestApplication(quickPbiTitle);}
 void QuickPbiApplicationChanged(ChangeEventArgs e){quickPbiApplication=e.Value?.ToString()??"";quickPbiApplicationPinned=quickPbiApplication!="";}
 void QuickPbiOwnerChanged(ChangeEventArgs e){quickPbiOwner=e.Value?.ToString()??"";prefs.LastBacklogOwner=quickPbiOwner;}
 string SuggestApplication(string title)
 {
  var apps=ApplicationTags();if(prefs.MyScopeOnly&&MyScopeApplications().Length>0)apps=apps.Where(a=>MyScopeApplications().Contains(a,StringComparer.OrdinalIgnoreCase)).ToArray();
  if(apps.Length==0||string.IsNullOrWhiteSpace(title))return "";
  var direct=apps.OrderByDescending(a=>a.Length).FirstOrDefault(a=>title.Contains(a,StringComparison.OrdinalIgnoreCase));if(direct is not null)return direct;
  var words=title.Split(new[]{' ','-','_','/',':','.'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Length>=3).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var scored=apps.Select(app=>new {
   App=app,
   Score=tagHistory.Concat(planningItems).Where(w=>w.Tags.Contains(app,StringComparer.OrdinalIgnoreCase))
     .Select(w=>w.Title.Split(new[]{' ','-','_','/',':','.'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Count(words.Contains)).Sum()
  }).OrderByDescending(x=>x.Score).ThenBy(x=>x.App,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
  return scored is {Score:>0}?scored.App:"";
 }
 async Task CreateHomeBacklog()
 {
  if(homeAddCreating||meta is null)return;homeAddError="";
  var title=homeAddTitle.Trim();if(title.Length is <1 or >255){homeAddError="Enter a title between 1 and 255 characters.";return;}
  var iteration=meta.Iterations.FirstOrDefault(i=>i.Path.Equals(homeModalIterationPath,StringComparison.OrdinalIgnoreCase))??CurrentSprint;
  if(iteration is null){homeAddError="No sprint is available.";return;}
  var type=meta.Types.FirstOrDefault(t=>t.Name=="Product Backlog Item")??meta.Types.FirstOrDefault(t=>t.Name=="User Story")??meta.Types.FirstOrDefault(t=>Planning.IsPlanningType(t.Name,PlanningPrefs));
  if(type is null){homeAddError="No supported backlog item type is available.";return;}
  var area=meta.Scope.FirstOrDefault()?.Path??meta.Areas.FirstOrDefault()??"";
  var changes=new List<Change>{new(ItemField.Title,title),new(ItemField.Area,area),new(ItemField.Iteration,iteration.Path)};
  if(homeAddOwner!="")changes.Add(new(ItemField.Owner,homeAddOwner));
  if(homeAddApplication!="")changes.Add(new(ItemField.Tags,homeAddApplication));
  homeAddCreating=true;
  try{
   var created=await Tracker.CreateAsync(type.Name,changes,null,lifetime.Token);
   prefs.LastBacklogOwner=homeAddOwner;await Preferences.SaveAsync(prefs,lifetime.Token);
   planningItems.Add(created);if(created.Iteration.Equals(CurrentSprint?.Path,StringComparison.OrdinalIgnoreCase))items.Add(created);
   if((homeModalOwnerId==""||created.OwnerId==homeModalOwnerId)&&created.Iteration.Equals(homeModalIterationPath,StringComparison.OrdinalIgnoreCase))homeModalItems.Add(created);
   sprintCache.Clear();Notify($"#{created.Id} created.");ResetHomeAdd();StartDetailEdit(created);
  }catch(Exception e){homeAddError=e is TrackerException?e.Message:"The backlog item could not be created.";}
  finally{homeAddCreating=false;}
 }

 async Task ToggleMyScopeOnly(ChangeEventArgs e){prefs.MyScopeOnly=e.Value is true;try{await Preferences.SaveAsync(prefs,lifetime.Token);}catch(Exception ex){Error(ex);}}
 void SetClassificationScope(string tag,bool application,bool enabled)
 {
  var i=PlanningPrefs.Tags.FindIndex(t=>t.Tag.Equals(tag,StringComparison.OrdinalIgnoreCase));if(i<0)return;var old=PlanningPrefs.Tags[i];
  PlanningPrefs.Tags[i]=application?old with{ApplicationMyScope=enabled}:old with{InitiativeMyScope=enabled};
 }
 string[] AssignedHolidayCalendars(string personId)
 {
  if(PlanningPrefs.HolidayCalendarsByPerson.TryGetValue(personId,out var values)&&values.Length>0)return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if(PlanningPrefs.HolidayCalendarByPerson.TryGetValue(personId,out var legacy)&&legacy!="")return [legacy];
  return [];
 }
 bool HasHolidayCalendar(string personId,string code)=>AssignedHolidayCalendars(personId).Contains(code,StringComparer.OrdinalIgnoreCase);
 void TogglePersonHolidayCalendar(string personId,string code,bool enabled)
 {
  var values=AssignedHolidayCalendars(personId).ToHashSet(StringComparer.OrdinalIgnoreCase);
  if(enabled)values.Add(code);else values.Remove(code);
  if(values.Count==0)PlanningPrefs.HolidayCalendarsByPerson.Remove(personId);else PlanningPrefs.HolidayCalendarsByPerson[personId]=values.Order(StringComparer.OrdinalIgnoreCase).ToArray();
  PlanningPrefs.HolidayCalendarByPerson.Remove(personId);
 }
 string HolidayCalendarSummary(string personId)=>string.Join(" + ",AssignedHolidayCalendars(personId).Select(code=>HolidayCountries.FirstOrDefault(c=>c.Code==code)?.Name??code));

 void OpenDaysOffManager()
 {
  homeModal="days-off";homeModalTitle="Days off";homeModalError="";detail=null;homeModalItems=[];ResetLeaveEditor();
  leavePersonId=meta?.Me.Id??meta?.People.FirstOrDefault()?.Id??"";
 }
 void ResetLeaveEditor(){editingManualLeaveId=editingDevOpsIterationId="";editingDevOpsOriginalStart=editingDevOpsOriginalEnd=null;leaveStart=leaveEnd=leaveNote="";}
 void StartManualLeaveEdit(ManualDayOff leave){editingManualLeaveId=leave.Id;editingDevOpsIterationId="";editingDevOpsOriginalStart=editingDevOpsOriginalEnd=null;leavePersonId=leave.PersonId;leaveStart=DateOnly.FromDateTime(leave.Start.LocalDateTime).ToString("yyyy-MM-dd");leaveEnd=DateOnly.FromDateTime(leave.End.LocalDateTime).ToString("yyyy-MM-dd");leaveNote=leave.Note;}
 void StartDevOpsLeaveEdit(DevOpsLeaveRow leave){editingManualLeaveId="";editingDevOpsIterationId=leave.IterationId;editingDevOpsOriginalStart=leave.Range.Start;editingDevOpsOriginalEnd=leave.Range.End;leavePersonId=leave.PersonId;leaveStart=DateOnly.FromDateTime(leave.Range.Start.LocalDateTime).ToString("yyyy-MM-dd");leaveEnd=DateOnly.FromDateTime(leave.Range.End.LocalDateTime).ToString("yyyy-MM-dd");leaveNote="";}
 async Task SaveLeave()
 {
  if(leaveSaving||meta is null)return;homeModalError="";
  if(string.IsNullOrWhiteSpace(leavePersonId)||!DateOnly.TryParse(leaveStart,out var start)||!DateOnly.TryParse(leaveEnd,out var end)||end<start){homeModalError="Choose a person and a valid start/end date.";return;}
  var offset=TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);var from=new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue),offset);var to=new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue),offset);
  leaveSaving=true;
  try{
   if(editingDevOpsIterationId!=""){
    if(!capacityByIteration.TryGetValue(editingDevOpsIterationId,out var capacity))throw new TrackerException("Refresh sprint capacity before editing this Azure DevOps day off.");
    var member=capacity.Members.FirstOrDefault(m=>m.PersonId==leavePersonId)??throw new TrackerException("This person has no capacity row in that sprint.");
    var rows=member.DaysOff.ToList();var index=rows.FindIndex(r=>r.Start==editingDevOpsOriginalStart&&r.End==editingDevOpsOriginalEnd);
    if(index<0)throw new TrackerException("The Azure DevOps day off changed. Refresh before retrying.");
    rows[index]=new(from,to);await Tracker.SetDaysOffAsync(editingDevOpsIterationId,leavePersonId,rows,lifetime.Token);
    capacityByIteration[editingDevOpsIterationId]=await Tracker.CapacityAsync(editingDevOpsIterationId,lifetime.Token);
    if(CurrentSprint?.Id==editingDevOpsIterationId)sprintCapacity=capacityByIteration[editingDevOpsIterationId];
    Notify("Azure DevOps day off updated.");
   }else{
    var id=editingManualLeaveId==""?Guid.NewGuid().ToString("N"):editingManualLeaveId;
    prefs.ManualDaysOff.RemoveAll(x=>x.Id==id);prefs.ManualDaysOff.Add(new(id,leavePersonId,from,to,leaveNote.Trim()));
    await Preferences.SaveAsync(prefs,lifetime.Token);Notify(editingManualLeaveId==""?"Day off added.":"Day off updated.");
   }
   ResetLeaveEditor();
  }catch(Exception e){homeModalError=e is TrackerException?e.Message:"The day off could not be saved.";}
  finally{leaveSaving=false;}
 }
 async Task DeleteManualLeave(string id){prefs.ManualDaysOff.RemoveAll(x=>x.Id==id);await Preferences.SaveAsync(prefs,lifetime.Token);if(editingManualLeaveId==id)ResetLeaveEditor();Notify("Manual day off deleted.");}
 List<DevOpsLeaveRow> DevOpsLeaveRows()
 {
  if(meta is null)return [];
  var result=new List<DevOpsLeaveRow>();
  foreach(var (iterationId,capacity) in capacityByIteration){
   var iteration=meta.Iterations.FirstOrDefault(i=>i.Id==iterationId);if(iteration is null)continue;
   foreach(var member in capacity.Members)foreach(var range in member.DaysOff)if(range.End.Date>=DateTime.Today.AddDays(-30))
    result.Add(new(member.PersonId,member.PersonName,iterationId,iteration.Name,range));
  }
  return result.DistinctBy(x=>(x.PersonId,x.IterationId,x.Range.Start,x.Range.End)).OrderBy(x=>x.Range.Start).ToList();
 }
 sealed record DevOpsLeaveRow(string PersonId,string PersonName,string IterationId,string IterationName,DateRange Range);
}
