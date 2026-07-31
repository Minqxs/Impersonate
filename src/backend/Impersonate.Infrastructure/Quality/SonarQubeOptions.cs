namespace Impersonate.Infrastructure.Quality;
public sealed class SonarQubeOptions
{
 public int TimeoutSeconds{get;set;}=15;
 public bool AllowHttpLocalDevelopment{get;set;}
 public string[] AllowedHosts{get;set;}=[];
}
