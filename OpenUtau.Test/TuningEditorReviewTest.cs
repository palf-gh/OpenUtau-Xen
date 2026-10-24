using System;
using System.Globalization;
using System.Linq;
using OpenUtau.App;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public sealed class TuningEditorReviewTest {
        [Fact]
        public void SmallerEdoRebuildsDependentMetadataWithoutChangingSource() {
            var source = TuningPresets.EqualDivision(53);
            source.Name = "My tuning";
            source.Reference.Frequency = 432;
            source.InputMapping.IsScala = true;
            source.InputMapping.MapSize = 1;
            source.InputMapping.Mapping.Add(52);
            var rebuilt = TuningEditorStructure.Rebuild(source, 19, 53, PitchInterval.FromRatio("2/1"));
            rebuilt.Validate();
            Assert.Equal(19, rebuilt.DegreeCount);
            Assert.Equal("My tuning", rebuilt.Name);
            Assert.Equal(432, rebuilt.Reference.Frequency);
            Assert.Equal(14, rebuilt.Reference.Degree);
            Assert.False(rebuilt.InputMapping.IsScala);
            Assert.Equal(19, rebuilt.InputMapping.FormalOctave);
            Assert.All(rebuilt.Scales.SelectMany(scale => scale.Degrees), degree => Assert.InRange(degree, 0, 18));
            Assert.All(rebuilt.Notations.SelectMany(profile => profile.Names.Keys), degree => Assert.InRange(degree, 0, 18));
            Assert.All(rebuilt.Keyboard.Classes.Keys, degree => Assert.InRange(degree, 0, 18));
            Assert.Equal(53, source.DegreeCount);
            Assert.Equal(40, source.Reference.Degree);
            Assert.True(source.InputMapping.IsScala);
            Assert.Contains(source.Scales, scale => scale.Degrees.Contains(49));
        }
        [Fact]
        public void ExplicitCustomReferenceSurvivesStructuralRebuild() {
            var source = TuningPresets.EqualDivision(53);
            source.Reference.Degree = 7;
            var rebuilt = TuningEditorStructure.Rebuild(source, 19, 19, PitchInterval.FromRatio("2/1"));
            Assert.Equal(7, rebuilt.Reference.Degree);
        }
        [Fact]
        public void SmallerExplicitTuningKeepsExactIntervalsButDropsOldMappings() {
            var source = TuningPresets.All.Single(tuning => tuning.Id == "ji-5-limit");
            var rebuilt = TuningEditorStructure.Rebuild(source, 0, 7, source.Period);
            rebuilt.Validate();
            Assert.Equal(7, rebuilt.DegreeCount);
            Assert.Equal(source.Degrees.Take(7).Select(interval => interval.Ratio), rebuilt.Degrees.Select(interval => interval.Ratio));
            Assert.Empty(rebuilt.Notations);
            Assert.Single(rebuilt.Scales);
            Assert.Equal(Enumerable.Range(0, 7), rebuilt.Scales[0].Degrees);
        }
        [Theory]
        [InlineData(100, 2.208333333333333)]
        [InlineData(100.3, -1.6)]
        [InlineData(60, 0.5)]
        public void HorizontalDragNeverChangesPitchWithAnExpressiveOffset(double baseRow, double expressiveRows) {
            double initialPointer = baseRow + expressiveRows + 0.2;
            var anchor = new PitchRowDragAnchor(initialPointer, baseRow);
            Assert.Equal(0, anchor.Delta(initialPointer, baseRow));
            Assert.Equal(1, anchor.Delta(initialPointer + 1, baseRow));
            Assert.Equal(0, anchor.Delta(initialPointer + 1, Math.Round(baseRow, MidpointRounding.AwayFromZero) + 1));
        }
        [Theory]
        [InlineData("de-DE", "702,0", 702)]
        [InlineData("fr-FR", "702,25", 702.25)]
        [InlineData("de-DE", "702.25", 702.25)]
        [InlineData("en-US", "702.25", 702.25)]
        public void LocalisedDecimalsAndInvariantDecimalsKeepTheirMagnitude(string language, string text, double expected) {
            Assert.Equal(expected, XenNumberInput.Parse(text, CultureInfo.GetCultureInfo(language)), 10);
        }
        [Theory]
        [InlineData("en-US", "702,0")]
        [InlineData("en-US", "1,234")]
        [InlineData("de-DE", "1.234.567")]
        [InlineData("fr-FR", "NaN")]
        public void NumberGroupingAndNonFiniteValuesAreRejected(string language, string text) {
            Assert.Throws<FormatException>(() => XenNumberInput.Parse(text, CultureInfo.GetCultureInfo(language)));
        }
    }
}
