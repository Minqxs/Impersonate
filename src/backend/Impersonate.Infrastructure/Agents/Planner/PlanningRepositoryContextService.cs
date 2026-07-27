using System.Text;
using System.Text.Json;
using Impersonate.Application.Execution;
using Impersonate.Application.Planning;

namespace Impersonate.Infrastructure.Agents.Planner;

internal sealed class PlanningRepositoryContextService(IRepositoryWorkspaceService workspaces,IRepositoryTools tools,IExecutionArtifactStore artifacts):IPlanningRepositoryContextService
{
 private const int MaximumTreeEntries=500,MaximumFilesRead=30,MaximumFileBytes=100_000,MaximumTotalContextBytes=1_000_000;
 private static readonly string[] ManifestNames=[".sln",".csproj","package.json","vite.config","tsconfig","pom.xml","build.gradle","Cargo.toml","go.mod","requirements.txt","pyproject.toml"];
 public async Task<PlanningRepositoryContextResult> BuildAsync(Guid projectId,Guid runId,string repositoryUrl,string defaultBranch,string featureRequest,CancellationToken ct)
 {
  var prepared=await workspaces.PrepareAsync(new(projectId,runId,Guid.Empty,0,repositoryUrl,defaultBranch,[],null),ct);if(!prepared.Succeeded)return new(false,null,prepared.FailureCode,prepared.FailureMessage);var workspace=prepared.Workspace!;
  try
  {
   var listed=await tools.ListFilesAsync(workspace,".",ct);if(!listed.Succeeded)return new(false,null,"planning_context_failed",listed.FailureMessage);var tree=RepositoryEvidencePathPolicy.Rank(listed.Output.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries),featureRequest,MaximumTreeEntries).ToList();var selected=tree.Where(path=>FeatureMatch(path,featureRequest)).Concat(tree.Where(IsManifest)).Concat(tree.Where(IsArchitectureLocation)).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaximumFilesRead).ToList();var excerpts=new List<PlanningRelevantFile>();var frameworkContent=new Dictionary<string,string>();var total=0;
   foreach(var path in selected){var read=await tools.ReadFileAsync(workspace,path,ct);if(!read.Succeeded)continue;var truncated=read.Output.Length>MaximumFileBytes;var value=truncated?read.Output[..MaximumFileBytes]:read.Output;var bytes=Encoding.UTF8.GetByteCount(value);if(total+bytes>MaximumTotalContextBytes){var remaining=MaximumTotalContextBytes-total;if(remaining<=0)break;value=TruncateUtf8(value,remaining);truncated=true;bytes=Encoding.UTF8.GetByteCount(value);}excerpts.Add(new(path,value,truncated));frameworkContent[path]=value;total+=bytes;if(total>=MaximumTotalContextBytes)break;}
   var languages=DetectLanguages(tree);var frameworks=DetectFrameworks(frameworkContent);var layers=DetectLocations(tree,["Domain","Application","Infrastructure","Api","frontend","src"]);var tests=tree.Where(x=>x.Contains("test",StringComparison.OrdinalIgnoreCase)).Select(Parent).Distinct().Take(30).ToList();var migrations=tree.Where(x=>x.Contains("migration",StringComparison.OrdinalIgnoreCase)).Select(Parent).Distinct().Take(30).ToList();var summary=$"Bounded deterministic snapshot: {tree.Count} paths, {excerpts.Count} safe file excerpts, {total} UTF-8 bytes; languages: {string.Join(", ",languages.DefaultIfEmpty("Unknown"))}; frameworks: {string.Join(", ",frameworks.DefaultIfEmpty("Unknown"))}.";var payload=JsonSerializer.Serialize(new{tree,relevantFiles=excerpts,languages,frameworks,layers,testLocations=tests,migrationLocations=migrations,summary});var artifact=await artifacts.WriteTextAsync(new(projectId,runId,Guid.Empty,0),"planning-context.json",payload,"application/json",ct);var context=new PlanningRepositoryContext(tree,excerpts,languages,frameworks,layers,tests,migrations,summary,artifact.Reference,tree.ToHashSet(StringComparer.OrdinalIgnoreCase));return new(true,context,null,null);
  }
  catch(Exception ex) when(ex is IOException or InvalidOperationException or ArgumentException){return new(false,null,"planning_context_failed",ex.Message.Length<=500?ex.Message:ex.Message[..500]);}
 }
 private static bool FeatureMatch(string path,string request)=>request.Split([' ','-','_','/'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Length>=4).Take(12).Any(term=>path.Contains(term,StringComparison.OrdinalIgnoreCase));
 private static bool IsArchitectureLocation(string path)=>new[]{"domain","application","api","frontend","test","src"}.Any(part=>path.Split('/').Contains(part,StringComparer.OrdinalIgnoreCase));
 private static bool IsManifest(string path)=>ManifestNames.Any(name=>path.EndsWith(name,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(path).Equals(name,StringComparison.OrdinalIgnoreCase));
 private static List<string> DetectLanguages(IEnumerable<string> paths)=>paths.Select(path=>Path.GetExtension(path).ToLowerInvariant()).Select(x=>x switch{".cs"=>"C#",".ts" or ".tsx"=>"TypeScript",".js" or ".jsx"=>"JavaScript",".py"=>"Python",".java"=>"Java",".go"=>"Go",".rs"=>"Rust",_=>null}).Where(x=>x is not null).Cast<string>().Distinct().Order().ToList();
 private static List<string> DetectFrameworks(IReadOnlyDictionary<string,string> files){var text=string.Join('\n',files.Values);return new[]{("Microsoft.NET.Sdk",".NET"),("Microsoft.EntityFrameworkCore","Entity Framework Core"),("react","React"),("vite","Vite"),("@mui","MUI"),("next","Next.js"),("spring","Spring")}.Where(x=>text.Contains(x.Item1,StringComparison.OrdinalIgnoreCase)).Select(x=>x.Item2).Distinct().ToList();}
 private static string TruncateUtf8(string value,int maximumBytes){var length=Math.Min(value.Length,maximumBytes);while(length>0&&Encoding.UTF8.GetByteCount(value.AsSpan(0,length))>maximumBytes)length--;return value[..length];}
 private static List<string> DetectLocations(IEnumerable<string> tree,IEnumerable<string> names)=>names.Where(name=>tree.Any(path=>path.Contains(name,StringComparison.OrdinalIgnoreCase))).ToList();private static string Parent(string path)=>Normalize(Path.GetDirectoryName(path)??".");private static string Normalize(string path)=>path.Replace('\\','/').TrimStart('/');
}
