using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Linq;

[assembly: AssemblyVersion("1.5.0.5")]

namespace BFlatA
{
    public enum BuildMode
    {
        None,
        Flat,
        Tree,
        TreeDepositDependency
    }

    public enum Verb
    {
        Script,
        Build,
        BuildIl,
        Flatten,
        FlattenAll
    }

    public class ReferenceInfo
    {
        public string HintPath;
        public string Name;
        public string FusionName;
        public string SpecificVersion;
        public string Aliases;
        public bool Private;
    }

    public static class XExtention
    {
        public static IEnumerable<XElement> OfInclude(this IEnumerable<XElement> r, string elementName) => r.Where(i => i.Name.LocalName.ToLower() == elementName.ToLower() && i.Attribute("Include") != null);

        public static IEnumerable<XElement> OfRemove(this IEnumerable<XElement> r, string elementName) => r.Where(i => i.Name.LocalName.ToLower() == elementName.ToLower() && i.Attribute("Remove") != null);
    }

    public class ArgDefinition
    {
        public string Description;
        public string Group;
        public bool IsCaseSensitive;
        public bool IsDirectory;
        public bool IsFile;
        public bool IsFlag;
        public bool IsOption;
        public bool IsOptional;
        public bool IsOther;
        public string[] LongName;
        public string[] ShortName;
        public List<string> Value;

        public ArgDefinition(string[] shortName = null, string[] longName = null, string description = null, bool isFlag = false,
                             bool isOptional = true, bool isOption = false, bool isOther = false, bool isFile = false,
                             bool isDirectory = false, bool isCaseSensitive = false, string group = null,
                             string defaultValue = null)
        {
            IsCaseSensitive = isCaseSensitive;
            IsFlag = isFlag;
            IsOption = isOption;
            IsOther = isOther;
            ShortName = shortName;
            LongName = longName;
            Description = description;
            Value = new() { defaultValue };
            Group = group;
            IsFile = isFile;
            IsDirectory = isDirectory;
            IsOptional = isOptional;
        }
    }

    public sealed class BflataEqualityComparer : EqualityComparer<string>
    {
        private readonly bool _argEquality = false;
        private readonly bool _caseSensitive = false;

        public BflataEqualityComparer(bool caseSensitive = false, bool argEquality = false)
        {
            _caseSensitive = caseSensitive;
            _argEquality = argEquality;
        }

        public static string ReplaceArgEvalurationCharWithSpace(string str)
        {
            str = str.Trim();
            if (str.StartsWith(BFlatA.OPTION_CAP_CHAR))
            {
                var i = str.IndexOf(BFlatA.ARG_EVALUATION_CHAR);
                return i > 0 ? str[..i] + ' ' + str[(i + 1)..] : str;
            }
            else return str;
        }

        public override bool Equals(string x, string y)
        {
            if (_argEquality)
            {
                x = ReplaceArgEvalurationCharWithSpace(x);
                y = ReplaceArgEvalurationCharWithSpace(y);
            }
            return _caseSensitive ? x == y : x.ToLower() == y.ToLower();
        }

        public override int GetHashCode([DisallowNull] string obj)
        {
            if (_argEquality) obj = ReplaceArgEvalurationCharWithSpace(obj);
            return string.GetHashCode(_caseSensitive ? obj : obj.ToLower());
        }
    }

    internal static class BFlatA
    {
        public const char ARG_EVALUATION_CHAR = ':';
        public const string BUIDFILE_NAME = "build.rsp";
        public const int COL_WIDTH = 48;
        public const int COL_WIDTH_FOR_ARGS = 16;
        public const char COMMENT_TAG = '#';
        public const string COMPILER = "bflat";
        public const string CUSTOM_EXCLU_FILENAME = "packages.exclu";
        public const string EXCLU_EXT = "exclu";
        public const string LIB_CACHE_FILENAME = "packages.cache";
        public const string NUSPEC_CACHE_FILENAME = "nuspecs.cache";
        public const char OPTION_CAP_CHAR = '-';
        public const string OPTION_SEPARATOR = " -";
        public const string OPTION_SEPARATOR_ESCAPED = "~-";
        public const string PATH_PLACEHOLDER = "|";
        public static readonly string[] AllowedVerbs = new string[] { "build", "build-il", "flatten", "flatten-all" };
        public static readonly string[] IGNORED_SUBFOLDER_NAMES = { "bin", "obj" };
        public static readonly bool IsLinux = Path.DirectorySeparatorChar == '/';
        public static readonly string NL = Environment.NewLine;
        public static readonly char PathSep = Path.DirectorySeparatorChar;
        public static readonly string WorkingPath = Directory.GetCurrentDirectory();
        public static readonly XNamespace XSD_NUGETSPEC = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        public static Verb Action = Verb.Script;
        public static string Architecture = "x64";  //better be default to x64

        /// <summary>
        /// TODO:Dicitonary storing argument definitions...
        /// </summary>
        public static Dictionary<string, ArgDefinition> ArgBook = new()
        {
            { "Verb",new ArgDefinition(null,new[]{"build" ,"build-il","flatten","flatten-all"},"",true,false,defaultValue:"")},
            { "RootProject",new ArgDefinition(isOptional: false,isFile:true)},
            { "PackageRoot", new ArgDefinition(new[]{"-pr"},new[]{"--packageroot" },"",isOption:true, isDirectory:true)},
        };

        public static BflataEqualityComparer ArgEqualityComparer = new(false, true);
        public static List<string> BFAFiles = new();
        public static BuildMode BuildMode = BuildMode.None;
        public static List<string> CacheLib = new();
        public static List<string> CacheNuspec = new();
        public static bool DepositLib = false;
        public static string[] LibExclu = Array.Empty<string>();
        public static string Linker = null;
        public static string MSBuildStartupDirectory = Directory.GetCurrentDirectory();
        public static string OS = "windows";
        public static string OutputFile = null;
        public static string OutputType = "Exe";
        public static string PackageRoot = null;
        public static List<string> ParsedProjectPaths = new();
        public static BflataEqualityComparer PathEqualityComparer = new(IsLinux);
        public static List<string> PostBuildActions = new();
        public static List<string> PreBuildActions = new();
        public static string ResGenPath = "C:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v10.0A\\bin\\NETFX 4.8 Tools\\ResGen.exe";
        public static Dictionary<string, string> RootMacros = new();
        public static string RootProjectFile = null;
        public static List<string> RSPLinesToAppend = new();
        public static string RuntimePathExclu = null;
        public static string TargetFx = null;
        public static bool UseExclu = false;
        public static bool UseVerbose = false;
        private const string ASPNETCORE_APP = "microsoft.aspnetcore.app.runtime";
        private const string NETCORE_APP = "microsoft.netcore.app.runtime";
        private const string WINDESKTOP_APP = "microsoft.windowsdesktop.app.runtime";
        public static string LibPathSegment { get; } = PathSep + "lib" + PathSep;
        public static string OSArchMoniker { get; } = $"{GetOSMoniker(OS)}-{Architecture}";
        public static string RootProjectName => Path.GetFileNameWithoutExtension(RootProjectFile);
        public static string RootProjectPath => Path.GetDirectoryName(RootProjectFile);
        public static string RuntimesPathSegment { get; } = PathSep + "runtimes" + PathSep;
        public static bool UseBFA => RootProjectFile.ToLower().EndsWith(".bfa");
        public static bool UseBuild => Action == Verb.Build || Action == Verb.BuildIl;
        public static bool UseBuildIL => Action == Verb.BuildIl;
        public static bool UseFlatten => Action == Verb.Flatten || Action == Verb.FlattenAll;
        public static bool UseFlattenAll => Action == Verb.FlattenAll;
        public static bool UseLinker => !string.IsNullOrEmpty(Linker);

        public static string AppendScriptBlock(string script, string myScript) => script + (script == null || script.EndsWith("\n") ? "" : NL + NL) + myScript;

        public static string ApplyMacros(this string path, Dictionary<string, string> msBuildMacros)
        {
            foreach (var m in msBuildMacros) path = path.Replace($"$({m.Key})", m.Value);
            return path;
        }

        public static Process Build(string cmd)
        {
            Console.WriteLine($"- Executing build script: {(cmd.Length > 22 ? cmd[..22] : cmd)}...");
            Process buildProc = null;
            if (!string.IsNullOrEmpty(cmd))
            {
                try
                {
                    if (cmd.StartsWith(COMPILER))
                    {
                        var paths = Environment.GetEnvironmentVariable("PATH").Split(IsLinux ? ':' : ';') ?? new[] { "./" };

                        var compilerPath = paths.FirstOrDefault(i => File.Exists(i + PathSep + (IsLinux ? COMPILER : COMPILER + ".exe")));
                        if (Directory.Exists(compilerPath))
                        {
                            buildProc = Process.Start(compilerPath + PathSep + COMPILER, cmd.Remove(0, COMPILER.Length));
                        }
                        else Console.WriteLine("Error:" + COMPILER + " doesn't exist in PATH!");
                    }
                    else buildProc = Process.Start(cmd);
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
            else Console.WriteLine($"Error:build script is emtpy!");
            return buildProc;
        }

        public static Dictionary<string, string> CallResGen(string resxFile, string projectName)
        {
            Dictionary<string, string> resBook = new();

            var frmCsFile = resxFile.Replace(".resx", ".cs");
            var strongTypeCsFile = resxFile.Replace(".resx", ".designer.cs");
            string myNamespace = projectName.Replace(" ", "_");

            try
            {
                var file2Open = File.Exists(frmCsFile) ? frmCsFile : (File.Exists(strongTypeCsFile) ? strongTypeCsFile : null);
                if (!string.IsNullOrEmpty(file2Open))
                {
                    using var csReader = new StreamReader(File.OpenRead(file2Open));
                    while (!csReader.EndOfStream)
                    {
                        var line = csReader.ReadLine().Trim().Split(' ');
                        if (line.Length >= 2 && line[0] == "namespace")
                        {
                            myNamespace = line[1];
                            break;
                        }
                    }
                }

                var targetResourcesFileName = myNamespace + "." + Path.GetFileNameWithoutExtension(resxFile) + ".resources";
                var tempDir = Directory.CreateTempSubdirectory().FullName;
                var pathToRes = tempDir + PathSep + targetResourcesFileName;
                Process.Start(ResGenPath, new[] { "/useSourcePath", resxFile, pathToRes }).WaitForExit();
                resBook.TryAdd(Path.GetFullPath(pathToRes), "");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return resBook;
        }

        public static bool CopyFile(string src, string dest)
        {
            src = src.TrimQuotes();
            dest = dest.TrimQuotes();
            if (File.Exists(src))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(src, dest);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Exclude Exclus and Runtime libs.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static IEnumerable<string> DoExclude(this IEnumerable<string> paths) => paths
            .Where(i => !i.Contains(PathSep + "runtime."))
            .Where(i => !LibExclu.Any(x => i.Contains(PathSep + x + PathSep)));

        public static IEnumerable<string> ExtractAttributePathValues(this IEnumerable<XElement> x, string attributeName, string refPath, Dictionary<string, string> msBuildMacros)
            => x.SelectMany(i => GetAbsPaths(ApplyMacros(i.Attribute(attributeName)?.Value, msBuildMacros), refPath));

        public static string[] ExtractExclu(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                try
                {
                    Console.WriteLine($"Extracting exclus from:{path}");
                    return Directory.GetFiles(path, "*.*.dll").Select(i => Path.GetFileNameWithoutExtension(i).ToLower())
                        .Where(i => i.StartsWith("system.") || i.StartsWith("microsoft.")).ToArray();
                }
                catch (Exception ex) { Console.WriteLine($"{ex.Message}{NL}"); }

            return Array.Empty<string>();
        }

        public static int FlattenProject(string projectName, string projectPath, Verb action,
                                         IEnumerable<string> codeBook, IEnumerable<string> libBook,
                                         IEnumerable<string> nativeLibBook, Dictionary<string, string> resBook,
                                         IEnumerable<string> linkerArg, IEnumerable<string> definedConstants, IEnumerable<string> restArgs,
                                         string outputPath = null)
        {
            codeBook = codeBook.Distinct(PathEqualityComparer);
            libBook = libBook.Distinct(PathEqualityComparer);
            nativeLibBook = nativeLibBook.Distinct(PathEqualityComparer);
            restArgs = restArgs.Distinct(ArgEqualityComparer);
            linkerArg = linkerArg.Distinct(ArgEqualityComparer);
            definedConstants = definedConstants.Distinct(ArgEqualityComparer);

            Console.WriteLine($"Flatten project:{projectName}");
            string targetRoot = null;
            try
            {
                targetRoot = Directory.CreateDirectory(outputPath ?? $"{projectName}.flat").FullName;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -1;
            }

            string getFlattenedDest(string src) => Path.GetFullPath(targetRoot + PathSep + Path.GetRelativePath(projectPath, src).Replace(".." + PathSep, ""));

            List<string> doCopy(IEnumerable<string> book)
            {
                List<string> localBook = new();
                foreach (var f in book.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).Order())
                {
                    var dest = getFlattenedDest(f);
                    if (CopyFile(f, dest)) localBook.Add(dest);
                }
                return localBook;
            }

            IEnumerable<string> flatBook = null;

            StringBuilder cmd = new();
            if (codeBook.Any())
            {
                flatBook = doCopy(codeBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))));
                if (flatBook.Any())
                {
                    cmd.Append(NL);
                    cmd.AppendJoin(NL, flatBook);
                }
                Console.WriteLine($"- Copied {flatBook.Count()}/{codeBook.Count()} code files(*.cs)");
            }
            if (resBook.Any())
            {
                flatBook = doCopy(resBook.DistinctBy(kv => Path.GetFileName(kv.Key))
                    .Select(kv => Path.GetFullPath(kv.Key.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).Order());
                if (flatBook.Any())
                {
                    cmd.Append(NL + "-res ");
                    cmd.AppendJoin(NL + "-res ", flatBook);
                }
                Console.WriteLine($"- Copied {flatBook.Count()}/{resBook.Count} embedded resources(*.resx and other)");
            }

            if (libBook.Any())
            {
                var book = libBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).Order();
                flatBook = action == Verb.FlattenAll ? doCopy(book) : book;
                if (flatBook.Any())
                {
                    cmd.Append(NL + "-r ");
                    cmd.AppendJoin(NL + "-r ", flatBook);
                }
                Console.WriteLine($"- {(action == Verb.FlattenAll ? "Copied" : "Found")} {flatBook.Count()}/{libBook.Count()} dependent libs(*.dll|*.so)");
            }
            if (nativeLibBook.Any())
            {
                var book = nativeLibBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).Order();
                flatBook = action == Verb.FlattenAll ? doCopy(book) : book;
                if (flatBook.Any())
                {
                    cmd.Append(NL + "--ldflags ");
                    cmd.AppendJoin(NL + "--ldflags ", flatBook);
                }
                Console.WriteLine($"- {(action == Verb.FlattenAll ? "Copied" : "Referenced")} {flatBook.Count()}/{nativeLibBook.Count()} dependent native libs(*.lib|*.a)");
            }

            if (definedConstants.Any())
            {
                cmd.Append(NL + "-d ");
                cmd.AppendJoin(NL + "-d ", definedConstants);
            }
            if (linkerArg.Any())
            {
                cmd.Append(NL + "--ldflags ");
                cmd.AppendJoin(NL + "--ldflags ", linkerArg);
            }
            if (restArgs.Any())
            {
                cmd.Append(NL);
                cmd.AppendJoin(NL, restArgs);
            }

            if (cmd.Length > 0)
            {
                Console.WriteLine($"Writing '{projectName}.bfa'...");
                cmd.Insert(0, $"#Project:[{projectName}], Timestamp:[{DateTime.Now}]{NL}" +
                    $"#This BFA file is generated to be served to BFlatA(https://github.com/xiaoyuvax/bflata) with -inc:<BFA file> option.");
                WriteScript(cmd.ToString(), $"{targetRoot}{PathSep}{projectName}.bfa");
            }
            else Console.WriteLine($"No dependencies found.");

            return 0;
        }

        public static Dictionary<string, string> FlattenResX(Dictionary<string, string> resBook, string projectName)
        {
            Dictionary<string, string> myResBook = new();
            foreach (var r in resBook)
            {
                var fullPath = Path.GetFullPath(r.Key.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory));
                if (Path.GetExtension(fullPath) == ".resx") foreach (var kv in CallResGen(fullPath, projectName)) myResBook.TryAdd(kv.Key, kv.Value);  //use absPath
                else myResBook.TryAdd(r.Key, null);  //use relPath
            }

            return myResBook;
        }

        public static string GenerateScript(string projectName,
                                            IEnumerable<string> restArgs,
                                            IEnumerable<string> codeBook,
                                            IEnumerable<string> libBook,
                                            IEnumerable<string> nativeLibBook,
                                            IDictionary<string, string> resBook,
                                            BuildMode buildMode,
                                            string packageRoot,
                                            string outputType = "Exe",
                                            bool isDependency = false,
                                            string outputFile = null)
        {
            restArgs = restArgs.Distinct(ArgEqualityComparer);
            codeBook = codeBook.Distinct(PathEqualityComparer);
            libBook = libBook.Distinct(PathEqualityComparer);
            nativeLibBook = nativeLibBook.Distinct(PathEqualityComparer);
            resBook = resBook.DistinctBy(kv => Path.GetFileName(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);//TODO: res uniqueness, by path or by name.

            Console.WriteLine($"Generating build script for:{projectName}");

            StringBuilder cmd = new();
            cmd.AppendLine($"#Project:[{projectName}], Timestamp:[{DateTime.Now}]");
            cmd.AppendLine($"#This response file is generated by BFlatA(https://github.com/xiaoyuvax/bflata), and is intended to be compiled by BFlat (https://github.com/bflattened/bflat).");
            if (buildMode == BuildMode.Tree)
            {
                if (!HasOption(restArgs, "-o"))
                {
                    if (isDependency)
                    {
                        cmd.AppendLine($"-o {projectName}.dll ");  //all dependencies will use the ext of ".dll", even it's an exe, the name doesn't matter.
                    }
                    else if (!string.IsNullOrEmpty(outputFile))
                    {
                        cmd.AppendLine($"-o {outputFile} ");
                    }
                }

                if (!string.IsNullOrEmpty(outputType))
                {
                    cmd.AppendLine($"--target {outputType} ");
                }

                if (restArgs.Any())
                {
                    cmd.AppendLine(string.Join(NL, restArgs));  // arg per line for Response File
                    Console.WriteLine($"- Found {restArgs.Count()} args to be passed to BFlat.");
                }
            }

            if (codeBook.Any())
            {
                cmd.AppendJoin(NL, codeBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).Order());
                Console.WriteLine($"- Found {codeBook.Count()} code files(*.cs)");
            }

            if (libBook.Any())
            {
                cmd.Append(NL + "-r ");
                cmd.AppendJoin(NL + "-r ", libBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, packageRoot))).Order());
                Console.WriteLine($"- Found {libBook.Count()} dependent libs(*.dll|*.so)");
            }
            if (nativeLibBook.Any())
            {
                cmd.Append(NL + "--ldflags ");
                cmd.AppendJoin(NL + "--ldflags ", nativeLibBook.Select(i => "\"" + Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory)) + "\"").Order());
                Console.WriteLine($"- Found {nativeLibBook.Count()} dependent native libs(*.lib|*.a)");
            }

            if (resBook.Any())
            {
                cmd.Append(NL + "-res ");
                var distinctRes = resBook.Select(kv => Path.GetFullPath(kv.Key.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory)) + (string.IsNullOrEmpty(kv.Value) ? "" : "," + kv.Value)).Order();
                cmd.AppendJoin(NL + "-res ", distinctRes);
                Console.WriteLine($"- Found {distinctRes.Count()} embedded resources(*.resx and other)");
            }
            if (buildMode != BuildMode.Tree) cmd.Append(NL);  //last return at the end of a script;

            return cmd.ToString();
        }

        public static bool HasOption(IEnumerable<string> args, string optNameWithCapChar)
        {
            var optLen = optNameWithCapChar.Length;
            return args.Any(i => i.Length > optLen && i[..optLen].ToLower() == optNameWithCapChar.ToLower() && (i[optLen] == ARG_EVALUATION_CHAR || i[optLen] == ' '));
        }

        public static IEnumerable<string> GetAbsPaths(string path, string basePath)
        {
            if (string.IsNullOrEmpty(path)) return Array.Empty<string>();

            string fullPath = Path.GetFullPath(ToSysPathSep(path), basePath);
            string pattern = Path.GetFileName(fullPath);
            if (pattern.Contains('*'))
            {
                fullPath = Path.GetDirectoryName(fullPath);
                string[] fileLst = Array.Empty<string>();
                try
                {
                    fileLst = Directory.GetFiles(fullPath, pattern);
                    foreach (var i in fileLst) Console.Write(i); //Debug:
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
                return fileLst;
            }
            else return new[] { fullPath };
        }

        public static List<string> GetCodeFiles(string path, List<string> removeLst, List<string> includeLst = null, Dictionary<string, string> msBuildMacros = null)
        {
            List<string> codeFiles = new();
            try
            {
                var files = Directory.GetFiles(ToSysPathSep(path), "*.cs").Except(removeLst)
                    .Where(i => !IGNORED_SUBFOLDER_NAMES.Any(x => i.Contains(PathSep + x + PathSep)));
                if (includeLst != null) files = files.Concat(includeLst);
                files = files.Distinct(PathEqualityComparer).ToRefedPaths();
                codeFiles.AddRange(files);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            foreach (var d in Directory.GetDirectories(path).Where(i => !IGNORED_SUBFOLDER_NAMES.Any(j => i.ToLower().EndsWith(j)))) codeFiles.AddRange(GetCodeFiles(d, removeLst));

            return codeFiles;
        }

        public static string GetExt(string outputType = "Exe") => outputType switch
        {
            "Exe" => "",
            "WinExe" => ".exe",
            _ => ".dll"
        };

        public static string[] GetFrameworkLibs(string frameworkName)
        {
            var runtimePath = GetFrameworkPath(frameworkName);

            if (Directory.Exists(runtimePath)) return Directory.GetFiles(runtimePath, "*.dll");
            else return Array.Empty<string>();
        }

        public static string GetFrameworkPath(string frameworkName)
        {
            string getLibPath(string fxPath) => Path.GetFullPath(Directory.GetDirectories(fxPath).OrderDescending()
                       .FirstOrDefault(i => i.Contains($"{PathSep}{TargetFx.Replace("net", "")}")) + RuntimesPathSegment + OSArchMoniker + LibPathSegment + TargetFx);

            if (!string.IsNullOrEmpty(PackageRoot))
            {
                string frameworkPath = PackageRoot + PathSep + $"{frameworkName}.{OSArchMoniker}";
                if (Directory.Exists(frameworkPath)) return getLibPath(frameworkPath);
            }

            return null;
        }

        public static Dictionary<string, string> GetMacros(string projectFile)
        {
            string projectPath = Path.GetDirectoryName(projectFile);
            if (string.IsNullOrEmpty(projectPath)) projectPath = ".";
            projectPath = Path.GetFullPath(projectPath);

            string projectName = Path.GetFileNameWithoutExtension(projectFile);
            return new()
            {
                { "MSBuildProjectDirectory",projectPath.TrimPathEnd()},
                //{"MSBuildProjectDirectoryNoRoot},
                { "MSBuildProjectExtension",Path.GetExtension(projectFile)},
                { "MSBuildProjectFile",Path.GetFileName(projectFile)},
                { "MSBuildProjectFullPath",projectFile},
                { "MSBuildProjectName",projectName},
                { "MSBuildRuntimeType",TargetFx},

                { "MSBuildThisFile",Path.GetFileName(projectFile)},
                { "MSBuildThisFileDirectory",projectPath.TrimPathEnd() + PathSep},
                //{"MSBuildThisFileDirectoryNoRoot},
                { "MSBuildThisFileExtension",Path.GetExtension(projectFile)},
                { "MSBuildThisFileFullPath",projectPath},
                { "MSBuildThisFileName",Path.GetFileNameWithoutExtension(projectFile)},

                {"MSBuildStartupDirectory", MSBuildStartupDirectory.TrimPathEnd() },
                {"NativeOutputPath", Path.GetFullPath( Path.GetDirectoryName(OutputFile ?? "./.")).TrimPathEnd()+PathSep },
                {"TargetName", projectName },
                { "NativeBinaryExt",OutputType.ToLower() switch{ "exe" => IsLinux?"":".exe","winexe"=>".exe", "shared"=> IsLinux?".so":".dll", _=>""} }
            };
        }

        public static string GetOSMoniker(string archArg) => archArg switch { "windows" => "win", "linux" => "linux", _ => "uefi" };

        public static bool IsReadable(string file)
        {
            if (File.Exists(file))
            {
                try
                {
                    Thread.Sleep(1000);
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }
                catch { return false; }
            }
            return false;
        }

        public static List<string> LoadCache(string fileName)
        {
            List<string> cache = new();
            int count = 0;
            try
            {
                using var st = new StreamReader(File.OpenRead(fileName));
                Console.Write($"Items loaded:");
                var lastCursorPost = Console.GetCursorPosition();
                while (!st.EndOfStream)
                {
                    Console.SetCursorPosition(lastCursorPost.Left, lastCursorPost.Top);

                    var line = st.ReadLine();
                    if (!string.IsNullOrEmpty(line) && Directory.Exists(line))
                    {
                        cache.Add(line);
                        count++;
                        Console.Write(count);
                    }
                }
                return cache.DoExclude().ToList();
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            Console.WriteLine("");

            return cache;
        }

        public static void LoadExclu(string excluFile)
        {
            int count = 0;
            List<string> exclude = new();
            try
            {
                if (File.Exists(excluFile))
                {
                    count = 0;
                    Console.WriteLine($"Exclu file found:.{PathSep}{Path.GetFileName(excluFile)}");
                    Console.Write($"Exclus loaded:");
                    (int Left, int Top) = Console.GetCursorPosition();
                    using var st = new StreamReader(File.OpenRead(excluFile));
                    while (!st.EndOfStream)
                    {
                        var line = st.ReadLine().ToLower();
                        if (!string.IsNullOrEmpty(line) && !line.StartsWith('#') && !exclude.Contains(line))
                        {
                            exclude.Add(line);
                            count++;
                        }
                        Console.SetCursorPosition(Left, Top);
                        Console.Write(count);
                    }
                    LibExclu = exclude.ToArray();
                }

                Console.WriteLine();  //NL here
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            Console.WriteLine("");
        }

        /// <summary>
        /// Match Referenced Packages from LibCache
        /// </summary>
        /// <param name="allLibPaths"></param>
        /// <param name="packageReferences"></param>
        /// <param name="packageRoot"></param>
        /// <param name="multiTargets"></param>
        /// <param name="libBook">Reference of libBook shall not be altered</param>
        /// <returns></returns>
        public static List<string> MatchPackages(string[] allLibPaths,
                                                 Dictionary<string, string> packageReferences,
                                                 string packageRoot,
                                                 IEnumerable<string> multiTargets = null,
                                                 List<string> libBook = null)
        {
            libBook ??= new();
            bool gotit = false;

            if (allLibPaths?.Length > 0)
                foreach (var package in packageReferences)
                {
                    gotit = false;

                    // string requiredLibPath = null, compatibleLibPath = null;
                    IEnumerable<string> matchedPackageOfVerPaths = null;
                    string actualTarget = null, actualVersion = null, packageNameLo = null, packagePathSegment = null;
                    packageNameLo = package.Key.ToLower();
                    packagePathSegment = PathSep + packageNameLo + PathSep;
                    //Check Exclu first
                    if (LibExclu.Contains(packageNameLo))
                    {
                        if (UseVerbose) Console.WriteLine($"Info:ignore package Exclu:{packageNameLo}");
                    }
                    else foreach (var target in multiTargets)
                        {
                            matchedPackageOfVerPaths = null;
                            actualTarget = null;

                            int[] loVerReq = Array.Empty<int>(), hiVerReq = Array.Empty<int>();
                            if (package.Value.StartsWith('[') && package.Value.EndsWith(']')) //version range
                            {
                                var split = package.Value.TrimStart('[').TrimEnd(']').Split(',');
                                if (split.Length >= 2)
                                {
                                    loVerReq = Ver2IntArray(split[0]);
                                    hiVerReq = Ver2IntArray(split[1]);
                                    //if the two versions of different length
                                    var maxLen = Math.Max(loVerReq.Length, hiVerReq.Length);
                                    loVerReq = loVerReq.PadRight(maxLen);
                                    hiVerReq = hiVerReq.PadRight(maxLen);
                                }
                            }
                            else loVerReq = hiVerReq = Ver2IntArray(package.Value);

                            if (loVerReq.Length == hiVerReq.Length) // can only compare when lengths equal.
                            {
                                matchedPackageOfVerPaths = allLibPaths.Where(i =>
                                {
                                    var splittedPath = new List<string>(i.Split(PathSep));
                                    var idx = splittedPath.IndexOf(packageNameLo);

                                    if (idx >= 0 && idx < splittedPath.Count - 3
                                    && (splittedPath[^1] == target || splittedPath[^1].StartsWith("netstandard")))
                                    {
                                        var verDigits = Ver2IntArray(splittedPath[idx + 1]);
                                        for (int j = 0; j < verDigits.Length; j++)
                                        {
                                            if (verDigits[j] > loVerReq[j] && verDigits[j] < hiVerReq[j]) return true;
                                            else if (verDigits[j] < loVerReq[j] || verDigits[j] > hiVerReq[j]) return false;
                                            else if (j == verDigits.Length - 1 && verDigits[j] >= loVerReq[j] && verDigits[j] <= hiVerReq[j]) return true;
                                        }
                                    }
                                    return false;
                                }); //don't have to DoExclude() here, for allLibPaths r filtered already.
                            }

                            //deduplication of libs references (in Flat mode, dependencies may not be compatible among projects, the top version will be kept)
                            string libPath = null, absLibPath = null;

                            if (matchedPackageOfVerPaths != null) foreach (var d in matchedPackageOfVerPaths)
                                {
                                    absLibPath = Path.GetFullPath(d + PathSep + package.Key + ".dll");

                                    //get case-sensitive file path
                                    try
                                    {
                                        absLibPath = Directory.GetFiles(d).FirstOrDefault(i => i.ToLower() == absLibPath.ToLower());
                                    }
                                    catch (Exception ex) { Console.WriteLine(ex.Message); }

                                    if (absLibPath != null)
                                    {
                                        //Incase packageRoot not given.
                                        libPath = string.IsNullOrEmpty(packageRoot) ? absLibPath : absLibPath.Replace(packageRoot, PATH_PLACEHOLDER);
                                        //Package name might be case-insensitive in .csproj file, while path will be case-sensitive on Linux.
                                        var duplicatedPackages = libBook.Where(i => i.Contains(packagePathSegment));

                                        if (duplicatedPackages.Any())
                                        {
                                            //determine newer version by path string order, no matter if libPath is one of duplicatedPackages.
                                            libPath = duplicatedPackages.Concat(new[] { libPath })
                                                .OrderByDescending(i => i.Replace("netstandard", "").Replace("net", "").Replace("netcoreapp", "").Replace("netcore", "").Replace(".", "")).First();
                                            foreach (var p in duplicatedPackages.ToArray()) libBook.Remove(p);
                                            libBook.Add(libPath);
                                        }
                                        else libBook.Add(libPath);

                                        gotit = true;
                                        break;
                                    }
                                }

                            if (libPath != null) //libPath should be the sole top lib reference in libBook
                            {
                                actualTarget = Path.GetFileName(Path.GetDirectoryName(libPath));
                                var splittedPath = new List<string>(libPath.Split(PathSep));
                                var idx = splittedPath.IndexOf(packageNameLo);
                                if (idx > 0) actualVersion = splittedPath[idx + 1];  //special case: microsoft.win32.registry\5.0.0\runtimes\win\lib\netstandard2.0\Microsoft.Win32.Registry.dll
                            }

                            //if no target matched from actual path, use 'target' specified by user.
                            actualTarget ??= target;
                            actualVersion ??= package.Value;

                            //Parse .nuspec file to obtain package dependencies, while libPath doesn't have to exist.
                            //Note:some package contains no lib, but .neuspec file with reference to other packages.
                            string packageOfVerPathSegment = packagePathSegment + actualVersion;
                            string packagOfVerPath = null;
                            if (gotit)
                            {
                                var firstHalf = absLibPath.Split(LibPathSegment).FirstOrDefault();
                                if (firstHalf.EndsWith(packageOfVerPathSegment)) packagOfVerPath = firstHalf;
                            }
                            else if (!LibExclu.Contains(packageNameLo)) packagOfVerPath = CacheNuspec.FirstOrDefault(i => i.EndsWith(packageOfVerPathSegment));

                            if (!string.IsNullOrEmpty(packagOfVerPath))
                            {
                                var nuspecPath = Path.GetFullPath(packagOfVerPath + PathSep + packageNameLo + ".nuspec");  //nuespec filename is all lower case
                                if (File.Exists(nuspecPath))
                                {
                                    using var stream = File.OpenRead(nuspecPath);
                                    var nuspecDoc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);

                                    var nodes = nuspecDoc.Root.Descendants(XSD_NUGETSPEC + "group");
                                    nodes = nodes.FirstOrDefault(g => g.Attribute("targetFramework")?.Value.ToLower().TrimStart('.') == actualTarget)?.Elements();
                                    var myNugetPackages = nodes?.ToDictionary(kv => kv.Attribute("id")?.Value, kv => kv.Attribute("version")?.Value);
                                    if (myNugetPackages?.Any() == true) libBook = MatchPackages(allLibPaths, myNugetPackages, packageRoot, new[] { actualTarget }, libBook); //Append Nuget dependencies to libBook
                                    break;
                                }
                                else Console.WriteLine($"Warning:nuspecFile not exists, packages dependencies cannot be determined!! {nuspecPath}");
                            }
                            else Console.WriteLine($"Warning:package referenced not found!! {packageNameLo} {actualVersion}");

                            //If any dependency found for any target, stop matching other targets in order(the other targets usually r netstandard).
                            if (gotit) break;
                        }
                }

            return libBook;
        }

        public static int[] PadRight(this int[] intArray, int length)
        {
            if (intArray.Length < length)
            {
                int[] newArray = new int[length];
                Array.Copy(intArray, newArray, intArray.Length);
                return newArray;
            }
            else return intArray;
        }

        public static List<string> ParseArgs(IEnumerable<string> args, bool reArrange = true)
        {
            //Parse input args
            List<string> restArgs = new(args);
            List<string> bfaFiles = new();
            if (args.Count() == 0 || args.Contains("-?") || args.Contains("/?") || args.Contains("-h") || args.Contains("--help"))
            {
                ShowHelp();
                restArgs.Clear();
                restArgs.Add("?");
            }
            else
            {
                // Verbs must present at the first arg.
                var verb = restArgs[0].ToLower();
                bool verbDetected = false;
                if (AllowedVerbs.Contains(verb))
                {
                    Action = verb switch
                    {
                        "build" => Verb.Build,
                        "build-il" => Verb.BuildIl,
                        "flatten" => Verb.Flatten,
                        "flatten-all" => Verb.FlattenAll,
                        _ => Verb.Script
                    };
                    verbDetected = true;
                    restArgs.RemoveAt(0);
                }

                if ((verbDetected || string.IsNullOrEmpty(RootProjectFile)) && !restArgs[0].StartsWith("-") && File.Exists(restArgs[0])) //input args don't need trimming quotes in path
                {
                    RootProjectFile = restArgs[0];
                    //Allow .BFA to be built.
                    if (Path.GetExtension(RootProjectFile).ToLower() == ".bfa")
                    {
                        bfaFiles.Add(RootProjectFile);
                    }
                    restArgs.RemoveAt(0);
                }

                //rearrange restArgs
                if (reArrange && restArgs.Count > 0) restArgs = string.Join(' ', restArgs).SplitArgs().ToList();

                //process options:
                //restArgs r changing through process, must be fixed to an array first.
                //process environmental args first:
                foreach (var a in restArgs.ToArray())
                {
                    if (TryTakeArg(a, "-h", "--home", restArgs, out string h))
                    {
                        h = h.TrimQuotes();
                        if (Directory.Exists(h)) MSBuildStartupDirectory = Path.GetFullPath(h).TrimPathEnd();
                        else Console.WriteLine($"Warning:Home path does not exist or is invalid! {h}");
                    }
                }

                //Apply macros
                if (!string.IsNullOrEmpty(RootProjectFile)) RootMacros = GetMacros(RootProjectFile);
                if (RootMacros.Any()) restArgs = restArgs.Select(i => i.ApplyMacros(RootMacros)).ToList();

                foreach (var a in restArgs.ToArray())
                {
                    if (TryTakeArg(a, "-pr", "--packageroot", restArgs, out string pr))
                    {
                        pr = pr.TrimQuotes();
                        if (Directory.Exists(pr)) PackageRoot = Path.GetFullPath(pr).TrimPathEnd();  //the ending PathSep may cause shell script variable invalid like $PRcommon.log/ after replacement by placeholder @
                        else Console.WriteLine($"Warning:PacakgeRoot does not exist or is invalid! {pr}");
                    }
                    else if (TryTakeArg(a, "", "--resgen", restArgs, out string rg))
                    {
                        rg = rg.TrimQuotes();
                        if (File.Exists(rg))
                        {
                            ResGenPath = Path.GetFullPath(rg).TrimPathEnd();
                            restArgs.Add("--feature:System.Resources.ResourceManager.AllowCustomResourceTypes=true");
                            restArgs.Add("--feature:System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization=true");
                        }
                        else Console.WriteLine($"Warning:Resgen.exe does not exist or is invalid! {rg}");
                    }
                    else if (TryTakeArg(a, "", "--linker", restArgs, out string lnk))
                    {
                        lnk = lnk.TrimQuotes().TrimPathEnd();
                        if (File.Exists(lnk))
                        {
                            Linker = Path.GetFullPath(lnk);
                            restArgs.Add("-c"); //suppress BFlat Linker
                        }
                        else Console.WriteLine($"Warning:Linker does not exist or is invalid! {lnk}");
                    }
                    else if (TryTakeArg(a, "-inc", "--include", restArgs, out string inc) && File.Exists(inc))
                    {
                        bfaFiles.Add(inc);
                    }
                    else if (TryTakeArg(a, "-pra", "--prebuild", restArgs, out string prb)) PreBuildActions.Add(prb);
                    else if (TryTakeArg(a, "-poa", "--postbuild", restArgs, out string pob)) PostBuildActions.Add(pob);
                    else if (TryTakeArg(a, "-bm", "--buildmode", restArgs, out string bm)) BuildMode = ParseBuildMode(bm);
                    else if (TryTakeArg(a, "", "--target", restArgs, out string t)) OutputType = t;
                    else if (TryTakeArg(a, "-fx", "--framework", restArgs, out string fx)) TargetFx = fx.ToLower();
                    else if (TryTakeArg(a, "", "--arch", restArgs, out string ax))
                    {
                        Architecture = ax.ToLower();
                        restArgs.Add($"--arch:{ax}");
                    }
                    else if (TryTakeArg(a, "", "--os", restArgs, out string os))
                    {
                        OS = os.ToLower();
                        restArgs.Add($"--os:{os}");
                    }
                    else if (TryTakeArg(a, "-o", "--out", restArgs, out string o)) //hijack -o arg of BFlat, and it shall not be passed to dependent project
                    {
                        OutputFile = o;
                        restArgs.Add($"-o:{o}");
                    }
                    else if (a.ToLower() == "--verbose")
                    {
                        UseVerbose = true;
                    }
                    else if (TryTakeArg(a, "-xx", "--exclufx", restArgs, out string xx))
                    {
                        xx = xx.TrimQuotes();
                        if (Directory.Exists(xx)) RuntimePathExclu = Path.GetFullPath(xx).TrimPathEnd();
                        else Console.WriteLine($"Warning:RuntimePath does not exist or is invalid! {xx}");
                    }
                    else if (a.ToLower() == "-dd" || a.ToLower() == "--depdep")
                    {
                        DepositLib = true;
                        restArgs.Remove(a);
                    }
                }
            }

            //process bflata args from included BFA files(the later will overwrite the earlier, except for ProjectFile)
            if (bfaFiles.Count > 0)
            {
                var lines = ReadAllFileLines(bfaFiles)
                    .Where(i => !i.TrimStart().StartsWith(COMMENT_TAG))
                    .SelectMany(i => i.SplitArgsButQuotes(preserveQuote: true));

                var rspRestArgs = ParseArgs(lines, false);
                restArgs.AddRange(rspRestArgs.Except(restArgs));
                BFAFiles.AddRange(bfaFiles);
            }

            //init arg values
            if (BuildMode == BuildMode.TreeDepositDependency)
            {
                BuildMode = BuildMode.Tree;
                DepositLib = true;
            }

            if (UseBuild)
            {
                if (BuildMode == BuildMode.None) BuildMode = BuildMode.Flat; //default BuildMode for Build option
            }
            else if (UseFlatten) BuildMode = BuildMode.Flat;  //can only flattens in Flat mode
            else if (BuildMode == BuildMode.Tree) Console.WriteLine("Warning:.rsp script generated under TREE mode is not buildable!");

            if (string.IsNullOrEmpty(TargetFx)) TargetFx = "net7.0";

            if (string.IsNullOrEmpty(RuntimePathExclu) && !string.IsNullOrEmpty(PackageRoot)) RuntimePathExclu = GetFrameworkPath(NETCORE_APP);

            return restArgs;
        }

        public static BuildMode ParseBuildMode(string typeStr) => typeStr.ToLower() switch
        {
            "flat" => BuildMode.Flat,
            "tree" => BuildMode.Tree,
            "treed" => BuildMode.TreeDepositDependency,
            _ => BuildMode.None
        };

        public static int ParseProject(BuildMode buildMode,
                                                 string projectFile,
                                                 string[] allLibPaths,
                                                 string target,
                                                 string packageRoot,
                                                 out string projectName,
                                                 out string outputType,
                                                 out string script,
                                                 in List<string> restArgs,
                                                 in List<string> codeBook,
                                                 in List<string> libBook,
                                                 in List<string> nativeLibBook,
                                                 in List<string> refProjectBook,
                                                 in List<string> linkerArgs,
                                                 in List<string> defineConstants,
                                                 in Dictionary<string, string> resBook,
                                                 bool isDependency = false)
        {
            outputType = "Exe";
            script = null;
            projectName = Path.GetFileNameWithoutExtension(projectFile);

            string projectPath = Path.GetFullPath(Path.GetDirectoryName(projectFile));

            if (string.IsNullOrEmpty(projectFile)) return -1;

            List<string> removeBook = new();
            List<string> includeBook = new();
            List<string> contentBook = new();
            List<string> myRefProject = new();
            Dictionary<string, ReferenceInfo> referenceBook = new();
            Dictionary<string, ReferenceInfo> nativeReferenceBook = new();

            bool useWinform = false;
            bool useWpf = false;
            int err = 0;

            List<string> targets = new();

            Dictionary<string, string> packageReferences = new();

            //TreeMode uses local macros for each project
            Dictionary<string, string> msBuildMacros = GetMacros(projectFile);

            ///Flag if already built
            if (!ParsedProjectPaths.Contains(projectFile))
            {
                Console.WriteLine($"Parsing Project:{projectFile} ...");
                if (!string.IsNullOrEmpty(projectFile) && File.Exists(projectFile))
                {
                    XDocument xdoc = new();
                    try
                    {
                        using var stream = File.OpenRead(projectFile);
                        xdoc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return -0x13;
                    }

                    //Start Analysis of csproj file.
                    var pj = xdoc.Root;

                    if (pj != null && pj.Name.LocalName.ToLower() == "project")
                    {
                        //Flatten all item groups
                        var ig = pj.Descendants().Where(i => i.Name.LocalName == "ItemGroup").SelectMany(i => i.Elements());
                        var imports = pj.Descendants("Import");

                        //Flatten all property groups
                        var pg = pj.Descendants("PropertyGroup").SelectMany(i => i.Elements());
                        targets = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "targetframework" || i.Name.LocalName.ToLower() == "targetframeworks")?.Value
                            .Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(i => i.ToLower())?
                            .ToList() ?? new() { target }; //if no targets specified, use default target.

                        outputType = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "outputtype")?.Value ?? outputType;
                        projectName = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "assemblyname")?.Value ?? projectName;
                        useWinform = pg.Any(i => i.Name.LocalName.ToLower() == "usewindowsforms");
                        useWpf = pg.Any(i => i.Name.LocalName.ToLower() == "usewpf");

                        //DefinedConstants
                        defineConstants.AddRange(pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "defineconstants")?.Value?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>());

                        //Compiler Args
                        var ilcOptimizationPreference = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "ilcoptimizationpreference")?.Value ?? "";
                        var nostdlib = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "nostdlib")?.Value;
                        var noStandardLibraries = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "nostandardlibraries")?.Value;
                        var ilcSystemModule = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "ilcsystemmodule")?.Value ?? "";
                        var optimize = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "optimize")?.Value ?? "";
                        if (nostdlib?.ToLower() == "true" || noStandardLibraries?.ToLower() == "true" || !string.IsNullOrEmpty(ilcSystemModule))
                        {
                            if (!restArgs.Any(i => i[..8].ToLower() == "--stdlib")) restArgs.Add("--stdlib:None");
                        }

                        if (!string.IsNullOrEmpty(ilcOptimizationPreference))
                        {
                            var existingOptFlag = restArgs.FirstOrDefault(i => i == "-Ot" || i == "-Os" || i == "-O0");

                            switch (ilcOptimizationPreference.ToLower())
                            {
                                case "speed":
                                    restArgs.Remove(existingOptFlag);
                                    restArgs.Add("-Ot");
                                    break;

                                case "size":
                                    restArgs.Remove(existingOptFlag);
                                    restArgs.Add("-Os");
                                    break;

                                default:
                                    if (optimize.ToLower() == "true") goto case "speed";
                                    break;
                            }
                        }

                        //Linker Args
                        var linkerArg = ig.FirstOrDefault(i => i.Name.LocalName.ToLower() == "linkerarg")?.Attribute("Include")?.Value;
                        var entrypointSymbol = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "entrypointsymbol")?.Value;
                        var linkerSubsystem = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "linkersubsystem")?.Value;
                        var incremental = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "incremental")?.Value;
                        var baseAddress = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "baseaddress")?.Value;
                        if (!string.IsNullOrEmpty(linkerArg)) linkerArgs.AddRange(linkerArg.SplitArgsButQuotesAsBarehead());
                        if (!string.IsNullOrEmpty(entrypointSymbol)) linkerArgs.Add((IsLinux ? "-entry:" : "/ENTRY:") + entrypointSymbol);
                        if (!string.IsNullOrEmpty(linkerSubsystem)) linkerArgs.Add((IsLinux ? "-subsystem:" : "/SUBSYSTEM:") + linkerSubsystem);
                        if (!string.IsNullOrEmpty(baseAddress)) linkerArgs.Add((IsLinux ? "-base:" : "/BASE:") + baseAddress);
                        linkerArgs.Add((IsLinux ? "-incremental:" : "/INCREMENTAL:") + (incremental ?? "no"));

                        //If project setting is not compatible with specified framework, then quit.
                        bool isStandardLib = targets?.Any(i => i.StartsWith("netstandard")) == true;

                        bool hasTarget = targets?.Any(i => i.Contains(target)) == true;
                        if (hasTarget || isStandardLib)
                        {
                            AddElement2List(ig.OfRemove("Compile"), removeBook, "Code Exclude", "Remove");
                            AddElement2List(ig.OfInclude("Compile"), includeBook, "Code");
                            AddElement2List(ig.OfInclude("Content"), contentBook, "Content");
                            AddElement2List(ig.OfInclude("NativeLibrary"), nativeLibBook, "NativeLib");
                            AddResources2Dict(ig.OfInclude("EmbeddedResource"), resBook, "EmbeddedResource", useAbsolutePath: false);

                            #region Compatible with earlier version of .csproj file(not tested), may not be supported in the future.

                            AddReferences2Dict(ig.OfInclude("Reference"), referenceBook, "ManagedAssembly");
                            AddReferences2Dict(ig.OfInclude("NativeReference"), nativeReferenceBook, "NativeReference");

                            //Managed Assemblies, ToDo:Test
                            foreach (var r in referenceBook)
                            {
                                //Assemblies with hintpath will be added to libBook
                                if (!string.IsNullOrEmpty(r.Value.HintPath)) libBook.Add(r.Value.HintPath);
                                //Assemblies with specific version will be added to packageReferences
                                else if (!string.IsNullOrEmpty(r.Value.SpecificVersion)) packageReferences.TryAdd(r.Key, r.Value.SpecificVersion);
                                else packageReferences.TryAdd(r.Key, "0.0.0.0-7.0.0.0"); //give a version range, but not tested
                            }

                            //Native references, ToDo:Test
                            nativeLibBook.AddRange(nativeReferenceBook.Select(i => i.Value.HintPath));

                            #endregion Compatible with earlier version of .csproj file(not tested), may not be supported in the future.

                            //Parse Package Dependencies
                            foreach (var pr in ig.OfInclude("PackageReference")) packageReferences.TryAdd(pr.Attribute("Include")?.Value, pr.Attribute("Version")?.Value);

                            //if specified targets is included, make it preferable to search.

                            if (hasTarget)
                            {
                                targets.Remove(target);
                                targets.Insert(0, target);
                            }
                            else targets = targets.Where(i => i.StartsWith("netstandard")).OrderByDescending(i => i).ToList();  //otherwise, only netstardard targets allowed.
                            List<string> libBook2;
                            //Match lib from cache (TreeMode: each project uses its own Libs, so don't pass in libBook)
                            if (buildMode == BuildMode.Tree)
                            {
                                if (DepositLib) libBook2 = MatchPackages(allLibPaths, packageReferences, packageRoot, targets, libBook);
                                else libBook2 = MatchPackages(allLibPaths, packageReferences, packageRoot, targets);
                            }
                            else libBook2 = MatchPackages(allLibPaths, packageReferences, packageRoot, targets, libBook);

                            //Search all CodeFiles in current workingPath and underlying subfolders, except those from removeList (all Paths r expanded to absolute paths)
                            //might include redundencies,and should be distinctized at the last step, the reference of codeBook shall not be changed here by using Dictinct().
                            codeBook.AddRange(GetCodeFiles(projectPath, removeBook, includeBook, msBuildMacros));

                            //Append Form related .resx file
                            foreach (var c in codeBook.Where(i => i.ToLower().EndsWith(".designer.cs"))
                                .Select(i => i[..^12] + ".resx")
                                .Where(i => File.Exists(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory)))
                                    ) resBook.TryAdd(c, null);

                            //Recursively parse/build all referenced projects
                            foreach (string p in ig.OfInclude("ProjectReference").ExtractAttributePathValues("Include", projectPath, msBuildMacros)
                                .Concat(imports.ExtractAttributePathValues("Project", projectPath, msBuildMacros))
                                .Where(i => !i.ToLower().EndsWith(".targets")))
                            {
                                string refPath = p.ApplyMacros(msBuildMacros);

                                if (!string.IsNullOrEmpty(refPath) && File.Exists(refPath))
                                {
                                    string innerScript = "", innerProjectName = "", innerOutputType = "";

                                    if (buildMode == BuildMode.Tree)
                                    {
                                        if (DepositLib) err = ParseProject(buildMode, refPath, allLibPaths, target,
                                                                           packageRoot, out innerProjectName,
                                                                           out innerOutputType, out innerScript,
                                                                           restArgs, null, libBook2, nativeLibBook, refProjectBook,
                                                                           null, null, null, true);
                                        else err = ParseProject(buildMode, refPath, allLibPaths, target,
                                                                packageRoot, out innerProjectName, out innerOutputType,
                                                                out innerScript, restArgs, new List<string>(),
                                                                new List<string>(), new List<string>(),
                                                                new List<string>(), new List<string>(),
                                                                new List<string>(), new Dictionary<string, string>(),
                                                                true);
                                    }
                                    else err = ParseProject(buildMode, refPath, allLibPaths, target, packageRoot,
                                                            out innerProjectName, out innerOutputType, out innerScript,
                                                            restArgs, codeBook, libBook2, nativeLibBook,
                                                            new List<string>(), new List<string>(), new List<string>(),
                                                            resBook, true);

                                    if (err == 0 && buildMode == BuildMode.Tree)  // <0 bflata errors, >0 bflat errors
                                    {
                                        script = AppendScriptBlock(script, innerScript);

                                        //add local projects to references
                                        myRefProject.Add("-r " + innerProjectName + GetExt(innerOutputType));
                                    }
                                    else if (err != 0)
                                    {
                                        Console.WriteLine($"Error:failure building dependency:{projectName}=>{innerProjectName}!!! ");
                                        break; //any dependency failure,break!
                                    }
                                }
                            }

                            //Add Predefined Framework dependencies
                            if (useWinform) foreach (var p in GetFrameworkLibs(WINDESKTOP_APP).Where(i => !Path.GetFileName(i).StartsWith("PresentationFramework"))) libBook.Add(p.Replace(packageRoot, PATH_PLACEHOLDER));
                            if (useWpf) foreach (var p in GetFrameworkLibs(WINDESKTOP_APP)) libBook.Add(p.Replace(packageRoot, PATH_PLACEHOLDER));

                            //build current project, so far all required referenced projects must have been built to working dir (as .dll).
                            if (err == 0 && buildMode == BuildMode.Tree)
                            {
                                string myScript = "";
                                string argOutputFile = projectName + (isDependency ? ".dll" : (IsLinux ? "" : ".exe"));

                                refProjectBook?.AddRange(myRefProject.Except(refProjectBook));
                                var refProjectBook2 = new List<string>(refProjectBook);
                                string getRefProjectLines() => string.Join("", (DepositLib ? refProjectBook2 : myRefProject).Select(i => buildMode == BuildMode.Tree ? NL + i : i + NL));

                                Dictionary<string, string> myFlatResBook = FlattenResX(resBook, projectName);

                                RouteLinkerArgs(restArgs, linkerArgs, nativeLibBook, false);
                                restArgs.AddRange(defineConstants.Select(i => "-d " + i));

                                if (UseBuild)
                                {
                                    myScript = GenerateScript(projectName, restArgs, codeBook, libBook, nativeLibBook, myFlatResBook, buildMode, packageRoot, outputType, isDependency, argOutputFile);
                                    myScript += getRefProjectLines();

                                    Process buildProc = null;

                                    WriteScript(myScript);
                                    Console.WriteLine($"Building {(isDependency ? "dependency" : "root")}:{projectName}...");
                                    if (isDependency) buildProc = Build("bflat build-il @build.rsp");
                                    else buildProc = Build($"bflat {(UseBuildIL ? "build-il" : "build")} @build.rsp");

                                    buildProc?.WaitForExit();
                                    Console.WriteLine($"Compiler exit code:{buildProc.ExitCode}");

                                    if (buildProc.ExitCode != 0)
                                    {
                                        Console.WriteLine($"Error:Building failure:{projectName}!!!");
                                        return buildProc.ExitCode;
                                    }
                                    //Must wait for dependecy to be compiled
                                    if (!SpinWait.SpinUntil(() => IsReadable(argOutputFile), 10000)) Console.WriteLine($"Error:building timeout!");
                                    else Console.WriteLine("Script execution completed!");
                                }
                                else if (UseFlatten || UseFlattenAll)
                                {
                                }
                                else
                                {
                                    myScript = GenerateScript(projectName, restArgs.Concat(linkerArgs), codeBook, libBook, nativeLibBook, myFlatResBook, buildMode, packageRoot, outputType, isDependency, argOutputFile);
                                    myScript += getRefProjectLines();
                                    Console.WriteLine($"Appending Script:{projectName}...{NL}");
                                    script = AppendScriptBlock(script, myScript);
                                }
                            }
                            else if (err != 0) return err;
                        }
                        else
                        {
                            Console.WriteLine($"Warnning:Project properties are not compatible with the target:{target}, {projectFile}!!! ");
                            return -0x13;
                        }

                        ParsedProjectPaths.Add(projectFile);

                        //[Local methods]
                        int AddElement2List(IEnumerable<XElement> elements, List<string> book, string displayAction, string action = "Include", bool useAbsolutePath = true)
                        {
                            //This method is easy to extent to more categories.
                            //CodeFiles and PackageReferences r exceptions and stored otherwise.
                            int counter = 0;
                            foreach (var i in elements)
                            {
                                IEnumerable<string> items = ExtractAttributePathValues(elements, action, projectPath, msBuildMacros);
                                //relative paths used by script is relative to WorkingPath
                                if (!useAbsolutePath) items = items.ToRefedPaths();

                                book.AddRange(items.Except(book));

                                counter += items.Count();
                            }
                            if (counter > 0) Console.WriteLine($"{displayAction,24}\t[{action}]\t{counter} items added!");
                            return counter;
                        }

                        int AddResources2Dict(IEnumerable<XElement> elements, Dictionary<string, string> book, string displayAction, string action = "Include", bool useAbsolutePath = true)
                        {
                            int counter = 0;

                            IEnumerable<string> items = ExtractAttributePathValues(elements, action, projectPath, msBuildMacros);
                            //relative paths used by script is relative to WorkingPath
                            if (!useAbsolutePath) items = items.ToRefedPaths();

                            foreach (var p in items)
                            {
                                if (book.TryAdd(p, Path.GetFileName(p))) counter++;
                            }

                            if (counter > 0) Console.WriteLine($"{displayAction,24}\t[{action}]\t{counter} items added!");
                            return counter;
                        }
                        int AddReferences2Dict(IEnumerable<XElement> elements, Dictionary<string, ReferenceInfo> book, string displayAction, string action = "Include", bool useAbsolutePath = true)
                        {
                            int counter = 0;
                            foreach (var i in elements)
                            {
                                var assmblyName = i.Attribute(action)?.Value;
                                var specificVersion = i.Descendants("SpecificVersion").FirstOrDefault()?.Value;
                                var hintPath = i.Descendants("HintPath").FirstOrDefault()?.Value;
                                bool _private = i.Descendants("Private").FirstOrDefault()?.Value.ToLower() == "true";

                                hintPath = GetAbsPaths(hintPath, projectPath).FirstOrDefault();
                                if (book.TryAdd(assmblyName, new ReferenceInfo() { HintPath = hintPath, Name = assmblyName, SpecificVersion = specificVersion, Private = _private })) counter++;
                            }
                            if (counter > 0) Console.WriteLine($"{displayAction,24}\t[{action}]\t{counter} items added!");
                            return counter;
                        }
                    }
                    else return -0x12;
                }
                else return -0x11;
            }
            else
            {
                if (UseVerbose) Console.WriteLine($"Warning:project already parsed, ignoring it..." + projectFile);
            }

            return 0;
        }

        /// <summary>
        /// Pre-caching nuget package "/lib/" paths
        /// </summary>
        /// <param name="packageRoot"></param>
        public static void PreCacheLibs(string packageRoot)
        {
            Console.WriteLine($"Caching Nuget packages from path:{packageRoot} ...");

            int pathCount = 0;
            var lastCursorPost = Console.GetCursorPosition();
            bool dontCacheLib = CacheLib.Any();
            bool dontCacheNuspec = CacheNuspec.Any();
            try
            {
                if (!string.IsNullOrEmpty(packageRoot))
                    foreach (var i in Directory.GetDirectories(packageRoot, "*", SearchOption.AllDirectories).DoExclude())
                    {
                        pathCount++;
                        Console.SetCursorPosition(lastCursorPost.Left, lastCursorPost.Top);
                        Console.Write($"Libs found:{CacheLib.Count}/Nuspec found:{CacheNuspec.Count}/Folders searched:{pathCount}");
                        var splitted = i.Split(PathSep, StringSplitOptions.RemoveEmptyEntries);

                        if (!dontCacheLib && splitted[^2].ToLower() == "lib") CacheLib.Add(i);
                        else if (!dontCacheNuspec && Directory.GetFiles(i, "*.nuspec").Any()) CacheNuspec.Add(i);
                    };
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            Console.WriteLine($"{NL}Found {CacheLib.Count} nuget packages!");
        }

        public static List<string> ReadAllFileLines(IEnumerable<string> rspFiles)
        {
            List<string> lines = new();
            foreach (string i in rspFiles)
                try
                {
                    using StreamReader sr = new(File.OpenRead(i));
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine()?.Trim();
                        if (!string.IsNullOrEmpty(line)) lines.Add(line);
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            return lines;
        }

        public static string[] RemoveArg(string[] restParams, string a) => restParams.Except(new[] { a }).ToArray();

        public static void RouteLinkerArgs(in List<string> restArgs, in List<string> linkerArgs, in List<string> nativeLibs, bool useLinker)
        {
            //split ldFlags to minimal
            List<string> ldFlagArgs = new();
            foreach (var ldFlag in restArgs.Where(i => i.ToLower().StartsWith("--ldflags")).ToArray())
            {
                ldFlagArgs.Add(ldFlag.Replace("--ldflags ", "").Replace("--ldflags:", "").TrimQuotes().Replace('\'', '"'));
                restArgs.Remove(ldFlag);
            }

            ldFlagArgs = ldFlagArgs.SelectMany(i => i.SplitArgsButQuotes()) //split "-" capped args
                .SelectMany(i => i.SplitArgsButQuotesWindows())  //split "/" capped args
                .ToList();

            if (useLinker)
            {
                linkerArgs.AddRange(ldFlagArgs);
                linkerArgs.AddRange(nativeLibs);
            }
            else
            {
                restArgs.AddRange(ldFlagArgs.Select(i => "--ldflags " + i));
                restArgs.AddRange(linkerArgs.Select(i => "--ldflags " + i));
            }
            return;
        }

        public static void ShowHelp()
        {
            Console.WriteLine($"  Usage: bflata [build|build-il|flatten|flatten-all] <root csproj file> [options]{NL}");
            Console.WriteLine("  [build|build-il|flatten|flatten-all]".PadRight(COL_WIDTH) + "BUILD|BUILD-IL = build with BFlat in %Path% in native or in IL.");
            Console.WriteLine($"{"",-COL_WIDTH}FLATTEN = extract code files from project hierachy into a \"flattened, Go-like\" path hierachy,");
            Console.WriteLine($"{"",-COL_WIDTH}FALTTEN-ALL = flatten + copy all dependencies and resources to dest path,");
            Console.WriteLine($"{"",-COL_WIDTH}both with dependency references written to a BFA file.");
            Console.WriteLine($"{"",-COL_WIDTH}If omitted, generate build script only, with -bm option still valid.{NL}");
            Console.WriteLine("  <root .csproj file>".PadRight(COL_WIDTH) + "Must be the 2nd arg if 'build' specified, or the 1st otherwise, only 1 root project allowed.");
            Console.WriteLine($"{NL}Options:{NL}");
            Console.WriteLine("  -pr|--packageroot:<path to package storage>".PadRight(COL_WIDTH) + $"eg.'C:\\Users\\%username%\\.nuget\\packages' or '$HOME/.nuget/packages'.{NL}");
            Console.WriteLine("  -h|--home:<MSBuildStartupDirectory>".PadRight(COL_WIDTH) + $"Path to VS solution usually, default:current directory.");
            Console.WriteLine($"{"",-COL_WIDTH}Caution: this path may not be the same as <root csproj file>,");
            Console.WriteLine($"{"",-COL_WIDTH}and is needed for entire solution to compile correctly.{NL}");
            Console.WriteLine("  -fx|--framework:<moniker>".PadRight(COL_WIDTH) + "The TFM compatible with the built-in .net runtime of BFlat(see 'bflat --info')");
            Console.WriteLine($"{"",-COL_WIDTH}mainly purposed for matching dependencies, e.g. 'net7.0'{NL}");
            Console.WriteLine("  -bm|--buildmode:<flat|tree|treed>".PadRight(COL_WIDTH) + "FLAT = flatten project tree to one for building;(default)");
            Console.WriteLine($"{"",-COL_WIDTH}TREE = build each project alone and reference'em accordingly with -r option;");
            Console.WriteLine($"{"",-COL_WIDTH}TREED = '-bm:tree -dd'.{NL}");
            Console.WriteLine("  --resgen:<path to resgen.exe>".PadRight(COL_WIDTH) + $"Path to Resource Generator(e.g. ResGen.exe).{NL}");
            Console.WriteLine("  -inc|--include:<path to BFA file>".PadRight(COL_WIDTH) + $"BFA files(.bfa) contain any args for BFlatA, each specified by -inc:<filename>.{NL}");
            Console.WriteLine($"{"",-COL_WIDTH}Unlike RSP file, each line in BFA file may contain multiple args with macros enabled(listed at foot).");
            Console.WriteLine($"{"",-COL_WIDTH}BFAs can be used as project-specific build profile, somewhat equivalent to .csproj file.");
            Console.WriteLine($"{"",-COL_WIDTH}If any arg duplicated, valid latters will overwrite, except for <root .csproj file>.{NL}");
            Console.WriteLine("  -pra|--prebuild:<cmd or path to executable>".PadRight(COL_WIDTH) + $"One command line to execute before build.(Multiple allowed).{NL}");
            Console.WriteLine("  -poa|--postbuild:<cmd or path to executable>".PadRight(COL_WIDTH) + $"One command line to execute after build.(Multiple allowed).{NL}");
            Console.WriteLine($"{NL}Shared Options with BFlat:{NL}");
            Console.WriteLine("  --target:<Exe|Shared|WinExe>".PadRight(COL_WIDTH) + $"Build Target.Default:<BFlat default>{NL}");
            Console.WriteLine("  --os <Windows|Linux|Uefi>".PadRight(COL_WIDTH) + $"Build Target.Default:Windows.{NL}");
            Console.WriteLine("  --arch <x64|arm64|x86|...>".PadRight(COL_WIDTH) + $"Platform archetecture.<BFlat default>{NL}");
            Console.WriteLine("  -o|--out:<File>".PadRight(COL_WIDTH) + $"Output file path for the root project.{NL}");
            Console.WriteLine("  --verbose".PadRight(COL_WIDTH) + "Enable verbose logging");
            Console.WriteLine($"{NL}Obsolete Options:{NL}");
            Console.WriteLine("  -dd|--depdep".PadRight(COL_WIDTH) + "Deposit Dependencies mode, valid with '-bm:tree', equivalently '-bm:treed',");
            Console.WriteLine($"{"",-COL_WIDTH}where dependencies of child projects are deposited and served to parent project,");
            Console.WriteLine($"{"",-COL_WIDTH}as to fulfill any possible reference requirements{NL}");
            Console.WriteLine("  -xx|--exclufx:<dotnet Framework path>".PadRight(COL_WIDTH) + "Path where lib exclus will be extracted from.");
            Console.WriteLine($"{"",-COL_WIDTH}e.g. 'C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\7.0.2'");
            Console.WriteLine($"{"",-COL_WIDTH}Extracted exclus stored in '<moniker>.exclu' for further use with moniker specified by -fx opt.");
            Console.WriteLine($"{"",-COL_WIDTH}If path not given, BFlatA searches -pr:<path> with -fx:<framework>, automatically.{NL}");
            Console.WriteLine($"{NL}Note:{NL}");
            Console.WriteLine("  Any other args will be passed 'as is' to BFlat, except for '-o'.");
            Console.WriteLine("  For options, the ':' char can also be replaced with a space. e.g. -pr:<path> = -pr <path>.");
            Console.WriteLine("  Do make sure <ImplicitUsings> switched off in .csproj file and all namespaces properly imported.");
            Console.WriteLine("  Once '<moniker>.exclu' file is saved, it can be used for any later build, and a 'packages.exclu' is always loaded and can be used to store extra shared exclus, where 'exclu' is the short for 'Excluded Packages'.");
            Console.WriteLine($"{NL}Examples:{NL}");
            Console.WriteLine($"  bflata xxxx.csproj -pr:C:\\Users\\username\\.nuget\\packages -fx=net7.0 -bm:treed{NL}");
            Console.WriteLine($"  bflata build xxxx.csproj -pr:C:\\Users\\username\\.nuget\\packages --arch x64 --ldflags /libpath:\"C:\\Progra~1\\Micros~3\\2022\\Enterprise\\VC\\Tools\\MSVC\\14.35.32215\\lib\\x64\" --ldflags \"/fixed -base:0x10000000 --subsystem:native /entry:Entry /INCREMENTAL:NO\"{NL}");

            Console.WriteLine($"{NL}Macors defined:{NL}");
            foreach (var kv in GetMacros("c:\\ProjectPath\\ProjectFile.csproj")) Console.WriteLine($"  {kv.Key,-26} = {kv.Value ?? "<default>"}");
        }

        /// <summary>
        /// Split args no matter starts with barehead arg or capped arg.
        /// </summary>
        /// <param name="argStr">dont have to be trimmed.</param>
        /// <returns></returns>
        public static IEnumerable<string> SplitArgs(this string argStr, char optCapChar = OPTION_CAP_CHAR, string optSeparator = OPTION_SEPARATOR, string optSeparatorEscaped = OPTION_SEPARATOR_ESCAPED)
            => (((argStr = argStr.Trim()).StartsWith(optCapChar) ? ' ' : '\u0001') + argStr)   //tag the barehead arg
            .Split(optSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(i =>
            {
                i = i.Trim().Replace(optSeparatorEscaped, optSeparator);  //restore the escaped
                var r = i.StartsWith('\u0001') ? i.TrimStart('\u0001') : optCapChar + i; //restore the barehead args & capped options
                r = r.Replace("\0", ""); //remove zero char
                return r;
            });

        /// <summary>
        /// Split args and leave whatever in quotes untouched
        /// </summary>
        /// <param name="argStr">don't have to be trimmed</param>
        /// <param name="quoteChar"></param>
        /// <param name="preserveQuote"></param>
        /// <returns></returns>
        public static IEnumerable<string> SplitArgsButQuotes(this string argStr,
                                                             char quoteChar = '"',
                                                             char optCapChar = OPTION_CAP_CHAR,
                                                             string optSeparator = OPTION_SEPARATOR,
                                                             string optSeparatorEscaped = OPTION_SEPARATOR_ESCAPED,
                                                             bool preserveQuote = true)
        {
            //allow lines in .BFA file to contain more than one arg each.
            List<string> sliced = new();
            if (argStr.Contains(quoteChar))
            {
                argStr = argStr.Trim();
                //process Quotes
                var splitted = argStr.Split(quoteChar, StringSplitOptions.None);
                for (int j = 0; j < splitted.Length; j++) if ((j + 1) % 2 == 0)
                        splitted[j] = splitted[j].Replace(optSeparator, optSeparatorEscaped); //even pos: in quotes, escaping the OPTION_SEPARATOR e.g." -"
                                                                                              //rejoin
                argStr = string.Join(preserveQuote ? quoteChar : '\0', splitted);
            }

            sliced.AddRange(argStr.SplitArgs(optCapChar, optSeparator, optSeparatorEscaped));

            return sliced;
        }

        public static IEnumerable<string> SplitArgsButQuotesAsBarehead(this string argStr) => argStr.SplitArgsButQuotes('\'', '\0', " ", "\u0002", true).Select(i => i.Replace('\'', '"'));

        public static IEnumerable<string> SplitArgsButQuotesWindows(this string argStr) => argStr.SplitArgsButQuotes('"', '/', optSeparator: " /", optSeparatorEscaped: "~/");

        public static IEnumerable<string> ToRefedPaths(this IEnumerable<string> paths) => paths.Select(i => PATH_PLACEHOLDER + PathSep + Path.GetRelativePath(MSBuildStartupDirectory, i));

        public static string ToSysPathSep(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (path.Contains('/') && PathSep != '/') path = path.Replace('/', PathSep);
                else if (path.Contains('\\') && PathSep != '\\') path = path.Replace('\\', PathSep);
            }
            return path;
        }

        public static string TrimPathEnd(this string str) => str.TrimEnd(new[] { '/', '\\' });

        public static string TrimQuotes(this string str, char quote = '"')
        {
            str = str.Trim();
            if (str.StartsWith(quote) && str.EndsWith(quote)) return str.Trim(quote);
            else return str;
        }

        public static bool TryTakeArg(string a, string shortName, string longName, List<string> restArgs, out string value)
        {
            value = null;
            var loa = a.ToLower();
            if (!string.IsNullOrEmpty(shortName) && (loa.StartsWith(shortName + ARG_EVALUATION_CHAR) || loa.StartsWith(shortName + ' ')))
            {
                //restParams for BFlat
                restArgs.Remove(a);
                value = a[(shortName.Length + 1)..];

                return true;
            }
            else if (!string.IsNullOrEmpty(longName) && (loa.StartsWith(longName + ARG_EVALUATION_CHAR) || loa.StartsWith(longName + ' ')))
            {
                restArgs.Remove(a);
                value = a[(longName.Length + 1)..];
                return true;
            }
            return false;
        }

        public static int[] Ver2IntArray(string verStr) => verStr.Split('.').Select(i => int.TryParse(i, out int n) ? n : 0).ToArray();

        public static void WriteCache(string fileName, List<string> cache)
        {
            try
            {
                using var st = File.Create(fileName);
                foreach (var l in cache) st.Write(Encoding.UTF8.GetBytes(l + NL));
                Console.WriteLine($"{fileName} saved!");
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public static void WriteExclu(string moniker)
        {
            try
            {
                var excluFile = moniker + "." + EXCLU_EXT;
                using var st = File.Create(excluFile);
                foreach (var l in LibExclu) st.Write(Encoding.UTF8.GetBytes(l + NL));
                Console.WriteLine($"{LibExclu.Length} exclus saved to:{excluFile} !");
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public static int WriteScript(string script, string scriptFileName = BUIDFILE_NAME)
        {
            if (string.IsNullOrEmpty(script)) return -1;

            try
            {
                var buf = Encoding.UTF8.GetBytes(script.ToString());
                using var st = File.Create(scriptFileName);
                st.Write(buf);
                st.Flush();
                Console.WriteLine($"Script:{scriptFileName} written!{NL}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
            return 0;
        }

        private static int ExecuteCmds(string title, List<string> cmd)
        {
            Process buildProc = null;
            int exitCode = 0;
            foreach (var a in cmd)
            {
                var splitted = a.TrimQuotes().SplitArgsButQuotesAsBarehead().ToArray();
                if (splitted.Length > 1)
                {
                    try
                    {
                        buildProc = Process.Start(splitted[0], string.Join(' ', splitted[1..]));
                        buildProc.WaitForExit();
                    }
                    catch (Exception ex) { Console.WriteLine($"{ex.Message}"); }

                    if (buildProc == null) Console.WriteLine($"Error occurred during executing {title}!!");
                    else Console.WriteLine($"{title} exit code:{buildProc.ExitCode} - [{a}]");
                }
                else Console.WriteLine($"{title} invalid:{a}!");
                if (buildProc != null) exitCode += buildProc.ExitCode;
            }

            return exitCode;
        }

        private static int Main(string[] args)
        {
            List<string> codeBook = new(), libBook = new(), refProjectBook = new(), nativeLibBook = new(), linkerArgs = new(), defineConstants = new(); ;
            Dictionary<string, string> resBook = new();

            Console.WriteLine($"BFlatA V{Assembly.GetEntryAssembly().GetName().Version} @github.com/xiaoyuvax/bflata{NL}" +
                $"Description:{NL}" +
                $"  A wrapper/build script generator for BFlat, a native C# compiler, for recusively building .csproj file with:{NL}" +
                $"    - Referenced projects{NL}" +
                $"    - Nuget package dependencies{NL}" +
                $"    - Embedded resources{NL}" +
                $"  Before using BFlatA, you should get BFlat first at https://flattened.net.{NL}");

            //Parsing args
            List<string> restArgs = ParseArgs(args);

            if (restArgs == null) return -1;
            else if (restArgs.Contains("?")) return 0;

            //echo args.
            string padLine(string key, string value) => key.PadRight(COL_WIDTH_FOR_ARGS) + value;
            Console.WriteLine($"{NL}--ARGS--------------------------------");
            Console.WriteLine(padLine("Action", $":{Action}"));
            Console.WriteLine(padLine("BuildMode", $":{BuildMode}"));
            Console.WriteLine(padLine("DepositDep", $":{(DepositLib ? "On" : "Off")}"));
            Console.WriteLine(padLine("Target", $":{OutputType}"));
            Console.WriteLine(padLine("TargetOS", $":{OS}"));
            Console.WriteLine(padLine("Output", $":{OutputFile ?? "<Default>"}"));
            Console.WriteLine(padLine("TargetFx", $":{TargetFx}"));
            Console.WriteLine(padLine("PackageRoot", $":{PackageRoot ?? "<N/A>"}"));
            Console.WriteLine(padLine("Home", $":{MSBuildStartupDirectory}"));
            if (BFAFiles.Count > 0) Console.WriteLine(padLine("BFA Includes", $":{BFAFiles.Count}"));
            Console.WriteLine(padLine("Args for BFlat", $":{(restArgs.Count > 20 ? string.Join(' ', restArgs.ToArray()[..20]) + " ..." : string.Join(' ', restArgs))}"));
            Console.WriteLine();

            if (!string.IsNullOrEmpty(RootProjectFile))
            {
                var fxExclu = TargetFx + ".exclu";
                Console.WriteLine($"--LIB EXCLU---------------------------");
                if (File.Exists(CUSTOM_EXCLU_FILENAME)) LoadExclu(CUSTOM_EXCLU_FILENAME);
                if (File.Exists(fxExclu)) LoadExclu(fxExclu);
                else if (Directory.Exists(RuntimePathExclu))
                {
                    LibExclu = ExtractExclu(RuntimePathExclu);
                    WriteExclu(TargetFx);
                }

                Console.WriteLine($"--LIB CACHE---------------------------");
                if (File.Exists(LIB_CACHE_FILENAME))
                {
                    Console.WriteLine($"Package cache found!");
                    CacheLib = LoadCache(LIB_CACHE_FILENAME);
                }
                if (File.Exists(NUSPEC_CACHE_FILENAME))
                {
                    Console.WriteLine($"{NL}Nuspec cache found!");
                    CacheNuspec = LoadCache(NUSPEC_CACHE_FILENAME);
                }
                if (!string.IsNullOrEmpty(PackageRoot) && (CacheLib.Count == 0 || CacheNuspec.Count == 0))
                {
                    PreCacheLibs(PackageRoot);
                    if (CacheLib.Count > 0) WriteCache(LIB_CACHE_FILENAME, CacheLib);
                    if (CacheNuspec.Count > 0) WriteCache(NUSPEC_CACHE_FILENAME, CacheNuspec);
                }
                Console.WriteLine();

                int err = 0;
                string projectName = Path.GetFileNameWithoutExtension(RootProjectFile);
                string script = "";
                if (!UseBFA)
                {
                    Console.WriteLine($"{NL}--PARSING-----------------------------");
                    err = ParseProject(BuildMode, RootProjectFile, CacheLib.ToArray(), TargetFx, PackageRoot, out _, out _,
                                       out _, restArgs, codeBook, libBook, nativeLibBook, refProjectBook, linkerArgs,
                                       defineConstants, resBook, false);
                }

                if (err == 0)
                {
                    if (UseFlatten && !UseBFA)
                    {
                        Console.WriteLine($"{NL}--FLATTENING--------------------------");
                        FlattenProject(projectName, RootProjectPath, Action, codeBook, libBook, nativeLibBook, resBook, linkerArgs, defineConstants, restArgs, OutputFile);
                    }
                    else
                    {
                        Console.WriteLine($"{NL}--SCRIPTING---------------------------");

                        if (BuildMode != BuildMode.Tree)
                        {
                            //set default outputfile
                            OutputFile ??= projectName + (IsLinux ? "" : ".exe");
                            Dictionary<string, string> myFlatResBook = FlattenResX(resBook, projectName);

                            RouteLinkerArgs(restArgs, linkerArgs, nativeLibBook, UseLinker);
                            restArgs.AddRange(defineConstants.Select(i => "-d " + i));

                            //explicitly specify BuldMode.Tree as to generate the header part.
                            script = GenerateScript(projectName, restArgs, codeBook, libBook, nativeLibBook, myFlatResBook, BuildMode.Tree, PackageRoot, OutputType, false, OutputFile);
                        }

                        if (!string.IsNullOrEmpty(script))
                        {
                            //Write build scripts
                            if (WriteScript(script) == 0)
                            {
                                if (UseBuild && BuildMode == BuildMode.Flat)
                                {
                                    Process buildProc = null;
                                    int actionExitCode = 0;

                                    if (PreBuildActions.Any())
                                    {
                                        Console.WriteLine($"{NL}--PREBUILD-ACTIONS-------------------");
                                        actionExitCode = ExecuteCmds("Prebuild actions", PreBuildActions);
                                    }

                                    if (actionExitCode == 0)
                                    {
                                        Console.WriteLine($"{NL}--BUILDING----------------------------");
                                        //Start Building
                                        Console.WriteLine($"Building in FLAT mode:{projectName}...");
                                        buildProc = Build($"bflat {(UseBuildIL ? "build-il" : "build")} @build.rsp");
                                        if (buildProc != null)
                                        {
                                            buildProc.WaitForExit();
                                            Console.WriteLine($"Compiler exit code:{buildProc.ExitCode}");

                                            if (buildProc.ExitCode == 0)  //invoke linker
                                            {
                                                if (UseLinker)
                                                {
                                                    var objFileName = string.IsNullOrEmpty(OutputFile) ? projectName + ".obj" : OutputFile.Replace(Path.GetExtension(OutputFile), ".obj");
                                                    if (File.Exists(objFileName))
                                                    {
                                                        if (WriteScript($"{objFileName}{NL}" + string.Join(NL, linkerArgs), "link.rsp") == 0)
                                                        {
                                                            buildProc = Process.Start(Linker, "@link.rsp");
                                                            buildProc?.WaitForExit();
                                                            Console.WriteLine($"Linker exit code:{buildProc.ExitCode}");

                                                            if (buildProc.ExitCode == 0 && PostBuildActions.Any())
                                                            {
                                                                Console.WriteLine($"{NL}--POSTBUILD-ACTIONS------------------");
                                                                actionExitCode = ExecuteCmds("Postbuild actions", PostBuildActions);
                                                                if (actionExitCode != 0) Console.WriteLine($"Postbuild failure!!"); return -0xA;
                                                            }
                                                            else Console.WriteLine($"Linker failure!!"); return -0x9;
                                                        }
                                                        else Console.WriteLine($"Error writing link.rsp!!"); return -0x8;
                                                    }
                                                    else Console.WriteLine($"Object file not exists:{objFileName}"); return -0x7;
                                                }//UseLinker
                                            }
                                            else Console.WriteLine($"Compiler failure!!"); return -0x6;
                                        }
                                        else Console.WriteLine($"Error occurred during buidling project!!"); return -0x5;
                                    }
                                    else Console.WriteLine($"Pre-build failure!!"); return -0x4;
                                }
                            }
                            else Console.WriteLine($"Error writing buid.rsp!!"); return -0x3;
                        }
                        else Console.WriteLine($"No script generated!!"); return -0x2;
                    }
                }
                else
                {
                    Console.WriteLine($"Error occurred during parsing project file(s)!!({err})");
                    return err;
                }

                Console.WriteLine($"--END---------------------------------");
            }
            else
            {
                Console.WriteLine($"Project file not specified!!{NL}");
                Console.WriteLine($"use -? -h or --help for help and usage informatioon.");
                return -1;
            }

            return 0;
        }
    }
}