using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SprintPilot.Application;
using SprintPilot.Domain;
using SprintPilot.Infrastructure;
using SprintPilot.Web.Components;
using System.Text;
using System.Text.Json;

var destination = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("SprintPilot-planning-preview.html");
var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var css = await File.ReadAllTextAsync(Path.Combine(repository, "src/SprintPilot.Web/wwwroot/app.css"));
var example = PlanningExample.Create(new DateOnly(2026, 9, 15));
var snapshot = Planning.Build(example.Items, example.Metadata, example.Settings, example.Today, DateTimeOffset.UtcNow);
var services = new ServiceCollection().AddLogging().BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
var output = new StringBuilder();
output.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>SprintPilot planning preview</title><style>");
output.Append(css);
output.Append(".preview-shell{max-width:1300px;margin:auto;padding:24px}.preview-shell>header{display:flex;justify-content:space-between;gap:20px;align-items:center;margin-bottom:26px}.preview-shell>header strong{font-size:21px}.preview-shell h1{margin:0}.preview-pane[hidden]{display:none}.preview-detail[hidden]{display:none}.preview-detail{margin-top:25px;padding:18px;border:1px solid var(--border)}.preview-detail td{white-space:normal}.preview-banner{margin:0 0 18px;color:var(--accent)}@media(max-width:700px){.preview-shell{padding:16px}.preview-shell>header{align-items:flex-start;flex-direction:column}}\n</style></head><body><div class='preview-shell' id='sprintpilot-planning-preview'><header><strong>SprintPilot</strong><h1>Planning overview</h1><span>Platform team</span></header><p class='preview-banner'>Sample data · interactive illustration · no Azure DevOps connection</p>");
foreach(var horizon in Enum.GetValues<PlanningHorizon>())
{
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var view = await renderer.RenderComponentAsync<PlanningDashboard>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Snapshot"] = snapshot, ["Settings"] = example.Settings, ["Selected"] = horizon
        }));
        return view.ToHtmlString();
    });
    if(!html.Contains("Applications") || !html.Contains("People &amp; capacity")) throw new Exception("Planning component did not render its expected sections.");
    output.Append($"<div class='preview-pane' data-period='{horizon}' {(horizon == PlanningHorizon.Current ? "" : "hidden")}>{html}</div>");
}
output.Append("<section class='preview-detail' id='preview-details' aria-live='polite' hidden></section></div>");
var data = example.Items.Select(w => new { w.Id, w.Title, w.Owner, w.Estimate, Tags = string.Join("; ",w.Tags), w.Iteration });
output.Append("<script type='application/json' id='planning-sample-data'>" + JsonSerializer.Serialize(data) + "</script><script>");
output.Append(await File.ReadAllTextAsync(Path.Combine(repository, "tools/SprintPilot.Preview/preview.js")));
output.Append("</script></body></html>");
await File.WriteAllTextAsync(destination, output.ToString());
Console.WriteLine("Rendered all five planning horizons from the Blazor component: " + destination);
