using System.Net;
using System.Text.RegularExpressions;
using SprintPilot.Domain;
namespace SprintPilot.Application;
public static partial class ContentText {
 [GeneratedRegex("<[^>]+>")] private static partial Regex Html();
 public static string Plain(string value)=>WebUtility.HtmlDecode(Html().Replace(Regex.Replace(value,"</(p|div|li|h[1-6])>|<br\\s*/?>","\n",RegexOptions.IgnoreCase),"" )).Trim();
 public static string HtmlEncode(string text)=>"<div>"+WebUtility.HtmlEncode(text).Replace("\n","<br>")+"</div>";
}
public record QualityCheck(string Name,bool Passed,int Weight);
public record QualityReport(int Score,QualityCheck[] Checks);
public static class Quality {
 public static QualityReport Evaluate(WorkItem item,Preferences prefs,IEnumerable<WorkItem> peers) {
  var d=ContentText.Plain(item.Description);var a=ContentText.Plain(item.Acceptance);
  var rules=new Dictionary<string,bool>{["Clear title"]=item.Title.Trim().Length>=12,["Description"]=d.Length>0,["Acceptance criteria"]=a.Length>0,["Sprint"]=item.Iteration.Length>0,["Area"]=item.Area.Length>0,["Estimate"]=item.Estimate is >0,["Assignee"]=item.OwnerId.Length>0,["Tags"]=item.Tags.Length>0,["Testing information"]=Regex.IsMatch(d+" "+a,@"\b(test|testing|validation|regression)\b",RegexOptions.IgnoreCase),["Distinct title"]=!peers.Any(x=>x.Id!=item.Id && Similar(x.Title,item.Title))};
  var checks=rules.Select(x=>new QualityCheck(x.Key,x.Value,Math.Clamp(prefs.QualityWeights.GetValueOrDefault(x.Key),0,100))).ToArray();
  var total=checks.Sum(x=>x.Weight);return new(total==0?0:(int)Math.Round(100d*checks.Where(x=>x.Passed).Sum(x=>x.Weight)/total),checks);
 }
 public static bool Similar(string a,string b) {
  static HashSet<string> Words(string s)=>Regex.Matches(s.ToLowerInvariant(),@"\w+").Select(x=>x.Value).ToHashSet();
  var x=Words(a);var y=Words(b);var union=x.Union(y).Count();return union>0 && x.Intersect(y).Count()/(double)union>=.8;
 }
 public static string[] Issues(WorkItem w,Metadata meta,Preferences prefs,IEnumerable<WorkItem> loaded,Iteration? sprint) {
  var list=new List<string>();var done=Finished(w,meta);
  if(!done){if(w.OwnerId=="")list.Add("Unassigned");if(w.Estimate is null or <=0)list.Add("Missing estimate");if(w.Tags.Length==0)list.Add("Missing tags");list.Add("Unfinished");
   if(w.Changed<DateTimeOffset.UtcNow.AddDays(-Math.Max(1,prefs.StaleDays)))list.Add("Stale");
   if(sprint?.Finish is {} end && end-DateTimeOffset.UtcNow<TimeSpan.FromDays(3) && meta.Types.FirstOrDefault(t=>t.Name==w.Type)?.States.Any(s=>s.Name==w.State && s.Category=="Proposed")==true)list.Add("Still new late in sprint");
  }
  if(done && loaded.Any(c=>w.Children.Contains(c.Id) && !Finished(c,meta)))list.Add("Completed parent / unfinished child");
  return list.ToArray();
 }
 public static bool Finished(WorkItem item,Metadata metadata)=>metadata.Types.FirstOrDefault(t=>t.Name==item.Type)?.IsFinished(item.State)==true;
}
