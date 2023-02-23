# BFlatA
A building script generator or wrapper for recusively building .csproj file with depending Nuget packages &amp; embedded resources for BFlat, a native C# compiler ([github.com/bflattened/bflat](https://github.com/bflattened/bflat))

This program is relevent to an issue from bflat: https://github.com/bflattened/bflat/issues/61

##  Usage:

     Usage: bflata [build] <csprojectfile> [options]
    
      build                                         Build with BFlat in %Path%, if present,ignores -sm arg; if omitted,generate build script only while -bm arg still effective.
      <csprojectfile>                               Only one project is allowed here, other files will be passed to BFlat.
    
    Options:
      -pr|--packageroot:<path to package storage>   eg.C:\Users\%username%\.nuget\packages or $HOME/.nuget/packages .
      -fx|--framework:<moniker>                     the TFM(Target Framework Moniker),such as 'net7.0' or 'netstandard2.1' etc. usually lowercase.
      -bm|--buildmode:<flat|tree>                   flat=flatten reference project trees to one and build;tree=build each project alone and reference'em accordingly with -r option.
      -sm|--scriptmode:<cmd|sh>                     Windows Batch file(.cmd) or Linux .sh file.
      -t|--target:<Exe|Shared|WinExe>               Build Target, this arg will also be passed to BFlat.

    Note:
      Any other args will be passed 'as is' to bflat.
      BflatA uses '-arg:value' style only, '-arg value' is not supported, though args passing to bflat are not subject to this rule.
      Only the first existing file would be processed as .csproj file.      

    Examples:
      bflata xxxx.csproj -pr:C:\Users\username\.nuget\packages -fx=net7.0 -sm:bat -bm:tree  <- only generate BAT script which builds project tree orderly.
      bflata build xxxx.csproj -pr:C:\Users\username\.nuget\packages -sm:bat --arch x64  <- build and generate BAT script,and '--arch x64' r passed to BFlat.

## Compile:
 Since Bflata is a very tiny program that you can use any of following compilers to build it:
- BFlat [github.com/bflattened/bflat](https://github.com/bflattened/bflat) 
- Dotnet C# compiler
- Mono C# compiler

of course BFlat is prefered to build the program entirely to native code(without IL at all):

## Known issues:

- So far, the "--buildmode:tree" option doesn't work for BFlat, for BFlat are not known to support compiling native libs which can be served in the -r option(i.e. u can only refer to assemblies built by dotnet compiler), but BFlatA will still generate the building script and reference project outputs accordingly.The expected scene had been that each referenced projects being compiled with BFlat separatedly in their respective depending hierachy and referenced accordingly till being compiled into one executable.
- The "--buildMode:flat" option(default, if -bm not served) can generate flattened building script which incorproate all code files, package references and resources of all referenced .csproj files into one, but this solution cannot solve the issue of version incompatibility of dependencies from different projects, especially secondary dependencies required by Nuget packages. Since version consistency of projects can be eliminated by making all projects reference the same packages of the same version, but secondary dependencies among precompiled packages are various and not changeable.
- Some but not all dependencies referenced by nuget packages starts with "System.*" might have already been included in runtime, which is enabled with "bflat --stdlib Dotnet" option, and have been all excluded from the BFlata generated building script (for most scenario), otherwise you may see error CS0433 as below:

> error CS0433: The type 'MD5' exists in both
> 'System.Security.Cryptography.Algorithms, Version=4.2.1.0,
> Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' and
> 'System.Security.Cryptography, Version=7.0.0.0, Culture=neutral,
> PublicKeyToken=b03f5f7f11d50a3a'

but not all lib starts with "System." shall be excluded, such as System.CodDom.dll, which is used by BenchmarkDotNet, and this problem would possibly also be better solved in the future by an exclusion file.
	
- Parsing resources in .resx file is not implemented yet, for lacking of knowledge of how BFlat handles resources described in .resx file.
## Demo project

[ObjectPoolReuseCaseDemo](https://github.com/xiaoyuvax/ObjectPoolReuseCaseDemo) is a simple C# project with one Project Reference and one Nuget Package reference together with several secondary dependencies, and is a typical scenario for demonstrating how BFlata works with BFlat.

> Note:It is important to disable `<ImplicitUsings>` in .csproj file,
> and make sure all necessary namespaces are imported, especially `using
> System;` if you are building with `--stdlib Dotnet` option of bflat.

Following command build the project in default Flat mode(i.e. code files and references of underlying projects are to be merged and built together), and a 'build.cmd' script file will also be genreated (always for Flat mode, for it's a workaround to solve the 'arguments too long' problem) .

	bflata build ObjectPoolDemo.csproj -pr:C:\Users\%username%\.nuget\packages -fx:net7.0 --stdlib Dotnet

output:

    BFlatA V1.0.0.1 (github.com/xiaoyuvax/bflata)
    Description:
      A building script generator or wrapper for recusively building .csproj file with depending Nuget packages & embedded resources for BFlat, a native C# compiler(flattened.net).
    
    Caching Nuget packages from path:C:\Users\Administrator.SP\.nuget\packages ...
    Libs found:7916/Folders searched:30489
    Found 7916 nuget packages!
    
    Parsing Project:D:\Repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\ObjectPoolDemo\ObjectPoolDemo.csproj ...
    Parsing Project:D:\Repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\Wima.Log.csproj ...
    Found 9 code files(*.cs)
    Found 17 dependent libs(*.dll)
    Appending Script:Wima.Log...
    Found 9 code files(*.cs)
    Found 17 dependent libs(*.dll)
    Appending Script:ObjectPoolDemo...
    
    Found 2 args to be passed to BFlat.
    Found 9 code files(*.cs)
    Found 17 dependent libs(*.dll)
    Scripting ObjectPoolDemo...
    Script sucessfully written!
    Building in Flat mode:ObjectPoolDemo...
    Executing building script...
    .\ObjectPoolDemo\WMLogService.Core\WimaLoggerBase.cs(358): Trim analysis warning IL2026: Wima.Log.WimaLoggerBase.<>c.<WriteInternal>b__130_0(StackFrame): Using member 'System.Diagnostics.StackFrame.GetMethod()' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Metadata for the method might be incomplete or removed.
    C:\_oss\common-logging\src\Common.Logging.Portable\Logging\LogManager.cs(567): Trim analysis warning IL2072: Common.Logging.LogManager.<>c__DisplayClass35_0.<BuildLoggerFactoryAdapterFromLogSettings>b__0(): 'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to 'System.Activator.CreateInstance(Type,Object[])'. The return value of method 'Common.Logging.Configuration.LogSetting.FactoryAdapterType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
    C:\_oss\common-logging\src\Common.Logging.Portable\Logging\LogManager.cs(571): Trim analysis warning IL2072: Common.Logging.LogManager.<>c__DisplayClass35_0.<BuildLoggerFactoryAdapterFromLogSettings>b__0(): 'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in call to 'System.Activator.CreateInstance(Type)'. The return value of method 'Common.Logging.Configuration.LogSetting.FactoryAdapterType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.

and following is the content of the building script generated above:

    @SET PR=C:\Users\administrator\.nuget\packages
    @bflat build --target Exe --stdlib Dotnet ^
    ..\..\..\..\ObjectPoolDemo\ObjectPoolDemo\ObjectPoolDemo.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\ESSevice.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\IndexableDoc.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\LogLine.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\WimaLogger.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\WimaLoggerBase.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\WimaLoggerConfiguration.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\WimaLoggerExtensions.cs ^
    ..\..\..\..\ObjectPoolDemo\WMLogService.Core\WimaLoggerProvider.cs ^
    -r %PR%\common.logging.core\3.4.1\lib\netstandard1.0\Common.Logging.Core.dll ^
    -r %PR%\common.logging\3.4.1\lib\netstandard1.3\Common.Logging.dll ^
    -r %PR%\elasticsearch.net\7.17.5\lib\netstandard2.1\Elasticsearch.Net.dll ^
    -r %PR%\microsoft.csharp\4.6.0\lib\netstandard2.0\Microsoft.CSharp.dll ^
    -r %PR%\microsoft.extensions.configuration.abstractions\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Configuration.Abstractions.dll ^
    -r %PR%\microsoft.extensions.configuration.binder\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Configuration.Binder.dll ^
    -r %PR%\microsoft.extensions.configuration\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Configuration.dll ^
    -r %PR%\microsoft.extensions.dependencyinjection.abstractions\7.0.0\lib\netstandard2.1\Microsoft.Extensions.DependencyInjection.Abstractions.dll ^
    -r %PR%\microsoft.extensions.dependencyinjection\7.0.0\lib\netstandard2.1\Microsoft.Extensions.DependencyInjection.dll ^
    -r %PR%\microsoft.extensions.logging.abstractions\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll ^
    -r %PR%\microsoft.extensions.logging.configuration\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Configuration.dll ^
    -r %PR%\microsoft.extensions.logging\7.0.0\lib\netstandard2.1\Microsoft.Extensions.Logging.dll ^
    -r %PR%\microsoft.extensions.objectpool\7.0.3\lib\netstandard2.0\Microsoft.Extensions.ObjectPool.dll ^
    -r %PR%\microsoft.extensions.options.configurationextensions\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Options.ConfigurationExtensions.dll ^
    -r %PR%\microsoft.extensions.options\7.0.0\lib\netstandard2.1\Microsoft.Extensions.Options.dll ^
    -r %PR%\microsoft.extensions.primitives\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Primitives.dll ^
    -r %PR%\nest\7.17.5\lib\netstandard2.0\Nest.dll
