// Groovy Script: http://www.groovy-lang.org/syntax.html
// Jenkins DSL: https://github.com/jenkinsci/job-dsl-plugin/wiki

import jobs.generation.Utilities;
import jobs.generation.InternalUtilities;

static getJobName(def os, def configName) {
  return "${os}_${configName}"
}

static addArchival(def job, def filesToArchive, def filesToExclude) {
  def doNotFailIfNothingArchived = false
  def archiveOnlyIfSuccessful = false

  Utilities.addArchival(job, filesToArchive, filesToExclude, doNotFailIfNothingArchived, archiveOnlyIfSuccessful)
}

static addGithubPRTriggerForBranch(def job, def branchName, def jobName) {
  def prContext = "prtest/${jobName.replace('_', '/')}"
  def triggerPhrase = "(?i)^\\s*(@?dotnet-bot\\s+)?(re)?test\\s+(${prContext})(\\s+please)?\\s*\$"
  def triggerOnPhraseOnly = false

  Utilities.addGithubPRTriggerForBranch(job, branchName, prContext, triggerPhrase, triggerOnPhraseOnly)
}

static addXUnitDotNETResults(def job, def configName) {
  def resultFilePattern = "**/artifacts/${configName}/TestResults/*.xml"
  def skipIfNoTestFiles = false
    
  Utilities.addXUnitDotNETResults(job, resultFilePattern, skipIfNoTestFiles)
}

static addBuildSteps(def job, def projectName, def os, def configName, def isPR) {
  def buildJobName = getJobName(os, configName)
  def buildFullJobName = Utilities.getFullJobName(projectName, buildJobName, isPR)

  job.with {
    steps {
      if(os == "Windows_NT") {
        batchFile(".\\build\\CIBuild.cmd -configuration ${configName} -prepareMachine")
      }
      else {
        shell("./build/cibuild.sh --configuration ${configName}")
      }
    }
  }
}

[true, false].each { isPR ->
  ['Ubuntu16.04', 'Windows_NT'].each { os ->
    ['debug', 'release'].each { configName ->
      def projectName = GithubProject
      def branchName = GithubBranchName

      def osBase = os
      def machineAffinity = 'latest-or-auto'

      def filesToArchive = "**/artifacts/${configName}/**"
      def filesToExclude = "**/artifacts/${configName}/obj/**"

      def jobName = getJobName(os, configName)
      def fullJobName = Utilities.getFullJobName(projectName, jobName, isPR)
      def myJob = job(fullJobName)

      InternalUtilities.standardJobSetup(myJob, projectName, isPR, "*/${branchName}")

      if (isPR) {
        addGithubPRTriggerForBranch(myJob, branchName, jobName)
      } else {
        Utilities.addGithubPushTrigger(myJob)
      }
      
      addArchival(myJob, filesToArchive, filesToExclude)
//      addXUnitDotNETResults(myJob, configName)

      Utilities.setMachineAffinity(myJob, os, machineAffinity)

      addBuildSteps(myJob, projectName, os, configName, isPR)
    }
  }
}