using Impersonate.Application.Planning;
using Xunit;
namespace Impersonate.Application.Tests;
public sealed class PlannerPlanValidatorTests
{
 [Fact] public void Accepts_ordered_bounded_plan(){var plan=new PlannerPlan("Summary",true,[],[new(1,"Add domain state","Add the state transition.",["Invalid transitions are rejected."]),new(2,"Expose endpoint","Add the project-scoped endpoint.",["The endpoint returns an accepted response."])],null,null);Assert.Empty(PlannerPlanValidator.Validate(plan,12));}
 [Fact] public void Rejects_non_contiguous_sequences(){var plan=new PlannerPlan("Summary",true,[],[new(2,"Task","Description",["Criterion"])],null,null);Assert.Contains(PlannerPlanValidator.Validate(plan,12),x=>x.Contains("Sequences"));}
 [Fact] public void Requires_clarification_details(){var plan=new PlannerPlan("Summary",false,[],[],null,null);Assert.Equal(2,PlannerPlanValidator.Validate(plan,12).Count);}
 [Fact] public void Rejects_duplicate_titles_and_task_limit(){var plan=new PlannerPlan("Summary",true,[],[new(1,"Same","First",["One"]),new(2,"same","Second",["Two"])],null,null);var errors=PlannerPlanValidator.Validate(plan,1);Assert.Contains(errors,x=>x.Contains("Maximum"));Assert.Contains(errors,x=>x.Contains("unique"));}
 [Fact] public void Rejects_missing_summary_placeholders_and_execution_claims(){var plan=new PlannerPlan("",true,[],[new(1,"TODO endpoint","I inspected the repository and ran the tests.",["TBD"])],null,null);var errors=PlannerPlanValidator.Validate(plan,12);Assert.Contains(errors,x=>x.Contains("summary"));Assert.Contains(errors,x=>x.Contains("Placeholder"));Assert.Contains(errors,x=>x.Contains("inspection"));}
 [Fact] public void Rejects_missing_acceptance_criteria(){var plan=new PlannerPlan("Summary",true,[],[new(1,"Add endpoint","Add the endpoint.",[])],null,null);Assert.Contains(PlannerPlanValidator.Validate(plan,12),x=>x.Contains("Acceptance criteria"));}
}
