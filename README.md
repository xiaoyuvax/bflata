


# BFlatA

`BFlatA Purpose: Write your program in VS, and Build it Flat` (with [bflat](https://github.com/bflattened/bflat))

BFlatA is a wrapper/building script generator for BFlat, a native C# compiler, for recusively building .csproj file with:
 - Referenced projects
 - Nuget package dependencies
 - Embedded resources  

  You can find BFlat, the native C# compiler at [flattened.net](https://flattened.net).
  
  BFlata is relevent to an issue from bflat: https://github.com/bflattened/bflat/issues/61

Update 23-03-15 (V1.2.1.1):
- Add support for NativeLibrary.

Update 23-03-03 (V1.2.0.0):
- Adding support compiling .resx files by calling resgen.exe, which u must specify with `--resgen`. Namespace for each .resx file will be determined by checking corresponding code file. eg. myForm.cs for myForm.resx and Resources.Designer.cs for Resources.resx. But there might still be some problem running winform program with resources, as described in Known Issues below.

Update 23-02-26 (V1.1.0.0):
- Exclu mechanism added. Some (but not all) dependencies referenced by nuget packages with name starting with "System.*" might have already been included in runtime, which is enabled with `bflat --stdlib Dotnet` option, and must be excluded from the BFlata generated building script, otherwise you may see a lot of error CS0433 as below:

> error CS0433: The type 'MD5' exists in both
> 'System.Security.Cryptography.Algorithms, Version=4.2.1.0,
> Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' and
> 'System.Security.Cryptography, Version=7.0.0.0, Culture=neutral,
> PublicKeyToken=b03f5f7f11d50a3a'

BFlatA introduced a mechanism called "Exclu" to exclude packages from dependencies during scripting. Literally, Exclu means the Excluded Packages. BFlatA supports extracting names to be excluded from Dotnet runtime, more exactly, the specific Shared Frameworks such as Microsoft.NETCore.App(which is what BFlat incorprates so far), to generate ".exclu" file, which can be reused in later builds, by using the `-xx` option which uses the monker name specified by `-fx` to save/load Exclus from files accordingly, in addition to an always-load "packages.exclu" file, if exists, where you can put in custom Exclus.
I provided a `net7.0.exclu` file in the repository as the default Exclu for net7.0, which is the default target of both BFlata and BFlat by now, and you can copy it to ur working directory, if you don't want to extract Exclus by youself from Dotnet runtime(with -xx option).

- 'build-il' is allowed now. This building mode is consistent with that of BFlat, and it only affects root project. Dependent projects will always being built under TREE mode with 'build-il' on.
- A new Build Mode `-bm:treed` is introduced to provide a shortcut for `-bm:tree -dd`, simply a short.
- BFlat style args are supported now, u can use both space or ":" for evaluation of argument values.
- BFlat's `-o|--out<file>`  option is now hijacked by BFlatA as to be properly scripted in the generated script. At least, it will not be passed to every referenced projects.

Update 23-02-24: 
- Response file(.rsp) support added, and 'arguments too long' problem solved. The .rsp script will be taken as default building script format of BFlatA. You can use the generated build.rsp file like `bflat build @build.rsp` to build a FLATTENED project. 
Note: a single .rsp file itself does not support building project Tree, instead you may use `-st:bat` or `-st:sh` to generate script that supports building project trees, or build through BFlatA directly(with both `build` and `-bm:tree` option on).  
- Paths cache of Nuget packages are now saved to packages.cache in the working path, and will be reused at the next run, as to improve performance.
- A new `-dd` (Deposit Dependencies) option is introduced for compiling projects who uses references of child projects indirectly, and still offering certain extent of version consistency, where dependencies are added-up (deposited) along the hiearchy line and be accumulatively served to the parent project level by level (if any dependency version conflict upon merging with parent level, higher version will be retained, as to guarantee maximal version compatibility).


##  Usage:

	  Usage: bflata [build|build-il] <csproj file> [options]

	  [build|build-il]                              Build with BFlat in %Path%, with -st option ignored.
							If omitted, generate building script only, with -bm option still valid.

	  <.csproj file>                                Must be the 2nd arg if 'build' specified, or the 1st otherwise, only 1 project allowed.

	Options:
	  -pr|--packageroot:<path to package storage>   eg.'C:\Users\%username%\.nuget\packages' or '$HOME/.nuget/packages'.

	  -rp|--refpath:<any path to be related>        A reference path to generate paths for files in the building script,
							can be optimized to reduce path lengths.Default is '.' (current dir).

	  -fx|--framework:<moniker>                     The TFM compatible with the built-in .net runtime of BFlat(see 'bflat --info')
							mainly purposed for matching dependencies, e.g. 'net7.0'

	  -bm|--buildmode:<flat|tree|treed>             FLAT = flatten project tree to one for building;
							TREE = build each project alone and reference'em accordingly with -r option;
							TREED = '-bm:tree -dd'.

	  -st|--scripttype:<rsp|bat|sh>                 Response File(.rsp,default) or Windows Batch file(.cmd/.bat) or Linux Shell Script(.sh) file.

	  --resgen:<path to resgen.exe>                 Path to Resource Generator(e.g. ResGen.exe),which compiles .resx file to binary.


	Shared Options(will also be passed to BFlat):
	  --target:<Exe|Shared|WinExe>                  Build Target.default:by bflat

	  --os <Windows|Linux|Uefi>                     Build Target.default:Windows.

	  --arch <x64|arm64|x86|...>                    Platform archetecture.default:x64.

	  -o|--out:<File>                               Output file path for the root project.

	  --verbose                                     Enable verbose logging

	Obsolete Options:
	  -dd|--depdep                                  Deposit Dependencies mode, valid with '-bm:tree', equivalently '-bm:treed',
							where dependencies of child projects are deposited and served to parent project,
							as to fulfill any possible reference requirements

	  -xx|--exclufx:<dotnet Shared Framework path>  If path valid, lib exclus will be extracted from the path.
							e.g. 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\7.0.2'
							and extracted exclus will be saved to '<moniker>.exclu' for further use, where moniker is specified by -fx option.

							If this path not explicitly specified, BFlatA will search -pr:<path> and -fx:<framework> for Exclus,automatically.

	Note:
	  Any other args will be passed 'as is' to BFlat, except for '-o'.
	  For options, the ':' char can also be replaced with a space. e.g. -pr:<path> = -pr <path>.
	  Do make sure <ImplicitUsings> switched off in .csproj file and all namespaces properly imported.
	  The filename for the building script are one of 'build.rsp,build.cmd,build.sh' and the .rsp file allows larger arguments and is prefered.
	  Once '<moniker>.exclu' file is saved, you can use it for any later build, and a 'packages.exclu' is always loaded and can be used to store extra shared exclus, where 'exclu' is the short for 'Excluded Packages'.

	Examples:
	  bflata xxxx.csproj -pr:C:\Users\username\.nuget\packages -fx=net7.0 -st:bat -bm:treed  <- only generate BAT script which builds project tree orderly with Deposit Dependencies.
	  bflata build xxxx.csproj -pr:C:\Users\username\.nuget\packages -st:bat --arch x64  <- build in FLAT mode with default target at .net7.0 and '--arch x64' passed to BFlat while the option -st:bat ignored.
      

## Compile from source:
 Since Bflata is a very tiny program that you can use any of following compilers to build it:
- BFlat [github.com/bflattened/bflat](https://github.com/bflattened/bflat) 
- Dotnet C# compiler
- Mono C# compiler

of course BFlat is prefered to build the program entirely to native code(without IL at all):

## Known issues:

- The "--buildmode:tree" option builds by reference hierachy, but the referenced projects are actually built with 'bflat build-il' option, which produces IL assemblies rather than native code, and only the root project is to be built in native code. This is because so far BFlat is not known to produce native .dll lib which can be referenced with -r option (if it will do so someday, or known to be able to do so, the TREE mode would actually work for native code then). Note: TREE mode is useful dealing with the scenarios that Nuget package uses dependencies whose versions are not compatible with the parent project (in FLAT mode, as would cause errors), for the lib is compiled independently. Moreover with the `-dd`(Deposit Dependency) option, parent project who indirectly use child project's dependencies might also be solved. 
- The "--buildmode:flat" option (default, if -bm not served) generates flattened building script which incorproate all code files, package references and resources of all referenced .csproj files into one, but this solution cannot solve the issue of version incompatibility of dependencies from different projects, especially secondary dependencies required by Nuget packages. Version inconsistency of projects can be eliminated by making all projects reference the same packages of the same version, but secondary dependencies among precompiled packages are various and not changeable	
- Although current version support calling resgen.exe (specified by `--resgen` option) to compile .resx file to embeddable binary(.resources) and reference'em from temp directory at build-time and namespace for resources would be adopted from coressponding code files, eg. myForm.cs for myForm.resx and Resources.Designer.cs for Resources.resx, error message like below would still occur. It is probably due to bflat internal settings, as reported here: https://github.com/bflattened/bflat/issues/92

![image](https://user-images.githubusercontent.com/6511226/222661726-92f1afb7-ba9f-4e3b-8c7e-f25148119edc.png)


## Demo project

[ObjectPoolReuseCaseDemo](https://github.com/xiaoyuvax/ObjectPoolReuseCaseDemo) is a simple C# project with one Project Reference and one Nuget Package reference together with several secondary dependencies, and is a typical scenario for demonstrating how BFlata works with BFlat.

> Note:It is important to disable `<ImplicitUsings>` in .csproj file,
> and make sure all necessary namespaces are imported, especially `using
> System;` if you are building with `--stdlib Dotnet` option of bflat.

Following command build the project in the default FLAT mode(i.e. code files and references of underlying projects are to be merged and built together), and a buiding script file (default is `build.rsp`) will also be genreated.
You can also build the demo project in TREE mode, with `-bm:tree` option, but an additional `-dd` option would be required to build this demo(while not all projects need it). The `-dd` option is required in case a project somehow misses dependencies from referenced child projects, which is not directly referenced in the .csproj file), and is only valid when `-bm:tree` option is used. With presence of this option, dependencies of referenced child projects would be added up(deposited) and served accumulatively to the parent project during building process. 

	bflata build ObjectPoolDemo.csproj -pr:C:\Users\%username%\.nuget\packages -fx:net7.0 --stdlib Dotnet 

output:

    BFlatA V1.1.0.0 @github.com/xiaoyuvax/bflata
    Description:
      A wrapper/building script generator for BFlat, a native C# compiler, for recusively building .csproj file with:
        - Referenced projects
        - Nuget package dependencies
        - Embedded resources
      Before using BFlatA, you should get BFlat first at https://flattened.net.
    
    
    --ARGS--------------------------------
    Build:On
    DepositDep:On
    BuildMode:Flat
    ScriptMode:RSP
    TargetFx:net7.0
    PackageRoot:C:\Users\xiaoyu\.nuget\packages
    RefPath:C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\BFlatA\bin\Debug\net7.0
    Target:Exe
    
    --LIB EXCLU---------------------------
    Exclu file found:.\net7.0.exclu
    Exclus loaded:166
    
    --LIB CACHE---------------------------
    Package cache found!
    Libs loaded:1609
    
    --BUILDING----------------------------
    Parsing Project:C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\ObjectPoolDemo\ObjectPoolDemo.csproj ...
    Parsing Project:C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\Wima.Log.csproj ...
    
    Generating script:ObjectPoolDemo
    - Found 9 code files(*.cs)
    - Found 16 dependent libs(*.dll)
    Writing script:ObjectPoolDemo...
    Script written!
    
    Building in FLAT mode:ObjectPoolDemo...
    - Executing building script: bflat build @build.rsp...
    --END---------------------------------
    C:\Users\Xiaoyu\source\ObjectPoolDemo\WMLogService.Core\WimaLoggerBase.cs(358): Trim analysis warning IL2026: Wima.Log.WimaLoggerBase.<>c.<WriteInternal>b__130_0(StackFrame): Using member 'System.Diagnostics.StackFrame.GetMethod()' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Metadata for the method might be incomplete or removed.
    C:\_oss\common-logging\src\Common.Logging.Portable\Logging\LogManager.cs(567): Trim analysis warning IL2072: Common.Logging.LogManager.<>c__DisplayClass35_0.<BuildLoggerFactoryAdapterFromLogSettings>b__0(): 'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to 'System.Activator.CreateInstance(Type,Object[])'. The return value of method 'Common.Logging.Configuration.LogSetting.FactoryAdapterType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
    C:\_oss\common-logging\src\Common.Logging.Portable\Logging\LogManager.cs(571): Trim analysis warning IL2072: Common.Logging.LogManager.<>c__DisplayClass35_0.<BuildLoggerFactoryAdapterFromLogSettings>b__0(): 'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in call to 'System.Activator.CreateInstance(Type)'. The return value of method 'Common.Logging.Configuration.LogSetting.FactoryAdapterType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.

and following is the content of the building script (Response File) generated above:

    --target Exe 
    -stdlib Dotnet
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\ObjectPoolDemo\ObjectPoolDemo.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\ESSevice.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\IndexableDoc.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\LogLine.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\WimaLogger.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\WimaLoggerBase.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\WimaLoggerConfiguration.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\WimaLoggerExtensions.cs
    C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\WimaLoggerProvider.cs
    -r C:\Users\xiaoyu\.nuget\packages\common.logging.core\3.4.1\lib\netstandard1.0\Common.Logging.Core.dll
    -r C:\Users\xiaoyu\.nuget\packages\common.logging\3.4.1\lib\netstandard1.3\Common.Logging.dll
    -r C:\Users\xiaoyu\.nuget\packages\elasticsearch.net\7.17.5\lib\netstandard2.1\Elasticsearch.Net.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.configuration.abstractions\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Configuration.Abstractions.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.configuration.binder\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Configuration.Binder.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.configuration\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Configuration.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.dependencyinjection.abstractions\7.0.0\lib\netstandard2.1\Microsoft.Extensions.DependencyInjection.Abstractions.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.dependencyinjection\7.0.0\lib\netstandard2.1\Microsoft.Extensions.DependencyInjection.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.logging.abstractions\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.logging.configuration\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Configuration.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.logging\7.0.0\lib\netstandard2.1\Microsoft.Extensions.Logging.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.objectpool\7.0.3\lib\netstandard2.0\Microsoft.Extensions.ObjectPool.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.options.configurationextensions\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Options.ConfigurationExtensions.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.options\7.0.0\lib\netstandard2.1\Microsoft.Extensions.Options.dll
    -r C:\Users\xiaoyu\.nuget\packages\microsoft.extensions.primitives\7.0.0\lib\netstandard2.0\Microsoft.Extensions.Primitives.dll
    -r C:\Users\xiaoyu\.nuget\packages\nest\7.17.5\lib\netstandard2.0\Nest.dll

Otherwise, you can generate Windows Batch or Linux Shell Script without the "build" keyword, and select ScriptType by `-st` option, and BuildMode by `-bm` option.
