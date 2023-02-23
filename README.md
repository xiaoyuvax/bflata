# bflata
A building script generator or wrapper for recusively building .csproj file with depending Nuget packages &amp; embedded resources for BFlat, a native C# compiler ([github.com/bflattened/bflat](https://github.com/bflattened/bflat))

This program is relevent to a post from bflat: https://github.com/bflattened/bflat/issues/61

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
- Some but not all dependencies referenced by nuget packages starts with "System.*" might have already been included in runtime, which is enabled with "bflat --stdlib Dotnet" option, and should have been excluded from the BFlata generated building script, but so far they are kept and should be manually removed if any error CS0433 occurs,as below:

> error CS0433: The type 'MD5' exists in both
> 'System.Security.Cryptography.Algorithms, Version=4.2.1.0,
> Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' and
> 'System.Security.Cryptography, Version=7.0.0.0, Culture=neutral,
> PublicKeyToken=b03f5f7f11d50a3a'

*This problem would possibly be solved in the future either by providing an exclusion file or by parsing the output of BFlat.
	
- Parsing resources in .resx file is not implemented yet, for lacking of knowledge of how BFlat handles resource files described in .resx file.




