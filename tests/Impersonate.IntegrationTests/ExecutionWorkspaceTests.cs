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
 public void Sanitized_environment_uses_explicit_allowlist_and_excludes_secrets()
 {
  var proxyName="HTTPS_PROXY";var secretName="IMPERSONATE_TEST_API_KEY";var oldProxy=Environment.GetEnvironmentVariable(proxyName);var oldSecret=Environment.GetEnvironmentVariable(secretName);try{Environment.SetEnvironmentVariable(proxyName,"http://proxy.example.test:8080");Environment.SetEnvironmentVariable(secretName,"never-copy-this");var environment=new Impersonate.Infrastructure.Execution.AllowlistedChildProcessEnvironmentBuilder().Build();Assert.Equal("http://proxy.example.test:8080",environment[proxyName]);Assert.DoesNotContain(secretName,environment.Keys,StringComparer.OrdinalIgnoreCase);Assert.DoesNotContain(environment.Keys,x=>x.Contains("TOKEN",StringComparison.OrdinalIgnoreCase)||x.Contains("API_KEY",StringComparison.OrdinalIgnoreCase));if(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("SystemRoot") is{} systemRoot){Assert.Equal(systemRoot,environment["systemroot"]);Assert.Contains("SystemRoot",environment.Keys,StringComparer.OrdinalIgnoreCase);}}
  finally{Environment.SetEnvironmentVariable(proxyName,oldProxy);Environment.SetEnvironmentVariable(secretName,oldSecret);}
 }

 [Fact]
 public async Task Execution_readiness_starts_sanitized_git_and_validates_workspace_root()
 {
  var root=Path.Combine(Path.GetTempPath(),"impersonate-readiness-"+Guid.NewGuid().ToString("N"));try{var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Execution:WorkspaceRoot",root},{"Execution:ArtifactRoot",Path.Combine(root,"artifacts")},{"Ai:DataProtectionKeyPath",Path.Combine(root,"keys")}}).Build();var services=new ServiceCollection().AddLogging();services.AddSingleton<IConfiguration>(configuration);services.AddInfrastructure(configuration,new TestEnvironment());await using var provider=services.BuildServiceProvider();var result=await provider.GetRequiredService<IExecutionEnvironmentReadinessService>().CheckAsync(default);Assert.True(result.Ready,string.Join(" ",result.Blockers));Assert.True(result.GitAvailable);Assert.True(result.GitVersionSucceeded);Assert.True(result.SanitizedProcessSucceeded);Assert.True(result.WorkspaceRootWritable);if(OperatingSystem.IsWindows()&&Environment.GetEnvironmentVariable("SystemRoot") is not null){Assert.True(result.CoreEnvironmentValid);Assert.Contains("SystemRoot",result.SuppliedVariableNames,StringComparer.OrdinalIgnoreCase);}}
  finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
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
 [Fact]
 public async Task Public_repository_workspace_smoke_when_explicitly_enabled()
 {
  var repository=Environment.GetEnvironmentVariable("IMPERSONATE_PUBLIC_REPOSITORY_SMOKE_URL");if(string.IsNullOrWhiteSpace(repository))return;var root=Path.Combine(Path.GetTempPath(),"impersonate-public-smoke-"+Guid.NewGuid().ToString("N"));try{var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Execution:WorkspaceRoot",Path.Combine(root,"workspaces")},{"Execution:ArtifactRoot",Path.Combine(root,"artifacts")},{"Ai:DataProtectionKeyPath",Path.Combine(root,"keys")}}).Build();var services=new ServiceCollection().AddLogging();services.AddSingleton<IConfiguration>(configuration);services.AddInfrastructure(configuration,new TestEnvironment());await using var provider=services.BuildServiceProvider();var prepared=await provider.GetRequiredService<IRepositoryWorkspaceService>().PrepareAsync(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,repository,"main",[],null),default);Assert.True(prepared.Succeeded,$"{prepared.FailureCode}: {prepared.FailureMessage}");var tools=provider.GetRequiredService<IRepositoryTools>();var status=await tools.RunCommandAsync(prepared.Workspace!,new("git",["status","--porcelain"]),default);Assert.True(status.Succeeded);Assert.Empty(status.Output.Trim());var head=await tools.RunCommandAsync(prepared.Workspace!,new("git",["rev-parse","HEAD"]),default);Assert.True(head.Succeeded);Assert.NotEmpty(head.Output.Trim());}
  finally{if(Directory.Exists(root)){foreach(var path in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))File.SetAttributes(path,FileAttributes.Normal);Directory.Delete(root,true);}}
 }
 private static string Run(string executable,IReadOnlyList<string> arguments,string cwd){var start=new ProcessStartInfo(executable){WorkingDirectory=cwd,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException(error);return output;}
 private sealed class TestEnvironment:IHostEnvironment { public string EnvironmentName{get;set;}="Testing";public string ApplicationName{get;set;}="Impersonate.IntegrationTests";public string ContentRootPath{get;set;}=Directory.GetCurrentDirectory();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider(); }
}
