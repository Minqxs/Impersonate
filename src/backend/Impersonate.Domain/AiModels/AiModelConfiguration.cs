namespace Impersonate.Domain.AiModels;
public enum AgentRole { Planner, Coder, Reviewer }
public sealed class AiModelProfile
{
 private AiModelProfile(){} private AiModelProfile(string name,string provider,string identifier,string? description,bool enabled,DateTimeOffset now){Id=Guid.NewGuid();DisplayName=Req(name,150);Provider=Req(provider,50);ModelIdentifier=Req(identifier,200);Description=Opt(description,1000);IsEnabled=enabled;CreatedAtUtc=UpdatedAtUtc=now;}
 public Guid Id{get;private set;} public string DisplayName{get;private set;}=null!;public string Provider{get;private set;}=null!;public string ModelIdentifier{get;private set;}=null!;public string? Description{get;private set;}public bool IsEnabled{get;private set;}public DateTimeOffset CreatedAtUtc{get;private set;}public DateTimeOffset UpdatedAtUtc{get;private set;}
 public static AiModelProfile Create(string name,string provider,string identifier,string? description=null,bool enabled=true,DateTimeOffset? now=null)=>new(name,provider,identifier,description,enabled,now??DateTimeOffset.UtcNow);
 public void Update(string name,string provider,string identifier,string? description,DateTimeOffset? at=null){DisplayName=Req(name,150);Provider=Req(provider,50);ModelIdentifier=Req(identifier,200);Description=Opt(description,1000);UpdatedAtUtc=at??DateTimeOffset.UtcNow;}
 public void SetEnabled(bool enabled,DateTimeOffset? at=null){IsEnabled=enabled;UpdatedAtUtc=at??DateTimeOffset.UtcNow;}
 private static string Req(string? value,int max){var clean=value?.Trim();if(string.IsNullOrWhiteSpace(clean))throw new ArgumentException("Value is required.");if(clean.Length>max)throw new ArgumentOutOfRangeException(nameof(value));return clean;}private static string? Opt(string? value,int max){var clean=value?.Trim();if(string.IsNullOrWhiteSpace(clean))return null;if(clean.Length>max)throw new ArgumentOutOfRangeException(nameof(value));return clean;}
}
public sealed class AgentModelAssignment
{
 private AgentModelAssignment(){}private AgentModelAssignment(AgentRole role,Guid modelId,Guid? projectId,DateTimeOffset now){if(modelId==Guid.Empty)throw new ArgumentException("Model ID is required.");Id=Guid.NewGuid();AgentRole=role;AiModelProfileId=modelId;ProjectId=projectId;CreatedAtUtc=UpdatedAtUtc=now;}
 public Guid Id{get;private set;}public AgentRole AgentRole{get;private set;}public Guid AiModelProfileId{get;private set;}public Guid? ProjectId{get;private set;}public DateTimeOffset CreatedAtUtc{get;private set;}public DateTimeOffset UpdatedAtUtc{get;private set;}
 public static AgentModelAssignment Create(AgentRole role,Guid modelId,Guid? projectId=null,DateTimeOffset? now=null)=>new(role,modelId,projectId,now??DateTimeOffset.UtcNow);public void ReplaceModel(Guid modelId,DateTimeOffset? at=null){if(modelId==Guid.Empty)throw new ArgumentException("Model ID is required.");AiModelProfileId=modelId;UpdatedAtUtc=at??DateTimeOffset.UtcNow;}
}
