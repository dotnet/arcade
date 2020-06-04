Part of the TargetFramework SDK is the binplacing infrastructure which is described below. The TargetFramework SDK infrastructure itself isn't yet documented.

## BinPlacing

Supports copying to additional paths based on which `TargetFramework` among `TargetFrameworks` is best for a given `BinplaceTargetFramework`

### BinPlacing Items

- BinPlaceItem
    - Typically computed by the BinPlacing targets to determine what assets to binplace.
    - Identity: source of file to binplace.  For example: the built output dll, pdb, content files, etc.
    - Metadata:
        - TargetPath: when specified can indicate the relative path, including filename, to place the item.

- BinPlaceTargetFramework
    - Typically specified by the repository to control the output directories of projects.
    - Identity: `TargetFramework` to binplace for. A `BinPlaceTargetFramework` is *active* if it is the best `TargetFramework` for the currently building project `TargetFramework`. When active it's behavior is defined by the metaadata below.
    - Metadata:
        - RefPath: directory to copy `BinPlaceItem`s when `BinPlaceRef` is set to true.
        - RuntimePath: directory to copy `BinPlaceItem`s when `BinPlaceRuntime` is set to true.
        - TestPath: directory to copy `BinPlaceItem`s when `BinPlaceTest` is set to true.
        - PackageFileRefPath: directory to write props file containing `BinPlaceItem`s when `BinPlaceRef` is set to true.
        - PackageFileRuntimePath: directory to write props file containing `BinPlaceItem`s when `BinPlaceRuntime` is set to true.
        - ItemName: An item name to use instead of `BinPlaceItem` for the source of items for this `BinPlaceTargetFramework`.
        - SetProperties: Name=Value pairs of properties that should be set.

## BinPlacing Properties
- BinPlaceRef: When set to true `BinPlaceItem`s are copied to the `RefPath` of active `BinPlaceTargetFramework`s.  Props are written to the `PackageFileRefPath` directory.
- BinPlaceRuntime: When set to true `BinPlaceItem`s are copied to the `RuntimePath` of active `BinPlaceTargetFramework`s.  Props are written to the `PackageFileRuntimePath` directory.
- BinPlaceTest:  When set to true `BinPlaceItem`s are copied to the `TestPath` of active `BinPlaceTargetFramework`s.
