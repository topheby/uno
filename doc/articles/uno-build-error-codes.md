---
uid: Build.Solution.error-codes
---
# Uno error codes

## MSBuild Errors

### UNOB0001: Cannot build with both Uno.WinUI and Uno.UI NuGet packages referenced

This error code means that a project has determined what both `Uno.WinUI` and `Uno.UI` packages are referenced.

To fix this issue, you may be explicitly referencing `Uno.UI` and `Uno.WinUI` in your `csproj`, or you may be referencing a NuGet package that is incompatible with your current project's configuration.

For instance, if your project references `Uno.WinUI`, and you try to reference `SkiaSharp.View.Uno`, you will get this error. To fix it, you'll need to reference `SkiaSharp.View.Uno.WinUI` instead.

### UNOB0002: Project XX contains a reference to Uno Platform but does not contain a WinAppSDK compatible target framework

This error code means that a WinAppSDK project is referencing a project in your solution which is not providing a `net7.0-windows10.xx` TargetFramework.

This can happen if a project contains only a `net7.0` TargetFramework and has a NuGet reference to `Uno.WinUI`.

To fix this, it is best to start from a `Cross Platform Library` project template provided by the Uno Platform [visual studio extension](xref:Guide.HowTo.Create-Control-Library), or using [`dotnet new`](xref:Uno.GetStarted.dotnet-new).

### UNOB0003: Ignoring resource XX, could not determine the language

This error may occur during resources (`.resw`) analysis if the framework does not recognize the specified language code.

For instance, the language code `zh-CN` is not recognized and `zh-Hans` should be used instead.

### UNOB0004: The $(UnoVersion) property must match the version of the Uno.Sdk defined in global.json

The build process has determined that an MSBuild property was defined to override `UnoVersion`. This property is defined by the Uno.Sdk and cannot be changed and must be updated through the `global.json` file

Follow this guide in order to [update the Uno Platform packages](xref:Uno.Development.UpgradeUnoNuget).

### UNOB0005: The Version of Uno.WinUI must match the version of the Uno.Sdk found in global.json

The build process has determined that the version of the Uno.WinUI NuGet package does not match the Uno.Sdk package version. This generally happens when the Uno.WinUI.* packages are updated through Visual Studio's NuGet Package manager.

Follow this guide in order to [update the Uno Platform packages](xref:Uno.Development.UpgradeUnoNuget).

### UNOB0006: The UnoFeature 'XX' was selected, but the property XXXVersion was not set

The build process has determined that you have specified an UnoFeature that requires a version to be set for a Package group such as Uno.Extensions, Uno.Toolkit.WinUI, Uno Themes, or C# Markup. Update your `csproj` file or `Directory.Build.props` with the property specified in the error along with the version that you would like to pin your build to.

### UNOB0007: AOT compilation is only supported in Release mode. Please set the 'Optimize' property to 'true' in the project file

The build process has detected that you have set the value `UnoGenerateAotProfile` to true for a build configuration that has not optimized the assembly (i.e. Debug build Configuration). You should only generate an AOT profile from a build Configuration that will optimize the assembly such as the Release build Configuration.

### UNOB0008: Building a WinUI class library with dotnet build is not supported

Building a `net8.0-windows10.x.x` class library using `dotnet build` is not supported at this time because of a [Windows App SDK issue](https://github.com/microsoft/WindowsAppSDK/issues/3548), when the library contains XAML files.

To work around this, use `msbuild /r` on Windows. You can build using `msbuild` with a **Developer Command Prompt for VS 2022**, or by using `vswhere` or using [GitHub actions scripts](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/ci-for-winui3?pivots=winui3-packaged-csharp) in a CI environment.

This particular check can be disabled by setting the msbuild property `UnoDisableValidateWinAppSDK3548` to `true`.

### UNOB0009: Uno Platform Implicit Package references are enabled

When Uno Implicit Package References are enabled you do not need to provide an explicit PackageReference for the Packages listed in the build message. You should open the csproj file, find and remove the `<PackageReference Include="{Package name}" />` as listed in the build message.

Alternatively you may disable the Implicit Package References

```xml
<PropertyGroup>
  <DisableImplicitUnoPackages>true</DisableImplicitUnoPackages>
</PropertyGroup>
```

## Compiler Errors

### UNO0001

A member is not implemented, see [this page](xref:Uno.Development.NotImplemented) for more details.

### UNO0002

**Do not call Dispose() on XXX**

On iOS and Catalyst, calling `Dispose()` or `Dispose(bool)` on a type inheriting directly from `UIKit.UIView` can lead to unstable results. It is not needed to call `Dispose` as the runtime will do so automatically during garbage collection.

Invocations to `Dispose` can cause the application to crash in `__NSObject_Disposer drain`, cause `ObjectDisposedException` exception to be thrown. More information can be found in [xamarin/xamarin-macios#19493](https://github.com/xamarin/xamarin-macios/issues/19493).
