using System.Diagnostics;
using Impersonate.Application.Execution;
using Impersonate.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class ExecutionWorkspaceTests
{
 [Fact]
 public async Task Execution_creates_a_real_diff_in_isolation_without_creating_a_commit()
 {
  var root=Path.Combine(Path.GetTempPath(),"impersonate-execution-"+Guid.NewGuid().ToString("N"));var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
  try
  {
   Run("git",["init","-b","main"],source);Run("git",["config","user.email","fixture@example.test"],source);Run("git",["config","user.name","Fixture"],source);await File.WriteAllTextAsync(Path.Combine(source,"README.md"),"baseline\n");Run("git",["add","README.md"],source);Run("git",["commit","-m","fixture baseline"],source);var originalHead=Run("git",["rev-parse","HEAD"],source).Trim();
   var values=new Dictionary<string,string?>{{"Execution:WorkspaceRoot",Path.Combine(root,"workspaces")},{"Execution:ArtifactRoot",Path.Combine(root,"artifacts")},{"Ai:DataProtectionKeyPath",Path.Combine(root,"keys")}};var configuration=new ConfigurationBuilder().AddInMemoryCollection(values).Build();var services=new ServiceCollection().AddLogging();services.AddSingleton<IConfiguration>(configuration);services.AddInfrastructure(configuration,new TestEnvironment());await using var provider=services.BuildServiceProvider();var workspaceService=provider.GetRequiredService<IRepositoryWorkspaceService>();var tools=provider.GetRequiredService<IRepositoryTools>();var request=new WorkspaceRequest(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,source,"main",[],null);var prepared=await workspaceService.PrepareAsync(request,default);Assert.True(prepared.Succeeded,prepared.FailureMessage);
   var patch="diff --git a/feature.txt b/feature.txt\nnew file mode 100644\nindex 0000000..a88e8d8\n--- /dev/null\n+++ b/feature.txt\n@@ -0,0 +1 @@\n+implemented\n";var applied=await tools.ApplyPatchAsync(prepared.Workspace!,patch,default);Assert.True(applied.Succeeded,applied.FailureMessage);var diff=await tools.GetDiffAsync(prepared.Workspace!,default);Assert.Contains("feature.txt",diff.Output);var workspaceHead=await tools.RunCommandAsync(prepared.Workspace!,new("git",["rev-parse","HEAD"]),default);Assert.Equal(originalHead,workspaceHead.Output.Trim());Assert.Equal(originalHead,Run("git",["rev-parse","HEAD"],source).Trim());Assert.False(File.Exists(Path.Combine(source,"feature.txt")));
  }
  finally{if(Directory.Exists(root)){foreach(var path in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))File.SetAttributes(path,FileAttributes.Normal);Directory.Delete(root,true);}}
 }
 private static string Run(string executable,IReadOnlyList<string> arguments,string cwd){var start=new ProcessStartInfo(executable){WorkingDirectory=cwd,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException(error);return output;}
 private sealed class TestEnvironment:IHostEnvironment { public string EnvironmentName{get;set;}="Testing";public string ApplicationName{get;set;}="Impersonate.IntegrationTests";public string ContentRootPath{get;set;}=Directory.GetCurrentDirectory();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider(); }
}
