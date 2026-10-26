using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenUtau.Core.JobPlugins {
    public sealed class JobPluginInfo {
        public string FilePath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string PluginVersion { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string SourceHash { get; set; } = string.Empty;
        /// <summary>Menu label; the parent folder is appended when names collide.</summary>
        public string DisplayName { get; set; } = string.Empty;
        public IReadOnlyList<string> UnsupportedApis { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> RestrictedFeatures { get; set; } = Array.Empty<string>();
        /// <summary>True when the static scan found no unsupported API or restricted facility.</summary>
        public bool FullySupported => UnsupportedApis.Count == 0 && RestrictedFeatures.Count == 0;
        public string Status => string.Join(", ", UnsupportedApis) +
            (UnsupportedApis.Count > 0 && RestrictedFeatures.Count > 0 ? "; " : "") +
            string.Join(", ", RestrictedFeatures);
    }

    public sealed class JobPluginCatalogResult {
        public List<JobPluginInfo> Plugins { get; } = new List<JobPluginInfo>();
        public List<string> Diagnostics { get; } = new List<string>();
    }

    public sealed class JobPluginProcessParameters {
        public int BeginTick { get; set; }
        public int EndTick { get; set; }
        public int SongTick { get; set; }
    }

    public sealed class JobPluginLimits {
        /// <summary>Lua instructions between cancellation and time checks.</summary>
        public int SliceInstructions { get; set; } = 20_000;
        public long InstructionBudget { get; set; } = 2_000_000_000;
        /// <summary>Script time, excluding time spent in dialogs.</summary>
        public TimeSpan TimeLimit { get; set; } = TimeSpan.FromMinutes(2);
        public int MaxSourceBytes { get; set; } = 1 << 20;
        public CancellationToken Cancellation { get; set; }
    }

    public sealed class JobPluginDialogField {
        public string Name { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        /// <summary>0 integer, 1 numeric boolean, 2 floating point, 3 text, 4 choice list.</summary>
        public int Type { get; set; }
        /// <summary>int, bool, double or string according to <see cref="Type"/>.</summary>
        public object? Value { get; set; }
        public List<string> Choices { get; } = new List<string>();
    }

    public sealed class JobPluginDialog {
        public string Title { get; set; } = string.Empty;
        public List<JobPluginDialogField> Fields { get; } = new List<JobPluginDialogField>();
    }

    public interface IJobPluginInteraction {
        /// <summary>Shows the dialog modally and updates field values. Returns false on cancel.</summary>
        bool ShowDialog(JobPluginDialog dialog);
        /// <summary>Shows a message with Win32 button flags 0 (OK) or 4 (Yes/No) and returns the Win32 result.</summary>
        int ShowMessage(string message, int flags);
    }

    public sealed class JobPluginException : Exception {
        public JobPluginException(string message) : base(message) { }
        public JobPluginException(string message, Exception inner) : base(message, inner) { }
    }

    public static class JobPluginApi {
        public const int Resolution = 480;
        public const int MessageOk = 0;
        public const int MessageYesNo = 4;
        public const int ResultOk = 1;
        public const int ResultYes = 6;
        public const int ResultNo = 7;

        static readonly string[] BaseSupportedNames = {
            "VSSeekToBeginNote", "VSGetNextNote", "VSGetNextNoteEx", "VSInsertNote", "VSInsertNoteEx",
            "VSUpdateNote", "VSUpdateNoteEx", "VSRemoveNote", "VSGetMusicalPart", "VSUpdateMusicalPart",
            "VSGetMusicalPartSinger", "VSSeekToBeginControl", "VSGetNextControl", "VSGetControlAt",
            "VSGetDefaultControlValue", "VSInsertControl", "VSUpdateControl", "VSUpdateControlAt",
            "VSRemoveControl", "VSSeekToBeginTempo", "VSGetNextTempo", "VSGetTempoAt",
            "VSSeekToBeginTimeSig", "VSGetNextTimeSig", "VSGetTimeSigAt", "VSDlgSetDialogTitle",
            "VSDlgAddField", "VSDlgDoModal", "VSDlgGetIntValue", "VSDlgGetBoolValue",
            "VSDlgGetFloatValue", "VSDlgGetStringValue", "VSMessageBox",
        };

        // Supported V4-style note-expression API, with OpenUtau host value scales.
        static readonly string[] NoteExpressionNames = {
            "VSSeekToBeginNoteExpression", "VSGetNextNoteExpression", "VSGetNoteExpressionAt",
            "VSInsertNoteExpression", "VSRemoveNoteExpression",
        };
        public static readonly string[] SupportedNames = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(BaseSupportedNames, NoteExpressionNames));
        public static readonly string[] UnsupportedNames = Array.Empty<string>();
    }
}
