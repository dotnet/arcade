# `eng/common` is managed by Arcade

Files under `eng/common` are managed in the [`dotnet/arcade`](https://github.com/dotnet/arcade) repository and syndicated to other repositories.

Do not edit files in this directory directly in downstream repositories. Those changes are likely to be overwritten the next time `eng/common` is updated.

If you need to change something in `eng/common`, make the change in the Arcade repository and ensure it is backported there so the update can flow correctly.

For more information about Arcade, see the [Arcade documentation](https://github.com/dotnet/arcade/tree/main/Documentation).
