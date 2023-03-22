[![Language switcher](https://img.shields.io/badge/Language%20%2F%20%E8%AF%AD%E8%A8%80-English%20%2F%20%E8%8B%B1%E8%AF%AD-blue)](https://github.com/xiaoyuvax/bflata/blob/main/README.md)

# BFlatA

目的：VS写工程，打平编译（成本机代码）。

BFlatA 是套壳BFlat（C#本机代码编译器）用于递归构建带有以下内容的 `.csproj` 文件的包装器/构建脚本生成器：

- 引用的项目（不管引用关系有多复杂）
- Nuget 包依赖项（包括包的依赖）
- 嵌入式资源

您可以在 [flattened.net](https://flattened.net) 下载原生 C# 编译器 BFlat。

本工程与BFlat的这个问题相关：https://github.com/bflattened/bflat/issues/61

更新 23-03-03 (V1.2.0.0)：
- 增加了通过调用 resgen.exe 编译 .resx 文件的支持，需要使用 --resgen 参数指定。每个 .resx 文件的命名空间将通过检查相应的代码文件确定。例如，myForm.resx 对应 myForm.cs，Resources.resx 对应 Resources.Designer.cs。但是在下面的已知问题中描述的使用资源运行 winform 程序可能仍然存在一些问题。

更新 23-02-26 (V1.1.0.0)：
- 添加了 Exclu 机制
  一些（但不是全部）Nuget 包引用的依赖项名称以 "System.*" 开头，可能已经包含在运行时中，并使用 `bflat --stdlib Dotnet` 选项启用，必须从 BFlata 生成的构建脚本中排除，否则您可能会看到大量以下的 error CS0433 错误：

> error CS0433: The type 'MD5' exists in both
> 'System.Security.Cryptography.Algorithms, Version=4.2.1.0,
> Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' and
> 'System.Security.Cryptography, Version=7.0.0.0, Culture=neutral,
> PublicKeyToken=b03f5f7f11d50a3a'

BFlatA 引入了一个名为 "Exclu" 的机制，在脚本中排除依赖包。实际上，Exclu 意味着被排除的包。BFlatA 支持从 Dotnet 运行时中提取要排除的名称，更准确地说是特定的共享框架，例如 Microsoft.NETCore.App（这是 BFlat 目前包含的内容），生成 ".exclu" 文件，该文件可以通过使用 `-xx` 选项（使用 `-fx` 指定的 monker 名称）从文件中保存/加载 Exclus，此外，如果存在，则始终会加载 "packages.exclu" 文件，在其中可以放置自定义 Exclus。
我在存储库中提供了一个名为net7.0.exclu的文件作为net7.0的默认排除文件，它是BFlata和BFlat的默认目标。如果您不想从Dotnet运行时中自己提取排除项（使用-xx选项），则可以将其复制到您的工作目录中。

- 'build-il' 现在是允许的。此构建模式与 BFlat 的构建模式一致，并且仅影响根项目。依赖项目将始终在启用 'build-il' 的 TREE 模式下进行构建。
- 引入了一个新的构建模式 `-bm:treed`，提供了一个 `-bm:tree -dd` 的快捷方式，简单易懂。
- 现在支持BFlat样式参数，您可以使用空格或“:”来评估参数值。
- BFlat的'-o|--out<file>'选项现在被 BFlatA 接管，以便在生成的脚本中正确地编写脚本。至少，它不会被传递给每个引用的项目。

更新 23-02-24： 
- 支持响应文件(.rsp)，解决了“参数太长”的问题。.rsp脚本将作为BFlatA的默认构建脚本格式。您可以像 `bflat build @build.rsp` 一样使用生成的 build.rsp 文件来构建一个扁平的项目。
注：单个.rsp文件本身不支持构建项目树，您可以使用 `-st:bat` 或 `-st:sh` 生成支持构建项目树的脚本，或者直接通过BFlatA构建（同时打开 `build` 和 `-bm:tree` 选项）。
- 现在Nuget包的路径缓存将保存在工作路径的“packages.cache”中，并将在下次运行时重用，以提高性能。
- 引入了一个新的-dd（Deposit Dependencies）选项，用于编译间接使用子项目引用的项目，并仍然提供某种程度的版本一致性。依赖关系沿层级线路添加（存储）并按层级级别累积为父项目服务（如果在与父级合并时存在任何依赖关系版本冲突，则保留更高版本，以保证最大版本兼容性）。


##  使用说明：
		Usage: bflata [build|build-il] <csproj file> [options]

		[build|build-il]                              使用 %Path% 中的 BFlat 进行构建，忽略 -st 选项。
							      如果省略，则仅生成构建脚本，但 -bm 选项仍然有效。

		<.csproj文件>                                 如果指定了 'build'，则必须是第二个参数，否则是第一个参数，仅允许一个项目。 

    选项：
	-pr|--packageroot:<path to package storage>       例如：'C:\Users\%username%\.nuget\packages' 或 '$HOME/.nuget/packages'。

	-rp|--refpath:<any path to be related>            生成构建脚本文件中文件路径的引用路径，
                                                      可以进行优化以缩短路径长度。默认为 '.'（当前目录）。

	-fx|--framework:<moniker>                     	  与 BFlat 内置的 .net runtime 兼容的 TFM，
                                                      主要用于匹配依赖项，例如 'net7.0'

	-bm|--buildmode:<flat|tree|treed>                 FLAT = 扁平化项目树以便构建；
                                                      TREE = 单独构建每个项目并相应地引用它们，使用 -r 选项；
                                                      TREED = '-bm:tree -dd'。
    -st|--scripttype:<rsp|bat|sh>                     响应文件(.rsp,默认)或Windows批处理文件(.cmd/.bat)或Linux Shell脚本(.sh)文件。
	
    Obsolete Options:
    -dd|--depdep                                      存储依赖项模式，与'-bm:tree'一起有效，
                                                      存储子项目的依赖项并将其按层次线累积提供给父项目，
                                                      以满足任何可能的引用要求。

    -t|--target:<Exe|Shared|WinExe>                   构建目标，此参数也将传递给BFlat。

    -o|--out:<File>                                   根项目的输出文件路径。

    -xx|--exclufx:<dotnet Shared Framework path>      如果路径有效，则将从该路径中提取lib exclus。
                                                      例如'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\7.0.2'
                                                      并且提取的exclus将保存到'<moniker>.exclu'以供进一步使用。
                                                      其中moniker由-fx选项指定。
						          如果未明确指定此路径，则BFlatA将自动搜索 -pr:<path> 和 -fx:<framework> 以查找 Exclus。
													  

     
	--resgen:<path to resgen.exe>                     参数指定资源生成器（例如ResGen.exe）的路径，用于将.resx文件编译为二进制文件。exclus。
	
	
	Shared Options(will also be passed to BFlat):
	  --target:<Exe|Shared|WinExe>                    默认构建目标：由 bflat 完成
  
	  --os <Windows|Linux|Uefi>                       默认构建目标：Windows

	  --arch <x64|arm64|x86|...>                      默认平台架构：x64
	
	
														
		
        注意：
                     除了“-o”选项之外，任何其他的参数都将原样传递给BFlat。
		         对于选项，可以使用空格代替“:”字符。例如，“-pr：<path>”=“-pr <path>”。
		         请确保在`.csproj`文件中关闭了`<ImplicitUsings>`，并且所有命名空间都已正确导入。
		         生成脚本的文件名为“build.rsp、build.cmd、build.sh”之一，`.rsp`文件允许更大的参数，是首选。
		         一旦保存了“<moniker>.exclu”文件，您可以将其用于任何后续构建，并且“packages.exclu”始终加载，可用于存储额外的共享排除项，其中“exclu”是“Excluded Packages”的缩写。
	    示例：
		         bflata xxxx.csproj -pr:C:\Users\username\.nuget\packages -fx=net7.0 -st:bat -bm:treed  // 仅生成构建脚本，按项目树的顺序使用 Deposit Dependencies。
		         bflata build xxxx.csproj -pr:C:\Users\username\.nuget\packages -st:bat --arch x64  // 该命令使用 FLAT 模式进行构建，并且默认的构建目标是 .NET 7.0。--arch x64 选项被传递给 BFlat 以指定构建的目标架构为 x64，而 -st:bat 选项则被忽略。
		

## 从源代码编译：
 由于 Bflata 是一个非常小的程序，您可以使用以下任何编译器来构建它：
- BFlat [github.com/bflattened/bflat](https://github.com/bflattened/bflat)
- Dotnet C# 编译器
- Mono C# 编译器

当然，最好使用 BFlat 将程序完全构建为本机代码（完全没有 IL）:

## 已知问题：

- "--buildmode:tree" 选项按照引用层次结构构建，但是被引用的项目实际上使用 'bflat build-il' 选项构建，这会生成 IL 程序集而不是本机代码，只有根项目才会以本机代码构建。这是因为目前还不知道 BFlat 是否能够生成可通过 -r 选项引用的本机 .dll 库（如果它有一天能够这样做，或者已知能够做到，那么 TREE 模式实际上就可以用于本机代码）。注意：TREE 模式在处理 Nuget 包使用与父项目不兼容的依赖项的情况时非常有用（在 FLAT 模式下会导致错误），因为库是独立编译的。此外，使用 -dd（Deposit Dependency）选项的父项间接使用子项目依赖项的父项目也可能得到解决。
- "--buildmode:flat" 选项（默认选项，如果未提供 -bm）生成扁平化的构建脚本，该脚本将所有引用的 .csproj 文件的所有代码文件、包引用和资源合并到一个文件中，但此解决方案无法解决来自不同项目的依赖项版本不兼容的问题，特别是 Nuget 包所需的次要依赖项。可以通过使所有项目引用相同版本的相同软件包来消除项目版本不一致，但是预编译软件包之间的次要依赖关系是各种各样的且不可更改的。
- 尽管当前版本支持调用resgen.exe（通过--resgen选项指定）来编译.resx文件以生成可嵌入的二进制文件（.resources），并在构建时从临时目录引用它们，资源的命名空间将采用相应的代码文件，例如myForm.cs对应myForm.resx，Resources.Designer.cs对应Resources.resx，但仍然会出现以下错误消息。这可能是由于BFlat的内部设置，正如此处所述:https://github.com/bflattened/bflat/issues/92

![image](https://user-images.githubusercontent.com/6511226/222661726-92f1afb7-ba9f-4e3b-8c7e-f25148119edc.png)

## 示例项目

[ObjectPoolReuseCaseDemo](https://github.com/xiaoyuvax/ObjectPoolReuseCaseDemo) 是一个简单的 C# 项目，其中包含一个项目引用和一个 Nuget 包引用，以及几个次要依赖项，是展示 BFlata 如何与 BFlat 协同工作的典型场景。

> 注意：在 .csproj 文件中禁用 <ImplicitUsings> 非常重要，
> 并确保导入了所有必要的命名空间，
> 特别是如果您使用 bflat 的 --stdlib Dotnet 选项进行构建，则需要使用 using System;。

以下命令以默认FLAT模式（即代码文件和基础项目的引用将合并并一起构建）构建项目，并且还将生成构建脚本文件（默认为build.rsp）。
您还可以使用 -bm:tree 选项在TREE模式下构建演示项目，但是需要额外的 -dd 选项来构建此演示项目（并非所有项目都需要它）。如果某个项目在 .csproj 文件中没有直接引用，但是在被引用的子项目中存在依赖项，则需要 -dd 选项，而且仅在使用 -bm:tree 选项时才有效。有了此选项，被引用的子项目的依赖项将被添加（积累）并在构建过程中累加为父项目提供服务。

输出内容:

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
	

以下是上面生成的构建脚本（响应文件）的内容：

    --target Exe 
    -stdlib Dotnet
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\ObjectPoolDemo\ObjectPoolDemo.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\ESSevice.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\IndexableDoc.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\LogLine.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\WimaLogger.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\WimaLoggerBase.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\WimaLoggerConfiguration.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\WimaLoggerExtensions.cs
    C:\Users\Xiaoyu\source\repos\LoraMonitor\branches\1.7-MemoryPack\ObjectPoolDemo\WMLogService.Core\WimaLoggerProvider.cs
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


否则，您可以生成不带“build”关键字的 Windows Batch 或 Linux Shell Script，并通过 `-st` 选项选择 ScriptType，通过 `-bm` 选项选择 BuildMode。
