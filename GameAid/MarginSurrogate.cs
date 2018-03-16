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

namespace GameAid
{
    public class MarginSurrogate : DependencyObject
    {
        FrameworkElement el = null;

        public MarginSurrogate(FrameworkElement el)
        {
            this.el = el;
        }

        public static readonly DependencyProperty TopProperty =
            DependencyProperty.Register("Top", typeof(double), typeof(MarginSurrogate),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTopChanged)));

        public double Top
        {
            get { return (double)GetValue(TopProperty); }
            set { SetValue(TopProperty, value); }
        }

        public static readonly DependencyProperty LeftProperty =
            DependencyProperty.Register("Left", typeof(double), typeof(MarginSurrogate),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(OnLeftChanged)));

        public double Left
        {
            get { return (double)GetValue(LeftProperty); }
            set { SetValue(LeftProperty, value); }
        }

        static void OnTopChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            MarginSurrogate control = (MarginSurrogate)obj;

            control.TopChanged((double)args.NewValue);
        }

        static void OnLeftChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            MarginSurrogate control = (MarginSurrogate)obj;

            control.LeftChanged((double)args.NewValue);
        }

        void TopChanged(double top)
        {
            Thickness t = el.Margin;
            t.Top = top;
            el.Margin = t;
        }

        void LeftChanged(double left)
        {
            Thickness t = el.Margin;
            t.Left = left;
            el.Margin = t;
        }
    }
}