using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.DiffSinger;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Test {
    public sealed class VoiceSmokeTheoryAttribute : TheoryAttribute {
        public VoiceSmokeTheoryAttribute() {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENUTAU_XEN_VOICE_ROOT")))
                Skip = "Set OPENUTAU_XEN_VOICE_ROOT to opt in to local licensed voice tests.";
            else if (Environment.GetEnvironmentVariable("OPENUTAU_XEN_SMOKE_ISOLATED") != "1")
                Skip = "Run tools/verify-xen-voices.ps1 in its dedicated test process.";
        }
    }
    [CollectionDefinition("Xen external voices", DisableParallelization = true)]
    public sealed class XenExternalVoiceCollection { }

    /// <summary>Opt-in real synthesis. Never edits or redistributes the user's original voicebank.</summary>
    [Collection("Xen external voices")]
    public class XenVoiceSmokeTest {
        [VoiceSmokeTheory]
        [InlineData("CLASSIC")]
        [InlineData("WORLDLINE-R")]
        [InlineData("DIFFSINGER")]
        public async Task RenderLocalVoiceAndMeasureMicrotonalPitch(string engine) {
            string preferences = Newtonsoft.Json.JsonConvert.SerializeObject(Preferences.Default);
            var previousPost = DocManager.Inst.PostOnUIThread ?? (_ => { });
            (string Name, string Directory)? staged = null;
            try {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string root = Environment.GetEnvironmentVariable("OPENUTAU_XEN_VOICE_ROOT")!;
            string output = Environment.GetEnvironmentVariable("OPENUTAU_XEN_SMOKE_OUTPUT")
                ?? Path.Combine(AppContext.BaseDirectory, "voice-smoke");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(PathManager.Inst.CachePath);
            Directory.CreateDirectory(PathManager.Inst.ResamplersPath);
            Directory.CreateDirectory(PathManager.Inst.WavtoolsPath);
            ToolsManager.Inst.SearchResamplers();
            ToolsManager.Inst.SearchWavtools();
            Preferences.Default.OnnxRunner = "CPU";
            Preferences.Default.NumRenderThreads = 1;
            Preferences.Default.DiffSingerSteps = 10;
            Preferences.Default.DiffSingerDepth = 0.2;
            DocManager.Inst.Initialize(Thread.CurrentThread, TaskScheduler.Default);
            DocManager.Inst.PostOnUIThread = _ => { };
            USinger singer = engine == "DIFFSINGER" ? LoadDiffSinger(root, output) : PrepareClassic(root, output);
            if (engine == "CLASSIC") staged = PrepareResampler();
            var results = new List<object>();
            var frequencies = new[] { 440.0, 440 * Math.Pow(2, 1.0 / 53), 550.0 };
            for (int index = 0; index < frequencies.Length; index++) {
                var project = Ustx.Create();
                project.pitchSystem = TuningPresets.All.First(t => t.Divisions == 53).Clone();
                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
                var track = project.tracks[0];
                track.Singer = singer;
                track.RendererSettings.renderer = engine;
                track.RendererSettings.Renderer = Renderers.CreateRenderer(engine);
                track.RendererSettings.Resampler = ToolsManager.Inst.GetResampler(staged?.Name);
                track.RendererSettings.resampler = track.RendererSettings.Resampler.ToString();
                track.RendererSettings.Wavtool = ToolsManager.Inst.GetWavtool(null);
                track.RendererSettings.wavtool = track.RendererSettings.Wavtool.ToString();
                var part = new UVoicePart { trackNo = 0, Duration = 1920 };
                project.parts.Add(part);
                var note = project.CreateNote(69, 0, 720);
                note.pitchAddress = PitchResolver.NearestAddress(project.pitchSystem, MusicMath.FreqToTone(frequencies[index]), true);
                note.pitch.data.Clear();
                note.ExtendedDuration = note.duration;
                note.PositionMs = 0;
                note.EndMs = project.timeAxis.TickPosToMsPos(note.End);
                note.RefreshPitch(project, track);
                part.notes.Add(note);
                string alias = engine == "DIFFSINGER" ? "en/aa" : "a";
                if (!singer.TryGetOto(alias, out _)) {
                    alias = engine == "DIFFSINGER" ? "ja/a" : "a";
                    Assert.True(singer.TryGetOto(alias, out _), "A sustained vowel must exist in the selected voice.");
                }
                var phoneme = new UPhoneme { Parent = note, phoneme = alias, rawPhoneme = alias, index = 0, position = 0 };
                part.phonemes.Add(phoneme);
                phoneme.Validate(new ValidateOptions(), project, track, part, note);
                Assert.False(phoneme.Error, phoneme.ErrorException?.ToString());
                var phrase = new RenderPhrase(project, track, part, part.phonemes);
                Assert.True(phrase.pitches.Length > 20);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var rendered = await track.RendererSettings.Renderer.Render(phrase,
                    new OpenUtau.Core.Render.Progress(100), 0, cancellation, false);
                Assert.NotNull(rendered.samples);
                Assert.True(rendered.samples.Length > 10000);
                string wav = Path.Combine(output, $"{engine}-{index}.wav");
                using (var writer = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(44100, 1)))
                    writer.WriteSamples(rendered.samples, 0, rendered.samples.Length);
                double measured = MeasureFrequency(rendered.samples, frequencies[index]);
                double error = 1200 * Math.Log2(measured / frequencies[index]);
                results.Add(new { Engine = engine, ExpectedHz = frequencies[index], MeasuredHz = measured,
                    ErrorCents = error, Source = singer.Location, Samples = rendered.samples.Length,
                    CacheHash = phrase.hash.ToString("x16"), Output = wav });
                File.WriteAllText(Path.Combine(output, engine + ".json"), JsonSerializer.Serialize(results,
                    new JsonSerializerOptions { WriteIndented = true }));
                Assert.InRange(Math.Abs(error), 0, 5);
            }
            } finally {
                Preferences.Default = Newtonsoft.Json.JsonConvert.DeserializeObject<Preferences.SerializablePreferences>(preferences)!;
                DocManager.Inst.PostOnUIThread = previousPost;
                if (staged.HasValue) {
                    File.Delete(Path.Combine(staged.Value.Directory, "resampler.exe"));
                    File.Delete(Path.Combine(staged.Value.Directory, "resampler.dll"));
                    Directory.Delete(staged.Value.Directory);
                    ToolsManager.Inst.SearchResamplers();
                }
            }
        }

        static ClassicSinger PrepareClassic(string root, string output) {
            string original = Path.Combine(root, "<classic voicebank>");
            var info = new Voicebank();
            VoicebankLoader.LoadInfo(info, Path.Combine(original, "character.txt"), root);
            var originalSinger = new ClassicSinger(info);
            originalSinger.EnsureLoaded();
            // Select a Japanese /a/ vowel and retain its measured oto parameters.
            var oto = originalSinger.Otos.First(o => o.Alias.Contains("あ", StringComparison.Ordinal)
                && File.Exists(o.File) && o.Consonant > o.Preutter && o.Cutoff < 0);
            string bank = Path.Combine(output, "isolated-classic");
            Directory.CreateDirectory(bank);
            string sample = Path.Combine(bank, "vowel.wav");
            File.Copy(oto.File, sample, true);
            string frq = VoicebankFiles.GetFrqFile(oto.File);
            if (File.Exists(frq)) File.Copy(frq, VoicebankFiles.GetFrqFile(sample), true);
            File.WriteAllText(Path.Combine(bank, "character.txt"), "name=Xen local vowel test\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(bank, "character.yaml"), "text_file_encoding: utf-8\n", Encoding.UTF8);
            string parameters = string.Join(",", new[] { oto.Offset, oto.Consonant, oto.Cutoff, oto.Preutter, oto.Overlap }
                .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
            File.WriteAllText(Path.Combine(bank, "oto.ini"), "vowel.wav=a," + parameters + "\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(output, "classic-source.json"), JsonSerializer.Serialize(new { oto.File, oto.Alias, Parameters = parameters }));
            var isolated = new Voicebank();
            VoicebankLoader.LoadInfo(isolated, Path.Combine(bank, "character.txt"), output);
            var singer = new ClassicSinger(isolated);
            singer.EnsureLoaded();
            return singer;
        }

        static (string Name, string Directory) PrepareResampler() {
            string source = Environment.GetEnvironmentVariable("OPENUTAU_XEN_UTAU_ENGINE") ?? @"<UTAU folder>";
            Directory.CreateDirectory(PathManager.Inst.ResamplersPath);
            string stage = Path.Combine(PathManager.Inst.ResamplersPath, "xen-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            foreach (var file in new[] { "resampler.exe", "resampler.dll" }) {
                string path = Path.Combine(source, file);
                Assert.True(File.Exists(path), path);
                File.Copy(path, Path.Combine(stage, file), false);
            }
            ToolsManager.Inst.SearchResamplers();
            string executable = Path.Combine(stage, "resampler.exe");
            var resampler = ToolsManager.Inst.Resamplers.Single(r => string.Equals(r.FilePath, executable, StringComparison.OrdinalIgnoreCase));
            return (resampler.ToString()!, stage);
        }

        static USinger LoadDiffSinger(string root, string output) {
            string bank = Path.Combine(root, "<diffsinger voicebank>", "configs");
            var info = new Voicebank();
            VoicebankLoader.LoadInfo(info, Path.Combine(bank, "character.txt"), root);
            Type type = typeof(USinger).Assembly.GetType("OpenUtau.Core.DiffSinger.DiffSingerSinger")!;
            var singer = (USinger)Activator.CreateInstance(type, info)!;
            string vocoder = Path.Combine(output, "vocoder");
            Directory.CreateDirectory(vocoder);
            var config = new DsVocoderConfig { model = Path.Combine(root, "<diffsinger voicebank>", "tgm_hifigan", "tgm_hifigan_v110.onnx"),
                mel_base = "e", mel_scale = "slaney", sample_rate = 44100, hop_size = 512,
                num_mel_bins = 128, mel_fmin = 40, mel_fmax = 16000 };
            File.WriteAllText(Path.Combine(vocoder, "vocoder.yaml"), Yaml.DefaultSerializer.Serialize(config));
            type.GetField("vocoder", BindingFlags.Public | BindingFlags.Instance)!.SetValue(singer, new DsVocoder(vocoder));
            return singer;
        }

        static double MeasureFrequency(float[] samples, double expected) {
            const int size = 8192;
            int start = Math.Clamp(samples.Length / 2 - size / 2, 0, samples.Length - size - 512);
            int first = Math.Max(2, (int)(44100 / (expected * Math.Pow(2, 100.0 / 1200))));
            int last = (int)Math.Ceiling(44100 / (expected / Math.Pow(2, 100.0 / 1200)));
            double Score(int lag) {
                double sum = 0, aa = 0, bb = 0;
                for (int i = 0; i < size; i++) {
                    double a = samples[start + i], b = samples[start + i + lag];
                    sum += a * b; aa += a * a; bb += b * b;
                }
                return sum / Math.Sqrt(Math.Max(1e-30, aa * bb));
            }
            int best = Enumerable.Range(first, last - first + 1).MaxBy(Score);
            double left = Score(best - 1), centre = Score(best), right = Score(best + 1);
            Assert.True(centre > 0.7, "The central region is not a stable voiced vowel.");
            double delta = 0.5 * (left - right) / (left - 2 * centre + right);
            return 44100 / (best + Math.Clamp(delta, -1, 1));
        }
    }
}
