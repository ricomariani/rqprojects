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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for Tile.xaml
    /// </summary>
    public partial class Tile : UserControl
    {
        public Tile()
        {
            InitializeComponent();
            Thickness = 2;
        }

        public double Rows { get; set; }
        public double Columns { get; set; }
        public double Thickness { get; set; }

        public const double MajorGridSize = 40.0;

        bool fBigMap = false;

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);

            double w = this.ActualWidth;
            double h = this.ActualHeight;

            context.DrawRectangle(Brushes.GhostWhite, null, new Rect(0, 0, w, h));

            Pen pen;
            
            if (fBigMap)
                pen = new Pen(Brushes.LightSlateGray, 5.0);
            else
                pen = new Pen(Brushes.Gray, 1.0);

            Pen pen2 = new Pen(Brushes.LightSlateGray, 0.5);

            Point p1 = new Point();
            Point p2 = new Point();

            Thickness t = this.Margin;

            double y0 = t.Top;
            double y1 = y0 + h;

            double yBase;
            double delta;


            delta = MajorGridSize;
            yBase = delta * (int)(y0 / delta);

            if (fBigMap)
            {
                delta *= 10;
            }

            for (double y = yBase; y < y1; y += delta)
            {
                if (y < y0) continue;
                p1.X = 0;
                p1.Y = y-y0;
                p2.X = w;
                p2.Y = y-y0;

                context.DrawLine(pen, p1, p2);
            }

            if (!fBigMap)
            {
                delta = MajorGridSize / 3;
                yBase = delta * (int)(y0 / delta);

                for (double y = yBase; y < y1; y += delta)
                {
                    if (y < y0) continue;

                    if ((int)(y / MajorGridSize) * ((int)MajorGridSize) == y)
                        continue;

                    p1.X = 0;
                    p1.Y = y - y0;
                    p2.X = w;
                    p2.Y = y - y0;

                    context.DrawLine(pen2, p1, p2);
                }
            }


            double x0 = t.Left;
            double x1 = x0 + w;

            double xBase;

            delta = MajorGridSize;
            xBase = delta * (int)(x0 / delta);

            if (fBigMap)
            {
                delta *= 10;
            }

            for (double x = xBase; x < x1; x += delta)
            {
                if (x < x0) continue;
                p1.X = x-x0;
                p1.Y = 0;
                p2.X = x-x0;
                p2.Y = h;

                context.DrawLine(pen, p1, p2);
            }

            if (!fBigMap)
            {
                delta = MajorGridSize / 3;
                xBase = delta * (int)(x0 / delta);

                for (double x = xBase; x < x1; x += delta)
                {
                    if (x < x0) continue;

                    if ((int)(x / MajorGridSize) * (int)(MajorGridSize) == x)
                        continue;

                    p1.X = x - x0;
                    p1.Y = 0;
                    p2.X = x - x0;
                    p2.Y = h;

                    context.DrawLine(pen2, p1, p2);
                }
            }
        }

        public void SetParentZoom(double z)
        {
            bool newBigness = (z < .5);

            if (newBigness != fBigMap)
            {
                fBigMap = newBigness;
                InvalidateVisual();
            }
        }
    }


    public class HalfArcTile : Tile
    {
        public HalfArcTile()
        {
            InitializeComponent();
        }

        StreamGeometry geometry = null;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            /*
            double w = sizeInfo.NewSize.Width;
            double h = sizeInfo.NewSize.Height;
            double th = Thickness;

            var eg = new EllipseGeometry(new Point(w/2, h), w/2, h);
            var rg = new RectangleGeometry(new Rect(-th, -th, w + th*2, h + th));
            var g = new CombinedGeometry(GeometryCombineMode.Intersect, rg, eg);    
            this.Clip = g;
             */

            this.Clip = null;

            geometry = null;
        }

        protected override void OnRender(DrawingContext context)
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            double th = Thickness;

            // base.OnRender(context);

            if (geometry == null)
            {
                geometry = new StreamGeometry();
                geometry.FillRule = FillRule.EvenOdd;
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(0, h), true /* is filled */, false /* is closed */);
                    ctx.ArcTo(new Point(w, h), new Size(w/2, h), 0, false, SweepDirection.Clockwise, true, false);
                }
            }
            context.DrawGeometry(null, new Pen(Brushes.Black, th), geometry);
        }
    }

    public class QuarterArcTile : Tile
    {
        public QuarterArcTile()
        {
            InitializeComponent();
        }

        StreamGeometry geometry = null;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            /*

            double w = sizeInfo.NewSize.Width;
            double h = sizeInfo.NewSize.Height;
            double th = Thickness;

            var eg = new EllipseGeometry(new Point(w, h), w, h);
            var rg = new RectangleGeometry(new Rect(-th, -th, w + th*2, h + th));
            var g = new CombinedGeometry(GeometryCombineMode.Intersect, rg, eg);
            this.Clip = g;
             */

            this.Clip = null;
            geometry = null;
        }

        protected override void OnRender(DrawingContext context)
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            double th = Thickness;

            // base.OnRender(context);

            if (geometry == null)
            {
                geometry = new StreamGeometry();
                geometry.FillRule = FillRule.EvenOdd;
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(0, h), true /* is filled */, false /* is closed */);
                    ctx.ArcTo(new Point(w, 0), new Size(w, h), 0, false, SweepDirection.Clockwise, true, false);
                }
            }
            context.DrawGeometry(null, new Pen(Brushes.Black, th), geometry);
        }
    }

}
