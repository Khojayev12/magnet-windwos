namespace MagnetWindows.Tests;

public class LayoutsTests
{
    // Odd sizes on purpose: widths and heights that do not divide by 2, 3 or 6, plus offset and negative origins
    // (a monitor left of the primary, a taskbar on top).
    public static TheoryData<Rect> Areas => new()
    {
        new Rect(0, 0, 1366, 728),
        new Rect(0, 0, 1920, 1032),
        new Rect(0, 0, 2560, 1392),
        new Rect(0, 0, 3440, 1392),
        new Rect(-1366, 40, 0, 768),
        new Rect(1920, -217, 1920 + 3440, -217 + 1439),
        new Rect(0, 0, 1921, 1081),
    };

    [Theory, MemberData(nameof(Areas))]
    public void Halves_meet_exactly_and_fill_the_area(Rect area)
    {
        var left = Layouts.Target(area, SnapAction.Left);
        var right = Layouts.Target(area, SnapAction.Right);
        var top = Layouts.Target(area, SnapAction.Top);
        var bottom = Layouts.Target(area, SnapAction.Bottom);

        Assert.Equal(area.Left, left.Left);
        Assert.Equal(left.Right, right.Left);
        Assert.Equal(area.Right, right.Right);
        Assert.All(new[] { left, right }, r => Assert.Equal((area.Top, area.Bottom), (r.Top, r.Bottom)));

        Assert.Equal(area.Top, top.Top);
        Assert.Equal(top.Bottom, bottom.Top);
        Assert.Equal(area.Bottom, bottom.Bottom);
        Assert.All(new[] { top, bottom }, r => Assert.Equal((area.Left, area.Right), (r.Left, r.Right)));
    }

    [Theory, MemberData(nameof(Areas))]
    public void Quarters_tile_the_area(Rect area)
    {
        var quarters = new[] { SnapAction.TopLeft, SnapAction.TopRight, SnapAction.BottomLeft, SnapAction.BottomRight }
            .Select(a => Layouts.Target(area, a)).ToArray();
        var (tl, tr, bl, br) = (quarters[0], quarters[1], quarters[2], quarters[3]);

        Assert.Equal((area.Left, area.Top), (tl.Left, tl.Top));
        Assert.Equal((area.Right, area.Bottom), (br.Right, br.Bottom));
        Assert.Equal(tl.Right, tr.Left);
        Assert.Equal(bl.Right, br.Left);
        Assert.Equal(tl.Bottom, bl.Top);
        Assert.Equal(tr.Bottom, br.Top);
        Assert.Equal((long)area.Width * area.Height, quarters.Sum(q => (long)q.Width * q.Height));
    }

    [Theory, MemberData(nameof(Areas))]
    public void Thirds_and_two_thirds_share_edges(Rect area)
    {
        var leftThird = Layouts.Target(area, SnapAction.LeftThird);
        var centerThird = Layouts.Target(area, SnapAction.CenterThird);
        var rightThird = Layouts.Target(area, SnapAction.RightThird);
        var leftTwo = Layouts.Target(area, SnapAction.LeftTwoThirds);
        var rightTwo = Layouts.Target(area, SnapAction.RightTwoThirds);

        Assert.Equal(area.Left, leftThird.Left);
        Assert.Equal(leftThird.Right, centerThird.Left);
        Assert.Equal(centerThird.Right, rightThird.Left);
        Assert.Equal(area.Right, rightThird.Right);
        Assert.Equal(area.Width, leftThird.Width + centerThird.Width + rightThird.Width);

        // Two-thirds pair exactly with the opposite third.
        Assert.Equal((area.Left, rightThird.Left), (leftTwo.Left, leftTwo.Right));
        Assert.Equal((leftThird.Right, area.Right), (rightTwo.Left, rightTwo.Right));
        Assert.All(new[] { leftThird, centerThird, rightThird, leftTwo, rightTwo },
            r => Assert.Equal((area.Top, area.Bottom), (r.Top, r.Bottom)));
    }

    [Theory, MemberData(nameof(Areas))]
    public void Center_two_thirds_spans_one_sixth_to_five_sixths(Rect area)
    {
        var r = Layouts.Target(area, SnapAction.CenterTwoThirds);

        Assert.Equal(area.Left + area.Width / 6, r.Left);
        Assert.Equal(area.Left + area.Width * 5 / 6, r.Right);
        Assert.Equal((area.Top, area.Bottom), (r.Top, r.Bottom));
        Assert.InRange(r.Left - area.Left - (area.Right - r.Right), -1, 1); // centered within a pixel
    }

    [Theory, MemberData(nameof(Areas))]
    public void Maximize_is_the_whole_work_area(Rect area) =>
        Assert.Equal(area, Layouts.Target(area, SnapAction.Maximize));

    [Theory, MemberData(nameof(Areas))]
    public void Every_fixed_layout_stays_inside_the_area(Rect area)
    {
        foreach (var action in Layouts.FixedLayouts)
        {
            var r = Layouts.Target(area, action);
            Assert.True(r.Width > 0 && r.Height > 0, $"{action} is empty");
            Assert.True(r.Left >= area.Left && r.Top >= area.Top && r.Right <= area.Right && r.Bottom <= area.Bottom, $"{action} leaves the area");
        }
    }

    [Theory]
    [InlineData(SnapAction.NextDisplay)]
    [InlineData(SnapAction.PreviousDisplay)]
    [InlineData(SnapAction.Center)]
    [InlineData(SnapAction.Restore)]
    public void Window_dependent_actions_are_not_fixed_layouts(SnapAction action) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Layouts.Target(new Rect(0, 0, 1920, 1080), action));

    [Fact]
    public void Center_keeps_size_and_centers()
    {
        var area = new Rect(0, 0, 1920, 1032);
        var r = Layouts.Center(area, new Rect(10, 20, 810, 620));

        Assert.Equal((800, 600), (r.Width, r.Height));
        Assert.Equal((560, 216), (r.Left, r.Top));
    }

    [Fact]
    public void Center_shrinks_a_window_larger_than_the_area()
    {
        var area = new Rect(-1366, 40, 0, 768);
        var r = Layouts.Center(area, new Rect(0, 0, 2000, 500));

        Assert.Equal(new Rect(-1366, 154, 0, 654), r);
    }

    [Theory]
    [InlineData(SnapAction.Left)]
    [InlineData(SnapAction.RightThird)]
    [InlineData(SnapAction.CenterTwoThirds)]
    [InlineData(SnapAction.BottomRight)]
    [InlineData(SnapAction.Maximize)]
    public void Moving_a_snapped_window_keeps_its_exact_layout(SnapAction action)
    {
        var from = new Rect(0, 0, 2561, 1392);
        var to = new Rect(2561, 0, 2561 + 1366, 728);

        var moved = Layouts.MoveToArea(Layouts.Target(from, action), from, to);

        Assert.Equal(Layouts.Target(to, action), moved);
    }

    [Fact]
    public void Moving_a_free_window_scales_its_relative_position_and_size()
    {
        var from = new Rect(0, 0, 1920, 1080);
        var to = new Rect(1920, 0, 1920 + 3840, 2160);

        var moved = Layouts.MoveToArea(new Rect(480, 270, 1440, 810), from, to);

        Assert.Equal(new Rect(1920 + 960, 540, 1920 + 2880, 1620), moved);
    }

    // Telegram on a 3440x1392 work area: it will not go below 758 px tall, so a half (696 px) cannot hold it.
    static readonly Rect Work = new(0, 0, 3440, 1392);

    [Fact]
    public void Oversized_bottom_half_is_anchored_to_the_taskbar_edge()
    {
        var target = Layouts.Target(Work, SnapAction.Bottom);
        var actual = new Rect(0, 696, 3440, 696 + 758);

        Assert.Equal(new Rect(0, 1392 - 758, 3440, 1392), Layouts.Settle(target, actual, Work));
    }

    [Fact]
    public void Oversized_top_half_stays_at_the_top()
    {
        var target = Layouts.Target(Work, SnapAction.TopRight);
        var actual = new Rect(1720, 0, 3440, 758);

        Assert.Equal(actual, Layouts.Settle(target, actual, Work));
    }

    [Fact]
    public void Oversized_right_third_is_anchored_to_the_right_edge()
    {
        var target = Layouts.Target(Work, SnapAction.RightThird);
        var actual = new Rect(target.Left, 0, target.Left + 1300, 1392);

        Assert.Equal(new Rect(3440 - 1300, 0, 3440, 1392), Layouts.Settle(target, actual, Work));
    }

    [Fact]
    public void Oversized_center_third_stays_centered_on_its_slot()
    {
        var target = Layouts.Target(Work, SnapAction.CenterThird);
        var actual = new Rect(target.Left, 0, target.Left + 1347, 1392);

        var settled = Layouts.Settle(target, actual, Work);

        Assert.Equal(1347, settled.Width);
        Assert.Equal(target.Left - 100, settled.Left);
    }

    [Fact]
    public void Window_taller_than_the_work_area_starts_at_its_top()
    {
        var target = Layouts.Target(Work, SnapAction.Left);
        var actual = new Rect(0, 0, 1720, 1500);

        Assert.Equal(new Rect(0, 0, 1720, 1500), Layouts.Settle(target, actual, Work));
    }

    [Fact]
    public void Window_that_fits_lands_on_the_target_corner()
    {
        var target = Layouts.Target(Work, SnapAction.BottomRight);
        var nudged = new Rect(target.Left + 7, target.Top, target.Right + 7, target.Bottom - 7);

        var settled = Layouts.Settle(target, nudged, Work);

        Assert.Equal((target.Left, target.Top), (settled.Left, settled.Top));
    }

    [Fact]
    public void Moving_keeps_a_window_inside_the_destination()
    {
        var from = new Rect(0, 0, 1920, 1080);
        var to = new Rect(-1366, 0, 0, 728);

        // Hangs off the right edge of the source monitor.
        var moved = Layouts.MoveToArea(new Rect(1500, 900, 2300, 1300), from, to);

        Assert.True(moved.Left >= to.Left && moved.Right <= to.Right && moved.Top >= to.Top && moved.Bottom <= to.Bottom);
    }
}
