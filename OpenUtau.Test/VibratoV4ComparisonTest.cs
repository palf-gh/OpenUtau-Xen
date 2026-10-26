using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    /// <summary>
    /// Opt-in comparison with V4. Set OPENUTAU_XEN_V4_VSQX to a project made by the vibrato
    /// measurement plugin (24 two-bar notes: types 1-16, then Standard 1 with flat depth 32, 64,
    /// 96, 127 and flat rate 0, 32, 96, 127) whose vibrato was converted to PIT. The report of each
    /// note's pitch error goes to OPENUTAU_XEN_V4_REPORT. Nothing from the project is stored.
    /// </summary>
    public class VibratoV4ComparisonTest {
        [Fact]
        public void PresetsFollowV4Pitch() {
            string? path = Environment.GetEnvironmentVariable("OPENUTAU_XEN_V4_VSQX");
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            string text = File.ReadAllText(path);
            double bpm = int.Parse(Regex.Match(text, @"<tempo><t>0</t><v>(\d+)</v>").Groups[1].Value) / 100.0;
            string part = Regex.Match(text, @"<vsPart>.*?</vsPart>", RegexOptions.Singleline).Value;
            var notes = Regex.Matches(part, @"<note>\s*<t>(\d+)</t>\s*<dur>(\d+)</dur>")
                .Select(m => (t: int.Parse(m.Groups[1].Value), dur: int.Parse(m.Groups[2].Value))).ToList();
            int pbs = 2;
            var pit = new List<(int t, double cents)>();
            foreach (Match m in Regex.Matches(part, @"<cc><t>(\d+)</t><v id=""([SP])"">(-?\d+)</v></cc>")) {
                int value = int.Parse(m.Groups[3].Value);
                if (m.Groups[2].Value == "S") pbs = value;
                else pit.Add((int.Parse(m.Groups[1].Value), value / 8192.0 * pbs * 100));
            }
            var project = Ustx.Create();
            project.tempos[0].bpm = bpm;
            var voicePart = new UVoicePart { trackNo = 0, Duration = notes.Max(n => n.t + n.dur) };
            project.parts.Add(voicePart);
            project.timeAxis.BuildSegments(project);
            var report = new StringBuilder("| Note | Case | RMS (cents) | Max (cents) |\n| --- | --- | --- | --- |\n");
            for (int i = 0; i < Math.Min(24, notes.Count); i++) {
                var (t0, dur) = notes[i];
                var note = project.CreateNote(60, t0, dur);
                note.vibrato.length = 100;
                note.vibrato.drift = 0;
                double seconds = project.timeAxis.MsBetweenTickPos(t0, t0 + dur) / 1000;
                string label;
                if (i < 16) {
                    VibratoPresets.Apply(note.vibrato, i + 1, seconds);
                    label = VibratoPresets.Get(i + 1)!.Name;
                } else {
                    VibratoPresets.Apply(note.vibrato, 1, seconds);
                    var normal = VibratoPresets.Get(1)!;
                    int depth = i < 20 ? new[] { 32, 64, 96, 127 }[i - 16] : 64;
                    int rate = i < 20 ? 64 : new[] { 0, 32, 96, 127 }[i - 20];
                    if (rate == 0) continue;
                    note.vibrato.depthCurve = Flat((float)normal.DepthCents(depth));
                    note.vibrato.rateCurve = Flat((float)normal.RateHz(rate));
                    label = $"Standard 1, depth {depth}, rate {rate}";
                }
                var actual = pit.Where(p => p.t >= t0 && p.t < t0 + dur).ToList();
                double sum = 0, max = 0;
                int count = 0;
                for (int tick = t0; tick < t0 + dur; tick += 5) {
                    int k = actual.FindLastIndex(p => p.t <= tick);
                    if (k < 0) continue;
                    double expected = actual[k].cents;
                    double xen = (note.vibrato.EvaluateAtTick(project, voicePart, note, tick).Y - note.AdjustedTone) * 100;
                    double error = Math.Abs(xen - expected);
                    sum += error * error;
                    max = Math.Max(max, error);
                    count++;
                }
                report.AppendLine($"| {i + 1} | {label} | {Math.Sqrt(sum / Math.Max(1, count)):0.0} | {max:0.0} |");
            }
            string output = Environment.GetEnvironmentVariable("OPENUTAU_XEN_V4_REPORT")
                ?? Path.Combine(AppContext.BaseDirectory, "vibrato-v4.md");
            File.WriteAllText(output, report.ToString(), new UTF8Encoding(false));
        }

        static List<VibratoPoint> Flat(float value) => new List<VibratoPoint> { new(0, value), new(1, value) };
    }
}
