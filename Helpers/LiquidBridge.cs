using System;
using System.Windows;
using System.Windows.Media;

namespace SqwizzeySwitch.Helpers;

/// <summary>
/// Builds a "liquid" outline: two round blobs (a, b) joined by a concave waist, like a
/// stretching water bridge that pinches off. Pure geometry — no pixel shader — so it runs
/// on the layered click-through overlay without a blur/threshold pass.
///
/// All coordinates are local to the drawing surface (DIP). <paramref name="neck"/> is the
/// waist thickness as a fraction (0..1) of the smaller blob radius: 1 ≈ fat bridge,
/// →0 the bridge thins, and ≤0 it has snapped (the two blobs are drawn separate).
/// </summary>
public static class LiquidBridge
{
    public static Geometry Build(Point a, double ra, Point b, double rb, double neck)
    {
        var g = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = g.Open())
        {
            double d = Dist(a, b);
            // Snapped, a blob drained away, or so close there's no meaningful neck → just blobs.
            if (neck <= 0.02 || ra <= 0.5 || rb <= 0.5 || d < Math.Abs(ra - rb) + 1)
            {
                if (ra > 0.5) AddCircle(ctx, a, ra);
                if (rb > 0.5) AddCircle(ctx, b, rb);
            }
            else
            {
                double th = Math.Atan2(b.Y - a.Y, b.X - a.X);
                double nx = Math.Cos(th + Math.PI / 2), ny = Math.Sin(th + Math.PI / 2); // left normal
                double dx = Math.Cos(th),                dy = Math.Sin(th);              // axis a→b

                // Attach points on each blob's "equator" (perpendicular to the axis).
                Point aTop = new(a.X + ra * nx, a.Y + ra * ny);
                Point aBot = new(a.X - ra * nx, a.Y - ra * ny);
                Point bTop = new(b.X + rb * nx, b.Y + rb * ny);
                Point bBot = new(b.X - rb * nx, b.Y - rb * ny);

                // Pinched waist at the midpoint, pulled inward by `neck`.
                Point mid = new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                double waist = Math.Min(ra, rb) * neck;
                Point wTop = new(mid.X + waist * nx, mid.Y + waist * ny);
                Point wBot = new(mid.X - waist * nx, mid.Y - waist * ny);

                double h = d * 0.22; // bezier handle length along the axis → smooth concave sides

                ctx.BeginFigure(aTop, true /*filled*/, true /*closed*/);

                // Top edge: aTop → waistTop → bTop (concave, via the pinched middle).
                ctx.BezierTo(new Point(aTop.X + dx * h, aTop.Y + dy * h),
                             new Point(wTop.X - dx * h, wTop.Y - dy * h), wTop, true, true);
                ctx.BezierTo(new Point(wTop.X + dx * h, wTop.Y + dy * h),
                             new Point(bTop.X - dx * h, bTop.Y - dy * h), bTop, true, true);

                // Around blob b's far side (away from a): semicircle bTop → bBot, bulging out.
                ctx.ArcTo(bBot, new Size(rb, rb), 0, false, SweepDirection.Counterclockwise, true, true);

                // Bottom edge back: bBot → waistBot → aBot.
                ctx.BezierTo(new Point(bBot.X - dx * h, bBot.Y - dy * h),
                             new Point(wBot.X + dx * h, wBot.Y + dy * h), wBot, true, true);
                ctx.BezierTo(new Point(wBot.X - dx * h, wBot.Y - dy * h),
                             new Point(aBot.X + dx * h, aBot.Y + dy * h), aBot, true, true);

                // Around blob a's far side: semicircle aBot → aTop, bulging out, closing the figure.
                ctx.ArcTo(aTop, new Size(ra, ra), 0, false, SweepDirection.Counterclockwise, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    private static void AddCircle(StreamGeometryContext ctx, Point c, double r)
    {
        var top = new Point(c.X, c.Y - r);
        var bot = new Point(c.X, c.Y + r);
        ctx.BeginFigure(top, true, true);
        ctx.ArcTo(bot, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);
        ctx.ArcTo(top, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);
    }

    private static double Dist(Point p, Point q)
    {
        double dx = p.X - q.X, dy = p.Y - q.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
