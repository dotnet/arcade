# Infrastructure Ask & Tell Mode

Infrastructure changes are handled a bit differently from the product because they don't actually ship.  
The main goal is to ensure that the product can still be built and shipped.

| Product "mode" | Branch Infra     | Shared Infra     | Notes                                              |
| ---------------| -----------------| ---------------- |----------------------------------------------------|
| Open           | Open             | Open             | Changes are handled according to normal policy     |
| Tell           | Tell --> Prod    | Tell --> Arcade  | Product branch changes are handled by the product team, and shared infra updates reviewed by the larger working group |
| Ask            | Tell --> Tactics | Tell --> Tactics | Tactics needs to know when in ask mode.  Holding off on 'ask mode' to Arcade for now  |
| Stabilization  | Ask --> Tactics  | Ask --> Tactics  | Changes when trying to get a coherent build can be very destabilizing  |

## Definitions:
- "Branch Infra": Infrastructure changes in the product branch
- "Shared Infra": Infrastructure changes in shared infra like Arcade, Helix, etc.
- "Tell Mode" for each product teams will follow their own process
- "Tell Mode" to Arcade means emailing the Arcade Working Group at arcadewg@microsoft.com.  We'll keep this lightweight for now.
- "Ask Mode" to tactics handled according the guidance managed by Lee Coward

## Servicing
There are a few things to keep in mind with servicing:
- Shared infra servicing workflow and policies are [found here](ArcadeServicing.md). 
- Servicing release are basically in perpetual 'Ask' and 'Stabilization' mode.