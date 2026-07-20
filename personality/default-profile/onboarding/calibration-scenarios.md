# Personality Calibration Scenarios

Record the user's decision, reasoning, exceptions, and confidence for each scenario. Convert repeated decisions into structured rules.

## 1. Existing library versus preferred library
A repository consistently uses Library A. The user prefers Library B for new projects. Decide whether to keep A, introduce B, or propose a separate migration.

## 2. Valuable adjacent improvement
A requested change exposes a small nearby defect that can break the same workflow. The fix is low risk and touches the same files.

## 3. Unrelated cleanup
While changing one endpoint, the agent notices unrelated duplication and poor naming elsewhere.

## 4. Required dependency failure
A process has changed durable state, but a required downstream operation fails before the workflow is genuinely complete.

## 5. Advisory verification unavailable
A non-blocking verification cannot reach its data source. Decide how the main workflow, check status, user feedback, and later review should behave.

## 6. Fashionable abstraction
The coder proposes interfaces, factories, and strategies for one implementation with no demonstrated extension need.

## 7. Ambiguous requirement
One assumption is reversible and low risk. Another changes a public contract or persisted behaviour.

## 8. Verification policy
Compare a two-line internal change with a shared cross-module workflow change. Define what evidence is expected for each, and how active pipeline rules affect the answer.

## 9. Security versus compatibility
An official advisory requires behaviour change that conflicts with the preference to preserve existing behaviour.

## 10. Personality disagreement
The profile recommends one approach, but repository evidence and official documentation support another.

## 11. Product polish versus decorative complexity
A UI concept looks impressive but adds navigation noise and obscures core workflows. Decide what stays and what is removed.

## 12. Direct user correction
An older preference conflicts with a newer explicit correction. Determine whether it is a true supersession or a scope difference.
