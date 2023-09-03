# Deployment Process Overview

Business Requirement: The process for deploying our services use a standard model across all services.

## Consistency in deployment

This epic will define a standard model that we will follow in order to deploy our services. All deployments follow yaml-based stages in pipelines. Ideally all deployments will use the following pattern: 

**Build Pipeline:**

1. Build
    - Build
    - Unit/Functional Tests
    - Publish Build Artifacts
2. Validate
    - SDL Runs
    - Pre-merge checks

**Deployment Pipeline:**

For each of service to be deployed:
- Build Pipeline as a resource:
  Consume a build pipeline as a resource into a deployment pipeline. This way deployments can be kicked-off independent to builds and re-running of just deployments using the same build artifacts is possible as well.
  ```
  resources:
  pipelines:
    - pipeline: MyAppCI
      source: CIPipeline
   -  pipeline: AnotherCI
      source: anotherCIPipeline
   ```
- Download all relevant build artifacts from the attached build pipeline.

The following are the stages in the deployment pipeline and each depends on the previous one to be completed successfully:
1. Pre-deployment checks
2. Deploy service
3. Post-deployment scenario tests

**Production Environments**

Specific to deploying to prod environment is an additional [manual approval check](https://docs.microsoft.com/en-us/azure/devops/pipelines/process/approvals?view=azure-devops#approvals) to prevent accidental deployment. 

**Deployment Workflow**

![Deployment Process](Images/Deployment_Workflow.svg)

**Why Stages?**

Stages provide the flexibility of having logical boundaries in pipelines, and can be arranged into a dependency graph (e.g Run Stage B only if Stage A succeeds). Stages also provide the ability to re-run parts of a pipeline; e.g. rerun a failed deployment or run just parts of a deployment which does not require rerunning the entire pipeline. 

![Stages](Images/Stages.PNG)


<!-- Begin Generated Content: Doc Feedback -->
<sub>Was this helpful? [![Yes](https://helix.dot.net/f/ip/5?p=Documentation%5CValidation%5CDeploymentProcess.md)](https://helix.dot.net/f/p/5?p=Documentation%5CValidation%5CDeploymentProcess.md) [![No](https://helix.dot.net/f/in)](https://helix.dot.net/f/n/5?p=Documentation%5CValidation%5CDeploymentProcess.md)</sub>
<!-- End Generated Content-->
name: .NET Core Build 
  
 on: 
   workflow_dispatch: 
   push: 
     branches: [ main ] 
     tags: 
       - 'v*' 
   pull_request: 
     branches: [ main ] 
     tags: 
       - 'v*' 
 jobs: 
   build-and-publish-release: 
  
     runs-on: ubuntu-18.04 
  
     steps: 
     - uses: actions/checkout@v2 
       with: 
           fetch-depth: 0 
     - name: Setup .NET Core 
       uses: actions/setup-dotnet@v1 
       with: 
         dotnet-version: 5.0.401 
  
     - name: Install dependencies 
       run: dotnet restore ContributorLicenseAgreement.sln 
       env: 
         DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true 
     - name: Build 
       run: dotnet build ContributorLicenseAgreement.sln --configuration Release --no-restore 
  
     - name: Test 
       run: dotnet test ContributorLicenseAgreement.sln --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage" --settings coverlet.runsettings.xml -r ./coverlet-results 
  
     - name: Generate coverage badge 
       if: github.ref == 'refs/heads/main' 
       uses: danielpalme/ReportGenerator-GitHub-Action@4.8.12 
       with: 
         reports: './coverlet-results/*/*.xml' # REQUIRED # The coverage reports that should be parsed (separated by semicolon). Globbing is supported. 
         targetdir: 'coveragereport' # REQUIRED # The directory where the generated report should be saved. 
         reporttypes: 'Badges' # The output formats and scope (separated by semicolon) Values: Badges, Clover, Cobertura, CsvSummary, Html, HtmlChart, HtmlInline, HtmlInline_AzurePipelines, HtmlInline_AzurePipelines_Dark, HtmlSummary, JsonSummary, Latex, LatexSummary, lcov, MarkdownSummary, MHtml, PngChart, SonarQube, TeamCitySummary, TextSummary, Xml, XmlSummary 
  
     - name: Update badge in README 
       if: github.ref == 'refs/heads/main' 
       run: | 
         git checkout coverage || git checkout -b coverage 
         mkdir -p docs/images 
         cp coveragereport/badge_shieldsio_linecoverage_green.svg docs/images/linecoverage.svg 
         git config --local user.email "action@github.com" 
         git config --local user.name "GitHub Action" 
         git add docs/images/linecoverage.svg 
         git commit -m 'Auto update coverage badge' || exit 0 
         git push origin HEAD:coverage
