using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenUtau.Core.JobPlugins {
    /// <summary>Finds JOB plugins without executing any of their code.</summary>
    public static class JobPluginCatalog {
        static readonly Regex manifestPattern = new Regex(@"\bfunction\s+manifest\s*\(");
        static readonly Regex mainPattern = new Regex(@"\bfunction\s+main\s*\(");
        static readonly Regex fieldPattern = new Regex(
            @"\b(name|comment|author|pluginID|pluginVersion|apiVersion)\s*=\s*(?:""((?:[^""\\\r\n]|\\.)*)""|'((?:[^'\\\r\n]|\\.)*)')");
        static readonly Regex apiPattern = new Regex(@"\b(VS[A-Za-z0-9_]+)\s*\(");
        static readonly (Regex pattern, string feature)[] restrictedPatterns = {
            (new Regex(@"\bos\s*\.\s*(execute|remove|rename|exit|tmpname|setlocale)\b"), "os.$1"),
            (new Regex(@"\bos\s*\.\s*getenv\b"), "os.getenv"),
            (new Regex(@"\bio\s*\.\s*(popen|write|output|input|lines|tmpfile)\b"), "io.$1"),
            (new Regex(@"\bio\s*\.\s*open\s*\([^)]*,\s*[""'][^""']*[wa+]"), "io.open (write)"),
            (new Regex(@"\b(require|dofile|loadfile|loadstring|load)\s*\("), "$1"),
            (new Regex(@"\bpackage\s*\.\s*(loadlib|loaders|searchers|cpath|preload)\b"), "package.$1"),
            (new Regex(@"\bdebug\s*\.\s*[A-Za-z]"), "debug"),
            (new Regex(@"\b(setfenv|getfenv|coroutine)\b"), "$1"),
        };
        static readonly UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);

        public static JobPluginCatalogResult Scan(IEnumerable<string> roots, int maxSourceBytes = 1 << 20) {
            var result = new JobPluginCatalogResult();
            var seenRoots = new HashSet<string>(PathComparer);
            var seenFiles = new HashSet<string>(PathComparer);
            foreach (var root in roots) {
                if (string.IsNullOrWhiteSpace(root)) {
                    continue;
                }
                string fullRoot;
                try {
                    fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                } catch (Exception e) {
                    result.Diagnostics.Add($"{root}: {e.Message}");
                    continue;
                }
                if (!seenRoots.Add(fullRoot)) {
                    continue;
                }
                if (!Directory.Exists(fullRoot)) {
                    result.Diagnostics.Add($"{fullRoot}: folder not found");
                    continue;
                }
                foreach (var file in EnumerateLua(fullRoot, result.Diagnostics)) {
                    if (!seenFiles.Add(file)) {
                        continue;
                    }
                    try {
                        result.Plugins.Add(Inspect(file, maxSourceBytes));
                    } catch (Exception e) {
                        result.Diagnostics.Add($"{file}: {e.Message}");
                    }
                }
            }
            AssignDisplayNames(result.Plugins);
            return result;
        }

        static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        static IEnumerable<string> EnumerateLua(string root, List<string> diagnostics) {
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0) {
                string dir = pending.Pop();
                try {
                    files.AddRange(Directory.GetFiles(dir)
                        .Where(f => string.Equals(Path.GetExtension(f), ".lua", StringComparison.OrdinalIgnoreCase))
                        .Where(f => !File.GetAttributes(f).HasFlag(FileAttributes.ReparsePoint)));
                    foreach (var sub in Directory.GetDirectories(dir)) {
                        // Linked folders could loop or leave the configured root.
                        if (!new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                            pending.Push(sub);
                        }
                    }
                } catch (Exception e) {
                    diagnostics.Add($"{dir}: {e.Message}");
                }
            }
            return files.Select(Path.GetFullPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ThenBy(f => f, StringComparer.Ordinal);
        }

        public static string ReadSource(string file, int maxSourceBytes) {
            var info = new FileInfo(file);
            if (info.Length > maxSourceBytes) {
                throw new JobPluginException($"source is larger than {maxSourceBytes} bytes");
            }
            byte[] bytes = File.ReadAllBytes(file);
            int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            try {
                return strictUtf8.GetString(bytes, offset, bytes.Length - offset);
            } catch (DecoderFallbackException) {
                throw new JobPluginException("source is not valid UTF-8");
            }
        }

        public static JobPluginInfo Inspect(string file, int maxSourceBytes = 1 << 20) {
            string source = ReadSource(file, maxSourceBytes);
            string code = StripComments(source);
            if (!mainPattern.IsMatch(code)) {
                throw new JobPluginException("no main(processParam, envParam) entry point");
            }
            var fields = new Dictionary<string, string>();
            var manifest = manifestPattern.Match(code);
            if (manifest.Success) {
                var block = FunctionBody(code, manifest.Index);
                foreach (Match m in fieldPattern.Matches(block)) {
                    string key = m.Groups[1].Value;
                    if (!fields.ContainsKey(key)) {
                        fields[key] = Unescape(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
                    }
                }
            }
            var apis = apiPattern.Matches(code).Select(m => m.Groups[1].Value).Distinct().ToList();
            var restricted = new List<string>();
            foreach (var (pattern, feature) in restrictedPatterns) {
                foreach (Match m in pattern.Matches(code)) {
                    string name = m.Result(feature);
                    if (!restricted.Contains(name)) {
                        restricted.Add(name);
                    }
                }
            }
            using var sha = SHA256.Create();
            return new JobPluginInfo {
                FilePath = file,
                Name = fields.GetValueOrDefault("name") is { Length: > 0 } n ? n : Path.GetFileNameWithoutExtension(file),
                Comment = fields.GetValueOrDefault("comment") ?? string.Empty,
                Author = fields.GetValueOrDefault("author") ?? string.Empty,
                PluginId = fields.GetValueOrDefault("pluginID") ?? string.Empty,
                PluginVersion = fields.GetValueOrDefault("pluginVersion") ?? string.Empty,
                ApiVersion = fields.GetValueOrDefault("apiVersion") ?? string.Empty,
                SourceHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))),
                UnsupportedApis = apis.Where(a => !JobPluginApi.SupportedNames.Contains(a)).OrderBy(a => a, StringComparer.Ordinal).ToArray(),
                RestrictedFeatures = restricted.ToArray(),
            };
        }

        static void AssignDisplayNames(List<JobPluginInfo> plugins) {
            foreach (var group in plugins.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) {
                bool duplicate = group.Count() > 1;
                foreach (var plugin in group) {
                    plugin.DisplayName = duplicate
                        ? $"{plugin.Name} ({Path.GetFileName(Path.GetDirectoryName(plugin.FilePath))})"
                        : plugin.Name;
                }
            }
            // Same name in same-named folders still needs a unique label.
            foreach (var group in plugins.GroupBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) {
                foreach (var plugin in group) {
                    plugin.DisplayName = $"{plugin.Name} ({plugin.FilePath})";
                }
            }
        }

        /// <summary>Text from a function header to its balanced "end", approximated over Lua block keywords.</summary>
        static string FunctionBody(string code, int start) {
            var tokens = new Regex(@"\b(function|if|do|repeat|end|until)\b");
            int depth = 0;
            foreach (Match m in tokens.Matches(code, start)) {
                switch (m.Value) {
                    case "function":
                    case "if":
                    case "do":
                    case "repeat":
                        depth++;
                        break;
                    default:
                        depth--;
                        break;
                }
                if (depth == 0) {
                    return code.Substring(start, m.Index - start);
                }
            }
            return code.Substring(start);
        }

        static string Unescape(string value) => Regex.Replace(value, @"\\(.)", m => m.Groups[1].Value switch {
            "n" => "\n",
            "t" => "\t",
            "r" => "\r",
            var c => c,
        });

        /// <summary>Removes comments, keeping string literals and line structure.</summary>
        public static string StripComments(string source) {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length) {
                char c = source[i];
                if (c == '"' || c == '\'') {
                    int j = i + 1;
                    while (j < source.Length && source[j] != c && source[j] != '\n') {
                        j += source[j] == '\\' ? 2 : 1;
                    }
                    j = Math.Min(j + 1, source.Length);
                    sb.Append(source, i, j - i);
                    i = j;
                } else if (c == '[' && LongBracket(source, i, out int level)) {
                    int close = source.IndexOf("]" + new string('=', level) + "]", i, StringComparison.Ordinal);
                    int j = close < 0 ? source.Length : close + level + 2;
                    sb.Append(source, i, j - i);
                    i = j;
                } else if (c == '-' && i + 1 < source.Length && source[i + 1] == '-') {
                    if (LongBracket(source, i + 2, out int commentLevel)) {
                        int close = source.IndexOf("]" + new string('=', commentLevel) + "]", i, StringComparison.Ordinal);
                        int j = close < 0 ? source.Length : close + commentLevel + 2;
                        sb.Append('\n', source.Substring(i, j - i).Count(ch => ch == '\n'));
                        i = j;
                    } else {
                        while (i < source.Length && source[i] != '\n') {
                            i++;
                        }
                    }
                } else {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        static bool LongBracket(string s, int i, out int level) {
            level = 0;
            if (i >= s.Length || s[i] != '[') {
                return false;
            }
            int j = i + 1;
            while (j < s.Length && s[j] == '=') {
                j++;
            }
            level = j - i - 1;
            return j < s.Length && s[j] == '[';
        }
    }
}
