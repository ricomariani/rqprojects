// The origin of this code is 
// https://blogs.msdn.microsoft.com/wpfsdk/2006/10/26/uncommon-dialogs-font-chooser-color-picker-dialogs/
// 
// it is not covered by Rico Mariani's MIT License Grant

using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;

namespace FontDialogSample
{
    internal class TypographicFeatureListItem : TextBlock, IComparable
    {
        readonly string _displayName;
        readonly DependencyProperty _chooserProperty;

        public TypographicFeatureListItem(string displayName, DependencyProperty chooserProperty)
        {
            _displayName = displayName;
            _chooserProperty = chooserProperty;
            this.Text = displayName;
        }

        public DependencyProperty ChooserProperty
        {
            get { return _chooserProperty; }
        }

        public override string ToString()
        {
            return _displayName;
        }

        int IComparable.CompareTo(object obj)
        {
            return string.Compare(_displayName, obj.ToString(), true, CultureInfo.CurrentCulture);
        }
    }
}
