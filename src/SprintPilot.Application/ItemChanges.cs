using SprintPilot.Domain;
namespace SprintPilot.Application;
public static class ItemChanges {
 public static WorkItem Apply(WorkItem item,IReadOnlyList<Change> changes,Metadata metadata){foreach(var c in changes){var s=c.Value?.ToString()??"";var person=metadata.People.FirstOrDefault(p=>p.Id==s||p.UniqueName==s);item=c.Field switch{
  ItemField.Title=>item with{Title=s},ItemField.Owner=>item with{Owner=person?.Name??s,OwnerId=person?.Id??s},ItemField.State=>item with{State=s},ItemField.Iteration=>item with{Iteration=s},ItemField.Area=>item with{Area=s},ItemField.Estimate=>item with{Estimate=c.Value is null?null:Convert.ToDouble(c.Value,System.Globalization.CultureInfo.InvariantCulture)},ItemField.Priority=>item with{Priority=c.Value is null?null:Convert.ToInt32(c.Value,System.Globalization.CultureInfo.InvariantCulture)},ItemField.Tags=>item with{Tags=s.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()},ItemField.Description=>item with{Description=s},ItemField.Acceptance=>item with{Acceptance=s},_=>item};}return item;}
}
