using System;
using System.Diagnostics;
using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;

namespace OpenUtau.Core.JobPlugins {
    /// <summary>
    /// Sandboxed Lua interpreter for one JOB plugin run. Every run gets a fresh script with only
    /// language, string, table, maths and time facilities; all code runs in pre-emptive coroutines.
    /// </summary>
    public sealed class JobPluginRuntime {
        const CoreModules Modules = CoreModules.Basic | CoreModules.GlobalConsts | CoreModules.TableIterators
            | CoreModules.Metatables | CoreModules.String | CoreModules.Table | CoreModules.ErrorHandling
            | CoreModules.Math | CoreModules.OS_Time;

        // Lua 5.1 names absent from MoonSharp, explicit failures for withheld facilities, a read-only
        // io.open limited to the plugin folder, and a Lua table.sort so comparators stay pre-emptible.
        const string Prelude = @"
local readFile = __jobReadFile
local log10 = __jobLog10
local pow = __jobPow
__jobReadFile = nil
__jobLog10 = nil
__jobPow = nil
if table.getn == nil then table.getn = function(t) return #t end end
if table.maxn == nil then
  table.maxn = function(t)
    local m = 0
    for k in pairs(t) do if type(k) == 'number' and k > m then m = k end end
    return m
  end
end
if unpack == nil then unpack = table.unpack end
if math.pow == nil then math.pow = pow end
if math.log10 == nil then math.log10 = log10 end
if math.mod == nil then math.mod = math.fmod end
if string.gfind == nil then string.gfind = string.gmatch end
do
  local function lt(a, b) return a < b end
  table.sort = function(t, cmp)
    cmp = cmp or lt
    local n = #t
    local function sift(root, stop)
      while true do
        local child = root * 2
        if child > stop then return end
        if child + 1 <= stop and cmp(t[child], t[child + 1]) then child = child + 1 end
        if cmp(t[root], t[child]) then
          t[root], t[child] = t[child], t[root]
          root = child
        else
          return
        end
      end
    end
    for start = math.floor(n / 2), 1, -1 do sift(start, n) end
    for stop = n, 2, -1 do
      t[1], t[stop] = t[stop], t[1]
      sift(1, stop - 1)
    end
  end
end
local function withheld(name)
  return function() error(name .. ' is not available to JOB plugins in OpenUtau Xen', 2) end
end
for _, name in ipairs({ 'execute', 'getenv', 'remove', 'rename', 'exit', 'tmpname', 'setlocale' }) do
  os[name] = withheld('os.' .. name)
end
require = withheld('require')
dofile = withheld('dofile')
loadfile = withheld('loadfile')
loadstring = withheld('loadstring')
load = withheld('load')
package = { path = '', cpath = '', loaded = {} }
io = {}
for _, name in ipairs({ 'popen', 'write', 'read', 'lines', 'output', 'input', 'tmpfile', 'close' }) do
  io[name] = withheld('io.' .. name)
end
function io.open(path, mode)
  mode = mode or 'r'
  if mode ~= 'r' and mode ~= 'rb' then
    error('io.open: JOB plugins may only read files in their own folder', 2)
  end
  local text, message = readFile(path)
  if text == nil then return nil, message end
  local pos = 1
  local file = {}
  function file:read(fmt)
    fmt = fmt or '*l'
    if fmt == '*a' or fmt == 'a' then
      local rest = string.sub(text, pos)
      pos = #text + 1
      return rest
    end
    if pos > #text then return nil end
    if fmt == '*l' or fmt == 'l' then
      local e = string.find(text, '\n', pos, true)
      local line
      if e then
        line = string.sub(text, pos, e - 1)
        pos = e + 1
      else
        line = string.sub(text, pos)
        pos = #text + 1
      end
      return (string.gsub(line, '\r$', ''))
    end
    if fmt == '*n' or fmt == 'n' then
      local s, e = string.find(text, '^%s*[-+]?%d*%.?%d+[eE]?[-+]?%d*', pos)
      if not s then return nil end
      pos = e + 1
      return tonumber(string.sub(text, s, e))
    end
    if type(fmt) == 'number' then
      local chunk = string.sub(text, pos, pos + fmt - 1)
      pos = pos + fmt
      return chunk
    end
    error('file:read: unsupported format', 2)
  end
  function file:lines()
    return function() return file:read('*l') end
  end
  function file:close() return true end
  return file
end
";

        readonly JobPluginLimits limits;
        readonly Stopwatch clock = new Stopwatch();
        long slices;

        public JobPluginRuntime(JobPluginLimits limits) {
            this.limits = limits;
        }

        public TimeSpan Elapsed => clock.Elapsed;

        /// <summary>Stops the time limit while a modal dialog waits for the user.</summary>
        public IDisposable PauseClock() {
            bool running = clock.IsRunning;
            clock.Stop();
            return new Resume(() => { if (running) clock.Start(); });
        }

        sealed class Resume : IDisposable {
            Action? action;
            public Resume(Action action) { this.action = action; }
            public void Dispose() { action?.Invoke(); action = null; }
        }

        sealed class DeniedLoader : ScriptLoaderBase {
            public override object LoadFile(string file, Table globalContext)
                => throw new ScriptRuntimeException("file loading is not available to JOB plugins");
            public override bool ScriptFileExists(string name) => false;
        }

        public Script CreateScript(string scriptDirectory, Action<string> log) {
            var script = new Script(Modules);
            script.Options.ScriptLoader = new DeniedLoader();
            script.Options.DebugPrint = log;
            script.Options.UseLuaErrorLocations = true;
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scriptDirectory)) + Path.DirectorySeparatorChar;
            script.Globals["__jobReadFile"] = DynValue.NewCallback((ctx, args) => ReadFile(root, args));
            script.Globals["__jobLog10"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(Math.Log10(args.AsType(0, "log10", DataType.Number).Number)));
            script.Globals["__jobPow"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(Math.Pow(
                args.AsType(0, "pow", DataType.Number).Number, args.AsType(1, "pow", DataType.Number).Number)));
            script.DoString(Prelude, null, "=prelude");
            return script;
        }

        DynValue ReadFile(string root, CallbackArguments args) {
            string path = args.AsType(0, "io.open", DataType.String).String;
            string full;
            try {
                full = Path.GetFullPath(path);
            } catch (Exception e) {
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString($"{path}: {e.Message}"));
            }
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(root, comparison)) {
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString($"{path}: outside the plugin folder"));
            }
            if (!File.Exists(full)) {
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString($"{path}: No such file or directory"));
            }
            try {
                return DynValue.NewString(JobPluginCatalog.ReadSource(full, limits.MaxSourceBytes));
            } catch (Exception e) {
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString($"{path}: {e.Message}"));
            }
        }

        /// <summary>Loads and runs top-level code, then calls main; returns its integer result.</summary>
        public int Run(Script script, string source, string chunkName, Func<Script, DynValue[]> mainArguments) {
            try {
                DynValue chunk = script.LoadString(source, null, chunkName);
                RunCoroutine(script, chunk, Array.Empty<DynValue>());
                DynValue main = script.Globals.Get("main");
                if (main.Type != DataType.Function) {
                    throw new JobPluginException("main(processParam, envParam) is not defined");
                }
                DynValue result = RunCoroutine(script, main, mainArguments(script));
                DynValue first = result.Type == DataType.Tuple
                    ? (result.Tuple.Length > 0 ? result.Tuple[0] : DynValue.Nil) : result;
                double? number = first.CastToNumber();
                if (number == null || number.Value != Math.Floor(number.Value)) {
                    throw new JobPluginException($"main returned {first.ToPrintString()} instead of an integer status");
                }
                return (int)number.Value;
            } catch (InterpreterException e) {
                throw new JobPluginException(e.DecoratedMessage ?? e.Message, e);
            }
        }

        DynValue RunCoroutine(Script script, DynValue function, DynValue[] args) {
            var coroutine = script.CreateCoroutine(function).Coroutine;
            coroutine.AutoYieldCounter = limits.SliceInstructions;
            clock.Start();
            try {
                DynValue result = coroutine.Resume(args);
                while (coroutine.State == CoroutineState.ForceSuspended) {
                    limits.Cancellation.ThrowIfCancellationRequested();
                    slices++;
                    if (slices * limits.SliceInstructions > limits.InstructionBudget) {
                        throw new JobPluginException("the plugin exceeded its instruction budget");
                    }
                    if (clock.Elapsed > limits.TimeLimit) {
                        throw new JobPluginException($"the plugin exceeded its time limit of {limits.TimeLimit.TotalSeconds:0} s");
                    }
                    result = coroutine.Resume();
                }
                return result;
            } finally {
                clock.Stop();
            }
        }
    }
}
