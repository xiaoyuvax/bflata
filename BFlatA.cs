using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

#if BFLAT
[assembly: System.Reflection.AssemblyVersionAttribute("1.1.0.1")]
#endif

namespace BFlatA
{
    public enum BuildMode
    {
        None,
        Flat,
        Tree,
        TreeDepositDependency
    }

    public enum ScriptType
    {
        RSP,
        BAT,
        SH
    }

    public static class XmlNodeExtension
    {
        public static XmlAttribute FirstAttribute(this XmlNode node, string attributeName) => node.Attributes.OfType<XmlAttribute>().FirstOrDefault(i => i.Name.ToLower() == attributeName.ToLower());

        public static XmlNode FirstChildNode(this XmlNode node, string elementName) => node.ChildNodes.OfType<XmlNode>().FirstOrDefault(i => i.Name.ToLower() == elementName.ToLower());

        public static XmlNode FirstNodeOfAttribute(this IEnumerable<XmlNode> nodes, string attributeName, string attributeValue)
                    => nodes.FirstOrDefault(i => i.FirstAttribute(attributeName)?.Value.ToLower() == attributeValue.ToLower());

        public static IEnumerable<XElement> OfInclude(this IEnumerable<XElement> r, string elementName) => r.Where(i => i.Name.LocalName.ToLower() == elementName.ToLower() && i.Attribute("Include") != null);

        public static IEnumerable<XElement> OfRemove(this IEnumerable<XElement> r, string elementName) => r.Where(i => i.Name.LocalName.ToLower() == elementName.ToLower() && i.Attribute("Remove") != null);
    }

    internal static class BFlatA
    {
        public const char ARG_EVALUATION_CHAR = ':';
        public const string CACHE_FILENAME = "packages.cache";
        public const int COL_WIDTH = 48;
        public const string COMPILER = "bflat";
        public const string CUSTOM_EXCLU_FILENAME_WITHOUT_EXT = "packages";
        public const string EXCLU_EXT = "exclu";
        public static readonly string[] IGNORED_SUBFOLDER_NAMES = { "bin", "obj" };
        public static readonly bool IsLinux = Path.DirectorySeparatorChar == '/';
        public static readonly string NL = Environment.NewLine;
        public static readonly char PathSepChar = Path.DirectorySeparatorChar;
        public static readonly string WorkingPath = Directory.GetCurrentDirectory();
        public static readonly XNamespace XSD_NUGETSPEC = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        public static BuildMode BuildMode = BuildMode.None;
        public static bool DepositLib = false;
        public static string[] LibCache = Array.Empty<string>();
        public static string[] LibExclu = Array.Empty<string>();
        public static string OutputType = "Exe";
        public static string PackageRoot = null;
        public static List<string> ParsedProjectPaths = new();
        public static string ProjectFile = null;
        public static string RefPath = Directory.GetCurrentDirectory();
        public static string RuntimePathExclu = null;
        public static ScriptType ScriptType = ScriptType.RSP;
        public static string TargetFx = null;
        public static string BFlatArgOut = null;
        public static bool UseBuild = false;
        public static bool UseBuildIL = false;
        public static bool UseExclu = false;

        /// <summary>
        /// Exclude Exclus and Runtime libs.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static IEnumerable<string> DoExclude(this IEnumerable<string> paths) =>
            paths.Where(i => !i.Contains(PathSepChar + "runtime."))
            .Where(i => !LibExclu.Any(x =>
            {
                var lox = x.ToLower();
                if (lox.EndsWith(".dll")) lox = lox.Replace(".dll", "");
                return i.Contains(PathSepChar + lox + PathSepChar);
            }
            ));

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

        public static string GenerateScript(string projectName,
                                            IEnumerable<string> restParams,
                                            IEnumerable<string> codeFileList,
                                            IEnumerable<string> libBook,
                                            IEnumerable<string> resPaths,
                                            ScriptType scriptType,
                                            BuildMode buildMode,
                                            string packageRoot,
                                            string outputType = "Exe",
                                            bool isDependency = false,
                                            string bflatArgOut = null
                                            )
        {
            Console.WriteLine($"{NL}Generating script:{projectName}");

            string lineFeed = GetLineFeed(scriptType);

            StringBuilder cmd = new();

            if (buildMode == BuildMode.Tree)
            {
                if (scriptType != ScriptType.RSP)
                {
                    cmd.Append((IsLinux ? "" : "@") + COMPILER + $" {(UseBuildIL || isDependency ? "build-il" : "build")} ");
                    Console.WriteLine();
                }

                if (isDependency)
                {
                    cmd.Append($"-o {projectName}.dll ");  //all dependencies will use the ext of ".dll", even it's an exe, the name doesn't matter.
                    if (scriptType == ScriptType.RSP) cmd.AppendLine();
                }
                else if (!string.IsNullOrEmpty(bflatArgOut))
                {
                    cmd.Append($"-o {bflatArgOut} ");
                    if (scriptType == ScriptType.RSP) cmd.AppendLine();
                }

                if (!string.IsNullOrEmpty(outputType))
                {
                    cmd.Append($"--target {outputType} ");
                    if (scriptType == ScriptType.RSP) cmd.AppendLine();
                }

                if (restParams.Any())
                {
                    cmd.Append(string.Join(" ", restParams.Select(i => i.StartsWith('-') ? Environment.NewLine + i : i)).TrimStart(Environment.NewLine.ToCharArray()));  // arg per line for Response File
                    Console.WriteLine($"- Found {restParams.Count()} args to be passed to BFlat.");
                }
            }

            if (codeFileList.Any())
            {
                cmd.Append(lineFeed);
                cmd.AppendJoin(lineFeed, codeFileList.Select(i => formatPath(i.Replace("@", getRefPathMacro()))).OrderBy(i => i));
                Console.WriteLine($"- Found {codeFileList.Count()} code files(*.cs)");
            }

            if (libBook.Any())
            {
                cmd.Append(lineFeed + "-r ");
                cmd.AppendJoin(lineFeed + "-r ", libBook.Select(i => formatPath(i.Replace("@", getPackageRootMacro()))).OrderBy(i => i));
                Console.WriteLine($"- Found {libBook.Count()} dependent libs(*.dll)");
            }

            if (resPaths.Any())
            {
                cmd.Append(lineFeed + "-res ");
                cmd.AppendJoin(lineFeed + "-res ", resPaths.Select(i => formatPath(i.Replace("@", getRefPathMacro()))).OrderBy(i => i));
                Console.WriteLine($"Found {resPaths.Count()} embedded resources(*.resx and other)");
            }
            if (buildMode != BuildMode.Tree) cmd.Append(lineFeed);  //last return at the end of a script;

            string getRefPathMacro() => scriptType switch { ScriptType.BAT => "%RP%", ScriptType.SH => "$RP", _ => RefPath };
            string getPackageRootMacro() => scriptType switch { ScriptType.BAT => "%PR%", ScriptType.SH => "$PR", _ => packageRoot };
            string formatPath(string path) => ScriptType == ScriptType.RSP ? Path.GetFullPath(path) : path;

            return cmd.ToString();
        }

        public static string GetExt(string outputType = "exe") => outputType switch
        {
            "Exe" => "",
            "WinExe" => ".exe",
            _ => ".dll"
        };

        public static string GetLineFeed(ScriptType scriptType = ScriptType.RSP, bool useConcactor = true) => scriptType switch
        {
            //Important: ^ is used for general placeholder for a linefeed used in script and will be replaced finally according to ScriptType
            ScriptType.BAT or ScriptType.SH when useConcactor => $" ^{NL}",
            _ => Environment.NewLine
        };

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

        public static void LoadCache()
        {
            var lastCursorPost = Console.GetCursorPosition();
            List<string> cache = new();
            int count = 0;
            try
            {
                using var st = new StreamReader(File.OpenRead(CACHE_FILENAME));
                while (!st.EndOfStream)
                {
                    count++;
                    Console.SetCursorPosition(lastCursorPost.Left, lastCursorPost.Top);
                    Console.Write($"Libs loaded:{count}");
                    var line = st.ReadLine();
                    if (!string.IsNullOrEmpty(line) && Directory.Exists(line)) cache.Add(line);
                }
                LibCache = cache.DoExclude().ToArray();
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            Console.WriteLine("");
        }

        public static void LoadExclu()
        {
            int count = 0;
            List<string> exclude = new();
            try
            {
                var excluFiles = Directory.GetFiles(".", "*." + EXCLU_EXT).Where(i =>
                  {
                      var excluFN = Path.GetFileNameWithoutExtension(i).ToLower();
                      return excluFN == TargetFx || excluFN == CUSTOM_EXCLU_FILENAME_WITHOUT_EXT;
                  }).ToArray();
                if (excluFiles.Length == 0) Console.WriteLine("No Exclu files found!");
                else foreach (string f in excluFiles)
                    {
                        count = 0;
                        Console.WriteLine($"Exclu file found:.{PathSepChar}{Path.GetFileName(f)}");
                        (int Left, int Top) = Console.GetCursorPosition();
                        using var st = new StreamReader(File.OpenRead(f));
                        while (!st.EndOfStream)
                        {
                            var line = st.ReadLine().ToLower();
                            if (!string.IsNullOrEmpty(line) && !line.StartsWith('#') && !exclude.Contains(line))
                            {
                                exclude.Add(line);
                                count++;
                            }
                            Console.SetCursorPosition(Left, Top);
                            Console.WriteLine($"Exclus loaded:{count}");
                        }
                        LibExclu = exclude.ToArray();
                    }
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            Console.WriteLine("");
        }

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
                    IEnumerable<string> matchedLibPaths = null;
                    string actualTarget = null, actualVersion = null;

                    foreach (var target in multiTargets)
                    {
                        matchedLibPaths = null;
                        actualTarget = null;

                        int[] loVerReq = Array.Empty<int>(), hiVerReq = Array.Empty<int>();
                        if (package.Value.StartsWith('[') && package.Value.EndsWith(']')) //version range
                        {
                            var split = package.Value.TrimStart('[').TrimEnd(']').Split(',');
                            if (split.Length >= 2)
                            {
                                loVerReq = Ver2IntArray(split[0]);
                                hiVerReq = Ver2IntArray(split[1]);
                            }
                        }
                        else loVerReq = hiVerReq = Ver2IntArray(package.Value);

                        if (loVerReq.Length == 3 && hiVerReq.Length == 3)
                        {
                            //Required
                            matchedLibPaths = allLibPaths.Where(i =>
                            {
                                var splittedPath = new List<string>(i.Split(PathSepChar));
                                var idx = splittedPath.IndexOf(package.Key.ToLower());

                                if (idx >= 0 && idx < splittedPath.Count - 1 && (splittedPath[^1] == target || splittedPath[^1].StartsWith("netstandard")))
                                {
                                    var verDigits = Ver2IntArray(splittedPath[idx + 1]);
                                    if (verDigits.Length == 3) for (int j = 0; j < 3; j++)
                                        {
                                            if (verDigits[j] > loVerReq[j] && verDigits[j] < hiVerReq[j]) return true;
                                            else if (verDigits[j] < loVerReq[j] || verDigits[j] > hiVerReq[j]) return false;
                                            else if (j == 2 && verDigits[j] >= loVerReq[j] && verDigits[j] <= hiVerReq[j]) return true;
                                        }
                                }
                                return false;
                            }).DoExclude();
                        }

                        //deduplication of libs references (in Flat mode, dependency may not be compatible among projects, the top version will be kept)
                        string libPath = null;
                        if (matchedLibPaths != null) foreach (var d in matchedLibPaths)
                            {
                                libPath = d + PathSepChar + package.Key + ".dll";

                                //get case-sensitive file path
                                libPath = Directory.GetFiles(d).FirstOrDefault(i => i.ToLower() == libPath.ToLower());

                                if (libPath != null)
                                {
                                    libPath = libPath.Replace(packageRoot, "@");
                                    //Package name might be case-insensitive in csproject file, while path will be case-sensitive on Linux.
                                    var duplicatedPackagePath = libBook
                                        .FirstOrDefault(i => i.Contains(PathSepChar + package.Key.ToLower() + PathSepChar));
                                    //determine newer version by path string order, compare from the end will optimize performance.
                                    if (duplicatedPackagePath == null) libBook.Add(libPath);
                                    else if (duplicatedPackagePath != libPath)
                                    {
                                        libPath = new[] { duplicatedPackagePath, libPath }
                                        .OrderByDescending(i => i.Replace("netstandard", "").Replace("net", "").Replace("netcoreapp", "").Replace("netcore", "").Replace(".", "")).First();
                                        libBook.Remove(duplicatedPackagePath);
                                        libBook.Add(libPath);
                                    }
                                    gotit = true;
                                    break;
                                }
                            }

                        if (libPath != null) //libPath should be the sole top lib reference in libBook
                        {
                            actualTarget = Path.GetFileName(Path.GetDirectoryName(libPath));
                            var splittedPath = new List<string>(libPath.Split(PathSepChar));
                            var idx = splittedPath.IndexOf(package.Key.ToLower());
                            if (idx > 0) actualVersion = splittedPath[idx + 1];  //special case: microsoft.win32.registry\5.0.0\runtimes\win\lib\netstandard2.0\Microsoft.Win32.Registry.dll
                        }

                        //if no target matched from actual path, use 'target' specified by user.
                        actualTarget ??= target;
                        actualVersion ??= package.Value;

                        //Parse .nuspec file to obtain package dependencies
                        var packageID = package.Key + PathSepChar + actualVersion;
                        var packagPath = Directory.GetDirectories(packageRoot, packageID).FirstOrDefault();
                        if (!string.IsNullOrEmpty(packagPath))
                        {
                            var nuspecPath = Path.GetFullPath(packagPath + PathSepChar + package.Key.ToLower() + ".nuspec");  //nuespec filename is all lower case
                            if (File.Exists(nuspecPath))
                            {
                                using var stream = File.OpenRead(nuspecPath);
                                var nuspecDoc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);

                                var nodes = nuspecDoc.Root.Descendants(XSD_NUGETSPEC + "group");
                                nodes = nodes.FirstOrDefault(g => g.Attribute("targetFramework")?.Value.ToLower().TrimStart('.') == actualTarget)?.Elements();
                                var myDependencies = nodes?.ToDictionary(kv => kv.Attribute("id")?.Value, kv => kv.Attribute("version")?.Value);
                                if (myDependencies?.Any() == true) libBook = MatchPackages(allLibPaths, myDependencies, packageRoot, new[] { actualTarget }, libBook); //Append Nuget dependencies to libBook
                                break;
                            }
                            else Console.WriteLine($"Warning:nuspecFile not exists, packages dependencies cannot be determined!! {nuspecPath}");
                        }
                        else Console.WriteLine($"Warning:package not found!! {packageID}");

                        //If any dependency found for any target, stop matching other targets(the other targets usually r netstandard).
                        if (gotit) break;
                    }
                }

            return libBook;
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
                                       string[] restParams,
                                       BuildMode buildMode,
                                       ScriptType scriptType,
                                       out string projectName,
                                       out string outputType,
                                       out string script,
                                       List<string> codeBook = null,
                                       List<string> libBook = null,
                                       List<string> resBook = null,
                                       bool isDependency = false
                                       )
        {
            outputType = "Shared";
            script = null;
            projectName = null;

            if (string.IsNullOrEmpty(projectFile)) return -1;

            resBook ??= new();
            codeBook ??= new();
            libBook ??= new();
            List<string> removeBook = new();
            List<string> contentBook = new();
            string refLocalProjects = "";
            string lineFeed = GetLineFeed(scriptType);
            projectName = Path.GetFileNameWithoutExtension(projectFile);
            int err = 0;

            string projectPath = "";
            List<string> targets = new();

            Dictionary<string, string> packageReferences = new();

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
                        var ig = pj.Descendants("ItemGroup").SelectMany(i => i.Elements());

                        //Flatten all property groups
                        var pg = pj.Descendants("PropertyGroup").SelectMany(i => i.Elements());
                        targets = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "targetframework" || i.Name.LocalName.ToLower() == "targetframeworks")?.Value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(i => i.ToLower())?.ToList();
                        outputType = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "outputtype")?.Value ?? outputType;
                        projectName = pg.FirstOrDefault(i => i.Name.LocalName.ToLower() == "assemblyname")?.Value ?? projectName;

                        //If project setting is not compatible with specified framework, then quit.
                        bool isStandardLib = targets?.Any(i => i.StartsWith("netstandard")) == true;
                        bool hasTarget = targets?.Any(i => i.Contains(target)) == true;
                        if (hasTarget || isStandardLib)
                        {
                            AddElement2List(ig.OfRemove("Compile"), removeBook, "CompileRemove", "Remove");
                            AddElement2List(ig.OfInclude("Content"), contentBook, "Content");
                            AddElement2List(ig.OfInclude("EmbeddedResource"), resBook, "EmbeddedResource", useAbsolutePath: false);

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
                            codeBook.AddRange(getCodeFiles(projectPath, removeBook).Except(codeBook));

                            //Recursively parse/build all referenced projects
                            foreach (var i in ig.OfInclude("ProjectReference"))
                            {
                                //make path absolute
                                var refProjectPath = getAbsPaths(i.Attribute("Include")?.Value, projectPath).FirstOrDefault();

                                string innerScript = "", innerProjectName = "", innerOutputType = "";

                                if (buildMode == BuildMode.Tree)
                                {
                                    if (DepositLib) err = ParseProject(refProjectPath,
                                                                       allLibPaths,
                                                                       target,
                                                                       packageRoot,
                                                                       restParams,
                                                                       buildMode,
                                                                       scriptType,
                                                                       out innerProjectName,
                                                                       out innerOutputType,
                                                                       out innerScript,
                                                                       libBook: libBook,
                                                                       isDependency: true);
                                    else err = ParseProject(refProjectPath,
                                                            allLibPaths,
                                                            target,
                                                            packageRoot,
                                                            restParams,
                                                            buildMode,
                                                            scriptType,
                                                            out innerProjectName,
                                                            out innerOutputType,
                                                            out innerScript,
                                                            isDependency: true);
                                }
                                else
                                    err = ParseProject(refProjectPath,
                                                       allLibPaths,
                                                       target,
                                                       packageRoot,
                                                       restParams,
                                                       buildMode,
                                                       scriptType,
                                                       out innerProjectName,
                                                       out innerOutputType,
                                                       out innerScript,
                                                       codeBook,
                                                       libBook,
                                                       resBook,
                                                       true);

                                if (err >= 0 && buildMode == BuildMode.Tree)  // <0 fatal, >=0 success
                                {
                                    script = AppendScriptBlock(script, innerScript, scriptType);

                                    //add local projects to references
                                    var refProject = "-r " + innerProjectName + GetExt(innerOutputType);
                                    refLocalProjects += buildMode == BuildMode.Tree ? lineFeed + refProject : refProject + lineFeed; ;
                                }
                            }

                            //build current project, so far all required referenced projects must have been built to working dir (as .dll).
                            if (err == 0 && buildMode == BuildMode.Tree)
                            {
                                string myScript = "";
                                string argOut = isDependency ? null : BFlatArgOut;
                                if (UseBuild)
                                {
                                    myScript = GenerateScript(projectName, restParams, codeBook, libBook, resBook, ScriptType.RSP, buildMode, packageRoot, outputType, isDependency, argOut);
                                    myScript += refLocalProjects;

                                    if (scriptType == ScriptType.RSP)
                                    {
                                        WriteScript(scriptType, packageRoot, projectName, myScript);
                                        Console.WriteLine($"Building {(isDependency ? "dependency" : "root")}:{projectName}...");
                                        if (isDependency) build("bflat build-il @build.rsp");
                                        else build($"bflat {(UseBuildIL ? "build-il" : "build")} @build.rsp");
                                    }
                                    else
                                    {
                                        //under build mode, scriptType is always RSP
                                    }

                                    var outputFile = argOut ?? (projectName + (isDependency ? ".dll" : (IsLinux ? "" : ".exe")));

                                    //Must wait for dependecy to be compiled
                                    if (!SpinWait.SpinUntil(() => IsReadable(outputFile), 20000)) Console.WriteLine($"Error:building timeout!");
                                    else Console.WriteLine("Script execution completed!");
                                }
                                else
                                {
                                    myScript = GenerateScript(projectName, restParams, codeBook, libBook, resBook, scriptType, buildMode, packageRoot, outputType, isDependency, argOut);
                                    myScript += refLocalProjects;
                                    Console.WriteLine($"Appending Script:{projectName}...{Environment.NewLine}");
                                    script = AppendScriptBlock(script, myScript, scriptType);
                                }
                            }
                        }
                        else Console.WriteLine($"Warnning:Project properties are not compatible with the target:{target}, {projectFile}!!! ");

                        ParsedProjectPaths.Add(projectFile);

                        //[Local methods]
                        int AddElement2List(IEnumerable<XElement> nodes, List<string> book, string key, string action = "Include", bool useAbsolutePath = true)
                        {
                            //This method is easy to extent to more categories.
                            //CodeFiles and PackageReferences r exceptions and stored otherwise.
                            int counter = 0;
                            foreach (var i in nodes)
                            {
                                IEnumerable<string> items = getAbsPaths(ToSysPathSep(i.Attribute(action)?.Value), projectPath);
                                //relative paths used by script is relative to WorkingPath
                                if (!useAbsolutePath) items = items.Select(i => "@" + PathSepChar + Path.GetRelativePath(RefPath, i));

                                book.AddRange(items.Except(book));

                                counter += items.Count();
                            }
                            if (counter > 0) Console.WriteLine($"{key,24}\t[{action}]\t{counter} items added!");
                            return counter;
                        }

                        IEnumerable<string> getAbsPaths(string path, string basePath)
                        {
                            if (string.IsNullOrEmpty(path)) return Array.Empty<string>();

                            string fullPath = Path.GetFullPath(projectPath + PathSepChar + ToSysPathSep(path), basePath);
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

                        List<string> getCodeFiles(string path, List<string> removeLst)
                        {
                            List<string> codeFiles = new();
                            try
                            {
                                var files = Directory.GetFiles(ToSysPathSep(path), "*.cs").Except(removeLst).Select(i => "@" + PathSepChar + Path.GetRelativePath(RefPath, i));
                                codeFiles.AddRange(files);
                            }
                            catch (Exception ex) { Console.WriteLine(ex.Message); }

                            foreach (var d in Directory.GetDirectories(path).Where(i => !IGNORED_SUBFOLDER_NAMES.Any(j => i.ToLower().EndsWith(j)))) codeFiles.AddRange(getCodeFiles(d, removeLst));

                            return codeFiles;
                        }
                    }
                    else return -0x12;
                }
                else return -0x11;
            }
            else
            {
                Console.WriteLine($"Warning:project already parsed, ignoring it..." + projectFile);
            }

            return 0;
        }

        public static ScriptType ParseScriptType(string typeStr) => typeStr.ToLower() switch
        {
            "sh" => ScriptType.SH,
            "cmd" or "bat" => ScriptType.BAT,
            _ => ScriptType.RSP
        };

        /// <summary>
        /// Pre-caching nuget package "/lib/" paths
        /// </summary>
        /// <param name="packageRoot"></param>
        public static void PreCacheLibs(string packageRoot)
        {
            Console.WriteLine($"Caching Nuget packages from path:{packageRoot} ...");
            var libPath = PathSepChar + "lib" + PathSepChar;
            int pathCount = 0, libCount = 0;
            var lastCursorPost = Console.GetCursorPosition();

            try
            {
                if (!string.IsNullOrEmpty(packageRoot)) LibCache = Directory.GetDirectories(packageRoot, "*", SearchOption.AllDirectories)
                   .Where(i =>
                   {
                       pathCount++;
                       Console.SetCursorPosition(lastCursorPost.Left, lastCursorPost.Top);
                       Console.Write($"Libs found:{libCount}/Folders searched:{pathCount}");
                       var splitted = i.Split(PathSepChar, StringSplitOptions.RemoveEmptyEntries);
                       if (splitted[^2].ToLower() == "lib")
                       {
                           libCount++;
                           return true;
                       }
                       else return false;
                   }).DoExclude().OrderByDescending(i => i).ToArray();
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            Console.WriteLine($"{NL}Found {LibCache.Length} nuget packages!");
        }

        public static string[] RemoveArg(string[] restParams, string a) => restParams.Except(new[] { a }).ToArray();

        public static void ShowHelp()
        {
            Console.WriteLine($"  Usage: bflata [build] <csproj file> [options]{NL}");
            Console.WriteLine("  [build|build-il]".PadRight(COL_WIDTH) + "Build with BFlat in %Path%, with -st option ignored.");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"If omitted, generate building script only, with -bm option still valid.{NL}");
            Console.WriteLine("  <.csproj file>".PadRight(COL_WIDTH) + "Must be the 2nd arg if 'build' specified,or the 1st otherwise,only 1 project allowed.");
            Console.WriteLine($"{NL}Options:");
            Console.WriteLine("  -pr|--packageroot:<path to package storage>".PadRight(COL_WIDTH) + $"eg.'C:\\Users\\%username%\\.nuget\\packages' or '$HOME/.nuget/packages'.{NL}");
            Console.WriteLine("  -rp|--refpath:<any path to be related>".PadRight(COL_WIDTH) + "A reference path to generate paths for files in the building script,");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"can be optimized to reduce path lengths.Default is '.' (current dir).{NL}");
            Console.WriteLine("  -fx|--framework:<moniker>".PadRight(COL_WIDTH) + "The TFM compatible with the built-in .net runtime of BFlat(see 'bflat --info')");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"mainly purposed for matching dependencies, e.g. 'net7.0'{NL}");
            Console.WriteLine("  -bm|--buildmode:<flat|tree|treed>".PadRight(COL_WIDTH) + "FLAT = flatten project tree to one for building;");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"TREE = build each project alone and reference'em accordingly with -r option;");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"TREED = '-bm:tree -dd'.{NL}");
            Console.WriteLine("  -st|--scripttype:<rsp|bat|sh>".PadRight(COL_WIDTH) + $"Response File(.rsp,default) or Windows Batch file(.cmd/.bat) or Linux Shell Script(.sh) file.{NL}");
            Console.WriteLine("  -dd|--depdep".PadRight(COL_WIDTH) + "Deposit Dependencies mode, valid with '-bm:tree',");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"where dependencies of child projects are deposited and served to parent project,");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"as to fulfill any possible reference requirements{NL}");
            Console.WriteLine("  -t|--target:<Exe|Shared|WinExe>".PadRight(COL_WIDTH) + $"Build Target, this arg will also be passed to BFlat.{NL}");
            Console.WriteLine("  -o|--out:<File>".PadRight(COL_WIDTH) + $"Output file path for the root project.This option will also be passed to BFlat.{NL}");
            Console.WriteLine("  -xx|--exclufx:<dotnet Shared Framework path>".PadRight(COL_WIDTH) + "If path valid, lib exclus will be extracted from the path.");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"e.g. 'C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\7.0.2'");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"and extracted exclus will be saved to '<moniker>.exclu' for further use.");
            Console.WriteLine("".PadRight(COL_WIDTH) + $"where moniker is specified by -fx option.{NL}");

            Console.WriteLine($"{NL}Note:");
            Console.WriteLine("  Any other args will be passed 'as is' to BFlat, except for '-o.");
            Console.WriteLine("  For options, the ':' char can also be replaced with space. e.g. -pr:<path> = -pr <path>.");
            Console.WriteLine("  Do make sure <ImplicitUsings> switched off in .csproj file and all namespaces properly imported.");
            Console.WriteLine("  The filenames for the building script are one of 'build.rsp,build.cmd,build.sh' and the .rsp file allows larger arguments and is prefered.");
            Console.WriteLine("  Once '<moniker>.exclu' file is saved, you can use it for any later build, and a 'packages.exclu' is always loaded and can be used to store extra shared exclus, where 'exclu' is the short for 'Excluded Packages'.");
            Console.WriteLine($"{NL}Examples:");
            Console.WriteLine("  bflata xxxx.csproj -pr:C:\\Users\\username\\.nuget\\packages -fx=net7.0 -st:bat -bm:treed  <- only generate BAT script which builds project tree orderly with Deposit Dependencies.");
            Console.WriteLine("  bflata build xxxx.csproj -pr:C:\\Users\\username\\.nuget\\packages -st:bat --arch x64  <- build in FLAT mode with '--arch x64' passed to BFlat and -st:bat ignored.");
        }

        public static string ToSysPathSep(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (path.Contains('/') && PathSepChar != '/') path = path.Replace('/', PathSepChar);
                else if (path.Contains('\\') && PathSepChar != '\\') path = path.Replace('\\', PathSepChar);
            }
            return path;
        }

        public static int[] Ver2IntArray(string verStr) => verStr.Split('.').Select(i => int.TryParse(i, out int n) ? n : 0).ToArray();

        public static void WriteCache()
        {
            try
            {
                using var st = File.Create(CACHE_FILENAME);
                foreach (var l in LibCache) st.Write(Encoding.UTF8.GetBytes(l + NL));
                Console.WriteLine($"Package cache saved!");
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

        public static void WriteScript(ScriptType scriptType, string packageRoot, string projectName, string script)
        {
            if (string.IsNullOrEmpty(script)) return;
            Console.WriteLine($"Writing script:{projectName}...");

            // If target PathSepChar is not the same with the generated script, replace'em all.
            var targetPathSepChar = GetPathSepChar(scriptType);
            if (Path.DirectorySeparatorChar != targetPathSepChar) script = script.Replace(Path.DirectorySeparatorChar, targetPathSepChar);

            if (scriptType == ScriptType.SH)
            {
                script = script.Replace('^', '\\');  // concactor placeholder will be replaced with that in the .sh script.
                script = $"#!/bin/sh\nRP={RefPath}\nPR={packageRoot}\n" + script;
            }
            else if (scriptType == ScriptType.BAT)
            {
                script = $"@SET RP={RefPath}\r\n@SET PR={packageRoot}\r\n" + script;
            }
            else
            {
                // Response file
            }

            try
            {
                var buf = Encoding.ASCII.GetBytes(script.ToString());
                using var st = File.Create(getBuildScriptFileName(scriptType));
                st.Write(buf);
                st.Flush();
                Console.WriteLine($"Script written!{NL}");
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            return;
        }

        private static string AppendScriptBlock(string script, string myScript, ScriptType scriptType)
                                        => script + (script == null || script.EndsWith("\n") ? "" : NL + NL) + myScript;

        private static void build(string myScript)
        {
            Console.WriteLine($"- Executing building script: {(myScript.Length > 22 ? myScript[..22] : myScript)}...");
            try
            {
                if (myScript.StartsWith(COMPILER))
                {
                    var paths = Environment.GetEnvironmentVariable("path").Split(IsLinux ? ':' : ';');
                    var compilerPath = paths.FirstOrDefault(i => File.Exists(i + PathSepChar + (IsLinux ? COMPILER : COMPILER + ".exe")));
                    if (Directory.Exists(compilerPath)) Process.Start(compilerPath + PathSepChar + COMPILER, myScript.Remove(0, COMPILER.Length));
                    else Console.WriteLine("Error:" + COMPILER + " doesn't exist in PATH!");
                }
                else Process.Start(myScript);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private static string getBuildScriptFileName(ScriptType scriptType = ScriptType.RSP) => scriptType switch
        {
            ScriptType.BAT => "build.cmd",
            ScriptType.SH => "build.sh",
            _ => "build.rsp"
        };

        private static char GetPathSepChar(ScriptType scriptType) => scriptType switch
        {
            ScriptType.SH => '/',
            ScriptType.BAT => '\\',
            _ => PathSepChar,
        };

        private static int Main(string[] args)
        {
            string[] restArgs;

            List<string> codeBook = new(), libBook = new(), resBook = new();

            Console.WriteLine($"BFlatA V{Assembly.GetEntryAssembly().GetName().Version} @github.com/xiaoyuvax/bflata{NL}" +
                $"Description:{NL}" +
                $"  A wrapper/building script generator for BFlat, a native C# compiler, for recusively building .csproj file with:{NL}" +
                $"    - Referenced projects{NL}" +
                $"    - Nuget package dependencies{NL}" +
                $"    - Embedded resources{NL}" +
                $"  Before using BFlatA, you should get BFlat first at https://flattened.net.{NL}");

            bool tryGetArg(string a, string shortName, string fullName, out string value)
            {
                value = null;
                var loa = a.ToLower();
                if (loa.StartsWith(shortName + ARG_EVALUATION_CHAR) || loa.StartsWith(fullName + ARG_EVALUATION_CHAR)
                    || loa.StartsWith(shortName + ' ') || loa.StartsWith(fullName + ' '))
                {
                    //restParams for BFlat
                    restArgs = RemoveArg(restArgs, a);
                    value = a[(shortName.Length + 1)..];
                    return true;
                }
                return false;
            }

            //Parse input args
            restArgs = args;
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

                    restArgs = RemoveArg(restArgs, restArgs[0]);
                }
                else if (restArgs[0].ToLower() == "build-il")
                {
                    UseBuild = UseBuildIL = true;
                    restArgs = RemoveArg(restArgs, restArgs[0]);
                }

                if (!restArgs[0].StartsWith("-") && File.Exists(restArgs[0]))
                {
                    ProjectFile = restArgs[0];
                    restArgs = RemoveArg(restArgs, restArgs[0]);
                }

                //rearrange restArgs
                restArgs = string.Join(' ', restArgs).Split("-", StringSplitOptions.RemoveEmptyEntries).Select(i => '-' + i.Trim()).ToArray();

                //process options
                foreach (var a in restArgs)
                {
                    if (tryGetArg(a, "-pr", "--packageroot", out string pr))
                        if (Directory.Exists(pr)) PackageRoot = Path.GetFullPath(pr);
                        else
                        {
                            Console.WriteLine($"Error:PacakgeRoot does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "-rp", "--refpath", out string rp))
                        if (Directory.Exists(rp)) RefPath = Path.GetFullPath(rp);
                        else
                        {
                            Console.WriteLine($"Error:RefPath does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "-xx", "--exclufx", out string xrtp))
                        if (Directory.Exists(xrtp)) RuntimePathExclu = Path.GetFullPath(xrtp);
                        else
                        {
                            Console.WriteLine($"Error:RuntimePath does not exist or is invalid!");
                            return -1;
                        }
                    else if (tryGetArg(a, "-st", "--scripttype", out string sm)) ScriptType = ParseScriptType(sm);
                    else if (tryGetArg(a, "-bm", "--buildmode", out string bm)) BuildMode = ParseBuildMode(bm);
                    else if (tryGetArg(a, "-t", "--target", out string t)) OutputType = t;
                    else if (tryGetArg(a, "-fx", "--framework", out string fx)) TargetFx = fx.ToLower();
                    else if (tryGetArg(a, "-o", "--out", out string o)) //hijack -o arg of BFlat, and it shall not be passed to denpendent project
                    {
                        BFlatArgOut = o;
                        restArgs = RemoveArg(restArgs, a);
                    }
                    else if (a.ToLower() == "-dd" || a.ToLower() == "--depdep")
                    {
                        DepositLib = true;
                        restArgs = RemoveArg(restArgs, a);
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
                ScriptType = ScriptType.RSP;  //default ScriptType for building and -st option is ignored under Building mode.
                if (BuildMode == BuildMode.None) BuildMode = BuildMode.Flat; //default BuildMode for Build option
            }
            else if (BuildMode == BuildMode.Tree) Console.WriteLine("Warning:.rsp script generated under TREE mode is not buildable!");

            if (string.IsNullOrEmpty(TargetFx)) TargetFx = "net7.0";

            //echo args.
            Console.WriteLine($"{NL}--ARGS--------------------------------");
            Console.WriteLine($"Build:{(UseBuild ? "On" : "Off")}");
            Console.WriteLine($"DepositDep:{(UseBuild ? "On" : "Off")}");
            Console.WriteLine($"Target:{OutputType}");
            Console.WriteLine($"BuildMode:{BuildMode}");
            Console.WriteLine($"ScriptMode:{ScriptType}");
            Console.WriteLine($"TargetFx:{TargetFx}");
            Console.WriteLine($"PackageRoot:{PackageRoot}");
            Console.WriteLine($"RefPath:{RefPath}");
            Console.WriteLine($"Args4BFlat:{string.Join(' ', restArgs)}");
            Console.WriteLine();

            if (!string.IsNullOrEmpty(ProjectFile))
            {
                Console.WriteLine($"--LIB EXCLU---------------------------");
                if (string.IsNullOrEmpty(RuntimePathExclu)) LoadExclu();
                else
                {
                    LibExclu = ExtractExclu(RuntimePathExclu);
                    WriteExclu(TargetFx);
                }
                Console.WriteLine($"--LIB CACHE---------------------------");
                if (File.Exists(CACHE_FILENAME))
                {
                    Console.WriteLine($"Package cache found!");
                    LoadCache();
                }
                if (LibCache.Length == 0)
                {
                    PreCacheLibs(PackageRoot);
                    if (LibCache.Length > 0) WriteCache();
                }
                Console.WriteLine();

                //Parse project and all project references recursively.
                if (UseBuild) Console.WriteLine($"--BUILDING----------------------------");
                else Console.WriteLine($"--SCRIPTING---------------------------");
                var err = ParseProject(ProjectFile, LibCache, TargetFx, PackageRoot, restArgs, BuildMode, ScriptType, out string projectName, out _, out string script, codeBook, libBook, resBook);

                if (err == 0)
                {
                    //overwrite script under Flat Mode, and explicitly specify BuldMode.Tree as to generate the header part.
                    if (BuildMode != BuildMode.Tree)
                        script = GenerateScript(projectName, restArgs, codeBook, libBook, resBook, ScriptType, BuildMode.Tree, PackageRoot, OutputType, false, BFlatArgOut);

                    if (!string.IsNullOrEmpty(script))
                    {
                        //Write to script file
                        WriteScript(ScriptType, PackageRoot, projectName, script);

                        if (UseBuild && BuildMode == BuildMode.Flat)
                        {
                            //Start Building
                            Console.WriteLine($"Building in FLAT mode:{projectName}...");
                            build(ScriptType == ScriptType.RSP ? $"bflat {(UseBuildIL ? "build-il" : "build")} @build.rsp" : (IsLinux ? "./build.sh" : "./build.cmd"));
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Error occurred during parsing project file(s)!!(0x{err.ToString("{0:X}")}");
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