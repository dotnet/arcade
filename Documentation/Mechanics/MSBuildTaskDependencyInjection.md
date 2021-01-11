# Purpose of MSBuildTaskBase Class

In an effort to make MSBuild Tasks more unit-testable, we are introducing this abstract base class to provide dependency injection support for the MSBuild Tasks classes. Enabling dependency injection will allow us to inject mock dependencies into the MSBuild Tasks classes for a better unit testing experience. 

In an effort to provide dependency injection for the MSBuild Tasks in Arcade, we wanted to have a consistent way in implementing dependency injection for the `Microsoft.Build.Utilities.Task` type. In a web service or command line application, the entry point into the program serves as a place to configure the dependency injection service collections and provider. Since there isn't an entry point like that for MSBuild Tasks, we have created an abstract class to handle the common elements of configuration. 

MSBuildTaskBase class can be found [here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.Arcade.Common/MSBuildTaskBase.cs).

For more reading about dependency injection: 
- [Architectural Principles: Dependency Injection](https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles#dependency-inversion)
- [Dependency Injection in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0)
- [Dependency Injection: Constructor Injection Behavior](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#constructor-injection-behavior)
- [PluralSight course: Dependency Injection in ASP.NET Core](https://www.pluralsight.com/courses/aspdotnet-core-dependency-injection)

## How to Implement in an MSBuild Task

1. In your MSBuild Task class, inherit the `MSBuildTaskBase` class found the in `Microsoft.Arcade.Common` project. 

```csharp
public class TaskClassName : MSBuildTaskBase
{
    ...
}
```

2. Implement the following two methods: `ExecuteTask` and `ConfigureServices`. 

    - `ExecuteTask` replaces the `Execute` method from Microsoft.Build.Utilities.Task object. Any functionality you would include in the `Execute` method should go into `ExecuteTask` instead. 
    - `ConfigureServices` should contain the configuration for the Service Collection that will resolve the dependencies.

3. Similarly to how interfaces are included into a constructor for a class that will have its dependencies injected into it, include the interfaces of the dependencies for the Task into `ExecuteTask` parameters. 

```csharp
public bool ExecuteTask(IThing thing, IFoo foo, IBar bar)
{ 
    ...
}
```

4. Similarly to how a service collection would be configured to resolve dependencies, `ConfigureServices` should contain the same kind of configuration. Ensure that you have configured all dependencies, 

```csharp
public override void ConfigureServices(IServiceCollection collection)
{
    collection.TryAddSingleton<IThing, ThingClass>();
    collection.TryAddSingleton<IFoo, FooClass>();
    ...
}
```

## How to Use with Tests

### Validating Dependency Injection Configuration

To ensure that the service collection has been properly configured, you should include a test to validate the registration of the dependencies. The following is an example of that kind of test: 

```csharp
[Fact]
public void AreDependenciesRegistered()
{
    MSBuildTaskClass task = new MSBuildTaskClass();

    var collection = new ServiceCollection();
    task.ConfigureServices(collection);
    var provider = collection.BuildServiceProvider();

    foreach(var dependency in task.GetExecuteParameterTypes())
    {
        var service = provider.GetRequiredService(dependency);
        service.Should().NotBeNull();
    }

    DependencyInjectionValidation.IsDependencyResolutionCoherent(
            s =>
            {
                task.ConfigureServices(s);
            },
            out string message
        )
        .Should()
        .BeTrue(message);
}
```

This test uses a helper class that is provided in the `Microsoft.DotNet.Internal.DependencyInjection.Testing` project. 

### Mocking Services for Task Unit Tests

1. Create the mocks for the services to be injected into the task, and add them to a new service collection to be used in the test. 

```csharp
var collection = new ServiceCollection()
    .AddSingleton(thingMock.Object)
    .AddSingleton(fooMock.Object);
```

2. Pass that collection into the Task's `ConfigureServices` method. If the `ConfigureServices` method registers dependencies using TryAdds, it shouldn't add conflicting dependency registrations into the collection. 

```csharp
MSBuildTaskClass task = new MSBuildTaskClass();
task.ConfigureServices(collection);
```

3. Build the service provider and pass it into the method that will resolve the dependencies and invoke the `ExecuteTask` method.

```csharp
using var provider = collection.BuildServiceProvider();
bool result = task.InvokeExecute(provider);
```

## What's Happening Behind the Scenes

The `MSBuildTaskBase` class inherits `Microsoft.Build.Utilities.Task`, so it implements the `Execute` method that MSBuild Task classes implement. It uses reflection to accomplish resolving the services defined in the concrete class' `ExecuteTask` method. 

The Task's `Execute` is traditionally the method that is called when running the task. It is required to be implemented when inheriting the `Microsoft.Build.Utilities.Task` class. 

The `Execute` method serves as the Task's entry point, that handles the registration of dependencies that are defined in the concrete implementation of the `MSBuildTaskBase` class. In this method, a new `ServiceCollection` is created. It is passed into the `ConfigureServices` method to be populated with the configuration defined in the concrete implementation. Then, it builds the service provider that is passed into the `InvokeExecute` method. 

```csharp
public override sealed bool Execute()
{
    ServiceCollection collection = new ServiceCollection();
    ConfigureServices(collection);
    using (var provider = collection.BuildServiceProvider())
    {
        return InvokeExecute(provider);
    }
}
```

`MSBuildTaskBase` contains a virtual implementation of `ConfigureServices` that configures common dependencies, such as `IFileSystem` (if you want to abstract the File and Directory classes so they can be mocked out for tests) and `TaskLoggingHelper` object called `Log`. It is expected that this method will be overridden. 

```csharp
public virtual void ConfigureServices(IServiceCollection collection)
{
    collection.TryAddSingleton<IFileSystem, FileSystem>();
    collection.TryAddSingleton(Log);
}
```

`InvokeExecute` is where the reflection magic happens. It looks up the `ExecuteTask` method on the concrete implementation, looks up the parameters (the dependencies) that are defined in the method, and resolves the dependencies with the provider that was configured and built. Then it passes along the required dependency implementations into the method when it ultimately invokes it. 

```csharp
public bool InvokeExecute(ServiceProvider provider)
{
    return (bool)GetExecuteMethod().Invoke(this, GetExecuteArguments(provider));
}
```