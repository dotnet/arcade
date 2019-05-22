# Microsoft.DotNet.Build.Tasks.Configuration

## Configuration system

This package implements a cross-targeting configuration system that permits overloading/encoding additional configuration parameters in the 'Configuration' property.

This configuration system differs from that provided by the .NET SDK in a couple distinct ways:
1. The behavior of a project in pre-SDK projects was to build a *single* default configuration.  The behavior of SDK projects is to build *all* configurations.  The behavior of this system is to build the *best* configuration of `BuildConfigurations` for the current `BuildConfiguration`.
2. The behavior of a project reference in pre-SDK projects was to build the *same* configuration as the referencing project (or *specific* configuration in case of an SLN).  The behavior of SDK projects is to build the *most-compatible TargetFramework*, where compatibility mappings are known by NuGet and not extensible.  The behavior of this system is to build the *most-compatible Configuration* where the compatibility mappings are provided by the repository.

Extends "traversal" builds: projects which use `@(Project)` items to refer to projects which need to be built.  These are extended by acting as outer builds for the projects listed and selecting the subset of configurations in those projects to build.

### Configuration Items
The following items must be defined in consuming projects:

 - Property
    - List of properties in order of precedence.
    - Identity: name of the property, EG: OSGroup
    - Metadata:
        - DefaultValue: default value for the property.  Default values may be omitted from configuration strings. EG: AnyOS
        - Precedence: integer indicating selection precedence.
        - Order: integer indicating configuration string ordering.

 - PropertyValue
    - Known values and their relation to other values of the same property.
    - Identity: value of a property
    - Metadata: 
        - Property: Name of property to which this value applies
        - Imports: List of other property values to consider, in breadth first order, after this value.
        - CompatibleWith: List of additional property values to consider, after all imports have been considered.
        - Each value will independently undergo a breadth-first traversal of imports.
        - Other values: Properties to be set when this configuration property is set.  All other metadata not listed here will be treated in this manner.

### Configuration Properties
The following properties must be defined in consuming projects:
 - BuildConfigurationFolder: the folder under which generated props files will be emitted.

The following properties may be defined:
 - BuildConfiguration: the currently targeted configuration for a build, ignored when BuildAllConfigurations is set or Configuration is already set.
 - BuildAllConfigurations: set to true in order to build all configurations for a project when a specific Configuration hasn't already been set.
 - BuildConfigurations: the relevant configurations of a project, analogous to TargetFrameworks
 - PackageConfigurations: typically a subset of BuildConfigurations that is used when building a packaging project and behaves in a similar manner.
 - Both BuildConfiruations and PackageConfigurations should be defined in a `Configurations.props` file next to the project itself.  Why? Because these must be defined *before* the props which parse out configuration statically for scenario 1 above.  We could avoid this if we instead always had an outer build when that could dispatch to an inner Build/Clean/ReBuild/etc target.
 - AdditionalBuildConfigurations: build the best configuration for additional build configurations.  Only honored in traversal builds.  It creates a middle-ground between BuildConfiguration (build single best for one configuration) and BuildAllConfigurations.

### Configuration Targets

 - GenerateConfigurationProps: should be called once for the repository to generate configuration parsing props files.
 - BuildAll / BuildAllConfigurations: builds all configurations in BuildConfigurations rather than just the best configuration.
 - RebuildAll / RebuildAllConfigurations: rebuilds all configurations in BuildConfigurations rather than just the best configuration.
 - CleanAll / CleanAllConfigurations: cleans all configurations in BuildConfigurations rather than just the best configuration.

## BinPlacing

Supports copying to additional paths based on which configuration among `BuildConfigurations` is best for a given `BinplaceConfiguration`

### BinPlacing Items

- BinPlaceItem
    - Typically comuputed by the BinPlacing targets to determine what assets to binplace.
    - Identity: source of file to binplace.  For example: the built output dll, pdb, content files, etc.
    - Metadata:
        - TargetPath: when specified can indicate the relative path, including filename, to place the item.

- BinPlaceConfiguration
    - Typically specified by the repository to control the output directories of projects.
    - Identity: configuration to binplace for.  A binplace configuration is *active* if it is the best configuration for the currently building project configuration.  When active it's behavior is defined by the meatadata below.
    - Metadata:
        - RefPath: directory to copy `BinPlaceItem`s when `BinPlaceRef` is set to true.
        - RuntimePath: directory to copy `BinPlaceItem`s when `BinPlaceRuntime` is set to true.
        - TestPath: directory to copy `BinPlaceItem`s when `BinPlaceTest` is set to true.
        - PackageFileRefPath: directory to write props file containing `BinPlaceItem`s when `BinPlaceRef` is set to true.
        - PackageFileRuntimePath: directory to write props file containing `BinPlaceItem`s when `BinPlaceRuntime` is set to true.
        - ItemName: An item name to use instead of `BinPlaceItem` for the source of items for this `BinPlaceConfiguration`.
        - SetProperties: Name=Value pairs of properties that should be set.

## BinPlacing Properties
- BinPlaceRef: When set to true `BinPlaceItem`s are copied to the `RefPath` of active `BinPlaceConfiguration`s.  Props are written to the `PackageFileRefPath` directory.
- BinPlaceRuntime: When set to true `BinPlaceItem`s are copied to the `RuntimePath` of active `BinPlaceConfiguration`s.  Props are written to the `PackageFileRuntimePath` directory.
- BinPlaceTest:  When set to true `BinPlaceItem`s are copied to the `TestPath` of active `BinPlaceConfiguration`s.
