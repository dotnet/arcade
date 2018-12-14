# Documentation Principles

This document is to provide value guidance when creating Arcade documentation. This supplements the [.NET Core Engineering Services Principles](https://microsoft.sharepoint.com/:w:/r/teams/netfx/engineering/_layouts/15/Doc.aspx?sourcedoc={ef69fcfc-3475-415a-b3ab-651a352b9bbe}&action=view&wdAccPdf=0&wdparaid=3E030EA3).

## Principles
Documentation, not code, is the primary source of information for customers.

Documentation is written for the customer.

Documentation is up-to-date.

Documentation is explicit.

Developers, including First Responders, are able to contribute documentation easily and without disruption.

Features are documented. Feature documentation explains why the feature is needed, what the feature does, and how the feature is used.

Questions are documented. If someone had to ask, it was overlooked in the docs.

## Plan

Value ease of maintenance and information quality.

Documentation starts with a single landing page (like current StartHere.md). All other documentation may be found within a few clicks of here.

Documentation is organized by category, then by product. Categories borrow from 1ES and mirror development flow. This provides natural boundaries and flow for both customers and developers, improving understanding. Existing documentation maps well to this space. The categories are: Code, Build, Test, Deploy.

Our customers are developers who understand their project's current build. Therefore there is high value in providing a mapping of existing non-Arcade build steps and concepts to Arcade build steps and concepts.

Our customers have high technical knowledge. Use this expectation to avoid over-documenting simple tasks.

Markdown files are sufficient for current efforts. GitHub pages provides a nice path for extending simple Markdown support, adding options and complexity. These options may be added as needed.
