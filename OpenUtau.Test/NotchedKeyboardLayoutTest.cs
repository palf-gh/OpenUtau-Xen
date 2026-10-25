using System;
using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class NotchedKeyboardLayoutTest {
        static NotchedKeyboardLayout Layout(params NotchedKeyKind[] kinds) =>
            NotchedKeyboardLayout.Create(kinds.Select((kind, degree) => new NotchedKeyStep(degree, kind)));

        [Theory]
        [InlineData(1, new[] { 1 })]
        [InlineData(5, new[] { 5 })]
        [InlineData(6, new[] { 3, 3 })]
        [InlineData(7, new[] { 4, 3 })]
        [InlineData(11, new[] { 4, 4, 3 })]
        [InlineData(13, new[] { 5, 4, 4 })]
        [InlineData(15, new[] { 5, 5, 5 })]
        public void RunsHaveBalancedColumnsWithAtMostFiveSteps(int count, int[] sizes) {
            var layout = Layout(Enumerable.Repeat(NotchedKeyKind.Natural, count).ToArray());
            Assert.Equal(sizes, layout.Columns.Select(column => column.Degrees.Count));
            Assert.Equal(Enumerable.Range(0, count), layout.Columns.SelectMany(column => column.Degrees));
            Assert.All(layout.Columns, column => Assert.Equal(0, column.RunIndex));
            Assert.All(layout.Keys, key => Assert.Equal(0, key.RunIndex));
            foreach (var column in layout.Columns) {
                int previous = int.MaxValue;
                for (int row = 0; row < column.Degrees.Count; row++) {
                    double y = (row + 0.5) / column.Degrees.Count;
                    int degree = layout.HitTest(column.Index + 0.5, y)!.Degree;
                    Assert.True(degree < previous);
                    previous = degree;
                }
            }
        }

        [Fact]
        public void FlatAndSharpRemainSeparateRunsEvenAtTheSameDepth() {
            var layout = Layout(NotchedKeyKind.Natural, NotchedKeyKind.Flat, NotchedKeyKind.Flat,
                NotchedKeyKind.Sharp, NotchedKeyKind.Sharp, NotchedKeyKind.Natural);
            Assert.Equal(new[] { 1, 2, 2, 1 }, layout.Columns.Select(column => column.Degrees.Count));
            Assert.Equal(1.0 / 3, layout.Columns[1].Depth);
            Assert.Equal(1.0 / 3, layout.Columns[2].Depth);
        }

        [Fact]
        public void BlackOwnersStaySeparateWhileSharedAndConflictKeepTheirRoles() {
            var layout = NotchedKeyboardLayout.Create(new[] {
                new NotchedKeyStep(0, NotchedKeyKind.Natural, 0),
                new NotchedKeyStep(1, NotchedKeyKind.Natural, 1),
                new NotchedKeyStep(2, NotchedKeyKind.Sharp, 0),
                new NotchedKeyStep(3, NotchedKeyKind.Sharp, 1),
                new NotchedKeyStep(4, NotchedKeyKind.Shared, 1),
                new NotchedKeyStep(5, NotchedKeyKind.Shared, 1),
                new NotchedKeyStep(6, NotchedKeyKind.Conflict),
                new NotchedKeyStep(7, NotchedKeyKind.Flat, 2),
            });
            Assert.Equal(new[] { 2, 1, 1, 2, 1, 1 }, layout.Columns.Select(column => column.Degrees.Count));
            Assert.Equal(new[] { -1, 0, 1, 1, -1, 2 }, layout.Columns.Select(column => column.Owner));
            Assert.Equal(Enumerable.Range(0, 6), layout.Columns.Select(column => column.RunIndex));
            Assert.Equal(NotchedKeyKind.Shared, layout.Columns[3].Kind);
            Assert.Equal(1.0 / 3, layout.Columns[3].Depth);
            Assert.Equal(NotchedKeyKind.Conflict, layout.Columns[4].Kind);
            Assert.Equal(2.0 / 3, layout.Columns[4].Depth);
            Assert.Equal(1, layout.Keys.Single(key => key.Degree == 1).Owner);
        }

        [Fact]
        public void OriginalRunIdentitySurvivesWhenMatchingKindsMeetBelowNotches() {
            var layout = Layout(NotchedKeyKind.Natural, NotchedKeyKind.Between,
                NotchedKeyKind.Sharp, NotchedKeyKind.Between, NotchedKeyKind.Natural);
            var middleLeft = layout.HitTest(2.5 - 0.001, 0.5)!;
            var middleRight = layout.HitTest(2.5 + 0.001, 0.5)!;
            Assert.Equal(NotchedKeyKind.Between, middleLeft.Kind);
            Assert.Equal(middleLeft.Kind, middleRight.Kind);
            Assert.NotEqual(middleLeft.RunIndex, middleRight.RunIndex);
            var bottomLeft = layout.HitTest(2.5 - 0.001, 0.9)!;
            var bottomRight = layout.HitTest(2.5 + 0.001, 0.9)!;
            Assert.Equal(NotchedKeyKind.Natural, bottomLeft.Kind);
            Assert.Equal(bottomLeft.Kind, bottomRight.Kind);
            Assert.NotEqual(bottomLeft.RunIndex, bottomRight.RunIndex);
            Assert.Equal(layout.Columns[bottomLeft.Column].RunIndex, bottomLeft.RunIndex);
            Assert.Equal(layout.Columns[bottomRight.Column].RunIndex, bottomRight.RunIndex);
        }

        [Fact]
        public void ANotchExtensionSelectsTheNaturalKeyRatherThanTheEndedBlackKey() {
            var layout = Layout(NotchedKeyKind.Natural, NotchedKeyKind.Sharp, NotchedKeyKind.Natural);
            Assert.Equal(1, layout.HitTest(1.25, 0.2)!.Degree);
            Assert.Equal(0, layout.HitTest(1.25, 0.8)!.Degree);
            Assert.Equal(2, layout.HitTest(1.75, 0.8)!.Degree);
            var key = layout.Keys.Single(key => key.Degree == 0);
            Assert.Contains(key.Rectangles, rectangle => rectangle.Contains(1.25, 0.8));
            Assert.True(key.Rectangles.Count >= 2);
        }

        [Fact]
        public void NestedDepthsFillTheOuterRectangleWithoutOverlaps() {
            var layout = Layout(NotchedKeyKind.Natural, NotchedKeyKind.Natural, NotchedKeyKind.Between,
                NotchedKeyKind.Flat, NotchedKeyKind.Flat, NotchedKeyKind.Flat,
                NotchedKeyKind.Between, NotchedKeyKind.Between,
                NotchedKeyKind.Sharp, NotchedKeyKind.Sharp, NotchedKeyKind.Sharp,
                NotchedKeyKind.Between, NotchedKeyKind.Natural, NotchedKeyKind.Natural);
            var rectangles = layout.Keys.SelectMany(key => key.Rectangles.Select(rectangle => (key, rectangle))).ToArray();
            Assert.Equal(layout.Width, rectangles.Sum(item => item.rectangle.Width * item.rectangle.Height), 10);
            for (int i = 0; i < rectangles.Length; i++) {
                var first = rectangles[i].rectangle;
                for (int j = i + 1; j < rectangles.Length; j++) {
                    var second = rectangles[j].rectangle;
                    double width = Math.Min(first.X + first.Width, second.X + second.Width) - Math.Max(first.X, second.X);
                    double height = Math.Min(first.Y + first.Height, second.Y + second.Height) - Math.Max(first.Y, second.Y);
                    Assert.True(width <= 1e-12 || height <= 1e-12);
                }
                double x = first.X + first.Width / 2, y = first.Y + first.Height / 2;
                Assert.Same(rectangles[i].key, layout.HitTest(x, y));
            }
            for (int x = 0; x < layout.Width * 20; x++) {
                for (int y = 0; y < 100; y++) {
                    double horizontal = (x + 0.5) / 20, vertical = (y + 0.5) / 100;
                    Assert.Single(rectangles.Where(item => item.rectangle.Contains(horizontal, vertical)));
                    Assert.NotNull(layout.HitTest(horizontal, vertical));
                }
            }
        }

        [Fact]
        public void FiniteEdgesUseTheNearestSurvivingKeysAndFallbacksStillFillTheDepth() {
            var layout = Layout(NotchedKeyKind.Flat, NotchedKeyKind.Natural, NotchedKeyKind.Between);
            Assert.Equal(1, layout.HitTest(0, 1)!.Degree);
            Assert.Equal(1, layout.HitTest(layout.Width, 1)!.Degree);
            Assert.Null(layout.HitTest(-0.01, 0.5));
            Assert.Null(layout.HitTest(0.5, 1.01));
            Assert.Null(layout.HitTest(double.NaN, 0.5));
            var fallback = Layout(NotchedKeyKind.Between, NotchedKeyKind.Between);
            Assert.NotNull(fallback.HitTest(0.5, 1));
            Assert.Equal(fallback.Width, fallback.Keys.SelectMany(key => key.Rectangles).Sum(rectangle => rectangle.Width * rectangle.Height), 10);
        }

        [Fact]
        public void SharedSlabAndColumnBoundariesHaveExactlyOneOwner() {
            var layout = Layout(Enumerable.Repeat(NotchedKeyKind.Natural, 13)
                .Concat(Enumerable.Repeat(NotchedKeyKind.Between, 7))
                .Concat(Enumerable.Repeat(NotchedKeyKind.Flat, 5))
                .Concat(Enumerable.Repeat(NotchedKeyKind.Conflict, 4))
                .Concat(Enumerable.Repeat(NotchedKeyKind.Sharp, 3))
                .Concat(Enumerable.Repeat(NotchedKeyKind.Natural, 6)).ToArray());
            var rectangles = layout.Keys.SelectMany(key => key.Rectangles.Select(rectangle => (key, rectangle))).ToArray();
            var xs = rectangles.SelectMany(item => new[] { item.rectangle.X, item.rectangle.Right }).Distinct();
            var ys = rectangles.SelectMany(item => new[] { item.rectangle.Y, item.rectangle.Bottom }).Distinct();
            foreach (double x in xs) {
                foreach (double y in ys) {
                    double insideX = x == layout.Width ? Math.BitDecrement(x) : x;
                    double insideY = y == layout.Height ? Math.BitDecrement(y) : y;
                    var owner = Assert.Single(rectangles.Where(item => item.rectangle.Contains(insideX, insideY)));
                    Assert.Same(owner.key, layout.HitTest(x, y));
                }
            }
        }

        [Fact]
        public void EmptyAndInvalidInputsHaveExplicitBehaviour() {
            Assert.Empty(Layout().Keys);
            Assert.Null(Layout().HitTest(0, 0));
            Assert.Throws<ArgumentException>(() => NotchedKeyboardLayout.Create(new[] {
                new NotchedKeyStep(1, NotchedKeyKind.Natural), new NotchedKeyStep(1, NotchedKeyKind.Between),
            }));
            Assert.Throws<ArgumentException>(() => NotchedKeyboardLayout.Create(new[] {
                new NotchedKeyStep(2, NotchedKeyKind.Natural), new NotchedKeyStep(1, NotchedKeyKind.Between),
            }));
        }
    }
}
