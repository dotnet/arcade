# Build Analysis Specifics

The Build Analysis check includes these details:

- Collapse failures of the same name across Jobs and Pipelines to improve discoverability. For example, failures of the same test across multiple operating systems are grouped together. 
- Highlight a failure's state in the target branch
- Provide direct links to the most common pages for continuing analysis
  - Link to the Azure DevOps build
  - Link to the log of the specific step that failed
  - Link to the Azure DevOps Test History for the pipeline
  - Link to test history of the specific test
  - Link to the Helix artifacts produced by a test
  - Link to the test execution details 

Each Build Analysis page also includes a "was this helpful" link. This allows you to call out specific highlights or lowlights in your experience with the check. This feedback is then used by the Dev WF team to refine the analysis.