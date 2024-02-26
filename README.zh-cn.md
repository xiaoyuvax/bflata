[![Language switcher](https://img.shields.io/badge/Language%20%2F%20%E8%AF%AD%E8%A8%80-English%20%2F%20%E8%8B%B1%E8%AF%AD-blue)](https://github.com/xiaoyuvax/bflata/blob/main/README.md)

# BFlatA

目的：VS写工程，打平用[BFlat](https://github.com/bflattened/bflat)编译（成本机代码）。

BFlatA是BFlat的包装器、构建脚本生成器和项目平坦化工具，用于递归地构建/平坦化原本是用MSBuild（由Visual Studio创建）的C#项目(.csproj)，包括其:

- 引用的项目（不管引用关系有多复杂）
- Nuget 包依赖项（包括包的依赖）
- 嵌入式资源
- 原生库依赖项
- .......

一些直观的用法示例：

如果bflat已正确安装、设置在%PATH%中并且参数正确，则运行以下命令：

	bflata build myproject.csproj

将生成myproject.exe和build.rsp。如果未指定build动词，则仅生成rsp文件，您可以稍后使用BFlat构建（请注意编译器BFlat和包装器BFlatA之间的区别）。

如果运行以下命令：

	bflata flatten-all myproject.csproj -h:d:\repos\moos

将生成一个名为myproject.flat的文件夹，其中包含myproject的所有代码文件、库和资源，甚至包括所有子项目所引用的内容，作为完整的项目包。您可以直接使用bflat构建此项目，类似'go build'。`flatten-all`指示打包依赖文件，诸如.lib文件（如果换成`flatten`则不打包依赖，指示在.bfa文件中记录依赖的路径）, `-h:d:\repos\moos\` 指示解决方案路径，这是根据MOOS的配置设定的，以便搜索依赖项。

您可以在 [flattened.net](https://flattened.net) 下载原生 C# 编译器 BFlat。

BFlatA源于BFlat的这个问题：https://github.com/bflattened/bflat/issues/61

## 更新日志 
更新 23-02-08 (V1.5.0.8)
- 允许匹配一个版本范围的包。

更新 23-03-30 (V1.4.3.0)
- `<NoStdLib>` 标志处理bug.

更新 23-03-29 (V1.4.2.2)
- 支持 Prebuild/Postbuild动作.
- 这个版本支持编译类似MOOS这种项目，详情见[演示项目](https://github.com/xiaoyuvax/bflata/blob/main/README.zh-cn.md#moos)

更新：23-03-22 (版本号：V1.4.2.0)
- 支持编译前和编译后动作执行外部命令。
- 此版本确保BFlatA+BFlat工具链能够支持编译像MOOS这样的项目，详情见[示例项目](https://github.com/xiaoyuvax/bflata/blob/main/README.zh-cn.md#moos)。

更新：23-03-22 (版本号：V1.4.2.0)
- 允许指定一个外部链接器，而不是与bflat一起提供的链接器，例如MSVC链接器(link.exe)。

<details>
<summary>[点击查看更早的更新...]</summary>

更新：23-03-19 (版本号：V1.4.1.0)
- 引入BFA文件（字面意思为BFlatA参数文件），而不是RSP文件，可以使用-inc:<BFA文件>选项将多个参数文件合并以生成构建脚本，它支持宏，并且不必将参数存储为“每行一个参数”。如果组织得当，可以像项目文件一样处理BFA文件，以便更方便地在不同的项目之间切换。

- 引入新的“flatten | flatten-all”动词，允许将代码文件及其依赖项/资源提取到指定的目标位置，其中项目文件按打平的、类似GO的路径层次结构组织，可以直接由BFlat构建（类似于“go build”），“flatten-all”还复制所有依赖库，以便您将路径结构和所有依赖项一起打包。

- 改进了参数中引号和宏的处理。

- 改进了对csproj文件中更为常见的属性的处理，例如'NoStdLib'、'BaseAddress'、'linkerSubsystem'、'EntrypointSymbol'等。

更新：23-03-16 (版本号：V1.3.0.0)
- 移除了对Windows批处理或Linux Shell脚本的支持（如果有RSP文件，则没有意义），现在BFlatA仅生成RSP脚本。

- 允许使用-inc:<rsp文件>选项指定一个或多个要包含的RSP文件，以防需要包含某些固定构建选项的特定组，并在某些项目中使用。通常可以将链接器参数、主路径放入一个rsp文件中，并将其与指定的csproj一起包含。

更新：23-03-15 (版本号：V1.2.1.1)
- 添加对NativeLibrary的支持。

更新 23-03-03 (V1.2.0.0)：
- 添加调用resgen.exe编译.resx文件的支持，必须使用“--resgen”指定。每个.resx文件的命名空间将通过检查相应的代码文件来确定。例如，myForm.cs对应于myForm.resx，Resources.Designer.cs对应于Resources.resx。但是在使用资源运行winform程序时可能仍会出现问题，如下面的已知问题所述。

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
</details>

##  使用说明：

	Usage: bflata [build|build-il|flatten|flatten-all] <root .csproj file> [options]

	[build|build-il|flatten|flatten-all]          BUILD|BUILD-IL 意思是使用 %Path% 中的 BFlat 进行本地或 IL 编译。
						      FLATTEN 表示从项目层次结构中提取代码文件，并将其放入“平铺的、类似 Go 的”路径层次结构中，
						      同时将依赖引用写入 BFA 文件中。						
						      如果省略，只生成构建脚本，但 -bm 选项仍然有效。

	<root csproj file>                            如果指定了 'build' 参数，则必须是第二个参数；否则，必须是第一个参数。只允许有一个根项目。 

    选项：
	-pr|--packageroot:<path to package storage>       例如：'C:\Users\%username%\.nuget\packages' 或 '$HOME/.nuget/packages'。
	
	-h|--home:<MSBuildStartupDirectory>               通常是指向 Visual Studio 解决方案的路径，默认为当前目录。
	                                                  注意：这个路径可能不同于根 csproj 文件的路径，但是为了整个解决方案能够正确编译，需要指定这个路径。
													  

	-fx|--framework:<moniker>                     	  与 BFlat 内置的 .net runtime 兼容的 TFM，
                                                      主要用于匹配依赖项，例如 'net7.0'

	-bm|--buildmode:<flat|tree|treed>                 FLAT = 扁平化项目树以便构建；
                                                      TREE = 单独构建每个项目并相应地引用它们，使用 -r 选项；
                                                      TREED = '-bm:tree -dd'。
    
	--resgen:<path to resgen.exe>                     指向资源生成器（例如 ResGen.exe）的路径。
	
	-inc|--include:<path to BFA file>                 BFA 文件（.bfa）包含任何 BFlatA 的参数，每个参数由 -inc:<filename> 指定。                                            
                                                      与RSP文件不同，BFA文件中的每一行可以包含多个启用了宏的参数（在底部列出）。
					                  BFA可以用作特定项目的构建配置文件，有点类似于.csproj文件。
						          如果有任何参数重复，则最后一个有效的参数将覆盖前面的参数，但不适用于<root csproj file>。
	-pra|--prebuild:<cmd or path to executable>       在编译前执行的一行命令行。可以有多个。
	
	-poa|--postbuild:<cmd or path to executable>  	  在编译后执行的一行命令行。可以有多个。
													  
 
    Shared Options with BFlat:                        
	
	--target:<Exe|Shared|WinExe>                      这是构建目标，默认为<BFlat default>。               
	
	--os <Windows|Linux|Uefi>                         这个指令的意思是构建目标，默认使用的是 Windows 平台。     
	
	--arch <x64|arm64|x86|...>                        这条信息表示构建的默认平台架构为x64。
	
	-o|--out:<File>                                   根项目的输出文件路径。
	
	--verbose                                         启用详细日志记录

    -xx|--exclufx:<dotnet Shared Framework path>      要提取的 lib exclus 的路径。
	
                                                      指定库排除文件（lib exclus）提取的路径，例如：'C：\Program Files\dotnet\shared\Microsoft.NETCore.App\7.0.2'。
		                                          提取的排除文件将存储在“<moniker>.exclu”中，并可通过使用-fx选项指定的 moniker 进行进一步使用。如果未提供路径，
						          则 BFlatA 会自动搜索使用 -fx:<framework> 选项指定的 moniker 的路径 -pr：<path>。
													  

  
	
	
	Shared Options(will also be passed to BFlat):
	  --target:<Exe|Shared|WinExe>                    默认构建目标：由 bflat 完成
  
	  --os <Windows|Linux|Uefi>                       默认构建目标：Windows

	  --arch <x64|arm64|x86|...>                      默认平台架构：x64
	
	
														
		
        注意：
                     除了“-o”选项之外，任何其他的参数都将原样传递给BFlat。
		         对于选项，可以使用空格代替“:”字符。例如，“-pr：<path>”=“-pr <path>”。
		         请确保在`.csproj`文件中关闭了`<ImplicitUsings>`，并且所有命名空间都已正确导入。
		         
		         一旦保存了“<moniker>.exclu”文件，它可以用于任何后续的构建，并且始终加载“packages.exclu”，可以用于存储额外的共享排除项，其中“exclu”是“Excluded Packages”的缩写。
	    示例：
		         bflata xxxx.csproj -pr:C:\Users\username\.nuget\packages -fx=net7.0 -bm:treed
				 
				 
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
      
		

## 从源代码编译：
 由于 Bflata 是一个非常小的程序，您可以使用以下任何编译器来构建它：
- BFlat [github.com/bflattened/bflat](https://github.com/bflattened/bflat)(Download binary [here](https://flattened.net))
- Dotnet C# 编译器
- Mono C# 编译器

当然，最好使用 BFlat 将程序完全构建为本机代码（完全没有 IL）:

## BFA file
- BFA文件（.bfa）包含BFlatA的任何有效参数，包括build和<root csproj file>，每个BFA文件可以由单个-inc：<filename>指定。因此，对于一个项目，您可以使用bflata -inc：myproject.bfa以使用在myproject.bfa文件中编写的所有参数构建项目。您还可以在共享的BFA文件中存储一些共享参数，例如那些较长的链接器参数（--ldflags：...），并在构建不同项目时引用它们，例如bflata build -inc：MyProject.bfa -inc：SharedArgSet1.bfa。

- 与RSP文件不同，BFA文件中的每行都可以包含启用宏的多个参数，并且这些参数将由bflata解析并最终合并到输出的构建脚本中。

- 因此，BFAs看起来并且可以像特定于项目的构建配置文件一样使用，甚至相当于.csproj文件，您可以灵活地使用它们。

- 如果任何参数重复，则有效的后续出现将覆盖前面的出现，但是<root csproj file>参数除外，它必须出现在参数列表的第1或第2个位置。

- 与RSP文件一样，在BFA文件中以“#”开头的行被认为是注释。

- 您可以使用帮助选项查看支持的宏，它们大多与MSBuild兼容。

## 已知问题：
- BFlatA不支持分析Analyzer（分析器）相关的依赖路径，如果你的工程使用分析器，请手动添加依赖。详情和讨论见：https://github.com/xiaoyuvax/bflata/issues/7
- "--buildmode:tree" 选项按照引用层次结构构建，但是被引用的项目实际上使用 'bflat build-il' 选项构建，这会生成 IL 程序集而不是本机代码，只有根项目才会以本机代码构建。这是因为目前还不知道 BFlat 是否能够生成可通过 -r 选项引用的本机 .dll 库（如果它有一天能够这样做，或者已知能够做到，那么 TREE 模式实际上就可以用于本机代码）。注意：TREE 模式在处理 Nuget 包使用与父项目不兼容的依赖项的情况时非常有用（在 FLAT 模式下会导致错误），因为库是独立编译的。此外，使用 -dd（Deposit Dependency）选项的父项间接使用子项目依赖项的父项目也可能得到解决。
- "--buildmode:flat" 选项（如果未提供"-bm"选项）会生成一个扁平的构建脚本，将所有引用的.csproj文件的所有代码文件、包引用和资源合并到一个脚本中，但这种解决方案无法解决来自不同项目的依赖项版本不兼容的问题，特别是Nuget包所需的二级依赖关系。可以通过使所有项目引用相同版本的相同软件包来消除项目之间的版本不一致性，但预编译包之间的二级依赖关系各不相同且无法更改。
- 尽管当前版本支持使用 --resgen 选项调用 resgen.exe 编译 .resx 文件为可嵌入二进制(.resources)，并在构建时从临时目录引用它们，资源的命名空间会采用相应的代码文件，例如 myForm.cs 用于 myForm.resx，Resources.Designer.cs 用于 Resources.resx，但仍然会出现以下类似的错误消息。这可能是由于 bflat 的内部设置引起的，可以在此处报告并找到解决方案：https://github.com/bflattened/bflat/issues/92

![image](https://user-images.githubusercontent.com/6511226/222661726-92f1afb7-ba9f-4e3b-8c7e-f25148119edc.png)

现在，如果指定了--resgen参数，BFlatA将自动添加相关的--feature参数以消除此错误。


## 示例项目

- [欢迎推荐可以通过BFlatA构建的项目，在问题板留言给我吧 ]

		
### [ObjectPoolReuseCaseDemo](https://github.com/xiaoyuvax/ObjectPoolReuseCaseDemo) 
是一个简单的 C# 项目，其中包含一个项目引用和一个 Nuget 包引用，以及几个次要依赖项，是展示 BFlata 如何与 BFlat 协同工作的典型场景。

<details>
<summary>[点击这里查看详情]</summary>
	
> 注意：在 .csproj 文件中禁用 <ImplicitUsings> 非常重要，
> 并确保导入了所有必要的命名空间，
> 特别是如果您使用 bflat 的 --stdlib Dotnet 选项进行构建，则需要使用 using System;。

以下命令以默认FLAT模式（即代码文件和基础项目的引用将合并并一起构建）构建项目，并且还将生成构建脚本文件（默认为build.rsp）。
您还可以使用 -bm:tree 选项在TREE模式下构建演示项目，但是需要额外的 -dd 选项来构建此演示项目（并非所有项目都需要它）。如果某个项目在 .csproj 文件中没有直接引用，但是在被引用的子项目中存在依赖项，则需要 -dd 选项，而且仅在使用 -bm:tree 选项时才有效。有了此选项，被引用的子项目的依赖项将被添加（积累）并在构建过程中累加为父项目提供服务。

输出内容:

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
    - Executing build script: bflat build @build.rsp...
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
</details>
	
### [MOOS](https://github.com/xiaoyuvax/MOOS)
是一个几乎用C#写成的原生系统，你可以完全用BFlat + BFlatA来编译它， 虽然它原来是在VS中编排的，且需要MSBuild + ILcompiler来编译。 
编译方法详情见: [如何用BFlat编译MOOS](https://github.com/xiaoyuvax/MOOS/blob/master/MOOS.bflat.CN.md#编译步骤)。
用BFlatA + BFlat工具链来编译MOOS相对来说是一个更复杂的情况，因为如上述连接中所述BFlat自带的链接器不能用需要换用MSVC链接器。但这个示例演示了BFlatA如何灵活地处理一些不常见的情况.
![image](https://user-images.githubusercontent.com/6511226/228498298-89ed4f3c-2aa2-4a4d-84ad-13599483575b.png)

### 其他已经成功通过bflata编译的项目
- [MarkovJunior](https://github.com/mxgmn/MarkovJunior) 相关问题：https://github.com/xiaoyuvax/bflata/issues/9 `bflata build ...\MarkovJunior\MarkovJunior.csproj -pr:C:\Users\<username>\.nuget\packages -fx:net7.0 -h:..\MarkovJunior`  
