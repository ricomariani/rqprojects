// Copyright (c) 2007-2018 Rico Mariani
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for GameMap.xaml
    /// </summary>
    public partial class GameMap : UserControl
    {
        class PathPoint
        {
            public Path path;
            public PathFigure fig;
            public LineSegment lineseg;
            public PolyLineSegment polyseg;
            public BezierSegment bezseg;
            public PolyBezierSegment polybezseg;

            public int ipoint;

            public Point v;
        }

        static double RoundScale(double x)
        {
            return Math.Floor(x * 100) / 100;
        }

        static SnapAction HardSnapCoordinate(ref double x)
        {
            double minor = 40.0 / 6.0;

            x = RoundCoordinate(Math.Round(x / minor) * minor);

            return SnapAction.Coordinate1;
        }

        static double HardSnapCoordinate(double x)
        {
            double minor = 40.0/6.0;

            return RoundCoordinate(Math.Round(x / minor) * minor);
        }

        static double RoundCoordinate(double x)
        {
            return Math.Floor(x * 4) / 4;
        }

        static double DistanceSquared(Point p1, Point p2)
        {
            return DistanceSquared(p1.X, p2.X, p1.Y, p2.Y);
        }

        static double DistanceSquared(double x1, double x2, double y1, double y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
        }

        Thickness RoundAndSnapMargin(double l, double t, double r, double b)
        {
            l = HardSnapCoordinate(l);
            t = HardSnapCoordinate(t);
            r = HardSnapCoordinate(r);
            b = HardSnapCoordinate(b);

            return new Thickness(l, t, r, b);
        }

        static Thickness RoundMargin(double l, double t, double r, double b)
        {
            l = Math.Floor(l * 4) / 4;
            t = Math.Floor(t * 4) / 4;
            r = Math.Floor(r * 4) / 4;
            b = Math.Floor(b * 4) / 4;

            return new Thickness(l, t, r, b);
        }

        static void MovePath(Path p, Point p1)
        {
            PathFigure pf;

            if (!TryExtractPathFigure(p, out pf))
                return;

            double dx = p1.X - pf.StartPoint.X;
            double dy = p1.Y - pf.StartPoint.Y;

            MoveElement(p, dx, dy);
        }

        static void MoveLineNoSnap(Line l, Point p1)
        {
            double dx = l.X2 - l.X1;
            double dy = l.Y2 - l.Y1;
            l.X1 = p1.X;
            l.Y1 = p1.Y;
            l.X2 = l.X1 + dx;
            l.Y2 = l.Y1 + dy;
        }

        static Point ReferencePoint(FrameworkElement el)
        {
            Line l = el as Line;
            Path p = el as Path;

            if (p != null)
            {
                ArcSegment arc;
                PathFigure pf;

                if (TryExtractArc(p, out pf, out arc))
                {
                    return pf.StartPoint;
                }
                else
                {
                    foreach (var pp in EnumeratePathPoints(p))
                    {
                        return pp.v; // return the first
                    }

                    return new Point(0, 0);
                }
            }
            else if (l != null)
            {
                return new Point(l.X1, l.Y1);
            }
            else
            {
                return new Point(el.Margin.Left, el.Margin.Top);
            }
        }        
        
        static void MoveElement(FrameworkElement el, double dx, double dy)
        {
            if (dx == 0 && dy == 0)
                return;

            Line l = el as Line;
            Path p = el as Path;

            if (p != null)
            {
                // we'll use this a lot in the path calculations
                Vector delta = new Vector(dx, dy);

                ArcSegment seg;
                PathFigure pf;
                if (TryExtractArc(p, out pf, out seg))
                {
                    // moving an arc, apply the delta to the start and end point
                    pf.StartPoint = RoundPoint(pf.StartPoint + delta);
                    seg.Point = RoundPoint(seg.Point + delta);
                }
                else
                {
                    // moving a path, apply the delta to every point
                    foreach (var pp in EnumeratePathPoints(p))
                    {
                        Point point = RoundPoint(pp.v + delta); 

                        if (pp.lineseg != null)
                        {
                            pp.lineseg.Point = point;
                        }
                        else if (pp.polyseg != null)
                        {
                            pp.polyseg.Points[pp.ipoint] = point;
                        }
                        else if (pp.bezseg != null)
                        {
                            var bezseg = pp.bezseg;
                            bezseg.Point1 = RoundPoint(bezseg.Point1 + delta);
                            bezseg.Point2 = RoundPoint(bezseg.Point2 + delta);
                            bezseg.Point3 = point;
                        }
                        else if (pp.polybezseg != null)
                        {
                            var polybezseg = pp.polybezseg;
                            polybezseg.Points[pp.ipoint - 2] = RoundPoint(polybezseg.Points[pp.ipoint - 2] + delta);
                            polybezseg.Points[pp.ipoint - 1] = RoundPoint(polybezseg.Points[pp.ipoint - 1] + delta);
                            polybezseg.Points[pp.ipoint] = point;
                        }
                        else
                        {
                            pp.fig.StartPoint = point;
                        }
                    }
                }
            }
            else if (l != null)
            {
                // moving a line, apply the delta to the ends, but there is special treatment for rounding lines to the nearest integer if moving by 1 pixel
                if (dx == 1)
                {
                    l.X1 = Math.Floor(l.X1);
                    l.X2 = Math.Floor(l.X2);
                }

                l.X1 = RoundCoordinate(l.X1 + dx);
                l.X2 = RoundCoordinate(l.X2 + dx);

                if (dy == 1)
                {
                    l.Y1 = Math.Floor(l.Y1);
                    l.Y2 = Math.Floor(l.Y2);
                }

                l.Y1 = RoundCoordinate(l.Y1 + dy);
                l.Y2 = RoundCoordinate(l.Y2 + dy);
            }
            else
            {
                // moving elements via margin, but special treatment to the nearest integer if moving by 1 pixel
                double leftNew = el.Margin.Left + dx;
                double topNew = el.Margin.Top + dy;

                if (dx == 1)
                    leftNew = Math.Floor(leftNew);

                if (dy == 1)
                    topNew = Math.Floor(topNew);

                el.Margin = RoundMargin(leftNew, topNew, 0, 0);
            }
        }

        static bool TryExtractPathFigure(Path p, out PathFigure pf)
        {
            pf = null;

            var g = p.Data;

            if (g == null)
                return false;

            var pg = g as PathGeometry;

            if (pg == null || pg.Figures.Count == 0)
                return false;

            pf = pg.Figures[0];

            return true;
        }

        static bool TryExtractArc(Path p, out PathFigure pf, out ArcSegment arc)
        {
            pf = null;
            arc = null;

            if (!TryExtractPathFigure(p, out pf))
                return false;

            if (pf.Segments == null || pf.Segments.Count == 0)
                return false;

            var segment0 = pf.Segments[0];

            if (segment0 == null)
                return false;

            arc = segment0 as ArcSegment;

            if (arc == null)
                return false;

            return true;
        }

        static bool TryExtractPathEndPoint(PathFigure pf, out double x, out double y)
        {
            x = 0;
            y = 0;

            if (pf == null)
                return false;

            int c = pf.Segments.Count;
            PathSegment ps = pf.Segments[c - 1];

            return TryExtractSegmentEnd(ps, out x, out y);
        }

        static bool TryExtractSegmentEnd(PathSegment ps, out double x, out double y)
        {
            x = 0;
            y = 0;

            if (ps == null)
                return false;

            if (ps is LineSegment)
            {
                LineSegment ls = ps as LineSegment;
                x = ls.Point.X;
                y = ls.Point.Y;
                return true;
            }
            else if (ps is PolyBezierSegment)
            {
                PolyBezierSegment ls = ps as PolyBezierSegment;
                if (ls.Points.Count == 0)
                    return false;

                int last = ls.Points.Count - 1;
                x = ls.Points[last].X;
                y = ls.Points[last].Y;
                return true;
            }
            else if (ps is PolyLineSegment)
            {
                PolyLineSegment ls = ps as PolyLineSegment;
                if (ls.Points.Count == 0)
                    return false;

                int last = ls.Points.Count - 1;
                x = ls.Points[last].X;
                y = ls.Points[last].Y;
                return true;
            }
            else if (ps is BezierSegment)
            {
                BezierSegment bs = ps as BezierSegment;
                x = bs.Point3.X;
                y = bs.Point3.Y;
                return true;
            }

            return false;
        }

        static Point LocateArcPeak(PathFigure pf, ArcSegment arc)
        {
            var r_rev = new RotateTransform(-arc.RotationAngle);
            var r_fwd = new RotateTransform(arc.RotationAngle);

            Point p_org = new Point(arc.Point.X - pf.StartPoint.X, arc.Point.Y - pf.StartPoint.Y);
            Point p = r_rev.Transform(p_org);

            var r = (-p.X) / 2 / arc.Size.Width;
            if (r < -1) r = -1;
            if (r > 1) r = 1;
            var t1 = Math.Acos(r);
            while (t1 > 0) t1 -= 2 * Math.PI;

            var theta = -t1 - Math.PI / 2;
            var theta_degrees = theta / Math.PI / 2 * 360;
            var t1_degrees = t1 / Math.PI / 2 * 360;
            var y_center = arc.Size.Height * Math.Sin(t1);
            var x_center = p.X / 2;

            if (arc.IsLargeArc) y_center = -y_center;

            Point zenith = new Point(x_center, y_center - arc.Size.Height);
            Point p3 = r_fwd.Transform(zenith);

            return new Point(p3.X + pf.StartPoint.X, p3.Y + pf.StartPoint.Y);
        }

        static Point LocateArcCenter(PathFigure pf, ArcSegment arc)
        {
            var r_rev = new RotateTransform(-arc.RotationAngle);
            var r_fwd = new RotateTransform(arc.RotationAngle);

            Point p_org = new Point(arc.Point.X - pf.StartPoint.X, arc.Point.Y - pf.StartPoint.Y);
            Point p = r_rev.Transform(p_org);

            var r = (-p.X) / 2 / arc.Size.Width;
            if (r < -1) r = -1;
            if (r > 1) r = 1;
            var t1 = Math.Acos(r);
            while (t1 > 0) t1 -= 2 * Math.PI;

            var theta = -t1 - Math.PI / 2;
            var theta_degrees = theta / Math.PI / 2 * 360;
            var y_center = arc.Size.Height * Math.Sin(t1);
            var x_center = p.X / 2;

            if (arc.IsLargeArc) y_center = -y_center;

            Point center = new Point(x_center, y_center);
            Point p3 = r_fwd.Transform(center);

            return new Point(p3.X + pf.StartPoint.X, p3.Y + pf.StartPoint.Y);
        }

        static void ComputeBoundingRect(FrameworkElement elBase, Rectangle boundingRect)
        {
            boundingRect.HorizontalAlignment = HorizontalAlignment.Left;
            boundingRect.VerticalAlignment = VerticalAlignment.Top;
            boundingRect.IsHitTestVisible = false;

            if (elBase is Path)
            {
                Path p = elBase as Path;

                ArcSegment seg;
                PathFigure pf;

                if (TryExtractArc(p, out pf, out seg))
                {
                    ComputeArcBoundingBox(pf, seg, boundingRect, p);
                }
                else
                {
                    ComputePathBoundingRect(p, boundingRect);
                }
            }
            else if (elBase is Line)
            {
                ComputeLineBoundingRect(elBase as Line, boundingRect);
            }
            else
            {
                ComputeGenericBoundingRect(elBase, boundingRect);
            }

            if (elBase.LayoutTransform != null && elBase.LayoutTransform != Transform.Identity)
            {
                double scaleX, scaleY, angle;
                ExtractScaleAndRotate(elBase.LayoutTransform, out scaleX, out scaleY, out angle);

                boundingRect.Width *= scaleX;
                boundingRect.Height *= scaleY;

                if (angle != 0)
                    boundingRect.LayoutTransform = new RotateTransform(angle);
                else
                    boundingRect.LayoutTransform = Transform.Identity;
            }
        }

        static void ComputeGenericBoundingRect(FrameworkElement elBase, Rectangle boundingRect)
        {
            double th = 1;

            boundingRect.Margin = RoundMargin(elBase.Margin.Left - th, elBase.Margin.Top - th, 0, 0);

            if (elBase.ActualHeight == 0 && elBase.ActualWidth == 0)
            {
                boundingRect.Width = elBase.Width + th * 2;
                boundingRect.Height = elBase.Height + th * 2;
            }
            else
            {
                boundingRect.Width = elBase.ActualWidth + th * 2;
                boundingRect.Height = elBase.ActualHeight + th * 2;
            }
        }

        static void ComputeLineBoundingRect(Line l, Rectangle boundingRect)
        {
            double x1 = Math.Min(l.X1, l.X2);
            double x2 = Math.Max(l.X1, l.X2);
            double y1 = Math.Min(l.Y1, l.Y2);
            double y2 = Math.Max(l.Y1, l.Y2);

            double th = l.StrokeThickness;

            boundingRect.Margin = RoundMargin(x1 - th, y1 - th, 0, 0);
            boundingRect.Width = x2 - x1 + 2 * th;
            boundingRect.Height = y2 - y1 + 2 * th;
        }

        static void ComputePathBoundingRect(Path p, Rectangle boundingRect)
        {
            var r = VisualTreeHelper.GetContentBounds(p);

            if (Double.IsInfinity(r.Width)|| Double.IsInfinity(r.Height))
            {
                ComputePathBoundingRectTheHardWay(p, boundingRect);
                return;
            }

            double th = p.StrokeThickness;

            boundingRect.Margin = new Thickness(r.Left - th, r.Top - th, 0, 0);
            boundingRect.Width = r.Width + 2 * th;
            boundingRect.Height = r.Height + 2 * th;
        }

        static void ComputePathBoundingRectTheHardWay(Path p, Rectangle boundingRect)
        {
            double xMin, yMin, xMax, yMax;
            xMin = yMin = double.PositiveInfinity;
            xMax = yMax = double.NegativeInfinity;

            foreach (var pp in EnumeratePathPoints(p))
            {
                xMax = Math.Max(xMax, pp.v.X);
                yMax = Math.Max(yMax, pp.v.Y);
                xMin = Math.Min(xMin, pp.v.X);
                yMin = Math.Min(yMin, pp.v.Y);
            }

            double th = p.StrokeThickness;

            boundingRect.Margin = RoundMargin(xMin - th, yMin - th, 0, 0);
            boundingRect.Width = xMax - xMin + 2 * th;
            boundingRect.Height = yMax - yMin + 2 * th;
        }


        static void ComputeArcBoundingBox(PathFigure pf, ArcSegment arc, Rectangle rect, Path path)
        {
            var r_rev = new RotateTransform(-arc.RotationAngle);
            var r_fwd = new RotateTransform(arc.RotationAngle);

            Point p_org = new Point(arc.Point.X - pf.StartPoint.X, arc.Point.Y - pf.StartPoint.Y);
            Point p = r_rev.Transform(p_org);

            var r = (-p.X) / 2 / arc.Size.Width;
            if (r < -1) r = -1;
            if (r > 1) r = 1;
            var t1 = Math.Acos(r);
            while (t1 > 0) t1 -= 2 * Math.PI;

            var theta = -t1 - Math.PI / 2;
            var theta_degrees = theta / Math.PI / 2 * 360;
            var y_center = arc.Size.Height * Math.Sin(t1);
            var x_center = p.X / 2;

            if (arc.IsLargeArc) y_center = -y_center;

            var corner = new Point(p.X, y_center - arc.Size.Height);
            double delta = 0;

            rect.Height = Math.Abs(y_center - arc.Size.Height);
            rect.Width = p.X;

            if (arc.IsLargeArc) 
                delta = (arc.Size.Width * 2 - p.X) / 2;
            
            double th = path.StrokeThickness;

            Point p0 = r_fwd.Transform(new Point(-th - delta, th));
            Point p1 = r_fwd.Transform(new Point(rect.Width + th + delta, th));
            Point p2 = r_fwd.Transform(new Point(rect.Width + th + delta, -rect.Height - th));
            Point p3 = r_fwd.Transform(new Point(-th - delta, -rect.Height - th));

            double xMin = Math.Min(Math.Min(Math.Min(p0.X, p1.X), p2.X), p3.X);
            double yMin = Math.Min(Math.Min(Math.Min(p0.Y, p1.Y), p2.Y), p3.Y);

            rect.Width += th*2 + 2*delta;
            rect.Height += th*2;

            rect.Margin = new Thickness(pf.StartPoint.X + xMin, pf.StartPoint.Y + yMin, 0, 0);

            rect.LayoutTransform = r_fwd;
        }

        static IEnumerable<PathPoint> EnumeratePathPoints(Path p)
        {
            PathGeometry pg = p.Data as PathGeometry;

            if (pg == null)
                yield break;

            foreach (PathFigure pf in pg.Figures)
            {
                yield return new PathPoint { path = p, fig = pf, v = pf.StartPoint };

                foreach (PathSegment seg in pf.Segments)
                {
                    if (seg is LineSegment)
                    {
                        LineSegment ls = seg as LineSegment;
                        yield return new PathPoint { path = p, fig = pf, lineseg = ls, v = ls.Point };
                    }
                    else if (seg is PolyLineSegment)
                    {
                        PolyLineSegment polyseg = seg as PolyLineSegment;

                        for (int i = 0; i < polyseg.Points.Count; i++)
                        {
                            yield return new PathPoint { path = p, fig = pf, polyseg = polyseg, v = polyseg.Points[i], ipoint = i };
                        }
                    }
                    else if (seg is BezierSegment)
                    {
                        BezierSegment bs = seg as BezierSegment;
                        yield return new PathPoint { path = p, fig = pf, bezseg = bs, v = bs.Point3 };
                    }
                    else if (seg is PolyBezierSegment)
                    {
                        PolyBezierSegment polyseg = seg as PolyBezierSegment;

                        for (int i = 2; i < polyseg.Points.Count; i += 3)
                        {
                            yield return new PathPoint { path = p, fig = pf, polybezseg = polyseg, v = polyseg.Points[i], ipoint = i };
                        }
                    }
                }
            }
        }

        static bool IsShiftDown()
        {
            return Keyboard.IsKeyDown(Key.RightShift) || Keyboard.IsKeyDown(Key.LeftShift);
        }

        static bool IsCtrlDown()
        {
            return Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftCtrl);
        }

        static bool IsAltDown()
        {
            return Keyboard.IsKeyDown(Key.RightAlt) || Keyboard.IsKeyDown(Key.LeftAlt);
        }

        internal void AnimateElementTopLeft(FrameworkElement el, double left, double top)
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(el, new NameScope());

            var ms = new MarginSurrogate(el);

            // Register the name with the page to which the element belongs.
            el.RegisterName("surrogate", ms);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(500));

            if (el.Margin.Top != top)
                Anim2Point(story, dur, "surrogate", MarginSurrogate.TopProperty, el.Margin.Top, top);

            if (el.Margin.Left != left)
                Anim2Point(story, dur, "surrogate", MarginSurrogate.LeftProperty, el.Margin.Left, left);

            story.Begin(el);
        }

        internal void AnimateElementLocation(FrameworkElement el, Thickness newLocation)
        {
            // don't take updates until the change is committed, the animation lasts 500ms
            waitUpdates = DateTime.Now.AddMilliseconds(750);

            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(el, new NameScope());

            var ms = new MarginSurrogate(el);

            // Register the name with the page to which the element belongs.
            el.RegisterName("surrogate", ms);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(500));

            if (el.Margin.Top != newLocation.Top)
                Anim2Point(story, dur, "surrogate", MarginSurrogate.TopProperty, el.Margin.Top, newLocation.Top);

            if (el.Margin.Left != newLocation.Left)
                Anim2Point(story, dur, "surrogate", MarginSurrogate.LeftProperty, el.Margin.Left, newLocation.Left);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    // the end is ragged, it's not accurate to get the bounding box in position at this time
                    // I have to wait for at least one paint to settle so I just wait 100ms and then grab the stuff
                    // this is really pretty terrible because Story should be done now...  if I don't do this
                    // then the bounding box doesn't match and worse yet the save can happen before it's really done!
                    Main.DelayAction(100, 
                        () =>
                        {
                            SaveFrameworkElement(el);

                            if (selectedList == null)
                            {
                                selectedList = new List<FrameworkElement>();
                            }

                            if (selectedListBoxes == null)
                            {
                                selectedListBoxes = new List<FrameworkElement>();
                            }

                            selectedList.Add(el);
                            selectedListBoxes.Add(AddBoundingRectangleForElement(el, animate: true));
                        });
                });

            story.Begin(el);
        }

        static bool IsUnmoveable(FrameworkElement el)
        {
            if (el == null)
                return true;

            if (el.Tag == null)
                return false;

            string s = el.Tag as string;

            if (s == null)
                return false;

            char c = s[s.Length - 1];
            return (c % 2) == 1;
        }

        Shape CreateHandleRectMisc(Point pt)
        {
            Rectangle r = new Rectangle();
            r.Fill = new RadialGradientBrush(Colors.Black, Colors.LightGray);
            r.Stroke = new SolidColorBrush(Colors.Black);

            PlaceHandleObject(pt, r);
            return r;
        }
        
        Shape CreateHandleRect(Point pt, string tag)
        {
            Rectangle r = new Rectangle();
            r.Tag = tag;
            r.Fill = new RadialGradientBrush(Colors.Black, Colors.LightGray);
            r.Stroke = new SolidColorBrush(Colors.Black);

            PlaceHandleObject(pt, r);
            AddHandle(tag, r);

            return r;
        }

        Shape CreateHandleTriangle(Point pt, string tag)
        {
            Polygon p = new Polygon();
            p.Tag = tag;
            p.Fill = new RadialGradientBrush(Colors.Black, Colors.White);
            p.Stroke = new SolidColorBrush(Colors.Black);

            p.Points.Add(new Point(-6, 3));
            p.Points.Add(new Point(0, -6));
            p.Points.Add(new Point(6, 3));

            PlacePolygonHandle(pt, p);
            AddHandle(tag, p);

            return p;
        }

        Shape CreateHandlePlus(Point pt, string tag)
        {
            Polygon p = new Polygon();
            p.Tag = tag;
            p.Fill = new RadialGradientBrush(Colors.Black, Colors.White);
            p.Stroke = new SolidColorBrush(Colors.Black);

            p.Points.Add(new Point(-5.0, +1.5));
            p.Points.Add(new Point(-1.5, +1.5));
            p.Points.Add(new Point(-1.5, +5.0));
            p.Points.Add(new Point(+1.5, +5.0));
            p.Points.Add(new Point(+1.5, +1.5));
            p.Points.Add(new Point(+5.0, +1.5));
            p.Points.Add(new Point(+5.0, -1.5));
            p.Points.Add(new Point(+1.5, -1.5));
            p.Points.Add(new Point(+1.5, -5.0));
            p.Points.Add(new Point(-1.5, -5.0));
            p.Points.Add(new Point(-1.5, -1.5));
            p.Points.Add(new Point(-5.0, -1.5));

            PlacePolygonHandle(pt, p);
            AddHandle(tag, p);

            return p;
        }

        Shape CreateHandleCross(Point pt, string tag)
        {
            Polygon p = new Polygon();
            p.Tag = tag;
            p.Fill = new RadialGradientBrush(Colors.Black, Colors.White);
            p.Stroke = new SolidColorBrush(Colors.Black);

            p.Points.Add(new Point(-3, -1));
            p.Points.Add(new Point(-5, -3));
            p.Points.Add(new Point(-3, -5));
            p.Points.Add(new Point(-1, -3));
            p.Points.Add(new Point(+1, -5));
            p.Points.Add(new Point(+3, -3));
            p.Points.Add(new Point(+1, -1));
            p.Points.Add(new Point(+3, +1));
            p.Points.Add(new Point(+1, +3));
            p.Points.Add(new Point(-1, +1));
            p.Points.Add(new Point(-3, 3));
            p.Points.Add(new Point(-5, 1));

            PlacePolygonHandle(pt, p);
            AddHandle(tag, p);

            return p;
        }

        Shape CreateHandleHollow(Point pt, string tag)
        {
            Ellipse el = new Ellipse();
            el.Tag = tag;
            el.Fill = new SolidColorBrush(Colors.Transparent);
            el.Stroke = new SolidColorBrush(Colors.Green);

            PlaceHollowObject(pt, el);
            AddHandle(tag, el);
            return el;
        }

        void PlaceHollowObject(Point pt, FrameworkElement el)
        {

            var handlesize = 3 * HandleSize / Math.Sqrt(GetCurrentZoom());
            Thickness margin = RoundMargin(pt.X - handlesize / 2, pt.Y - handlesize / 2, 0, 0);
            el.HorizontalAlignment = HorizontalAlignment.Left;
            el.VerticalAlignment = VerticalAlignment.Top;
            el.Width = handlesize;
            el.Height = handlesize;
            el.Margin = margin;
            canvas.Children.Add(el);

            AnimateAppearance(el, false);
        }
        
        Shape CreateHandleCircle(Point pt, string tag)
        {
            Ellipse el = new Ellipse();
            el.Tag = tag;
            el.Fill = new RadialGradientBrush(Colors.Black, Colors.White);
            el.Stroke = new SolidColorBrush(Colors.Black);

            PlaceHandleObject(pt, el);
            AddHandle(tag, el);
            return el;
        }

        Shape CreateHandleSine(Point pt, string tag)
        {
            const int thickness = 3;
            const int width = 5;
            const int control_strength = 10;

            Path path = new Path();
            PathGeometry pg = new PathGeometry();
            PathFigure pf = new PathFigure();
            pf.StartPoint = new Point(-width, 0);
            var seg = new PolyBezierSegment();
            pf.Segments.Add(seg);
            pg.Figures.Add(pf);
            pg.FillRule = FillRule.EvenOdd;
            path.Data = pg;
            path.Tag = tag;

            seg.Points.Add(new Point(0, control_strength));
            seg.Points.Add(new Point(0, -control_strength));
            seg.Points.Add(new Point(width, 0));

            seg.IsStroked = true;
            pf.IsClosed = false;
            pf.IsFilled = false;

            path.Stroke = new SolidColorBrush(Colors.Black);
            path.StrokeThickness = thickness;

            PlacePolygonHandle(pt, path);
            AddHandle(tag, path);

            return path;
        }

        void PlacePolygonHandle(Point pt, Shape el)
        {
            Thickness margin = RoundMargin(pt.X, pt.Y, 0, 0);
            el.HorizontalAlignment = HorizontalAlignment.Left;
            el.VerticalAlignment = VerticalAlignment.Top;
            el.Margin = margin;
            canvas.Children.Add(el);

            double scale = Math.Sqrt(1.0 / GetCurrentZoom());

            el.LayoutTransform = new ScaleTransform(scale, scale);

            AnimatePolygonAppearance(el);
        }

        void PlaceHandleObject(Point pt, Shape el)
        {
            double handlesize = HandleSize / Math.Sqrt(GetCurrentZoom());
            Thickness margin = RoundMargin(pt.X - handlesize / 2, pt.Y - handlesize / 2, 0, 0);
            el.HorizontalAlignment = HorizontalAlignment.Left;
            el.VerticalAlignment = VerticalAlignment.Top;
            el.Width = handlesize;
            el.Height = handlesize;
            el.Margin = margin; 
            
            canvas.Children.Add(el);
            AnimateAppearance(el, false);
        }

        public static void Anim3Point(Storyboard story, double z1, double z2, double z3, ref KeyTime kt0, ref KeyTime kt1, ref KeyTime kt2, string name, DependencyProperty property)
        {
            DoubleAnimationUsingKeyFrames anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(z1, kt0));
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(z2, kt1));
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(z3, kt2));
            Storyboard.SetTargetName(anim, name);
            Storyboard.SetTargetProperty(anim, new PropertyPath(property));
            story.Children.Add(anim);
        }

        public static void Anim3Point(Storyboard story, Duration dur, Duration dur2, string name, DependencyProperty property, double start, double mid, double end)
        {
            var kt0 = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0));
            var kt1 = KeyTime.FromTimeSpan(dur.TimeSpan);
            var kt2 = KeyTime.FromTimeSpan(dur.TimeSpan + dur2.TimeSpan);


            DoubleAnimationUsingKeyFrames anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(start, kt0));
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(mid, kt1));
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(end, kt2));
            Storyboard.SetTargetName(anim, name);
            Storyboard.SetTargetProperty(anim, new PropertyPath(property));
            story.Children.Add(anim);
        }

        public static void Anim2Point(Storyboard story, Duration dur, string name, DependencyProperty property, double start, double end)
        {
            DoubleAnimation anim = new DoubleAnimation(start, end, dur);
            Storyboard.SetTargetName(anim, name);
            Storyboard.SetTargetProperty(anim, new PropertyPath(property));
            story.Children.Add(anim);
        }

        public static void Anim2Point(Storyboard story, Duration dur, string name, DependencyProperty property, Point start, Point end)
        {
            PointAnimation anim = new PointAnimation(start, end, dur);
            Storyboard.SetTargetName(anim, name);
            Storyboard.SetTargetProperty(anim, new PropertyPath(property));
            story.Children.Add(anim);
        }

        static SnapAction SoftSnapPoint(ref Point pt)
        {
            double x = pt.X;
            double y = pt.Y;
            SnapAction sa1 = SoftSnapCoordinate(ref x);
            SnapAction sa2 = SoftSnapCoordinate(ref y);

            pt = new Point(x, y);

            return ScoreSnap(sa1, sa2);
        }

        static SnapAction HardSnapPoint(ref Point pt)
        {
            double x = pt.X;
            double y = pt.Y;
            SnapAction sa1 = HardSnapCoordinate(ref x);
            SnapAction sa2 = HardSnapCoordinate(ref y);

            pt = new Point(x, y);

            return ScoreSnap(sa1, sa2);
        }

        static SnapAction ScoreSnap(SnapAction sa1, SnapAction sa2)
        {
            int diffs = 0;

            if (sa1 != SnapAction.None) diffs++;
            if (sa2 != SnapAction.None) diffs++;

            switch (diffs)
            {
                default:
                    return SnapAction.None;
                case 1:
                    return SnapAction.Coordinate1;
                case 2:
                    return SnapAction.Coordinate2;
            }
        }

        static Vector HardSnapVector(Vector v)
        {
            return new Vector(HardSnapCoordinate(v.X), HardSnapCoordinate(v.Y));
        }

        static Vector SoftSnapVector(Vector v)
        {
            return new Vector(SoftSnapCoordinate(v.X), SoftSnapCoordinate(v.Y));
        }

        static Point RoundPoint(Point pt)
        {
            return new Point(RoundCoordinate(pt.X), RoundCoordinate(pt.Y));
        }

        static double SoftSnapCoordinate(double x)
        {
            const double tile_size = Tile.MajorGridSize;
            const double snap_size = 6.0;

            double boxes = Math.Floor(x / tile_size);

            double delta = x - boxes * tile_size;

            if (delta < snap_size)
                return boxes * tile_size;

            if (delta > tile_size - snap_size)
                return (1 + boxes) * tile_size;

            return x;
        }

        static SnapAction SoftSnapCoordinate(ref double x)
        {
            const double tile_size = Tile.MajorGridSize;
            const double snap_size = 6.0;

            double boxes = Math.Floor(x / tile_size);

            double delta = x - boxes * tile_size;

            if (delta < snap_size)
            {
                x = boxes * tile_size;
                return SnapAction.Coordinate1;
            }

            if (delta > tile_size - snap_size)
            {
                x = (1 + boxes) * tile_size;
                return SnapAction.Coordinate1;
            }

            return SnapAction.None;
        }
        
        static Transform MakeCombinationTransform(double scaleX, double scaleY, double angle)
        {
            ScaleTransform s = null;
            RotateTransform r = null;

            if (scaleX != 1.0 || scaleY != 1.0)
                s = new ScaleTransform(scaleX, scaleY);

            if (angle != 0)
                r = new RotateTransform(angle);

            if (s == null)
                return r;

            if (r == null)
                return s;

            TransformGroup tg = new TransformGroup();
            tg.Children.Add(s);
            tg.Children.Add(r);

            return tg;
        }

        static void ExtractScaleAndRotate(FrameworkElement el, out double scaleX, out double scaleY, out double rotateAngle)
        {
            ExtractScaleAndRotate(el.LayoutTransform, out scaleX, out scaleY, out rotateAngle);
        }

        static void ExtractScaleAndRotate(Transform t, out double scaleX, out double scaleY, out double rotateAngle)
        {
            scaleX = 1;
            scaleY = 1;
            rotateAngle = 0;

            // no transform, use identity
            if (t == null)
                return;

            if (t is TransformGroup)
            {
                // grab the pieces out of the group, note that general composition of transforms is not supported!
                TransformGroup tg = t as TransformGroup;
                foreach (Transform t0 in tg.Children)
                {
                    ExtractPrimitiveTransformInfo(t0, ref scaleX, ref scaleY, ref rotateAngle);
                }
            }
            else
            {
                // grab the items from the tranform, whichever they may be
                ExtractPrimitiveTransformInfo(t, ref scaleX, ref scaleY, ref rotateAngle);
            }
        }

        static void ExtractPrimitiveTransformInfo(Transform t, ref double scaleX, ref double scaleY, ref double rotateAngle)
        {
            if (t is ScaleTransform)
            {
                ScaleTransform s = t as ScaleTransform;
                scaleX = s.ScaleX;
                scaleY = s.ScaleY;
            }
            else if (t is RotateTransform)
            {
                RotateTransform r = t as RotateTransform;
                rotateAngle = r.Angle;
            }
        }

        static BezierSegment MakeStraightBezier(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0;
            double dy = y1 - y0;

            var p1 = new Point(x0 + dx / 3, y0 + dy / 3);
            var p2 = new Point(x0 + 2 * dx / 3, y0 + 2 * dy / 3);
            var p3 = new Point(x1, y1);

            var bseg = new BezierSegment(p1, p2, p3, true);
            return bseg;
        }

        void NoFill_Click(object sender, RoutedEventArgs e)
        {
            fillBrush = null;
            fillRect.Fill = new SolidColorBrush(Colors.Transparent);
        }

        void Fill_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var r = sp.Children[1] as Rectangle;

            fillBrush = r.Fill;
            fillRect.Fill = r.Fill;
        }


        void Stroke_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var r = sp.Children[1] as Rectangle;

            strokeBrush = r.Fill;
            strokeRect.Fill = r.Fill;
        }

        void NoStroke_Click(object sender, RoutedEventArgs e)
        {
            strokeBrush = null;
            strokeRect.Fill = new SolidColorBrush(Colors.Transparent);
        }

        void Opacity_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var r = sp.Children[1] as Rectangle;

            opacityRect.Opacity = r.Opacity;

            var x = m.Parent as ContextMenu;
            foreach (var ch in x.Items)
            {
                var mi = ch as MenuItem;
                if (mi == null)
                    continue;

                mi.IsCheckable = true;
                mi.IsChecked = false;
            }
            m.IsChecked = true;
        }

        void Thickness_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var l = sp.Children[1] as Line;

            if (l == null)
                return;

            thicknessSample.StrokeThickness = l.StrokeThickness;


            var x = m.Parent as ContextMenu;
            foreach (var ch in x.Items)
            {
                var mi = ch as MenuItem;
                if (mi == null)
                    continue;

                mi.IsCheckable = true;
                mi.IsChecked = false;
            }
            m.IsChecked = true;
        }

        void Thickness_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            thicknessSample.ContextMenu.IsOpen = true;
        }

        void Opacity_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            opacityRect.ContextMenu.IsOpen = true;
        }

        internal void ConsiderEval(EvalBundle b)
        {
            var dict = b.dict;
            if (dict == null || !dict.ContainsKey("purpose"))
            {
                return;
            }

            var purpose = dict["purpose"];

            if (purpose != "icons")
            {
                return;
            }

            var q = from k in dict.Keys
                    where k != "purpose"
                    orderby k
                    select k;

            double xPos = 40;
            double yPos = 40;

            BeginUndoUnit();
            ClearHandles();

            selectedList = new List<FrameworkElement>();

            foreach (var k in q)
            {
                var v = dict[k];

                FrameworkElement el = TryParseXaml(v);
                if (el == null)
                {
                    var t = new TextBlock();
                    t.Text = "Error";
                    t.HorizontalAlignment = HorizontalAlignment.Left;
                    t.VerticalAlignment = VerticalAlignment.Top;
                    t.IsHitTestVisible = true;
                    el = t;
                }

                el.Margin = new Thickness(xPos, yPos, 0, 0);

                el.Tag = FindFreeId(el);
                WireObject(el);
                AddCanvasChild(el);
                SaveFrameworkElement(el);

                selectedList.Add(el);

                var block = new TextBlock();

                block.Margin = new Thickness(xPos+40, yPos, 0, 0);
                block.HorizontalAlignment = HorizontalAlignment.Left;
                block.VerticalAlignment = VerticalAlignment.Top;
                block.Text = k;
                block.IsHitTestVisible = true;
                block.Foreground = strokeBrush;

                if (configuredFont)
                {
                    //              FontChooser.TransferFontProperties(fontSampleText, block);
                    block.FontFamily = fontSampleText.FontFamily;
                    block.FontWeight = fontSampleText.FontWeight;
                    block.FontStyle = fontSampleText.FontStyle;
                    block.FontStretch = fontSampleText.FontStretch;
                    block.FontSize = fontSampleText.FontSize;
                    block.TextDecorations = fontSampleText.TextDecorations;
                }

                el = block;
                el.Margin = new Thickness(xPos+40, yPos, 0, 0);

                el.Tag = FindFreeId(el);
                WireObject(el);
                AddCanvasChild(el);
                SaveFrameworkElement(el);

                selectedList.Add(el);

                yPos += 40;
            }

            var sel = selectedList;

            Main.DelayAction(300, () =>
            {
                if (sel == selectedList)
                    AddBoundingBoxesForSelection();
            });
        }
    }
}
