
# bflata
A building script generator or wrapper for recusively building .csproj file with depending Nuget packages &amp; embedded resources for BFlat, a native C# compiler ([github.com/bflattened/bflat](https://github.com/bflattened/bflat))

     Usage: bflata [build] <csprojectfile> [options]
    
      build                                         Build with BFlat in %Path%, if present,ignores -sm arg; if omitted,generate build script only while -bm arg still effective.
      <csprojectfile>                               Only one project is allowed here, other files will be passed to bflat.
    
    Options:
      -pr|--packageroot:<path to package storage>   eg.C:\Users\%username%\.nuget\packages or $HOME/.nuget/packages .
      -fx|--framework:<moniker>                     the TFM(Target Framework Moniker),such as 'net7.0' or 'netstandard2.1' etc. usually lowercase.
      -bm|--buildmode:<flat|tree>                   flat=flatten reference project trees to one and build;tree=build each project alone and reference'em accordingly with -r arg.
      -sm|--scriptmode:<cmd|sh>                     Windows Batch file(.cmd) or Linux .sh file.
      -t|--target:<Exe|Shared|WinExe>               Build Target, this arg will also be passed to BFlat.

## Note:
  Any other args will be passed 'as is' to bflat.
  BflatA uses '-arg:value' style only, '-arg value' is not supported, though args passing to bflat are not subject to this rule.
  Only the first existing file would be processed as .csproj file.

## Examples:
  bflata xxxx.csproj -pr:C:\Users\username\.nuget\packages -fx=net7.0 -sm:bat -bm:tree  <- only generate BAT script which builds project tree orderly.
  bflata build xxxx.csproj -pr:C:\Users\username\.nuget\packages -sm:bat --arch x64  <- build and generate BAT script,and '--arch x64' r passed to bflat.
