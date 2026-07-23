using System.Security.Cryptography;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

internal sealed class LocalExecutionArtifactStore:IExecutionArtifactStore
{
    private const string Prefix="artifact:";
    private readonly string root; private readonly int maximumBytes;
    public LocalExecutionArtifactStore(IOptions<ExecutionOptions> options)
    {
        root=Path.GetFullPath(options.Value.ArtifactRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Impersonate","execution-artifacts"));maximumBytes=options.Value.MaximumArtifactBytes;Directory.CreateDirectory(root);
    }
    public async Task<StoredArtifact> WriteTextAsync(ArtifactScope scope,string name,string content,string mediaType,CancellationToken ct)
    {
        var safeName=SafeName(name);var bytes=Encoding.UTF8.GetBytes(content);if(bytes.Length>maximumBytes)throw new InvalidOperationException("Artifact exceeds the configured size limit.");if(bytes.AsSpan().IndexOf((byte)0)>=0)throw new InvalidOperationException("Binary artifacts are not supported.");var relative=Path.Combine(scope.ProjectId.ToString("N"),scope.PipelineRunId.ToString("N"),scope.PlannedTaskId.ToString("N"),scope.AttemptNumber.ToString(),$"{Guid.NewGuid():N}-{safeName}");var path=Resolve(relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,bytes,ct);var hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();return new(Prefix+relative.Replace('\\','/'),hash,bytes.LongLength,mediaType,DateTimeOffset.UtcNow);
    }
    public async Task<string> ReadTextAsync(string reference,int maximumCharacters,CancellationToken ct)
    {
        if(!reference.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("Artifact reference is invalid.");var path=Resolve(reference[Prefix.Length..].Replace('/',Path.DirectorySeparatorChar));var info=new FileInfo(path);if(!info.Exists)throw new FileNotFoundException("Artifact was not found.");if(info.Length>maximumBytes)throw new InvalidOperationException("Artifact exceeds the configured size limit.");var value=await File.ReadAllTextAsync(path,Encoding.UTF8,ct);return value.Length<=maximumCharacters?value:value[..maximumCharacters];
    }
    private string Resolve(string relative){if(Path.IsPathRooted(relative)||relative.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Contains(".."))throw new ArgumentException("Artifact path is invalid.");var path=Path.GetFullPath(Path.Combine(root,relative));if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Artifact path escapes the configured root.");return path;}
    private static string SafeName(string name){var result=Path.GetFileName(name);if(string.IsNullOrWhiteSpace(result)||result!=name||result.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new ArgumentException("Artifact name is invalid.");return result;}
}
