using Impersonate.Domain.AiModels;using Xunit;
namespace Impersonate.Domain.Tests;
public sealed class AiModelConfigurationTests
{
 [Fact]public void Profile_validates_and_can_be_disabled(){Assert.Throws<ArgumentException>(()=>AiModelProfile.Create("","Anthropic","model"));var m=AiModelProfile.Create("Planner","Anthropic","model");m.SetEnabled(false);Assert.False(m.IsEnabled);}
 [Fact]public void Assignment_replacement_preserves_scope(){var project=Guid.NewGuid();var a=AgentModelAssignment.Create(AgentRole.Planner,Guid.NewGuid(),project);var next=Guid.NewGuid();a.ReplaceModel(next);Assert.Equal(next,a.AiModelProfileId);Assert.Equal(project,a.ProjectId);}
 [Fact]public void Global_assignment_has_no_project(){var a=AgentModelAssignment.Create(AgentRole.Reviewer,Guid.NewGuid());Assert.Null(a.ProjectId);}
}
