using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Linq;

[assembly: AssemblyVersion("1.3.0.0")]

namespace BFlatA
{
    public enum BuildMode
    {
        None,
        Flat,
        Tree,
        TreeDepositDependency
    }

    public static class XExtention
    {
        public static IEnumerable<XElement> OfInclude(this IEnumerable<XElement> r, string elementName) => r.Where(i => i.Name.LocalName.ToLower() == elementName.ToLower() && i.Attribute("Include") != null);

        public static IEnumerable<XElement> OfRemove(this IEnumerable<XElement> r, string elementName) => r.Where(i => i.Name.LocalName.ToLower() == elementName.ToLower() && i.Attribute("Remove") != null);
    }

    internal static class BFlatA
    {
        public const char ARG_EVALUATION_CHAR = ':';
        public const string BUIDFILE_NAME = "build.rsp";
        public const int COL_WIDTH = 48;
        public const int COL_WIDTH_FOR_ARGS = 20;
        public const string COMPILER = "bflat";
        public const string CUSTOM_EXCLU_FILENAME = "packages.exclu";
        public const string EXCLU_EXT = "exclu";
        public const string LIB_CACHE_FILENAME = "packages.cache";
        public const string NUSPEC_CACHE_FILENAME = "nuspecs.cache";
        public const string PATH_PLACEHOLDER = "|";
        public static readonly string[] IGNORED_SUBFOLDER_NAMES = { "bin", "obj" };
        public static readonly bool IsLinux = Path.DirectorySeparatorChar == '/';
        public static readonly string NL = Environment.NewLine;
        public static readonly char PathSepChar = Path.DirectorySeparatorChar;
        public static readonly string WorkingPath = Directory.GetCurrentDirectory();
        public static readonly XNamespace XSD_NUGETSPEC = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        public static string Architecture = "x64";
        public static BuildMode BuildMode = BuildMode.None;
        public static List<string> CacheLib = new();
        public static List<string> CacheNuspec = new();
        public static bool DepositLib = false;
        public static string[] LibExclu = Array.Empty<string>();
        public static string OS = "windows";
        public static string OutputFile = null;
        public static string OutputType = "Exe";
        public static string PackageRoot = "";
        public static List<string> ParsedProjectPaths = new();
        public static List<string> IncludedRSPFiles = new();
        public static string ProjectFile = null;
        public static string MSBuildStartupDirectory = Directory.GetCurrentDirectory();
        public static string ResGenPath = "C:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v10.0A\\bin\\NETFX 4.8 Tools\\ResGen.exe";
        public static string RuntimePathExclu = null;
        public static string TargetFx = null;
        public static bool UseBuild = false;
        public static bool UseBuildIL = false;
        public static bool UseExclu = false;
        public static bool UseVerbose = false;
        private const string ASPNETCORE_APP = "microsoft.aspnetcore.app.runtime";
        private const string NETCORE_APP = "microsoft.netcore.app.runtime";
        private const string WINDESKTOP_APP = "microsoft.windowsdesktop.app.runtime";

        public static string LibPathSegment { get; } = PathSepChar + "lib" + PathSepChar;
        public static string OSArchMoniker { get; } = $"{GetOSMoniker(OS)}-{Architecture}";
        public static string RuntimesPathSegment { get; } = PathSepChar + "runtimes" + PathSepChar;

        public static string AppendScriptBlock(string script, string myScript) => script + (script == null || script.EndsWith("\n") ? "" : NL + NL) + myScript;

        public static Process Build(string myScript)
        {
            Console.WriteLine($"- Executing building script: {(myScript.Length > 22 ? myScript[..22] : myScript)}...");
            Process buildProc = null;
            if (!string.IsNullOrEmpty(myScript))
            {
                try
                {
                    if (myScript.StartsWith(COMPILER))
                    {
                        var paths = Environment.GetEnvironmentVariable("PATH").Split(IsLinux ? ':' : ';') ?? new[] { "./" };

                        var compilerPath = paths.FirstOrDefault(i => File.Exists(i + PathSepChar + (IsLinux ? COMPILER : COMPILER + ".exe")));
                        if (Directory.Exists(compilerPath))
                        {
                            buildProc = Process.Start(compilerPath + PathSepChar + COMPILER, myScript.Remove(0, COMPILER.Length));
                        }
                        else Console.WriteLine("Error:" + COMPILER + " doesn't exist in PATH!");
                    }
                    else buildProc = Process.Start(myScript);
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
            else Console.WriteLine($"Error:building script is emtpy!");
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
                var pathToRes = tempDir + PathSepChar + targetResourcesFileName;
                Process.Start(ResGenPath, new[] { "/useSourcePath", resxFile, pathToRes }).WaitForExit();
                resBook.TryAdd(Path.GetFullPath(pathToRes), "");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return resBook;
        }

        /// <summary>
        /// Exclude Exclus and Runtime libs.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static IEnumerable<string> DoExclude(this IEnumerable<string> paths) => paths
            .Where(i => !i.Contains(PathSepChar + "runtime."))
            .Where(i => !LibExclu.Any(x => i.Contains(PathSepChar + x + PathSepChar)));

        public static IEnumerable<string> ExtractAttributePathValues(this IEnumerable<XElement> x, string attributeName, string refPath, Dictionary<string, string> msBuildMacros)
            => x.SelectMany(i => GetAbsPaths(ReplaceMsBuildMacros(i.Attribute(attributeName)?.Value, msBuildMacros), refPath));

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
                                                    IEnumerable<string> restParams,
                                            IEnumerable<string> codeBook,
                                            IEnumerable<string> libBook,
                                            IEnumerable<string> nativeLibBook,
                                            IDictionary<string, string> resBook,
                                            BuildMode buildMode,
                                            string packageRoot,
                                            string outputType = "Exe",
                                            bool isDependency = false,
                                            string bflatArgOut = null
                                            )
        {
            restParams = restParams.Distinct();
            codeBook = codeBook.Distinct();
            libBook = libBook.Distinct();
            nativeLibBook = nativeLibBook.Distinct();

            Console.WriteLine($"Generating script:{projectName}");

            string lineFeed = NL;

            StringBuilder cmd = new();

            if (buildMode == BuildMode.Tree)
            {
                if (isDependency)
                {
                    cmd.AppendLine($"-o {projectName}.dll ");  //all dependencies will use the ext of ".dll", even it's an exe, the name doesn't matter.
                }
                else if (!string.IsNullOrEmpty(bflatArgOut))
                {
                    cmd.AppendLine($"-o {bflatArgOut} ");
                }

                if (!string.IsNullOrEmpty(outputType))
                {
                    cmd.AppendLine($"--target {outputType} ");
                }

                if (restParams.Any())
                {
                    cmd.Append(string.Join(" ", restParams.Select(i => i.StartsWith('-') ? Environment.NewLine + i : i)).TrimStart(Environment.NewLine.ToCharArray()));  // arg per line for Response File
                    Console.WriteLine($"- Found {restParams.Count()} args to be passed to BFlat.");
                }
            }

            if (codeBook.Any())
            {
                cmd.Append(lineFeed);
                cmd.AppendJoin(lineFeed, codeBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).OrderBy(i => i));
                Console.WriteLine($"- Found {codeBook.Count()} code files(*.cs)");
            }

            if (libBook.Any())
            {
                cmd.Append(lineFeed + "-r ");
                cmd.AppendJoin(lineFeed + "-r ", libBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, packageRoot))).OrderBy(i => i));
                Console.WriteLine($"- Found {libBook.Count()} dependent libs(*.dll)");
            }
            if (nativeLibBook.Any())
            {
                cmd.Append(lineFeed + "--ldflags ");
                cmd.AppendJoin(lineFeed + "--ldflags ", nativeLibBook.Select(i => Path.GetFullPath(i.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory))).OrderBy(i => i));
                Console.WriteLine($"- Found {nativeLibBook.Count()} dependent native libs(*.lib)");
            }

            if (resBook.Any())
            {
                cmd.Append(lineFeed + "-res ");
                cmd.AppendJoin(lineFeed + "-res ", resBook.DistinctBy(kv => Path.GetFileName(kv.Key)).Select(kv => Path.GetFullPath(kv.Key.Replace(PATH_PLACEHOLDER, MSBuildStartupDirectory)) + (string.IsNullOrEmpty(kv.Value) ? "" : "," + kv.Value)).OrderBy(i => i));
                Console.WriteLine($"Found {resBook.Count} embedded resources(*.resx and other)");
            }
            if (buildMode != BuildMode.Tree) cmd.Append(lineFeed);  //last return at the end of a script;

            return cmd.ToString();
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
                    .Where(i => !IGNORED_SUBFOLDER_NAMES.Any(x => i.Contains(PathSepChar + x + PathSepChar)));
                if (includeLst != null) files = files.Concat(includeLst);
                files = files.Distinct().ToRefedPaths();
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

        public static string GetFrameworkPath(string frameworkName) => Path.GetFullPath(Directory.GetDirectories(PackageRoot + PathSepChar + $"{frameworkName}.{OSArchMoniker}").OrderDescending()
                        .FirstOrDefault(i => i.Contains($"{PathSepChar}{TargetFx.Replace("net", "")}"))
                + RuntimesPathSegment + OSArchMoniker + LibPathSegment + TargetFx);

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
                    Console.WriteLine($"Exclu file found:.{PathSepChar}{Path.GetFileName(excluFile)}");
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
                    packagePathSegment = PathSepChar + packageNameLo + PathSepChar;
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
                                    var splittedPath = new List<string>(i.Split(PathSepChar));
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
                                    absLibPath = Path.GetFullPath(d + PathSepChar + package.Key + ".dll");

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
                                var splittedPath = new List<string>(libPath.Split(PathSepChar));
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
                                var nuspecPath = Path.GetFullPath(packagOfVerPath + PathSepChar + packageNameLo + ".nuspec");  //nuespec filename is all lower case
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

        public static BuildMode ParseBuildMode(string typeStr) => typeStr.ToLower() switch
        {
            "flat" => BuildMode.Flat,
            "tree" => BuildMode.Tree,
            "treed" => BuildMode.TreeDepositDependency,
            _ => BuildMode.None
        };

        public static int ParseProject(string projectFile,
                                       string[] allLibPaths,
                                       string target,
                                       string packageRoot,
                                       List<string> restArg,
                                       BuildMode buildMode,
                                       out string projectName,
                                       out string outputType,
                                       out string script,
                                       List<string> codeBook = null,
                                       List<string> libBook = null,
                                       List<string> nativeLibBook = null,
                                       Dictionary<string, string> resBook = null,
                                       List<string> refProjectBook = null,
                                       bool isDependency = false)
        {
            outputType = "Shared";
            script = null;
            projectName = Path.GetFileNameWithoutExtension(projectFile);
            string projectPath = Path.GetFullPath(Path.GetDirectoryName(projectFile));

            if (string.IsNullOrEmpty(projectFile)) return -1;

            resBook ??= new();
            codeBook ??= new();
            libBook ??= new();
            nativeLibBook ??= new();
            List<string> removeBook = new();
            List<string> includeBook = new();
            List<string> contentBook = new();
            List<string> myRefProject = new();

            bool useWinform = false;
            bool useWpf = false;
            int err = 0;

            List<string> targets = new();

            Dictionary<string, string> packageReferences = new();

            Dictionary<string, string> msBuildMacros = new()
            {
                { "MSBuildProjectDirectory",projectPath.TrimPathEnd()},
                //{"MSBuildProjectDirectoryNoRoot},
                { "MSBuildProjectExtension",Path.GetExtension(projectFile)},
                { "MSBuildProjectFile",Path.GetFileName(projectFile)},
                { "MSBuildProjectFullPath",projectFile},
                { "MSBuildProjectName",projectName},
                { "MSBuildRuntimeType",TargetFx},

                { "MSBuildThisFile",Path.GetFileName(projectFile)},
                { "MSBuildThisFileDirectory",projectPath.TrimPathEnd() + PathSepChar},
                //{"MSBuildThisFileDirectoryNoRoot},
                { "MSBuildThisFileExtension",Path.GetExtension(projectFile)},
                { "MSBuildThisFileFullPath",projectPath},
                { "MSBuildThisFileName",Path.GetFileNameWithoutExtension(projectFile)},

                {"MSBuildStartupDirectory", MSBuildStartupDirectory.TrimPathEnd() + PathSepChar }
            };

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
                        projectPath = Path.GetFullPath(Path.GetDirectoryName(projectFile));
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

                        //If project setting is not compatible with specified framework, then quit.
                        bool isStandardLib = targets?.Any(i => i.StartsWith("netstandard")) == true;

                        bool hasTarget = targets?.Any(i => i.Contains(target)) == true;
                        if (hasTarget || isStandardLib)
                        {
                            AddElement2List(ig.OfRemove("Compile"), removeBook, "CompileRemove", "Remove");
                            AddElement2List(ig.OfInclude("Compile"), includeBook, "CompileInclude");
                            AddElement2List(ig.OfInclude("Content"), contentBook, "Content");
                            AddElement2List(ig.OfInclude("NativeLibrary"), nativeLibBook, "NativeLib");
                            AddElement2Dict(ig.OfInclude("EmbeddedResource"), resBook, "EmbeddedResource", useAbsolutePath: false);

                            //Parse Package Dependencies
                            foreach (var pr in ig.OfInclude("PackageReference")) packageReferences.TryAdd(pr.Attribute("Include")?.Value, pr.Attribute("Version")?.Value);

                            //if specified targets is included, make it preferable to search.

                            if (hasTarget)
                            {
                                targets.Remove(target);
                                targets.Insert(0, target);
                            }
                            else targets = targets.Where(i => i.StartsWith("netstandard")).OrderByDescending(i => i).ToList();  //otherwise, only netstardard targets allowed.

                            //Match lib from cache (TreeMode: each project uses its own Libs, so don't pass in libBook)
                            if (buildMode == BuildMode.Tree)
                            {
                                if (DepositLib) libBook = MatchPackages(allLibPaths, packageReferences, packageRoot, targets, libBook);
                                else libBook = MatchPackages(allLibPaths, packageReferences, packageRoot, targets);
                            }
                            else
                                libBook = MatchPackages(allLibPaths, packageReferences, packageRoot, targets, libBook);

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
                                string refProjectPath = p;
                                foreach (var m in msBuildMacros)
                                    if (refProjectPath.Contains($"$({m.Key})")) refProjectPath = ReplaceMsBuildMacros(refProjectPath, msBuildMacros);

                                if (!string.IsNullOrEmpty(refProjectPath) && File.Exists(refProjectPath))
                                {
                                    string innerScript = "", innerProjectName = "", innerOutputType = "";

                                    if (buildMode == BuildMode.Tree)
                                    {
                                        if (DepositLib) err = ParseProject(refProjectPath, allLibPaths, target, packageRoot,
                                                                           restArg, buildMode,
                                                                           out innerProjectName, out innerOutputType,
                                                                           out innerScript, null, libBook, nativeLibBook, null,
                                                                           refProjectBook, true);
                                        else err = ParseProject(refProjectPath, allLibPaths, target, packageRoot, restArg,
                                                                buildMode, out innerProjectName,
                                                                out innerOutputType, out innerScript, isDependency: true);
                                    }
                                    else err = ParseProject(refProjectPath, allLibPaths, target, packageRoot, restArg,
                                                            buildMode, out innerProjectName, out innerOutputType,
                                                            out innerScript, codeBook, libBook, nativeLibBook, resBook, isDependency: true);

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
                                string getRefProjectLines() => string.Join("", (DepositLib ? refProjectBook : myRefProject).Select(i => buildMode == BuildMode.Tree ? NL + i : i + NL));

                                Dictionary<string, string> myFlatResBook = FlattenResX(resBook, projectName);

                                if (UseBuild)
                                {
                                    myScript = GenerateScript(projectName, restArg, codeBook, libBook, nativeLibBook, myFlatResBook, buildMode, packageRoot, outputType, isDependency, argOutputFile);
                                    myScript += getRefProjectLines();

                                    Process buildProc = null;

                                    WriteScript(projectName, myScript);
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
                                else
                                {
                                    myScript = GenerateScript(projectName, restArg, codeBook, libBook, nativeLibBook, myFlatResBook, buildMode, packageRoot, outputType, isDependency, argOutputFile);
                                    myScript += getRefProjectLines();
                                    Console.WriteLine($"Appending Script:{projectName}...{BFlatA.NL}");
                                    script = AppendScriptBlock(script, myScript);
                                }
                            }
                            else if (err != 0) return err;
                        }
                        else Console.WriteLine($"Warnning:Project properties are not compatible with the target:{target}, {projectFile}!!! ");

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

                        int AddElement2Dict(IEnumerable<XElement> elements, Dictionary<string, string> book, string displayAction, string action = "Include", bool useAbsolutePath = true)
                        {
                            //This method is easy to extent to more categories.
                            //CodeFiles and PackageReferences r exceptions and stored otherwise.
                            int counter = 0;
                            foreach (var i in elements)
                            {
                                IEnumerable<string> items = ExtractAttributePathValues(elements, action, projectPath, msBuildMacros);
                                //relative paths used by script is relative to WorkingPath
                                if (!useAbsolutePath) items = items.ToRefedPaths();

                                foreach (var p in items) book.TryAdd(p, Path.GetFileName(p));

                                counter += items.Count();
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
                        var splitted = i.Split(PathSepChar, StringSplitOptions.RemoveEmptyEntries);

                        if (!dontCacheLib && splitted[^2].ToLower() == "lib") CacheLib.Add(i);
                        else if (!dontCacheNuspec && Directory.GetFiles(i, "*.nuspec").Any()) CacheNuspec.Add(i);
                    };
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            Console.WriteLine($"{NL}Found {CacheLib.Count} nuget packages!");
        }

        public static string[] RemoveArg(string[] restParams, string a) => restParams.Except(new[] { a }).ToArray();

        public static string ReplaceMsBuildMacros(string path, Dictionary<string, string> msBuildMacros)
        {
            foreach (var m in msBuildMacros)
                if (path.Contains($"$({m.Key})")) path = Path.GetFullPath(path.Replace($"$({m.Key})", m.Value));
            return path;
        }

        public static void ShowHelp()
        {
            Console.WriteLine($"  Usage: bflata [build|build-il] <csproj file> [options]{NL}");
            Console.WriteLine("  [build|build-il]".PadRight(COL_WIDTH) + "Build with BFlat in %Path%, with -st option ignored.");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"If omitted, generate building script only, with -bm option still valid.{NL}");
            Console.WriteLine("  <.csproj file>".PadRight(COL_WIDTH) + "Must be the 2nd arg if 'build' specified, or the 1st otherwise, only 1 project allowed.");
            Console.WriteLine($"{NL}Options:");
            Console.WriteLine("  -pr|--packageroot:<path to package storage>".PadRight(COL_WIDTH) + $"eg.'C:\\Users\\%username%\\.nuget\\packages' or '$HOME/.nuget/packages'.{NL}");
            Console.WriteLine("  -h|--home:<MSBuildStartupDirectory>".PadRight(COL_WIDTH) + $"Path to VS solution usually, or the equivalent execution path of MSBuild, default:current directory.");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"Caution: this path might not be the same path of the root project served as <.csproj> arg{NL}, and is needed for entire solution to compile correctly.");
            Console.WriteLine("  -fx|--framework:<moniker>".PadRight(COL_WIDTH) + "The TFM compatible with the built-in .net runtime of BFlat(see 'bflat --info')");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"mainly purposed for matching dependencies, e.g. 'net7.0'{NL}");
            Console.WriteLine("  -bm|--buildmode:<flat|tree|treed>".PadRight(COL_WIDTH) + "FLAT = flatten project tree to one for building;");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"TREE = build each project alone and reference'em accordingly with -r option;");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"TREED = '-bm:tree -dd'.{NL}");
            Console.WriteLine("  --resgen:<path to resgen.exe>".PadRight(COL_WIDTH) + $"Path to Resource Generator(e.g. ResGen.exe),which compiles .resx file to binary.{NL}");
            Console.WriteLine("  -inc|--include:<path to other RSP files>".PadRight(COL_WIDTH) + $"can be multiple{NL}");
            Console.WriteLine($"{NL}Shared Options(will also be passed to BFlat):");
            Console.WriteLine("  --target:<Exe|Shared|WinExe>".PadRight(COL_WIDTH) + $"Build Target.default:by bflat{NL}");
            Console.WriteLine("  --os <Windows|Linux|Uefi>".PadRight(COL_WIDTH) + $"Build Target.default:Windows.{NL}");
            Console.WriteLine("  --arch <x64|arm64|x86|...>".PadRight(COL_WIDTH) + $"Platform archetecture.default:x64.{NL}");
            Console.WriteLine("  -o|--out:<File>".PadRight(COL_WIDTH) + $"Output file path for the root project.{NL}");
            Console.WriteLine("  --verbose".PadRight(COL_WIDTH) + "Enable verbose logging");
            Console.WriteLine($"{NL}Obsolete Options:");
            Console.WriteLine("  -dd|--depdep".PadRight(COL_WIDTH) + "Deposit Dependencies mode, valid with '-bm:tree', equivalently '-bm:treed',");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"where dependencies of child projects are deposited and served to parent project,");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"as to fulfill any possible reference requirements{NL}");
            Console.WriteLine("  -xx|--exclufx:<dotnet Shared Framework path>".PadRight(COL_WIDTH) + "If path valid, lib exclus will be extracted from the path.");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"e.g. 'C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\7.0.2'");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"and extracted exclus will be saved to '<moniker>.exclu' for further use, where moniker is specified by -fx option.{NL}");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"If this path not explicitly specified, BFlatA will search -pr:<path> and -fx:<framework> for Exclus,automatically.");
            Console.WriteLine($"{NL}Note:");
            Console.WriteLine("  Any other args will be passed 'as is' to BFlat, except for '-o'.");
            Console.WriteLine("  For options, the ':' char can also be replaced with a space. e.g. -pr:<path> = -pr <path>.");
            Console.WriteLine("  Do make sure <ImplicitUsings> switched off in .csproj file and all namespaces properly imported.");
            Console.WriteLine("  The filename for the building script are one of 'build.rsp,build.cmd,build.sh' and the .rsp file allows larger arguments and is prefered.");
            Console.WriteLine("  Once '<moniker>.exclu' file is saved, you can use it for any later build, and a 'packages.exclu' is always loaded and can be used to store extra shared exclus, where 'exclu' is the short for 'Excluded Packages'.");
            Console.WriteLine($"{NL}Examples:");
            Console.WriteLine("  bflata xxxx.csproj -pr:C:\\Users\\username\\.nuget\\packages -fx=net7.0 -st:bat -bm:treed  <- only generate BAT script which builds project tree orderly with Deposit Dependencies.");
            Console.WriteLine("  bflata build xxxx.csproj -pr:C:\\Users\\username\\.nuget\\packages -st:bat --arch x64  <- build in FLAT mode with default target at .net7.0 and '--arch x64' passed to BFlat while the option -st:bat ignored.");
        }

        public static IEnumerable<string> ToRefedPaths(this IEnumerable<string> paths) => paths.Select(i => PATH_PLACEHOLDER + PathSepChar + Path.GetRelativePath(MSBuildStartupDirectory, i));

        public static string ToSysPathSep(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (path.Contains('/') && PathSepChar != '/') path = path.Replace('/', PathSepChar);
                else if (path.Contains('\\') && PathSepChar != '\\') path = path.Replace('\\', PathSepChar);
            }
            return path;
        }

        public static string TrimPathEnd(this string str) => str.TrimEnd(new[] { '/', '\\' });

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

        public static void WriteScript(string projectName, string script)
        {
            if (string.IsNullOrEmpty(script)) return;
            Console.WriteLine($"Writing script:{projectName}...");

            try
            {
                var buf = Encoding.UTF8.GetBytes(script.ToString());
                using var st = File.Create(BUIDFILE_NAME);
                st.Write(buf);
                st.Flush();
                Console.WriteLine($"Script written!{NL}");
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            return;
        }

        private static int Main(string[] args)
        {
            List<string> restArgs;

            List<string> codeBook = new(), libBook = new(), refProjectBook = new(), nativeLibBook = new();
            Dictionary<string, string> resBook = new();

            Console.WriteLine($"BFlatA V{Assembly.GetEntryAssembly().GetName().Version} @github.com/xiaoyuvax/bflata{NL}" +
                $"Description:{NL}" +
                $"  A wrapper/building script generator for BFlat, a native C# compiler, for recusively building .csproj file with:{NL}" +
                $"    - Referenced projects{NL}" +
                $"    - Nuget package dependencies{NL}" +
                $"    - Embedded resources{NL}" +
                $"  Before using BFlatA, you should get BFlat first at https://flattened.net.{NL}");

            bool tryGetArg(string a, string shortName, string longName, out string value)
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

            //Parse input args
            restArgs = new List<string>(args);
            if (args.Length == 0 || args.Contains("-?") || args.Contains("/?") || args.Contains("-h") || args.Contains("--help"))
            {
                ShowHelp();
                return 0;
            }
            else
            {
                // build must present at the first arg.
                if (restArgs[0].ToLower() == "build")
                {
                    UseBuild = true;

                    restArgs.RemoveAt(0);
                }
                else if (restArgs[0].ToLower() == "build-il")
                {
                    UseBuild = UseBuildIL = true;
                    restArgs.RemoveAt(0);
                }

                if (!restArgs[0].StartsWith("-") && File.Exists(restArgs[0]))
                {
                    ProjectFile = restArgs[0];
                    restArgs.RemoveAt(0);
                }

                //rearrange restArgs
                restArgs = (" " + string.Join(' ', restArgs)).Split(" -", StringSplitOptions.RemoveEmptyEntries).Select(i => '-' + i.Trim()).ToList();

                //process options
                foreach (var a in restArgs.ToArray())
                {
                    if (tryGetArg(a, "-pr", "--packageroot", out string pr))
                        if (Directory.Exists(pr)) PackageRoot = Path.GetFullPath(pr).TrimPathEnd();  //the ending PathSep may cause shell script variable invalid like $PRcommon.log/ after replacement by placeholder @
                        else
                        {
                            Console.WriteLine($"Error:PacakgeRoot does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "-h", "--home", out string h))
                        if (Directory.Exists(h)) MSBuildStartupDirectory = Path.GetFullPath(h).TrimPathEnd();
                        else
                        {
                            Console.WriteLine($"Error:RefPath does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "-xx", "--exclufx", out string xx))
                        if (Directory.Exists(xx)) RuntimePathExclu = Path.GetFullPath(xx).TrimPathEnd();
                        else
                        {
                            Console.WriteLine($"Error:RuntimePath does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "", "--resgen", out string rg))
                        if (File.Exists(rg)) ResGenPath = Path.GetFullPath(rg).TrimPathEnd();
                        else
                        {
                            Console.WriteLine($"Error:Resgen.exe does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "-inc", "--include", out string inc) && File.Exists(inc)) IncludedRSPFiles.Add(inc);
                    else if (tryGetArg(a, "", "--arch", out string ax)) Architecture = ax.ToLower();
                    else if (tryGetArg(a, "-bm", "--buildmode", out string bm)) BuildMode = ParseBuildMode(bm);
                    else if (tryGetArg(a, "", "--target", out string t)) OutputType = t;
                    else if (tryGetArg(a, "-fx", "--framework", out string fx)) TargetFx = fx.ToLower();
                    else if (tryGetArg(a, "", "--os", out string os)) OS = os.ToLower();
                    else if (tryGetArg(a, "-o", "--out", out string o)) //hijack -o arg of BFlat, and it shall not be passed to denpendent project
                    {
                        OutputFile = o;
                        restArgs.Remove(a);
                    }
                    else if (a.ToLower() == "--verbose")
                    {
                        UseVerbose = true;
                    }
                    else if (a.ToLower() == "-dd" || a.ToLower() == "--depdep")
                    {
                        DepositLib = true;
                        restArgs.Remove(a);
                    }
                }
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
            else if (BuildMode == BuildMode.Tree) Console.WriteLine("Warning:.rsp script generated under TREE mode is not buildable!");

            if (string.IsNullOrEmpty(TargetFx)) TargetFx = "net7.0";

            if (string.IsNullOrEmpty(RuntimePathExclu)) RuntimePathExclu = GetFrameworkPath(NETCORE_APP);

            //echo args.
            Console.WriteLine($"{NL}--ARGS--------------------------------");
            Console.WriteLine("Build".PadRight(COL_WIDTH_FOR_ARGS) + $":{(UseBuild ? "On" : "Off")}");
            Console.WriteLine("BuildMode".PadRight(COL_WIDTH_FOR_ARGS) + $":{BuildMode}");
            Console.WriteLine("DepositDep".PadRight(COL_WIDTH_FOR_ARGS) + $":{(DepositLib ? "On" : "Off")}");
            Console.WriteLine("Target".PadRight(COL_WIDTH_FOR_ARGS) + $":{OutputType}");
            Console.WriteLine("Output".PadRight(COL_WIDTH_FOR_ARGS) + $":{OutputFile ?? "<Default>"}");
            Console.WriteLine("TargetFx".PadRight(COL_WIDTH_FOR_ARGS) + $":{TargetFx}");
            Console.WriteLine("PackageRoot".PadRight(COL_WIDTH_FOR_ARGS) + $":{PackageRoot}");
            Console.WriteLine("Home".PadRight(COL_WIDTH_FOR_ARGS) + $":{MSBuildStartupDirectory}");
            if (IncludedRSPFiles.Count > 0) Console.WriteLine("RSP Includes".PadRight(COL_WIDTH_FOR_ARGS) + $":{IncludedRSPFiles.Count}");
            Console.WriteLine("Args4BFlat".PadRight(COL_WIDTH_FOR_ARGS) + $":{string.Join(' ', restArgs)}");
            Console.WriteLine();

            if (!string.IsNullOrEmpty(ProjectFile))
            {
                Console.WriteLine($"--LIB EXCLU---------------------------");
                var fxExclu = TargetFx + ".exclu";
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

                //Parse project and all project references recursively.

                Console.WriteLine($"{NL}--PARSING-----------------------------");
                var err = ParseProject(ProjectFile, CacheLib.ToArray(), TargetFx, PackageRoot, restArgs, BuildMode, out string projectName, out _, out string script, codeBook, libBook, nativeLibBook, resBook, refProjectBook, false);

                if (err == 0)
                {
                    Console.WriteLine($"{NL}--SCRIPTING---------------------------");
                    //overwrite script under Flat Mode, and explicitly specify BuldMode.Tree as to generate the header part.
                    if (BuildMode != BuildMode.Tree)
                    {
                        //set default outputfile
                        OutputFile ??= projectName + (IsLinux ? "" : ".exe");
                        Dictionary<string, string> myFlatResBook = FlattenResX(resBook, projectName);
                        script = GenerateScript(projectName, restArgs, codeBook, libBook, nativeLibBook, myFlatResBook, BuildMode.Tree, PackageRoot, OutputType, false, OutputFile);
                    }

                    if (!string.IsNullOrEmpty(script))
                    {
                        foreach (string i in IncludedRSPFiles)
                            try
                            {
                                using StreamReader sr = new(File.OpenRead(i));
                                var rsp = sr.ReadToEnd()?.Trim();
                                if (!string.IsNullOrEmpty(rsp)) script += NL + rsp;
                            }
                            catch (Exception ex) { Console.WriteLine(ex.Message); }

                        //Write to script file
                        WriteScript(projectName, script);

                        if (UseBuild && BuildMode == BuildMode.Flat)
                        {
                            if (UseBuild) Console.WriteLine($"{NL}--BUILDING----------------------------");
                            //Start Building
                            Console.WriteLine($"Building in FLAT mode:{projectName}...");
                            var buildProc = Build($"bflat {(UseBuildIL ? "build-il" : "build")} @build.rsp");
                            if (buildProc != null)
                            {
                                buildProc.WaitForExit();
                                Console.WriteLine($"Compiler exit code:{buildProc.ExitCode}");
                            }
                            else Console.WriteLine($"Error occurred during buidling project!!");
                        }
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