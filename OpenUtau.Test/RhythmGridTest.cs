using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Test {
    public class RhythmGridTest {
        [Fact]
        public void DottedQuarterResetsAtBarWithShortRemainder() {
            var grid = new RhythmGrid(4, true);
            Assert.Equal(new[] { 0, 720, 1440, 1920 }, grid.BarTicks(0, 1920, 480));
            Assert.Equal(new[] { 1920, 2640, 3360, 3840 }, grid.BarTicks(1920, 3840, 480));
        }
        [Fact]
        public void SeptupletRoundsFromOriginWithoutAccumulatedError() {
            var grid = new RhythmGrid(8, false, 7);
            Assert.Equal(new[] { 0, 137, 274, 411, 549, 686, 823, 960 }, grid.BarTicks(0, 960, 480));
            Assert.Equal(15, grid.BarTicks(0, 1920, 480).Count());
        }
        [Fact]
        public void DottedTripletIsIndependentFromBaseLength() {
            Assert.Equal(480, new RhythmGrid(4, true, 3).Step(480));
            Assert.Equal(160, new RhythmGrid(8, false, 3).Step(480));
            Assert.Equal(360, new RhythmGrid(4, false, 4).Step(480));
        }
        [Fact]
        public void TimeSignatureChangeStartsNewGrid() {
            var project = new UProject();
            project.timeSignatures.Clear();
            project.timeSignatures.Add(new UTimeSignature { barPosition = 0, beatPerBar = 4, beatUnit = 4 });
            project.timeSignatures.Add(new UTimeSignature { barPosition = 1, beatPerBar = 3, beatUnit = 4 });
            project.timeAxis.BuildSegments(project);
            var grid = new RhythmGrid(4, true);
            grid.Bounds(project.timeAxis, project.resolution, 1919, out int left, out int right);
            Assert.Equal(1440, left);
            Assert.Equal(1920, right);
            grid.Bounds(project.timeAxis, project.resolution, 1920, out left, out right);
            Assert.Equal(1920, left);
            Assert.Equal(2640, right);
            grid.Bounds(project.timeAxis, project.resolution, 3359, out left, out right);
            Assert.Equal(2640, left);
            Assert.Equal(3360, right);
        }
    }
}
