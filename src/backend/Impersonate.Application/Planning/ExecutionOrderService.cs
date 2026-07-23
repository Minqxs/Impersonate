namespace Impersonate.Application.Planning;
internal sealed class ExecutionOrderService:IExecutionOrderService
{
 private static readonly string[] Layers=["Domain","Contract","Persistence","Migration","Application","Api","FrontendState","FrontendUi","Testing","BuildConfiguration","Documentation","Unknown"];
 public ExecutionOrderResult Order(IReadOnlyList<PlannerTask> tasks)
 {
  var sequences=tasks.Select(x=>x.Sequence).ToHashSet();if(tasks.Any(x=>(x.DependsOnSequences??[]).Any(d=>d==x.Sequence||!sequences.Contains(d))))return new(false,[],["Dependencies must reference another task in the plan."]);var bySequence=tasks.ToDictionary(x=>x.Sequence);var remaining=tasks.ToDictionary(x=>x.Sequence,x=>(x.DependsOnSequences??[]).ToHashSet());var ordered=new List<PlannerTask>();
  while(remaining.Count>0){var ready=remaining.Where(x=>x.Value.All(d=>ordered.Any(o=>o.Sequence==d))).Select(x=>bySequence[x.Key]).OrderByDescending(x=>x.EstablishesSharedContract).ThenBy(x=>Conflict(x.ConflictRisk)).ThenBy(x=>Layer(x.ChangeType,x.AffectedAreas??[])).ThenBy(x=>x.Sequence).ThenBy(x=>x.Title,StringComparer.Ordinal).ToList();if(ready.Count==0)return new(false,[],["Task dependency graph contains a cycle."]);var next=ready[0];ordered.Add(next);remaining.Remove(next.Sequence);}
  return new(true,ordered.Select((task,index)=>{var execution=index+1;var adjusted=task.Sequence!=execution;return new OrderedPlannerTask(task,task.Sequence,execution,adjusted,adjusted?$"Moved from Planner position {task.Sequence} to {execution} to satisfy dependencies and conflict-aware layer ordering.":null);}).ToList(),[]);
 }
 private static int Conflict(string value)=>value.Equals("High",StringComparison.OrdinalIgnoreCase)?2:value.Equals("Moderate",StringComparison.OrdinalIgnoreCase)?1:0;
 private static int Layer(string changeType,IReadOnlyList<string> areas){var text=changeType+" "+string.Join(' ',areas);for(var i=0;i<Layers.Length;i++)if(text.Contains(Layers[i],StringComparison.OrdinalIgnoreCase))return i;return Layers.Length;}
}
