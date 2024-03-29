[![Language switcher](https://img.shields.io/badge/Language%20%2F%20%E8%AF%AD%E8%A8%80-Chinese%20%2F%20%E4%B8%AD%E6%96%87-blue)](https://github.com/xiaoyuvax/bflata/blob/main/README.zh-cn.md)


# BFlatA

`BFlatA Purpose: Write your program in VS, and Build it Flat` (with [bflat](https://github.com/bflattened/bflat))

BFlatA is a wrapper, build script generator and project flattener for BFlat - a native C# compiler, in order to recusively build/flatten C# projects(.csproj) originally created for MSBuild (by Visual Studio) with their:
 - Referenced projects
 - Nuget package dependencies
 - Native library dependencies
 - Embedded resources
 etc.

Some intuitive demos of usage:

	bflata build myproject.csproj

would produce a `myproject.exe` and a `build.rsp` if bflat is properly installed, set in %PATH%, and args properly served. If the `build` verb not specified, only rsp file generated, which you can use to build later with BFlat (please notice the difference between BFlat, the compiler and BFlatA, the wrapper).

	bflata flatten-all myproject.csproj

would produce a `myproject.flat` folder, into which all code files, libs, resources of myproject even together with what all child projects referenced would be extracted and copied, as a complete project package, in which u would expect to build the project with bflat direclty like 'go build'.

  You can find BFlat, the C# native compiler at [flattened.net](https://flattened.net).
  
  BFlatA is relevent to an issue from bflat: https://github.com/bflattened/bflat/issues/61
  
## Update Logs   
Update 24-02-08 (V1.5.0.8)
- Matching a version range of packages is allowed.

Update 23-03-30 (V1.4.3.0)
- `<NoStdLib>` flag bug fixed.

Update 23-03-29 (V1.4.2.2)
- Support Prebuild/Postbuild Actions.
- This version supports building project like MOOS, check out the [Demo Projects](https://github.com/xiaoyuvax/bflata/blob/main/README.md#moos)

Update 23-03-22 (V1.4.2.0)
- Allow specifying an external linker instead of that comes with bflat, such as MSVC linker(link.exe).

<details>
<summary>[Click to see earlier updates...]</summary>
Update 23-03-19 (V1.4.1.0)
- Introduced BFA file(literally BFlatA Arguments file) instead of RSP file, which can be used with -inc:<BFA file> option to combine multiple arg profiles to generate build script, and it supports macros and doesn't have to store args as "one arg per line". BFA file can be treated like a project file, in order to switch among different projects more conveniently, if well-organized.
- Introduced new `flatten|flatten-all` verbs allowing extraction of code files together with their dependencies/resources to specified dest location, where project files are organized in a flattened, GO-like path hierachy, which can be directly built by BFlat (similar with `go build`), `flatten-all` also copies all dependent libs so that you can pack the path structure together with all dependencies as a whole.
- Improved process for quotes and macros in args.
- Improved processing more well-known properties in csproj file, such as `<NoStdLib>, <BaseAddress>, <linkerSubsystem>, <EntrypointSymbol>`, etc.

Update 23-03-16 (V1.3.0.0):
- Removed support for Windows Batch or Linux Shell Script (not meaningful if RSP is available), now BFlatA only generates RSP script.
- Allow specifying one or more RSP files to be included with -inc:<rsp file> options, in case certain groups of fixed building options need to be included and used for certain project. Usually you can put linker args, home path in one rsp file and include it with specified csproj.

Update 23-03-15 (V1.2.1.1):
- Add support for NativeLibrary.

Update 23-03-03 (V1.2.0.0):
- Adding support compiling .resx files by calling resgen.exe, which u must specify with `--resgen`. Namespace for each .resx file will be determined by checking corresponding code file. eg. myForm.cs for myForm.resx and Resources.Designer.cs for Resources.resx. But there might still be some problem running winform program with resources, as described in Known Issues below.

Update 23-02-26 (V1.1.0.0):
- Exclu mechanism added. Some (but not all) dependencies referenced by nuget packages with name starting with "System.*" might have already been included in runtime, which is enabled with `bflat --stdlib Dotnet` option, and must be excluded from the BFlata generated build script, otherwise you may see a lot of error CS0433 as below:

> error CS0433: The type 'MD5' exists in both
> 'System.Security.Cryptography.Algorithms, Version=4.2.1.0,
> Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' and
> 'System.Security.Cryptography, Version=7.0.0.0, Culture=neutral,
> PublicKeyToken=b03f5f7f11d50a3a'

BFlatA introduced a mechanism called "Exclu" to exclude packages from dependencies during scripting. Literally, Exclu means the Excluded Packages. BFlatA supports extracting names to be excluded from Dotnet runtime, more exactly, the specific Shared Frameworks such as Microsoft.NETCore.App(which is what BFlat incorprates so far), to generate ".exclu" file, which can be reused in later builds, by using the `-xx` option which uses the moniker name specified by `-fx` to save/load Exclus from files accordingly, in addition to an always-load "packages.exclu" file, if exists, where you can put in custom Exclus.
I provided a `net7.0.exclu` file in the repository as the default Exclu for net7.0, which is the default target of both BFlata and BFlat by now, and you can copy it to ur working directory, if you don't want to extract Exclus by youself from Dotnet runtime(with -xx option).

- 'build-il' is allowed now. This verb is consistent with that of BFlat, and it only affects root project. Dependent projects will always being built under TREE mode with 'build-il' on.
- A new Build Mode `-bm:treed` is introduced to provide a shortcut for `-bm:tree -dd`, simply a short.
- BFlat style args are supported now, u can use both space or ":" for evaluation of argument values.
- BFlat's `-o|--out<file>`  option is now hijacked by BFlatA as to be properly scripted in the generated script. At least, it will not be passed to every referenced projects.

Update 23-02-24: 
- Response file(.rsp) support added, and 'arguments too long' problem solved. The .rsp script will be taken as default build script format of BFlatA. You can use the generated build.rsp file like `bflat build @build.rsp` to build a FLATTENED project. 
Note: a single .rsp file itself does not support building project Tree, instead you may use `-st:bat` or `-st:sh` to generate script that supports building project trees, or build through BFlatA directly(with both `build` and `-bm:tree` option on).  
- Paths cache of Nuget packages are now saved to packages.cache in the working path, and will be reused at the next run, as to improve performance.
- A new `-dd` (Deposit Dependencies) option is introduced for compiling projects who uses references of child projects indirectly, and still offering certain extent of version consistency, where dependencies are added-up (deposited) along the hiearchy line and be accumulatively served to the parent project level by level (if any dependency version conflict upon merging with parent level, higher version will be retained, as to guarantee maximal version compatibility).
</details>

## Usage


	  Usage: bflata [build|build-il|flatten|flatten-all] <root csproj file> [options]

	  [build|build-il|flatten|flatten-all]          BUILD|BUILD-IL = build with BFlat in %Path% in native or in IL.
							FLATTEN = extract code files from project hierachy into a "flattened, Go-like" path hierachy,
							FALTTEN-ALL = flatten + copy all dependencies and resources to dest path,
							both with dependency references written to a BFA file.
							If omitted, generate build script only, with -bm option still valid.

	  <root csproj file>                            Must be the 2nd arg if 'build' specified, or the 1st otherwise, only 1 root project allowed.

	Options:

	  -pr|--packageroot:<path to package storage>   eg.'C:\Users\%username%\.nuget\packages' or '$HOME/.nuget/packages'.

	  -h|--home:<MSBuildStartupDirectory>           Path to VS solution usually, default:current directory.
							Caution: this path may not be the same as <root csproj file>,
							and is needed for entire solution to compile correctly.

	  -fx|--framework:<moniker>                     The TFM compatible with the built-in .net runtime of BFlat(see 'bflat --info')
							mainly purposed for matching dependencies, e.g. 'net7.0'

	  -bm|--buildmode:<flat|tree|treed>             FLAT = flatten project tree to one for building;
							TREE = build each project alone and reference'em accordingly with -r option;
							TREED = '-bm:tree -dd'.

	  --resgen:<path to resgen.exe>                 Path to Resource Generator(e.g. ResGen.exe).

	  -inc|--include:<path to BFA file>             BFA files(.bfa) contain any args for BFlatA, each specified by -inc:<filename>.

							Unlike RSP file, each line in BFA file may contain multiple args with macros enabled(listed at foot).
							BFAs can be used as project-specific build profile, somewhat equivalent to .csproj file.
							If any arg duplicated, valid latters will overwrite, except for <root csproj file>.

	  -pra|--prebuild:<cmd or path to executable>   One command line to be executed before build.Can be multiple.

	  -poa|--postbuild:<cmd or path to executable>  One command line to be executed after build.Can be multiple.


	Shared Options with BFlat:

	  --target:<Exe|Shared|WinExe>                  Build Target.default:<BFlat default>

	  --os <Windows|Linux|Uefi>                     Build Target.default:Windows.

	  --arch <x64|arm64|x86|...>                    Platform archetecture.default:x64.

	  -o|--out:<File>                               Output file path for the root project.

	  --verbose                                     Enable verbose logging

	Obsolete Options:

	  -dd|--depdep                                  Deposit Dependencies mode, valid with '-bm:tree', equivalently '-bm:treed',
							where dependencies of child projects are deposited and served to parent project,
							as to fulfill any possible reference requirements

	  -xx|--exclufx:<dotnet Framework path>         Path where lib exclus will be extracted from.
							e.g. 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\7.0.2'
							Extracted exclus stored in '<moniker>.exclu' for further use with moniker specified by -fx opt.
							If path not given, BFlatA searches -pr:<path> with -fx:<framework>, automatically.


	Note:

	  Any other args will be passed 'as is' to BFlat, except for '-o'.
	  For options, the ':' char can also be replaced with a space. e.g. -pr:<path> = -pr <path>.
	  Do make sure <ImplicitUsings> switched off in .csproj file and all namespaces properly imported.
	  Once '<moniker>.exclu' file is saved, it can be used for any later build, and a 'packages.exclu' is always loaded and can be used to store extra shared exclus, where 'exclu' is the short for 'Excluded Packages'.

	Examples:

	  bflata xxxx.csproj -pr:C:\Users\username\.nuget\packages -fx:net7.0 -bm:treed

	  bflata build xxxx.csproj -pr:C:\Users\username\.nuget\packages --arch x64 --ldflags /libpath:"C:\Progra~1\Micros~3\2022\Enterprise\VC\Tools\MSVC\14.35.32215\lib\x64" --ldflags "/fixed -base:0x10000000 --subsystem:native /entry:Entry /INCREMENTAL:NO"


	Macors defined:

	  MSBuildProjectDirectory    = c:\ProjectPath
	  MSBuildProjectExtension    = .csproj
	  MSBuildProjectFile         = ProjectFile.csproj
	  MSBuildProjectFullPath     = c:\ProjectPath\ProjectFile.csproj
	  MSBuildProjectName         = ProjectFile
	  MSBuildRuntimeType         = <default>
	  MSBuildThisFile            = ProjectFile.csproj
	  MSBuildThisFileDirectory   = c:\ProjectPath\
	  MSBuildThisFileExtension   = .csproj
	  MSBuildThisFileFullPath    = c:\ProjectPath
	  MSBuildThisFileName        = ProjectFile
	  MSBuildStartupDirectory    = D:\Repos\bflata\bin\Debug\net7.0
      

## Compile from source:
 Since BflatA is a very tiny program that you can use any of following compilers to build it:
- [BFlat](https://github.com/bflattened/bflat) (Download binary [here](https://flattened.net), recommended.)
- Dotnet C# compiler
- Mono C# compiler

of course BFlat is prefered to build the program entirely to native code(without IL at all):

## BFA file
- BFA file(.bfa) contains any valid args for BFlatA, including `build` and `<root csproj file>`, each BFA file can be specified by a single `-inc:<filename>`. Therefore, for a project, you can use `bflata -inc:myproject.bfa` to build the project with all args written in that myproject.bfa file. You can also store some shared args in shared BFA file, such as those longer linker args(`--ldflags:...`) and reference them when build different projects, e.g. `bflata build -inc:MyProject.bfa -inc:SharedArgSet1.bfa`.
- Unlike RSP file, each line in BFA file may contain multiple options(option is a type of args) with macros enabled, but not for barehead args such as the "build" verb, <project path>, and these option strings are to be parsed by bflata and finally merged in the output build script. Barehead args(which has no option cap) must be written in a single line each. And so far the VERBs and the root .csproj file must present at the first two lines(comment lines skipped) in BFA file.
- Therefore, BFAs looks and can be used like project-specific build profile, or even somewhat equivalent to a .csproj file, you can use them flexibly‚
- If any arg duplicated, valid latter occurrences will overwrite the formers, except for `<root csproj file>` , which must present in the 1st or 2nd pos of the arg list, just following the `VERB`.
- Like RSP file, lines start with "#" in BFA file are considered comments.
- You can see macros supported by using '-?' option, which are mostly MSBuild compatible, usage shown in example below.
- Double quotes can be used as `"''"` way, where whatever inside a single/double quote would be considered as a whole despite presence of spaces, and the double quotes will be removed and the single quotes will be promoted to double quotes when pass to external program(such as the shell), as shown in example below:

		# Postbuild actions:
		-poa:cmd.exe /c copy /b loader.o + moos.exe "$(MSBuildStartupDirectory)\Tools\grub2\boot\kernel.bin"
		-poa:"'$(MSBuildStartupDirectory)\Tools\mkisofs.exe' -relaxed-filenames -J -R -o MOOS.iso -b boot/grub/i386-pc/eltorito.img -no-emul-boot -boot-load-size 4 -boot-info-table  '$(MSBuildStartupDirectory)\Tools\grub2'"
		-poa:"'D:\Program Files (x86)\VMware\VMware Player\vmplayer.exe' '$(MSBuildStartupDirectory)\Tools\VMWare\MOOS\MOOS.flat.vmx'"

## Known issues:

- Analyzer is not supported, you may see lib missing errors if your project is working with analyzers, you may try to add dependencies manually as described in https://github.com/xiaoyuvax/bflata/issues/7
- The "--buildmode:tree" option builds by reference hierachy, but the referenced projects are actually built with 'bflat build-il' option, which produces IL assemblies rather than native code, and only the root project is to be built in native code. This is because so far BFlat is not known to produce native .dll lib which can be referenced with -r option (if it will do so someday, or known to be able to do so, the TREE mode would actually work for native code then). Note: TREE mode is useful dealing with the scenarios that Nuget package uses dependencies whose versions are not compatible with the parent project (in FLAT mode, as would cause errors), for the lib is compiled independently. Moreover with the `-dd`(Deposit Dependency) option, parent project who indirectly use child project's dependencies might also be solved. 
- The "--buildmode:flat" option (default, if -bm not served) generates flattened build script which incorproate all code files, package references and resources of all referenced .csproj files into one, but this solution cannot solve the issue of version incompatibility of dependencies from different projects, especially secondary dependencies required by Nuget packages. Version inconsistency of projects can be eliminated by making all projects reference the same packages of the same version, but secondary dependencies among precompiled packages are various and not changeable	
- Winform application(WPF too) can be built to native but possibly too problematic to run. This is possibly becuase dotnet assemblies supporting Winform(or WPF) require managed metadata be well preserved which will be trimmed by native compiler (this is same problem for NativeAOT). 
- Although current version of BFlatA support calling resgen.exe (specified by `--resgen` option) to compile .resx file to embeddable binary(.resources) and reference'em from temp directory at build-time, and namespace for resources would be extracted from coressponding code files, eg. myForm.cs for myForm.resx and Resources.Designer.cs for Resources.resx, error message like below would still occur, as reported here with solution: https://github.com/bflattened/bflat/issues/92

![image](https://user-images.githubusercontent.com/6511226/222661726-92f1afb7-ba9f-4e3b-8c7e-f25148119edc.png)

Now BFlatA would automatically add relevant `--feature` args if `--resgen` arg is specified to eliminate this error. 

## Demo project

- [You are welcome to suggest more demo project which can be built by leaving a message at the Issues board]

### [ObjectPoolReuseCaseDemo](https://github.com/xiaoyuvax/ObjectPoolReuseCaseDemo) 
is a simple C# project with one Project Reference and one Nuget Package reference together with several secondary dependencies, and is a typical scenario for demonstrating how BFlata works with BFlat.
<details>
<summary>[Click to see more details]</summary>
	
> Note:It is important to disable `<ImplicitUsings>` in .csproj file,
> and make sure all necessary namespaces are imported, especially `using
> System;` if you are building with `--stdlib Dotnet` option of bflat.

Following command build the project in the default FLAT mode(i.e. code files and references of underlying projects are to be merged and built together), and a buiding script file (default is `build.rsp`) will also be genreated.
You can also build the demo project in TREE mode, with `-bm:tree` option, but an additional `-dd` option would be required to build this demo(while not all projects need it). The `-dd` option is required in case a project somehow misses dependencies from referenced child projects, which is not directly referenced in the .csproj file), and is only valid when `-bm:tree` option is used. With presence of this option, dependencies of referenced child projects would be added up(deposited) and served accumulatively to the parent project during building process. 

	bflata build ObjectPoolDemo.csproj -pr:C:\Users\%username%\.nuget\packages -fx:net7.0 --stdlib Dotnet 

output:

    BFlatA V1.1.0.0 @github.com/xiaoyuvax/bflata
    Description:
      A wrapper/build script generator for BFlat, a native C# compiler, for recusively building .csproj file with:
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
    RefPath:C:\Users\Xiaoyu\source\repos\BFlatA\bin\Debug\net7.0
    Target:Exe
    
    --LIB EXCLU---------------------------
    Exclu file found:.\net7.0.exclu
    Exclus loaded:166
    
    --LIB CACHE---------------------------
    Package cache found!
    Libs loaded:1609
    
    --BUILDING----------------------------
    Parsing Project:C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\ObjectPoolDemo\ObjectPoolDemo.csproj ...
    Parsing Project:C:\Users\Xiaoyu\source\repos\ObjectPoolDemo\WMLogService.Core\Wima.Log.csproj ...
    
    Generating script:ObjectPoolDemo
    - Found 9 code files(*.cs)
    - Found 16 dependent libs(*.dll)
    Writing script:ObjectPoolDemo...
    Script written!
    
    Building in FLAT mode:ObjectPoolDemo...
    - Executing build script: bflat build @build.rsp...
    --END---------------------------------
    C:\Users\Xiaoyu\source\ObjectPoolDemo\WMLogService.Core\WimaLoggerBase.cs(358): Trim analysis warning IL2026: Wima.Log.WimaLoggerBase.<>c.<WriteInternal>b__130_0(StackFrame): Using member 'System.Diagnostics.StackFrame.GetMethod()' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Metadata for the method might be incomplete or removed.
    C:\_oss\common-logging\src\Common.Logging.Portable\Logging\LogManager.cs(567): Trim analysis warning IL2072: Common.Logging.LogManager.<>c__DisplayClass35_0.<BuildLoggerFactoryAdapterFromLogSettings>b__0(): 'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to 'System.Activator.CreateInstance(Type,Object[])'. The return value of method 'Common.Logging.Configuration.LogSetting.FactoryAdapterType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
    C:\_oss\common-logging\src\Common.Logging.Portable\Logging\LogManager.cs(571): Trim analysis warning IL2072: Common.Logging.LogManager.<>c__DisplayClass35_0.<BuildLoggerFactoryAdapterFromLogSettings>b__0(): 'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in call to 'System.Activator.CreateInstance(Type)'. The return value of method 'Common.Logging.Configuration.LogSetting.FactoryAdapterType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.

and following is the content of the build script (Response File) generated above:

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
</details>
	  

### [MOOS](https://github.com/xiaoyuvax/MOOS)
is a native OS almost totally written in C#, and you can build it completely with BFlat + BFlatA + MSVCLinker, despite it was originally orchestrated in VS and require MSBuild + ILcompiler + MSVCLinker to build. 
Check out: [How to build MOOS with BFlatA](https://github.com/xiaoyuvax/MOOS/blob/master/MOOS.bflat.md#building-steps).
	
MOOS is a relatively more complicated example to build through BFlatA + BFlat toolchian, in which the linker comes with BFlat is replaced with MSVC linker, and it demonstrates how BFlatA can cope with some unusual condition flexibly.
![image](https://user-images.githubusercontent.com/6511226/228498471-0baf5415-b000-45f8-9c20-b35b3f634089.png)


### Other projects that have been verified to be successfully built by BFlatA
[MarkovJunior](https://github.com/mxgmn/MarkovJunior)  Issue:https://github.com/xiaoyuvax/bflata/issues/9  `bflata build ...\MarkovJunior\MarkovJunior.csproj -pr:C:\Users\<username>\.nuget\packages -fx:net7.0 -h:..\MarkovJunior`
