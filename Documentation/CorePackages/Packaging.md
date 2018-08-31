# Arcade SDK Packaging

Currently, the Arcade SDK does not provide any custom packaging logic. It makes use and ***strongly recommend*** that all onboarded repos use the vanilla .NET SDK `dotnet pack` target.

Some repos (e.g., CoreFX and ASPNet) have mentioned that in some cases custom packaging logic might be needed. Discussion around this is currently in progress [here](https://github.com/dotnet/arcade/issues/383) and the eventual work will be tracked on this [Epic](https://github.com/dotnet/arcade/issues/578).
