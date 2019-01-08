# Documentation Principles

This document is to provide value guidance when creating Arcade documentation. This supplements the [.NET Core Engineering Services Principles](https://microsoft.sharepoint.com/:w:/r/teams/netfx/engineering/_layouts/15/Doc.aspx?sourcedoc={ef69fcfc-3475-415a-b3ab-651a352b9bbe}&action=view&wdAccPdf=0&wdparaid=3E030EA3).

## Principles

1. Documentation, not code, is the primary source of information for customers.

2. Documentation is written for the customer.

3. Documentation is up-to-date.

4. Documentation is explicit.

5. Developers, including First Responders, are able to contribute documentation easily and without disruption.

6. Features are documented. Feature documentation explains why the feature is needed, what the feature does, and how the feature is used.

7. Questions are documented. If someone had to ask, it was overlooked in the docs.

## Project assumptions

- Value ease of maintenance and information quality.

- Arcade's customers are developers who understand their project's current build. Therefore there is high value in providing a mapping of existing non-Arcade build steps and concepts to Arcade build steps and concepts.

- Arcade's customers have high technical knowledge. Use this expectation to avoid over-documenting simple tasks.

- Examples should be simple but not trivial.

- Documentation starts with a single landing page (like current StartHere.md). All other documentation may be found within a few clicks of here.

- Documentation is organized by category, then by product. Categories borrow from 1ES and mirror development flow. This provides natural boundaries and flow for both customers and developers, improving understanding. Existing documentation maps well to this space. The categories are: Code, Build, Test, Deploy.

- Markdown files are sufficient for current efforts. GitHub pages provides a nice path for extending simple Markdown support, adding options and complexity. These options may be added as needed.

- The Arcade Project is itself a product, though it may also be viewed as a collection of smaller products. As such, Arcade also benefits from having product documentation.

## Writing documentation

Documentation is centered around a product. Product documentation contains all knowledge necessary for a user to be successful. They may be a single page or many as best fits the needs. Documentation spanning multiple documents should be anchored from a single starting document, perhaps as a table of contents or integrated with the Overview section.

Documentation for a product may be stored in multiple places. For example, a User's Manual may reside in a Azure Repo repository alongside the code while the design documents live in a Word document on SharePoint. In this case it is important that documentation be anchored in a single starting document.

Topics typical for product documentation include:

  - An *overview* describing the need the tool fulfills and how it does this.
  - *Requirements* and specifications for the product's execution and operation
  - *Architecture* and design details about the product's construction
  - *Concepts* or in-depth explanations fundamental to understanding and use
  - *How-to guides* or detailed explanation of the product's use
  - *Examples* or samples illustrating the use of the product
  - *Reference* information or detailed product APIs, CLI commands

An ideal documentation set would contain all of these items at an appropriate level of verbosity. When this is not possible, remember Customer Obsessed: Include information necessary to ensure a successful customer experience.

Be mindful of the differences between documentation intended for users of a product and documentation intended for developers of a product. Keeping the two separate can improve a user's experience, and providing design information to users can improve a user's self-sufficiency.

## Resources for writing

Some information to guide writers.

- [The 5 Writing Principles](https://aka.ms/writingprinciples)
- [Microsoft Writing Style Guide](https://aka.ms/style)
