# Renovate Dependency Update Tool

## Introduction

This document outlines the integration of [Renovate](https://github.com/renovatebot/renovate) into Arcade to automate dependency updates.
Renovate is an automated dependency update tool that generates PRs for updating dependencies from a wide variety of sources.

Renovate is similar to Dependabot in its purpose.
Dependabot should be used when possible.
However, Renovate supports a much broader range of [dependency types](https://docs.renovatebot.com/modules/datasource/), most notably Docker and GitHub releases.
For example, Renovate can automatically generate a PR to update a referenced Docker image when a newer version is available.

## Design

### Fork Mode

Protecting GitHub repositories in the dotnet organization from direct access by the Renovate tool is crucial.
Renovate will be used in fork mode, limiting its permissions to forked repositories.
This avoids giving write permissions to Renovate on any dotnet repository.
A GitHub bot account, `dotnet-renovate-bot`, is used to manage the Renovate operations.
This account has the ability to create forks from dotnet repositories, which will be the source of the head branch for PRs that are created.

GitHub scopes required by this account: `repo`, `workflow`.

### Repo Usage

Arcade provides an Azure DevOps pipeline YAML job template that repositories should utilize when making use of Renovate.
This template handles the execution of Renovate, ensuring a standardized approach across all repositories.
Repositories wishing to make use of Renovate can reference this template from a pipeline YAML file, setting the schedule trigger as desired.
Consuming repositories are responsible for providing their own [Renovate configuration file](https://docs.renovatebot.com/configuration-options/) that describes which dependencies should be updated.

## Renovate Configuration Patterns

TBD
