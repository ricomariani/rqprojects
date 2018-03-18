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
using System.Text.RegularExpressions;
using System.Reflection;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for GameMap.xaml
    /// </summary>
    public partial class GameMap : UserControl
    {
        Brush strokeBrush = new SolidColorBrush(Colors.Black);
        Brush fillBrush = new SolidColorBrush(Colors.AntiqueWhite);

        public object moving = null;

        Point pointPathLastClick = new Point();

        ContextMenu handleMenu = null;
        Dictionary<string, MenuItem> menuDictionary = new Dictionary<string, MenuItem>();
        Dictionary<string, string> dictCurrent = new Dictionary<string, string>();

        bool forceRequestLater = false;
        string lastMap = "";

        GameMap giveTarget = null;

        List<PathPoint> pathPointList = null;
        int pathPointIndex;

        const double HandleSize = 8;
        FrameworkElement handleTarget = null;
        List<FrameworkElement> selectedList = null;
        List<FrameworkElement> selectedListBoxes = null;

        double zoomPrev;
        double cxPreferred;
        double cyPreferred;

        class HandleInfo
        {
            public Shape shape;
            public string tag;
            public bool isNew;
        }

        Dictionary<string, HandleInfo> handles = new Dictionary<string, HandleInfo>();

        public GameMap()
        {
            InitializeComponent();

            buttonGive.Visibility = Visibility.Hidden;

            fillRect.Fill = fillBrush;
            strokeRect.Fill = strokeBrush;

            handleMenu = (ContextMenu)this.Resources["HandleMenu"];

            foreach (Object o in handleMenu.Items)
            {
                var mi = o as MenuItem;

                if (mi == null)
                    continue;

                menuDictionary[mi.Name] = mi;

                foreach (Object o2 in mi.Items)
                {
                    mi = o2 as MenuItem;

                    if (mi == null)
                        continue;

                    menuDictionary[mi.Name] = mi;
                }
            }
        }

        Grid GridParent()
        {
            return this.Parent as Grid;
        }

        void GameMap_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {

                case Key.X:
                    if (IsCtrlDown())
                    {
                        Cut_Click(sender, null);
                    }
                    break;

                case Key.V:
                    if (IsCtrlDown())
                    {
                        Paste_Click(sender, null);
                    }
                    break;


                case Key.C:
                    if (IsCtrlDown())
                    {
                        Copy_Click(sender, null);
                    }
                    break;

                case Key.Z:
                    if (IsCtrlDown())
                    {
                        Undo_Click(sender, null);
                    }
                    break;

                case Key.Y:
                    if (IsCtrlDown())
                    {
                        Redo_Click(sender, null);
                    }
                    break;

                case Key.Delete:
                    e.Handled = true;
                    DeleteSelection();
                    break;

                case Key.Up:
                    BeginUndoUnit();
                    KeyMoveHelper(e, 0, -1);
                    break;

                case Key.Down:
                    BeginUndoUnit();
                    KeyMoveHelper(e, 0, 1);
                    break;

                case Key.Left:
                    BeginUndoUnit();
                    KeyMoveHelper(e, -1, 0);
                    break;

                case Key.Right:
                    BeginUndoUnit();
                    KeyMoveHelper(e, 1, 0);
                    break;
            }
        }

        void DeleteSelection()
        {
            BeginUndoUnit();

            if (handleTarget != null)
            {
                DeleteElement(handleTarget);
            }

            if (selectedList != null)
            {
                List<FrameworkElement> list = selectedList;

                foreach (FrameworkElement el in list)
                    DeleteElement(el);
            }
        }

        void scrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (zoomCount > 0)
            {
                CapturePreferredScollPosition();
                zoomCount = 0;
            }

            zoomSlider.Value += e.Delta / 200.0;
            e.Handled = true;
        }

        void KeyMoveHelper(KeyEventArgs e, double dx, double dy)
        {
            if (IsCtrlDown())
            {
                // if it's a grid, slide the elements around inside the grid
                Grid g = handleTarget as Grid;
                if (g == null)
                    return;

                foreach (var child in g.Children)
                {
                    FrameworkElement el = child as FrameworkElement;
                    if (el != null)
                        MoveElement(el, dx, dy);
                }

                e.Handled = true;
                SaveFrameworkElement(handleTarget);
                return;
            }
            else
            {
                MoveSelectedElements(dx, dy);
                e.Handled = true;

                if (selectedList != null)
                    foreach (FrameworkElement el in selectedList)
                        SaveFrameworkElement(el);

                if (handleTarget != null)
                    SaveFrameworkElement(handleTarget);
            }
        }

        MainWindow Main { get { return MainWindow.mainWindow; } }

        internal void CustomInit()
        {
            ActivateDrawingButton(buttonMove);
            currentPath.Items.Clear();
            // currentPath.Items.Add("default");
            // currentPath.SelectedIndex = 0;

            SetOwnFontName();
        }

        void ResetCanvas()
        {
            canvas.Children.Clear();
            canvas.Children.Add(rectHit);

            formations.Items.Clear();
            AddFormationsMenuFixedItems();

            // this will get the hit rectangles in the correct orientation after the refresh
            ActivateDrawingButton(buttonActive);
        }

        internal bool AreRequestsSuspended()
        {
            // too soon, the server is probably sending us junk
            if (DateTime.Now <= waitUpdates)
                return true;

            if (moving != null)
                return true;

            return false;
        }

        internal bool ForceRequest()
        {
            return !AreRequestsSuspended() && forceRequestLater;
        }

        internal void SetMapPath(string k)
        {
            if (k != lastMap)
            {
                lastMap = k;
                currentPath.Text = k;
                ResetCanvas();
                dictCurrent = null;
                undoHistory.Clear();
                Main.SendHost(String.Format("dir _maps/{0}", currentPath.Text));
            }
        }

        internal void AddNewMap(string k)
        {
            currentPath.Items.Add(k);
            SetMapPath(k);
        }

        internal void Consider(DictBundle b)
        {
            if (b.path == "_gameaid/_remote")
            {
                SetNewDefaultMapRemotely(b);
                return;
            }

            if (b.path == "_maps")
            {
                SetNewAvailableMaps(b);
                return;
            }

            if (b.path != "_maps/" + currentPath.Text)
                return;

            if (AreRequestsSuspended())
            {
                // we're going to miss an update, we must be sure to ask for it later
                forceRequestLater = true;
                return;
            }

            forceRequestLater = false;

            ReplaceCanvasContents(b.dict);
        }

        void SetNewAvailableMaps(DictBundle b)
        {
            var text = currentPath.Text;

            currentPath.Items.Clear();
            currentPath.Text = text;

            var q = from k in b.dict.Keys
                    where k != ".."
                    orderby k
                    select k;

            foreach (var k in q)
            {
                if (k == "..")
                    continue;

                currentPath.Items.Add(k);

                if (k == text)
                {
                    currentPath.SelectedIndex = currentPath.Items.Count - 1;
                }
            }
        }

        void SetNewDefaultMapRemotely(DictBundle b)
        {
            string mapKey = Main.mychannel + "|map";

            if (!b.dict.ContainsKey(mapKey) || b.dict[mapKey] == "")
                return;

            var newMap = b.dict[mapKey];

            if (newMap == defaultMap)
                return;

            defaultMap = newMap;

            if (giveTarget == null)
            {
                SetMapPath(defaultMap);
            }
            else
            {
                if (Main.gm_mode)
                {
                    if (defaultMap != "MapParts")
                    {
                        defaultMap = "MapParts";
                        SetMapPath(defaultMap);
                    }
                }
            }
        }

        internal static Dictionary<string, string> CopyDict(Dictionary<string, string> dictionary)
        {
            Dictionary<string, string> dNew = new Dictionary<string, string>();

            foreach (string k in dictionary.Keys)
                dNew[k] = dictionary[k];

            return dNew;
        }

        void ReplaceCanvasContents(Dictionary<string, string> dict)
        {
            var selectedTags = new Dictionary<string, object>();

            if (selectedList != null)
            {
                foreach (FrameworkElement el in selectedList)
                {
                    var s = el.Tag as string;

                    if (s != null)
                        selectedTags.Add(s, null);
                }
            }

            // save the canonical info we need to restore the selection
            string savedHandleTag = handleTarget != null ? handleTarget.Tag as string : null;
            int savedPathPointIndex = pathPointIndex;
            FrameworkElement selectionToRestore = null;

            // save all the previous positions of elements
            var previousPosition = new Dictionary<string, Point>();
            foreach (var ui in canvas.Children)
            {
                FrameworkElement el = ui as FrameworkElement;

                if (el == null)
                    continue;

                var tag = el.Tag as string;

                if (tag == null)
                    continue;

                var pt = new Point(el.Margin.Left, el.Margin.Top);

                previousPosition[tag] = pt;
            }

            ResetCanvas();

            Dictionary<string, string> dictIncoming = CopyDict(dict);

            string[] keys = dictIncoming.Keys.ToArray();
            Array.Sort(keys, (string left, string right) =>
            {
                bool f1 = dictIncoming[left] == "<Folder>";
                bool f2 = dictIncoming[right] == "<Folder>";

                if (f1 && !f2)
                    return -1;

                if (f2 && !f1)
                    return 1;

                int r = String.Compare(left, right, true);
                if (r != 0) return r;
                return String.Compare(left, right);
            });

            double currentZoom = GetCurrentZoom();

            foreach (string k in keys)
            {
                if (k.Length > 2 && k[2] >= '0' && k[2] <= '9')
                {
                    int layer = k[2] - '0';
                    if (disableLayer[layer])
                        continue;
                }

                string v = dictIncoming[k];

                if (v == "<Folder>")
                    continue;

                byte[] byteData = Encoding.ASCII.GetBytes(v);

                Object obj = null;

                try
                {
                    obj = System.Windows.Markup.XamlReader.Load(new System.IO.MemoryStream(byteData));
                }
                catch (Exception)
                {
                    var tb = new TextBlock();
                    tb.Text = String.Format("Error in _maps/{0}.{1}", currentPath.Text, k);
                    obj = tb;
                }

                if (!(obj is FrameworkElement))
                    continue;

                FrameworkElement el = (FrameworkElement)obj;
                el.Tag = k;
                WireObject(el);
                AddCanvasChild(el);

                var tile = el as Tile;

                // tell the tile about the new parent zoom
                if (tile != null)
                {
                    tile.SetParentZoom(currentZoom);
                }


                if (k == savedHandleTag)
                {
                    selectionToRestore = el;
                }

                if (previousPosition.Count > 0)
                {
                    AnimateNewArrival(previousPosition, el);
                }

                if (selectedTags.ContainsKey(k))
                {
                    selectedTags.Remove(k);
                    selectedTags.Add(k, obj);
                }
            }

            if (dictCurrent == null || dictCurrent.Count == 0)
            {
                // Create a storyboard to contain the animation.
                Storyboard story = new Storyboard();

                // Create a name scope for the page.
                NameScope.SetNameScope(canvas, new NameScope());

                // Register the name with the page to which the element belongs.
                canvas.RegisterName("canvas", canvas);

                Duration dur = new Duration(TimeSpan.FromMilliseconds(1000));

                Anim2Point(story, dur, "canvas", Grid.OpacityProperty, 0, 1);

                story.Begin(canvas);
            }

            dictCurrent = dictIncoming;

            ClearHandles();

            foreach (FrameworkElement el in selectedTags.Values)
            {
                if (el == null)
                    continue;

                if (selectedList == null)
                {
                    selectedList = new List<FrameworkElement>();
                    selectedListBoxes = new List<FrameworkElement>();
                }

                selectedList.Add(el);
                selectedListBoxes.Add(AddBoundingRectangleForElement(el, animate: true));
            }

            if (selectionToRestore != null)
            {
                pathPointIndex = savedPathPointIndex;

                // defer this so that the handle position can be done accurately
                // in case ActualWidth is required
                Main.DelayAction(150, () => CreateHandles(selectionToRestore));
            }
        }

        void AnimateNewArrival(Dictionary<string, Point> previousPosition, FrameworkElement el)
        {
            string k = el.Tag as string;

            Point ptOld;
            if (previousPosition.TryGetValue(k, out ptOld))
            {
                // animate this object into position, all the non-path types are animated
                if (el is Grid || el is Border || el is Image || el is Rectangle || el is Ellipse || el is TextBlock)
                {
                    Thickness mNew = el.Margin;

                    // if the old position is different than the new position then create an animation that
                    // moves it from the old into the new position.  Note other things might be different too
                    // but they are not animated.  Just top, left.
                    if (ptOld.X != mNew.Left || ptOld.Y != mNew.Top)
                    {
                        // set the margin top and left to the old top left
                        Thickness m = el.Margin;
                        m.Left = ptOld.X;
                        m.Top = ptOld.Y;
                        el.Margin = m;

                        // animate to the new
                        AnimateElementTopLeft(el, mNew.Left, mNew.Top);
                    }
                }
            }
            else
            {
                AnimateAppearance(el, addHandles: false, durationMS: 500);
            }
        }

        string FindFreeId(FrameworkElement el)
        {
            if (el is MenuItem)
            {
                MenuItem m = el as MenuItem;

                string s = (m.Header as String);

                if (s != null)
                    return s;
            }

            int idStart = GetStartingId(el);

            return FindFreeIdFromStart(idStart);
        }

        string FindFreeIdFromStart(int idStart)
        {
            string id = "";

            for (; ; idStart += 2)
            {
                // make sure the odd version of the key is free and the even version
                id = String.Format("{0:000000}", idStart);
                if (dictCurrent.ContainsKey(id))
                    continue;

                string id2 = String.Format("{0:000000}", (idStart ^ 1));
                if (dictCurrent.ContainsKey(id2))
                    continue;

                break;
            }
            return id;
        }

        int GetStartingId(FrameworkElement el)
        {
            int id = GetStartingIdBasic(el);

            int layer = id / 1000;

            if (!disableLayer[layer])
                return id;

            int newLayer = layer + 1;
            while (newLayer < 10 && disableLayer[newLayer])
                newLayer++;

            if (newLayer < 10)
                return id + (newLayer - layer) * 1000;

            newLayer = layer - 1;
            while (newLayer >= 0 && disableLayer[newLayer])
                newLayer--;

            if (newLayer >= 0)
                return id + (newLayer - layer) * 1000;

            return id;
        }

        int GetStartingIdBasic(FrameworkElement el)
        {
            if (el is Rectangle || el is Ellipse || el is Tile)
                return 1000;

            if (el is Line || el is Path)
                return 5001; // lines and paths start locked, hence 5001 and not 5000

            if (el is TextBlock)
                return 3000;

            if (el is Grid)
                return 6000;

            if (el is Image)
            {
                var bm = ((Image)el).Source as BitmapImage;
                if (bm != null)
                {
                    var uri = bm.UriSource;

                    if (uri != null)
                    {
                        var path = uri.AbsolutePath;
                        if (path.Contains("/Mapping/"))
                        {
                            if (!path.Contains("/Tokens/") &&
                                !path.Contains("/Enemies/") &&
                                !path.Contains("/Animals/") &&
                                !path.Contains("/Token States"))
                            {
                                return 2000;
                            }
                            else
                            {
                                return 7000;
                            }
                        }
                    }
                }

                return 6000;
            }

            return 7000;
        }

        void AddCanvasChild(FrameworkElement el)
        {
            if (el.Tag == null || !(el.Tag is string))
            {
                el.Tag = null;
                SaveFrameworkElement(el);
            }

            if (el.Tag as String == "Formations")
            {
                AddFormationsMenu(el);
                return;
            }

            int index = -1;

            string elTag = (string)el.Tag;

            foreach (UIElement ui in canvas.Children)
            {
                var elChild = ui as FrameworkElement;

                if (elChild == null)
                    continue;

                if (elChild.Tag == null)
                    continue;

                string s = elChild.Tag as string;

                if (s == null)
                    continue;

                if (String.Compare(elTag, s) <= 0)
                {
                    index = canvas.Children.IndexOf(elChild);
                    break;
                }
            }

            if (index < 0)
                canvas.Children.Add(el);
            else
                canvas.Children.Insert(index, el);
        }

        void AddFormationsMenu(FrameworkElement el)
        {
            // if the formations element being added isn't a menu forget the whole thing
            MenuItem m = el as MenuItem;
            if (m == null)
                return;

            // get the main menu, the parent of the current formations menu
            Menu menu = formations.Parent as Menu;

            // remove the formations menu
            menu.Items.Remove(formations);

            // the new formations menu is the incoming menu item
            formations = m;

            // add the new formations menu
            menu.Items.Add(m);

            // remove everything before the first seperator
            // we do this because the fixed items in the formations menu
            // can change from build to build, so we don't want the saved items
            while (m.Items.Count > 0)
            {
                Separator sep = m.Items[0] as Separator;
                if (sep != null)
                    break;

                m.Items.RemoveAt(0);
            }

            // whatever's left in the new menu, wire in the click handler
            for (int i = 0; i < m.Items.Count; i++)
            {
                MenuItem item = m.Items[i] as MenuItem;
                if (item != null)
                {
                    item.Click += new RoutedEventHandler(Formation_Click);
                }
            }

            // and finally insert the fixed items back in, whatever they are in this version
            // of the tool.  Which is maybe not the same as what was saved
            AddFormationsMenuFixedItems();
        }

        void AddFormationsMenuFixedItems()
        {
            MenuItem m;

            m = new MenuItem();
            m.Header = "--\r\nTo Delete:\r\nUse Ctrl + Menu Item";
            m.IsEnabled = false;
            formations.Items.Insert(0, m);

            m = new MenuItem();
            m.Header = "Transpose X/Y";
            m.Click += new RoutedEventHandler(XYFlipFormation_Click);
            formations.Items.Insert(0, m);

            m = new MenuItem();
            m.Header = "Flip Vertical";
            m.Click += new RoutedEventHandler(YFlipFormation_Click);
            formations.Items.Insert(0, m);

            m = new MenuItem();
            m.Header = "Flip Horizontal";
            m.Click += new RoutedEventHandler(XFlipFormation_Click);
            formations.Items.Insert(0, m);

            m = new MenuItem();
            m.Header = "Add Selected";
            m.Click += new RoutedEventHandler(AddFormation_Click);
            formations.Items.Insert(0, m);
        }

        DateTime waitUpdates = DateTime.Now;

        const int waitMilliseconds = 6000;

        void SaveFrameworkElement(FrameworkElement element)
        {
            if (element == handleTarget)
            {
                SetPropertiesForHandleTarget();
            }

            SFE(element);
        }

        void SFE(FrameworkElement element)
        {
            // if we do not yet have a current dictionary someone did a very fast edit... just hold on to the item and save it when we do have a dictionary
            if (dictCurrent == null)
            {
                Main.DelayAction(2000, () => SFE(element));
                return;
            }

            bool fNewItem = false;

            waitUpdates = DateTime.Now.AddMilliseconds(waitMilliseconds);

            Object objectTag = element.Tag;
            string tag;

            if (objectTag == null || !(objectTag is string))
            {
                tag = FindFreeId(element);
                fNewItem = true;
            }
            else
            {
                tag = (string)element.Tag;
            }

            ContextMenu cm = element.ContextMenu;
            element.ClearValue(ContextMenuProperty);
            element.ClearValue(TagProperty);

            System.IO.StringWriter sw = new System.IO.StringWriter();
            System.Windows.Markup.XamlWriter.Save(element, sw);

            element.Tag = tag;
            if (cm != null)
            {
                element.ContextMenu = cm;
            }

            string s = sw.ToString();

            UpdateKeyDictionaryAndServer(tag, s, fNewItem);
        }

        public void SetPropertiesForHandleTarget()
        {
            if (handleTarget == null)
                return;

            Line l = handleTarget as Line;

            if (l != null)
            {
                SetLineProperties(l);
                return;
            }

            Path p = handleTarget as Path;

            if (p != null)
            {
                SetPathProperties(p);
                return;
            }

            SetGenericProperties(handleTarget);
        }


        void SetPathProperties(Path p)
        {
            var pw = MainWindow.propertyWindow;

            if (pw == null)
                return;

            pw.ClearRows();

            pw.Title = "Properties: " + handleTarget.Tag;

            ArcSegment arc;
            PathFigure pf;

            if (TryExtractArc(p, out pf, out arc))
            {
                pw.AddDoubleProperty("X1", pf.StartPoint.X, (x) => { BeginUndoUnit(); double dx = x - pf.StartPoint.X; MoveElement(p, dx, 0); SFE(p); });
                pw.AddDoubleProperty("Y1", pf.StartPoint.Y, (y) => { BeginUndoUnit(); double dy = y - pf.StartPoint.Y; MoveElement(p, 0, dy); SFE(p); });
                pw.AddDoubleProperty("X2", arc.Point.X, (x) => { BeginUndoUnit(); double dx = x - arc.Point.X; MoveElement(p, dx, 0); SFE(p); });
                pw.AddDoubleProperty("Y2", arc.Point.Y, (y) => { BeginUndoUnit(); double dy = y - arc.Point.Y; MoveElement(p, 0, dy); SFE(p); });

                pw.AddDoubleProperty("SizeX", arc.Size.Width, null);
                pw.AddDoubleProperty("SizeY", arc.Size.Height, null);
                pw.AddDoubleProperty("Rotation", arc.RotationAngle, null);
            }

            pw.AddDoubleProperty("Opacity", p.Opacity, (x) => { BeginUndoUnit(); p.Opacity = x; SFE(p); });

            if (TryExtractPathFigure(p, out pf))
            {
                pw.AddBooleanProperty("IsClosed", pf.IsClosed, (x) => { BeginUndoUnit(); pf.IsClosed = x; SFE(p); });
            }

            AddShapeProperties(p);

            MainWindow.propertyGameMap = this;
        }

        void SetLineProperties(Line l)
        {
            var pw = MainWindow.propertyWindow;

            if (pw == null)
                return;

            pw.ClearRows();

            pw.Title = "Properties: " + handleTarget.Tag;

            pw.AddDoubleProperty("X1", l.X1, (x) => { BeginUndoUnit(); l.X1 = x; SFE(l); });
            pw.AddDoubleProperty("Y1", l.Y1, (x) => { BeginUndoUnit(); l.Y1 = x; SFE(l); });
            pw.AddDoubleProperty("X2", l.X2, (x) => { BeginUndoUnit(); l.X2 = x; SFE(l); });
            pw.AddDoubleProperty("Y2", l.Y2, (x) => { BeginUndoUnit(); l.Y2 = x; SFE(l); });

            pw.AddDoubleProperty("Opacity", l.Opacity, (x) => { BeginUndoUnit(); l.Opacity = x; SFE(l); });

            AddShapeProperties(l);

            MainWindow.propertyGameMap = this;
        }

        void SetGenericProperties(FrameworkElement el)
        {
            var pw = MainWindow.propertyWindow;

            if (pw == null)
                return;

            pw.ClearRows();

            pw.Title = "Properties: " + handleTarget.Tag;

            double scaleX, scaleY, rotateAngle;
            ExtractScaleAndRotate(handleTarget, out scaleX, out scaleY, out rotateAngle);

            double widthStart = handleTarget.ActualWidth;
            double heightStart = handleTarget.ActualHeight;

            pw.AddDoubleProperty("X", el.Margin.Left, (x) => { BeginUndoUnit(); el.Margin = new Thickness(x, el.Margin.Top, 0, 0); SFE(el); });
            pw.AddDoubleProperty("Y", el.Margin.Top, (x) => { BeginUndoUnit(); el.Margin = new Thickness(el.Margin.Left, x, 0, 0); SFE(el); });

            TextBlock t = el as TextBlock;
            Image im = el as Image;
            Grid g = el as Grid;

            if (g != null || t != null)
            {
                pw.AddDoubleProperty("ScaleX", scaleX, (xscale) =>
                {
                    BeginUndoUnit();
                    ExtractScaleAndRotate(handleTarget, out scaleX, out scaleY, out rotateAngle);

                    Point pc1 = handleTarget.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
                    handleTarget.LayoutTransform = MakeCombinationTransform(xscale, scaleY, rotateAngle);
                    Point pc2 = handleTarget.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
                    handleTarget.Margin = RoundMargin(handleTarget.Margin.Left + pc1.X - pc2.X, handleTarget.Margin.Top + pc1.Y - pc2.Y, 0, 0);

                    SFE(handleTarget);
                });

                pw.AddDoubleProperty("ScaleY", scaleY, (yscale) =>
                {
                    BeginUndoUnit();
                    ExtractScaleAndRotate(handleTarget, out scaleX, out scaleY, out rotateAngle);

                    Point pc1 = handleTarget.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
                    handleTarget.LayoutTransform = MakeCombinationTransform(scaleX, yscale, rotateAngle);
                    Point pc2 = handleTarget.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
                    handleTarget.Margin = RoundMargin(handleTarget.Margin.Left + pc1.X - pc2.X, handleTarget.Margin.Top + pc1.Y - pc2.Y, 0, 0);

                    SFE(handleTarget);
                });
            }

            if (g != null)
            {
                pw.AddBrushProperty("Background", g.Background, (x) => 
                {
                    BeginUndoUnit();

                    if (x != null)
                        g.Background = x;
                    else
                        g.ClearValue(BackgroundProperty);

                    SFE(g);
                });
            }
            else if (t != null)
            {
                pw.AddStringProperty("Text", t.Text, (x) => { BeginUndoUnit(); t.Text = x; SFE(t); });

                pw.AddBrushProperty("Foreground", t.Foreground, (x) => 
                { 
                    BeginUndoUnit();
                    
                    if (x != null)
                        t.Foreground = x;
                    else
                        t.ClearValue(ForegroundProperty);

                    SFE(t); 
                });

                pw.AddBrushProperty("Background", t.Background, (x) => 
                { 
                    BeginUndoUnit();

                    if (x != null)
                        t.Background = x;
                    else
                        t.ClearValue(BackgroundProperty);

                    SFE(t);
                }
                );

                pw.AddFontProperty("Font", t, (x) =>
                {
                    BeginUndoUnit();

                    t.FontFamily = x.FontFamily;
                    t.FontWeight = x.FontWeight;
                    t.FontStyle = x.FontStyle;
                    t.FontStretch = x.FontStretch;
                    t.FontSize = x.FontSize;
                    t.TextDecorations = x.TextDecorations;

                    SFE(t);
                });
            }
            else if (im != null)
            {
                pw.AddDoubleProperty("Width", el.ActualWidth, (x) => { BeginUndoUnit(); el.Width = x; SFE(el); });
                pw.AddDoubleProperty("Height", el.ActualHeight, (x) => { BeginUndoUnit(); el.Height = x; SFE(el); });
            }
            else
            {
                pw.AddDoubleProperty("Width", el.Width, (x) => { BeginUndoUnit(); el.Width = x; SFE(el); });
                pw.AddDoubleProperty("Height", el.Height, (x) => { BeginUndoUnit(); el.Height = x; SFE(el); });
            }

            pw.AddDoubleProperty("RotateAngle", rotateAngle, (rot) =>
            {
                BeginUndoUnit();
                ExtractScaleAndRotate(handleTarget, out scaleX, out scaleY, out rotateAngle);

                Point pc1 = el.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
                el.LayoutTransform = MakeCombinationTransform(scaleX, scaleY, rot);
                Point pc2 = el.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
                el.Margin = RoundMargin(el.Margin.Left + pc1.X - pc2.X, el.Margin.Top + pc1.Y - pc2.Y, 0, 0);

                SFE(el);
            });

            Border b = el as Border;

            if (b != null)
            {
                pw.AddDoubleProperty("CornerRadius", b.CornerRadius.TopLeft, (x) => { BeginUndoUnit(); b.CornerRadius = new CornerRadius(x); SFE(b); });
            }

            AddShapeProperties(el);

            pw.AddDoubleProperty("Opacity", el.Opacity, (x) => { BeginUndoUnit(); el.Opacity = x; SFE(el); });
            pw.AddStringProperty("ToolTip", el.ToolTip == null ? "" : el.ToolTip.ToString(), (x) => 
            { 
                BeginUndoUnit();

                if (x == null || x == "")
                    el.ClearValue(ToolTipProperty);
                else
                    el.ToolTip = x; 

                SFE(el); 
            });

            MainWindow.propertyGameMap = this;
        }

        void AddShapeProperties(FrameworkElement el)
        {
            var pw = MainWindow.propertyWindow;

            if (pw == null)
                return;

            Shape shape = el as Shape;

            if (shape != null)
            {
                pw.AddDoubleProperty("StrokeThickness", shape.StrokeThickness, (x) => { BeginUndoUnit(); shape.StrokeThickness = x; SFE(shape); });

                pw.AddBrushProperty("Stroke", shape.Stroke, (x) => 
                { 
                    BeginUndoUnit();

                    if (x != null)
                        shape.Stroke = x;
                    else
                        shape.ClearValue(Shape.StrokeProperty); 
                    
                    SFE(shape); 
                });

                if (!(shape is Line))
                {
                    pw.AddBrushProperty("Fill", shape.Fill, (x) => 
                    { 
                        BeginUndoUnit();
 
                        if (x != null)
                            shape.Fill = x;
                        else
                            shape.ClearValue(Shape.FillProperty);

                        SFE(shape); 
                    });
                }
            }
        }

        void UpdateKeyDictionaryAndServer(string key, string value, bool fNewItem)
        {
            if (fNewItem)
            {
                Main.SendHost(String.Format("nn _maps/{0} {1} {2}", currentPath.Text, key, value));
            }
            else
            {
                if (dictCurrent != null && dictCurrent.ContainsKey(key) && dictCurrent[key] == value)
                    return;

                Main.SendHost(String.Format("n _maps/{0} {1} {2}", currentPath.Text, key, value));
            }

            if (dictCurrent != null)
            {
                if (dictCurrent.ContainsKey(key))
                {
                    AddUndoRecord(key, dictCurrent[key], value);
                    dictCurrent.Remove(key);
                }
                else
                {
                    AddUndoRecord(key, "", value);
                }

                dictCurrent[key] = value;
            }
        }

        internal double GetCurrentZoom()
        {
            const double max_zoom = 4.0;

            if (zoomSlider == null)
                return 1;

            double zoom = zoomSlider.Value / 10;
            zoom *= zoom;
            zoom *= max_zoom - .1;
            zoom += .1; // scales from .1 to max_zoom
            return zoom;
        }

        void scrollZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ComputeCanvasLayoutTransform();
        }

        void ComputeCanvasLayoutTransform()
        {
            if (canvas == null)
                return;

            double w = scrollViewer.ActualWidth;
            double h = scrollViewer.ActualHeight;

            double zoomNew = GetCurrentZoom();
            canvas.LayoutTransform = new ScaleTransform(zoomNew, zoomNew);

            if (zoomCount == 0)
            {
                double hNew = scrollViewer.HorizontalOffset / zoomNew;
                double vNew = scrollViewer.VerticalOffset / zoomNew;
                double cxNew = hNew + w / zoomNew / 2;
                double cyNew = vNew + h / zoomNew / 2;

                scrollViewer.ScrollToHorizontalOffset(zoomNew * (hNew + cxPreferred - cxNew));
                scrollViewer.ScrollToVerticalOffset(zoomNew * (vNew + cyPreferred - cyNew));
            }

            // find all the tiles in layer zero and tell them about the new zoom
            foreach (var o in canvas.Children)
            {
                var tile = o as Tile;

                // tell the tile about the new parent zoom
                if (tile != null)
                {
                    tile.SetParentZoom(zoomNew);
                }

                // stop if we leave layer zero
                var el = o as FrameworkElement;

                if (el != null)
                {
                    var s = el.Tag as string;

                    if (s != null && !s.StartsWith("000"))
                        break;
                }
            }
        }

        void map_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            CapturePreferredScollPosition();
        }

        void CapturePreferredScollPosition()
        {
            double w = scrollViewer.ActualWidth;
            double h = scrollViewer.ActualHeight;

            double zoom = GetCurrentZoom();
            double hOld = scrollViewer.HorizontalOffset / zoom;
            double vOld = scrollViewer.VerticalOffset / zoom;

            if (zoom == zoomPrev)
            {
                cxPreferred = hOld + w / zoom / 2;
                cyPreferred = vOld + h / zoom / 2;
            }

            zoomPrev = zoom;
        }

        Button buttonActive;

        void drawing_button_Click(object sender, RoutedEventArgs e)
        {
            ActivateDrawingButton((Button)sender);
            ClearHandles();
        }

        bool hardSnapEnabled = false;

        void buttonHardSnap_Click(object sender, RoutedEventArgs e)
        {
            hardSnapEnabled = !hardSnapEnabled;

            if (hardSnapEnabled)
                buttonHardSnap.LayoutTransform = new ScaleTransform(1.5, 1.5);
            else
                buttonHardSnap.ClearValue(LayoutTransformProperty);
        }

        void ActivateDrawingButton(Button b)
        {
            if (buttonActive != null)
            {
                buttonActive.ClearValue(LayoutTransformProperty);
            }

            buttonActive = b;
            buttonActive.LayoutTransform = new ScaleTransform(1.5, 1.5);

            CoverCanvasIfNeeded();
        }

        void CoverCanvasIfNeeded()
        {
            if (buttonActive == buttonMove || buttonActive == buttonPan || buttonActive == null || buttonActive == buttonGive)
            {
                // in these modes win interact with objects on the canvas, rectHit does not cover
                canvas.Children.Remove(rectHit);
                canvas.Children.Insert(0, rectHit);
                rectHit.IsHitTestVisible = true;
            }
            else
            {
                // in these modes we click on the canvas to add new things, rectHit covers the canvas
                canvas.Children.Remove(rectHit);
                canvas.Children.Add(rectHit);
                rectHit.IsHitTestVisible = true;
            }
        }

        void new_tile_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Tile t = new Tile();

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            t.Opacity = opacity;
            t.Thickness = thickness;

            BeginUndoUnit();
            MakeStandardObject(e, t);
        }

        void MakeStandardObject(MouseButtonEventArgs e, FrameworkElement el)
        {
            const int standard_size_in_tiles = 2;

            var pointStart = e.GetPosition(canvas);

            if (IsHardSnapEnabled())
            {
                pointStart = new Point(HardSnapCoordinate(pointStart.X), HardSnapCoordinate(pointStart.Y));
            }

            el.Width = Tile.MajorGridSize * standard_size_in_tiles;
            el.Height = Tile.MajorGridSize * standard_size_in_tiles;

            el.Margin = RoundMargin(pointStart.X, pointStart.Y, 0, 0);
            el.VerticalAlignment = VerticalAlignment.Top;
            el.HorizontalAlignment = HorizontalAlignment.Left;

            WireObject(el);
            SaveFrameworkElement(el);
            AddCanvasChild(el);
            AnimateAppearance(el, true);
        }

        void new_rect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Rectangle rect = new Rectangle();
            rect.Fill = fillBrush;
            rect.Stroke = strokeBrush;

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            rect.StrokeThickness = thickness;
            rect.Opacity = opacity;

            MakeStandardObject(e, rect);
        }

        void new_ellipse_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Ellipse ellipse = new Ellipse();
            ellipse.Fill = fillBrush;
            ellipse.Stroke = strokeBrush;

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            ellipse.StrokeThickness = thickness;
            ellipse.Opacity = opacity;

            BeginUndoUnit();
            MakeStandardObject(e, ellipse);
        }

        bool IsHardSnapEnabled()
        {
            return hardSnapEnabled || IsShiftDown();
        }

        void new_multiSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var pointMouse = e.GetPosition(canvas);

            var rectSelection = new Rectangle();
            rectSelection.Stroke = new SolidColorBrush(Colors.Black);
            rectSelection.Fill = null;
            rectSelection.HorizontalAlignment = HorizontalAlignment.Left;
            rectSelection.VerticalAlignment = VerticalAlignment.Top;
            rectSelection.Margin = RoundMargin(pointMouse.X, pointMouse.Y, 0, 0);
            rectSelection.Width = 1;
            rectSelection.Height = 1;

            canvas.Children.Add(rectSelection);

            WireRectangleForSelection(rectSelection);
            BeginMouseCapture(rectSelection, e);
        }

        void WireRectangleForSelection(Rectangle rectSelection)
        {
            var pointStart = new Point(rectSelection.Margin.Left, rectSelection.Margin.Top);
            List<FrameworkElement> savedSelection;

            if (IsShiftDown())
                savedSelection = selectedList;
            else
                savedSelection = null;

            ClearHandles();

            MouseEventHandler rectSelection_MouseMove = (object sender, MouseEventArgs e) =>
            {
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);

                double x1 = Math.Min(pointStart.X, pointEnd.X);
                double x2 = Math.Max(pointStart.X, pointEnd.X);
                double y1 = Math.Min(pointStart.Y, pointEnd.Y);
                double y2 = Math.Max(pointStart.Y, pointEnd.Y);

                rectSelection.Margin = RoundMargin(x1, y1, 0, 0);
                rectSelection.Width = x2 - x1;
                rectSelection.Height = y2 - y1;
            };

            MouseButtonEventHandler rectSelection_MouseUp = (object sender, MouseButtonEventArgs e) =>
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                if (moving != sender)
                    return;

                EndMouseCapture(sender);

                canvas.Children.Remove(rectSelection);

                ComputeSelectionFromRectangle(rectSelection, savedSelection);
                AddBoundingBoxesForSelection();

                rectSelection = null;
            };

            rectSelection.MouseMove += rectSelection_MouseMove;
            rectSelection.MouseUp += rectSelection_MouseUp;
        }

        void ComputeSelectionFromRectangle(Rectangle rectSelection, List<FrameworkElement> savedSelection)
        {
            var geo = new RectangleGeometry(
                        new Rect(rectSelection.Margin.Left,
                                 rectSelection.Margin.Top,
                                 rectSelection.Width,
                                 rectSelection.Height));

            selectedList = new List<FrameworkElement>();

            gridCounts.Clear();

            VisualTreeHelper.HitTest(
                canvas,
                null,
                new HitTestResultCallback(multiSelect_HitTest),
                new GeometryHitTestParameters(geo));

            foreach (Grid g in gridCounts.Keys)
            {
                if (GetGridContentCount(g) == gridCounts[g])
                {
                    if (!selectedList.Contains(g))
                        selectedList.Add(g);
                }
            }

            gridCounts.Clear();

            if (savedSelection != null)
            {
                foreach (FrameworkElement el in savedSelection)
                {
                    if (!selectedList.Contains(el))
                        selectedList.Add(el);
                }

                savedSelection = null;
            }
        }

        void AddBoundingBoxesForSelection()
        {
            if (selectedList == null || selectedList.Count == 0)
            {
                selectedList = null;
                selectedListBoxes = null;
                return;
            }

            selectedListBoxes = new List<FrameworkElement>();

            foreach (FrameworkElement el in selectedList)
            {
                selectedListBoxes.Add(AddBoundingRectangleForElement(el, animate: true));
            }

            if (buttonActive != null)
                buttonActive.Focus();
        }

        Dictionary<Grid, int> gridCounts = new Dictionary<Grid, int>();

        HitTestResultBehavior multiSelect_HitTest(HitTestResult result)
        {
            GeometryHitTestResult gr = (GeometryHitTestResult)result;

            switch (gr.IntersectionDetail)
            {
                case IntersectionDetail.FullyInside:
                    FrameworkElement el = gr.VisualHit as FrameworkElement;
                    if (el == null)
                        break;

                    if (el == rectHit)
                        break;

                    if (el.Parent != canvas)
                    {
                        Grid g = GetGridParent(el);

                        if (g == null || g.Parent != canvas)
                            break;

                        if (gridCounts.ContainsKey(g))
                        {
                            gridCounts[g] = gridCounts[g] + 1;
                        }
                        else
                        {
                            gridCounts[g] = 1;
                        }

                        break;
                    }

                    selectedList.Add(el);
                    break;

                case IntersectionDetail.FullyContains:
                case IntersectionDetail.Intersects:
                case IntersectionDetail.Empty:
                case IntersectionDetail.NotCalculated:
                    break;
            }

            return HitTestResultBehavior.Continue;
        }

        Grid GetGridParent(FrameworkElement el)
        {
            for (; ; )
            {
                Grid g = el.Parent as Grid;

                if (g == null)
                {
                    return null;
                }

                if (g.Parent == canvas)
                {
                    return g;
                }

                el = g;
            }
        }

        int GetGridContentCount(Grid g)
        {
            int count = 0;
            foreach (var child in g.Children)
            {
                if (child is Grid)
                    count += GetGridContentCount(child as Grid);
                else
                    count++;
            }

            return count;
        }

        void canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                pan_MouseDown(sender, e);
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            if (buttonActive == buttonMove)
                new_multiSelect_MouseDown(sender, e);

            if (buttonActive == buttonHalfArc)
                new_arc_MouseDown(sender, e, fQuarter: false);

            if (buttonActive == buttonQuarterArc)
                new_arc_MouseDown(sender, e, fQuarter: true);

            if (buttonActive == buttonLine)
                new_line_MouseDown(sender, e);

            if (buttonActive == buttonPan)
                pan_MouseDown(sender, e);

            if (buttonActive == buttonText)
                new_text_MouseDown(sender, e);

            if (buttonActive == buttonImage)
                new_img_MouseDown(sender, e);

            if (buttonActive == buttonTile)
                new_tile_MouseDown(sender, e);

            if (buttonActive == buttonEllipse)
                new_ellipse_MouseDown(sender, e);

            if (buttonActive == buttonRect)
                new_rect_MouseDown(sender, e);
        }

        void canvas_MouseMove(object sender, MouseEventArgs e)
        {
        }

        void canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }

        MouseButton buttonStarted;

        void pan_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (moving != null)
                return;
            
            var rect = new Rectangle();
            rect.Width = 0;
            rect.Height = 0;
            rect.Margin = new Thickness(-1, -1, 0, 0);

            canvas.Children.Add(rect);

            BeginMouseCapture(rect, e);
            WireRectangleForPanning(e.GetPosition(scrollViewer), rect);
        }

        void WireRectangleForPanning(Point pointStart, Rectangle rect)
        {
            double panXStart = scrollViewer.HorizontalOffset;
            double panYStart = scrollViewer.VerticalOffset;
            long panTimeStamp = DateTime.Now.Ticks / 10000;

            MouseEventHandler pan_MouseMove = (object sender, MouseEventArgs e) =>
            {
                if (moving != sender)
                    return;

                var point = e.GetPosition(scrollViewer);

                scrollViewer.ScrollToHorizontalOffset(panXStart + pointStart.X - point.X);
                scrollViewer.ScrollToVerticalOffset(panYStart + pointStart.Y - point.Y);
            };

            MouseButtonEventHandler pan_MouseUp = (object sender, MouseButtonEventArgs e) =>
            {
                if (moving != sender)
                    return;

                if (e.ChangedButton != buttonStarted)
                    return;

                EndMouseCapture(sender);
                ComputeCanvasLayoutTransform();
                canvas.Children.Remove(rect);

                var panNow = DateTime.Now.Ticks / 10000;

                if (panNow - panTimeStamp < 500)
                {
                    ZoomToCursor(e);
                }
            };

            rect.MouseMove += pan_MouseMove;
            rect.MouseUp += pan_MouseUp;
        }

        Point zoomPoint;
        int zoomCount;

        void ZoomToCursor(MouseButtonEventArgs e)
        {
            zoomPoint = e.GetPosition(canvas);
            zoomCount = 30;
            ZoomAgain();
        }

        Point ZoomOrigin(double zoom)
        {
            double w = scrollViewer.ActualWidth;
            double h = scrollViewer.ActualHeight;

            var zp = zoomPoint;
            zp.X *= zoom;
            zp.Y *= zoom;
            zp.X -= w / 2;
            zp.Y -= h / 2;

            return zp;
        }

        void ZoomAgain()
        {
            Main.DelayAction(30, () =>
            {
                // aborted
                if (zoomCount == 0)
                {
                    return;
                }

                double zoom = GetCurrentZoom();
                double xScreenOld = zoomPoint.X * zoom - scrollViewer.HorizontalOffset;
                double yScreenOld = zoomPoint.Y * zoom - scrollViewer.VerticalOffset;

                double zoomDelta = (10 - zoomSlider.Value) / 10;

                zoomSlider.Value = zoomSlider.Value + zoomDelta;

                zoom = GetCurrentZoom();
                double xScreenNew = zoomPoint.X * zoom - scrollViewer.HorizontalOffset;
                double yScreenNew = zoomPoint.Y * zoom - scrollViewer.VerticalOffset;

                double w = scrollViewer.ActualWidth / 2;
                double h = scrollViewer.ActualHeight / 2;

                double xDelta = (w - xScreenOld) / 10 + xScreenOld - xScreenNew;
                double yDelta = (h - yScreenOld) / 10 + yScreenOld - yScreenNew;

                var zoomX = scrollViewer.HorizontalOffset - xDelta;
                var zoomY = scrollViewer.VerticalOffset - yDelta;

                scrollViewer.ScrollToHorizontalOffset(zoomX);
                scrollViewer.ScrollToVerticalOffset(zoomY);

                zoomCount--;
                if (zoomCount > 0)
                {
                    ZoomAgain();
                }
                else
                {
                    CapturePreferredScollPosition();
                }
            });
        }

        void new_text_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var pointStart = e.GetPosition(canvas);

            var box = new TextBox();
            box.MinHeight = 20;
            box.MinWidth = 75;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalAlignment = VerticalAlignment.Top;
            box.Margin = RoundMargin(pointStart.X, pointStart.Y, 0, 0);
            canvas.Children.Add(box);

            box.Focus();
            box.KeyDown += new_text_KeyHandler;

            box.IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(new_text_IsKeyboardFocusedChanged);

            // wait 10 seconds minimum
            waitUpdates = DateTime.Now.AddMilliseconds(10000);
        }

        void new_text_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                return;

            new_text_block(sender);
        }

        public void new_text_KeyHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                TextBox t = sender as TextBox;
                t.Text = "";
                canvas.Children.Remove(t);
                return;
            }

            if (e.Key != Key.Enter)
                return;

            new_text_block(sender);
        }

        public void new_text_block(object sender)
        {
            var t = (TextBox)sender;
            canvas.Children.Remove(t);

            if (t.Text.Length == 0)
                return;

            BeginUndoUnit();

            var block = new TextBlock();

            block.Margin = t.Margin;
            block.HorizontalAlignment = HorizontalAlignment.Left;
            block.VerticalAlignment = VerticalAlignment.Top;
            block.Text = t.Text;
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

            WireObject(block);
            SaveFrameworkElement(block);
            AddCanvasChild(block);

            // while we are in text mode we want hit testing on the canvas to add new text
            // we don't want to click on stuff, so we use the hit rect to hide the canvas elements
            // the helper does the job for us.
            CoverCanvasIfNeeded();

            t.Text = "";
        }

        void new_img_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (imageWidth <= 0 || imageText == null || !imageText.StartsWith("http://"))
                return;

            var pointStart = e.GetPosition(canvas);

            var margin = new Thickness(pointStart.X, pointStart.Y, 0, 0);

            BeginUndoUnit();
            CreateImageFromPath(imageText, ref margin, imageWidth, "");
        }

        static public Image CreateImageObject(string text, ref Thickness margin, double width, string tooltip)
        {
            Image img = new Image();

            img.Margin = margin;
            img.HorizontalAlignment = HorizontalAlignment.Left;
            img.VerticalAlignment = VerticalAlignment.Top;
            img.IsHitTestVisible = true;

            if (width > 0)
                img.Width = width;

            if (tooltip != null && tooltip.Length > 0)
                img.ToolTip = tooltip;

            BitmapImage bi = new BitmapImage();

            try
            {
                // BitmapImage.UriSource must be in a BeginInit/EndInit block.
                bi.BeginInit();
                bi.UriSource = new Uri(text, UriKind.Absolute);
                bi.EndInit();
            }
            catch (Exception)
            {
                // couldn't make the picture, we don't care why, just ignore it
                return null;
            }

            img.Source = bi;
            return img;
        }

        void CreateImageFromPath(string text, ref Thickness margin, double width, string tooltip)
        {
            Image img = GameMap.CreateImageObject(text, ref margin, width, tooltip);
            WireObject(img);
            SaveFrameworkElement(img);
            AddCanvasChild(img);

            // in an image mode we want the hit rectangle to be on the bottom, so that when we click things they are hit
            // in the old world we added images by clicking and then putting up an edit window, back then
            // the hit rect had to be on top during image creation, that's no longer the case
            //
            // now we use the helper to set the cover rect as needed depending on our mode, if image insertion then
            // the cover rectangle needs to be updated, otherwise it needs to go to the bottom
            CoverCanvasIfNeeded();
        }

        void CreatePopupAndDossier(object sender)
        {
            // we recognize these three types of base elements as possible avatars
            if (sender is Border || sender is Grid || sender is Image)
            {
                FrameworkElement el = sender as FrameworkElement;
                string tip = el.ToolTip as String;
                if (String.IsNullOrEmpty(tip))
                    return;

                var dossier = Main.partyInfo1.DossierDict;
                var party = Main.partyInfo1.PartyDict;

                DisplayDossierSummary(el, tip, party, dossier);
            }            
        }

        void DisplayDossierSummary(FrameworkElement el, string name, Dictionary<string, string> party, Dictionary<string, string> dossier)
        {
            if (party == null || dossier == null || el == null || name == null)
                return;

            if (name.StartsWith("#"))
            {
                // this is a monster... we get the data differently
                DoDossierForMonster(el);
                return;
            }

            string bestName = name.Replace(" ", "_");

            if (!party.ContainsKey(bestName))
            {
                foreach (var k in party.Keys)
                {
                    if (k.Contains(name))
                    {
                        bestName = k;
                        break;
                    }
                }
            }

            if (!party.ContainsKey(bestName))
                return;

            var prefix = bestName + "|";

            ContextMenu m = new ContextMenu();

            var keyList = dossier.Keys.ToList();
            keyList.Sort();

            bool firstNonIndexed = false;

            foreach (var k in keyList)
            {
                if (!k.StartsWith(prefix))
                    continue;

                var val = dossier[k];
                var item = k.Substring(prefix.Length);

                var v = val.ToLower();

                // these things aren't interesting
                if (v.StartsWith("wife:") ||
                    v.StartsWith("husband:") ||
                    v.StartsWith("pets:") ||
                    v.StartsWith("companion:") ||
                    v.StartsWith("child:") ||
                    v.StartsWith("retainer:") ||
                    v.StartsWith("mount:") ||
                    v.StartsWith("love interest:") ||
                    v.StartsWith("loaned initiates:"))
                    continue;

                bool allDigits = IsAllDigits(item);

                if (!firstNonIndexed && !allDigits)
                {
                    firstNonIndexed = true;
                    if (m.Items.Count == 0)
                    {
                        var nameItem = new MenuItem();
                        nameItem.Header = bestName.Replace("_", " ");
                        nameItem.Click += new RoutedEventHandler((sender, args) => Main.vs1.ChangeToNamedPlayer(bestName));
                        m.Items.Add(nameItem);
                    }

                    Main.partyInfo1.AddManaEtcToMenu(bestName, m);
                    Main.readyRolls.AddPlayerRollsToMenu(bestName, m);

                    m.Items.Add(new Separator());
                }

                if (item == "life")
                {
                    continue;
                }

                var mi = new MenuItem();

                if (allDigits)
                {
                    mi.Header = val.Replace("_", " ");
                    if (m.Items.Count == 0)
                    {
                        mi.Click += new RoutedEventHandler((sender, args) => Main.vs1.ChangeToNamedPlayer(bestName));
                    }
                }
                else if (item.StartsWith("combat"))
                {
                    var hdr = val.Replace("_", " ");
                    mi.Header = hdr;

                    // var subItem = new MenuItem();
                    // subItem.Header = "Add Roll";
                    // subItem.Click += new RoutedEventHandler((sender, args) => AddReadyRoll_Click(bestName, hdr));
                    // mi.Items.Add(subItem);

                    mi.Click += new RoutedEventHandler((sender, args) => AddReadyRoll_Click(bestName, hdr));
                }
                else
                {
                    mi.Header = String.Format("{0}: {1}", item.Replace("|", "/").Replace("_", " "), val.Replace("_", " "));
                }

                m.Items.Add(mi);
            }

            DisplayTempPopupMenu(el, m);
        }

        // sample: "2h longspear attack:61 parry:57 sr:4 ap:10 dmg:1d10+1+1d2"
        static Regex combatRegex = new Regex("^(.*) attack:(\\d+) parry:(\\d+) sr:(\\d+) ap:(\\d+) dmg:(.*)$");

        void AddReadyRoll_Click(string name, string line)
        {
            var readyRolls = Main.readyRolls;

            var match = combatRegex.Match(line);
            if (match.Success)
            {
                var wpn = match.Groups[1].ToString().Trim();
                var pct = match.Groups[2].ToString().Trim();
                var sr = match.Groups[4].ToString().Trim();
                var dmg = match.Groups[6].ToString().Trim();
                var key = MakeSaveKey(name, wpn, "attack");

                var attackStrip = new AttackStrip();
                attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: null);
                readyRolls.AppendStripContents(name, attackStrip.Children, key);

                var parryPct = match.Groups[3].ToString().Trim();
                var parryAP = match.Groups[5].ToString().Trim();
                var parryKey = MakeSaveKey(name, wpn, "parry");

                var parryStrip = new ParryStrip();
                parryStrip.Init(name, parryChoice: wpn, ap: parryAP, parryPct: parryPct);
                readyRolls.AppendStripContents(name, parryStrip.Children, parryKey);
            }
        }

        static string MakeSaveKey(string name, string wpn, string action)
        {
            // example save key: "\\Keezheekoni\\_wpn\\fist\\attack"
            var baseKey = "\\" + name + "\\_wpn\\" + wpn + "\\" + action;
            return baseKey.Replace(" ", "_");
        }

        static void DisplayTempPopupMenu(FrameworkElement el, ContextMenu m)
        {
            if (m.Items.Count > 0)
            {
                el.ContextMenu = m;
                m.IsOpen = true;
                // get rid of it when it closes
                m.Closed += new RoutedEventHandler((o, s) => { el.ClearValue(ContextMenuProperty); });
            }
        }

        void DoDossierForMonster(FrameworkElement el)
        {
            string s;
            string tip = el.ToolTip as String;

            if (tip != null && tip.StartsWith("#"))
            {
                tip = tip.Substring(1);
                int space = tip.IndexOf(' ');
                if (space <= 0)
                    return;

                s = tip.Substring(0, space);
            }
            else
            {
                Border b = el as Border;
                if (b == null)
                    return;

                var t = b.Child as TextBlock;
                if (t == null)
                    return;

                s = t.Text;
            }

            ContextMenu m = new ContextMenu();
            Main.readyRolls.AddMonsterRollsToMenu(s, m);
            DisplayTempPopupMenu(el, m);
        }

        bool IsAllDigits(string v)
        {
            foreach (char c in v)
                if (!Char.IsDigit(c))
                    return false;

            return true;
        }

        void DeleteElement(FrameworkElement el)
        {
            if (el == null)
                return;

            UnlinkElement(el);

            AnimateDisappearance(el);
            ClearHandles();
        }

        void UnlinkElement(FrameworkElement el)
        {
            if (el == null)
                return;

            string tag = el.Tag as string;

            // the item might be pending addition, such as a short line
            // in that case there's no unlink from the server to do and
            // it's not yet in our dictionary
            if (tag != null)
            {
                UnlinkKeyDictionaryAndServer(tag);
            }
        }

        void UnlinkKeyDictionaryAndServer(string key)
        {
            waitUpdates = DateTime.Now.AddMilliseconds(waitMilliseconds);

            Main.SendHost(String.Format("del _maps/{0} {1}", currentPath.Text, key));

            if (dictCurrent.ContainsKey(key))
            {
                AddUndoRecord(key, dictCurrent[key], "");

                dictCurrent.Remove(key);
            }
        }

        void new_line_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var pointStart = e.GetPosition(canvas);

            if (IsHardSnapEnabled())
            {
                HardSnapPoint(ref pointStart);
            }

            Line line = new Line();

            SnapPoint(ref pointStart);

            line.X1 = pointStart.X;
            line.X2 = pointStart.X;
            line.Y1 = pointStart.Y;
            line.Y2 = pointStart.Y;

            line.Stroke = strokeBrush;

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            line.StrokeThickness = thickness;
            line.Opacity = opacity;
            line.HorizontalAlignment = HorizontalAlignment.Left;
            line.VerticalAlignment = VerticalAlignment.Top;

            WireObject(line);

            canvas.Children.Add(line);

            // use the helper to get the cover rectangle in the correct place for adding new lines if needed
            CoverCanvasIfNeeded();

            CreateHandles(line);

            // remove the extra line handles for a new line, while it's being dragged 
            RemoveHandleByTag("line_extend_start");
            RemoveHandleByTag("line_extend_end");

            HandleInfo info = GetHandleInfoByTag("line_end");

            // and now we're ready to drag, simulate a mousedown
            e.Handled = false;
            info.isNew = true;
            info.shape.RaiseEvent(e);
        }

        void GetThicknessAndOpacity(out double thickness, out double opacity)
        {
            thickness = thicknessSample.StrokeThickness;
            opacity = opacityRect.Opacity;
        }

        void general_MouseEnter(object sender, MouseEventArgs e)
        {
            FrameworkElement elBase = sender as FrameworkElement;

            Rectangle el = AddBoundingRectangleForElement(elBase, animate: false);

            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(el, new NameScope());

            // Register the name with the page to which the element belongs.
            el.RegisterName("element", el);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(500));

            Anim2Point(story, dur, "element", FrameworkElement.OpacityProperty, 1, 0);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    canvas.Children.Remove(el);
                });

            story.Begin(el);
        }

        // this does the early out tests for if you clicked on an element
        bool TryEarlyOutMouseDown(FrameworkElement el, MouseButtonEventArgs e)
        {
            if (TryEarlyOutMouseDownBasic(el, e))
                return true;

            if (buttonActive == buttonPan)
            {
                pan_MouseDown(rectHit, e);
                return true;
            }

            if (TryExtendOrMoveMassSelection(el, e))
            {
                return true;
            }

            ClearHandles();
            pathPointIndex = -1;

            if (buttonActive == buttonGive)
            {
                GiveElement(el);
                return true;
            }

            return false;
        }

        // these early out tests are suitable for if you clicked on an element or a handle
        bool TryEarlyOutMouseDownBasic(Object sender, MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Middle:
                    // middle button always does a pan... stop right here
                    e.Handled = true;
                    pan_MouseDown(rectHit, e);
                    return true;

                case MouseButton.Right:
                    // not handled, let it go through to pop the menu
                    CreatePopupAndDossier(sender);
                    return true;

                case MouseButton.Left:
                    // let that be processed
                    e.Handled = true;
                    return false;

                default:
                    // disregard any other buttons
                    return true;
            }
        }

        bool TryExtendOrMoveMassSelection(FrameworkElement el, MouseButtonEventArgs e)
        {
            if (selectedList != null)
            {
                if (IsShiftDown())
                {
                    // if the selection doesn't already contain the indicated element then add it
                    if (!selectedList.Contains(el))
                    {
                        selectedList.Add(el);
                        selectedListBoxes.Add(AddBoundingRectangleForElement(el, animate: false));
                    }

                    // if shift is down we won't clear the current selection regardless
                    return true;
                }
                else
                {
                    // shift isn't down but we clicked on something in the selection
                    // we may be starting a move of those items, don't clear the selection
                    if (selectedList.Contains(el))
                    {
                        massmove_MouseDown(el, e);
                        return true;
                    }
                }

                // we're not extending... shift wasn't down... clear the selection
                return false;
            }
            else
            {
                // if shift is down begin a new group selection
                if (handleTarget != null && IsShiftDown() && handleTarget != el)
                {
                    var savedTarget = handleTarget;

                    ClearHandles();
                    selectedList = new List<FrameworkElement>();
                    selectedList.Add(savedTarget);
                    selectedList.Add(el);
                    AddBoundingBoxesForSelection();
                    return true;
                }
            }

            return false;
        }

        void new_arc_MouseDown(object sender, MouseButtonEventArgs e, bool fQuarter)
        {
            // this will make a half arc or a quarter arc, it isn't general path creation
            if (e.ChangedButton != MouseButton.Left)
                return;

            var pointStart = e.GetPosition(canvas);

            if (IsHardSnapEnabled())
            {
                pointStart = new Point(HardSnapCoordinate(pointStart.X), HardSnapCoordinate(pointStart.Y));
            }

            SnapPoint(ref pointStart);

            Point pointEnd;

            if (!fQuarter)
                pointEnd = new Point(pointStart.X + 2, pointStart.Y);
            else
                pointEnd = new Point(pointStart.X + 1, pointStart.Y + 1);

            // Create a path to draw a geometry with.
            Path path = new Path();
            PathGeometry pg = new PathGeometry();
            PathFigure pf = new PathFigure();
            pf.StartPoint = pointStart;

            ArcSegment arc = new ArcSegment(pointEnd, new Size(1, 1), 0, false, SweepDirection.Clockwise, true);
            pf.Segments.Add(arc);
            pg.Figures.Add(pf);
            pg.FillRule = FillRule.EvenOdd;
            path.Data = pg;

            path.Stroke = strokeBrush;

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            path.StrokeThickness = thickness;
            path.Opacity = opacity;
            path.HorizontalAlignment = HorizontalAlignment.Left;
            path.VerticalAlignment = VerticalAlignment.Top;

            WireObject(path);

            canvas.Children.Add(path);

            canvas.Children.Remove(rectHit);
            canvas.Children.Add(rectHit);

            CreateHandles(path);

            var handle = GetHandleByTag("arc_end");

            // simulate a mousedown and begin the drag at the end
            e.Handled = false;
            handle.RaiseEvent(e);
        }

        public void Lock_Click(object sender, RoutedEventArgs e)
        {
            if (handleTarget == null)
                return;

            BeginUndoUnit();
            LockElement(handleTarget);
        }

        void LockElement(FrameworkElement el)
        {
            if (el == null || IsUnmoveable(el))
                return;

            string idOld = el.Tag as string;

            if (idOld == null)
                return;

            int id;
            if (!Int32.TryParse(idOld, out id))
                return;

            id |= 1;
            string idNew = String.Format("{0:000000}", id);

            UnlinkElement(el);
            el.Tag = idNew;
            SaveFrameworkElement(el);
        }

        public void Unlock_Click(object sender, RoutedEventArgs e)
        {
            if (handleTarget == null)
                return;

            BeginUndoUnit();

            bool fResetZOrder = IsShiftDown();
            UnlockElement(handleTarget, fResetZOrder);
        }

        void UnlockElement(FrameworkElement el, bool fResetZOrder)
        {
            if (el == null)
                return;

            if (!fResetZOrder && !IsUnmoveable(el))
                return;

            string idOld = el.Tag as string;

            if (idOld == null)
                return;

            int id;
            if (!Int32.TryParse(idOld, out id))
                return;

            id &= ~1;
            string idNew = String.Format("{0:000000}", id);

            UnlinkElement(el);
            if (fResetZOrder)
                el.Tag = null;
            else
                el.Tag = idNew;

            SaveFrameworkElement(el);
        }

        public void SetTooltip_Click(object sender, RoutedEventArgs e)
        {
            if (handleTarget == null)
                return;

            var target = handleTarget;

            string tip = target.ToolTip as String;

            if (tip == null)
                tip = "";

            ManyKey dlg = new ManyKey("Set Tooltip", "Tooltip:", tip);

            if (dlg.ShowDialog() == true)
            {
                string v = dlg.Results[0];

                if (v == null || v == "")
                    target.ClearValue(ToolTipProperty);
                else
                    target.ToolTip = v;

                BeginUndoUnit();
                SaveFrameworkElement(target);
            }
        }

        public void BulkLayer_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;

            if (mi == null)
                return;

            string s = mi.Name;

            if (s == null || s.Length != 2)
                return;

            int id = 1000 * (s[1] - '0');

            BeginUndoUnit();

            if (selectedList == null)
            {
                // try single item
                NewLayer_Click(sender, e);
                return;
            }

            foreach (FrameworkElement el in selectedList)
            {
                UnlinkElement(el);

                el.Tag = FindFreeIdFromStart(id);

                SaveFrameworkElement(el);

                canvas.Children.Remove(el);
                AddCanvasChild(el);
            }
        }

        public void NewLayer_Click(object sender, RoutedEventArgs e)
        {
            if (handleTarget == null)
                return;

            MenuItem mi = sender as MenuItem;

            if (mi == null)
                return;

            BeginUndoUnit();

            string s = mi.Name;

            if (s == null || s.Length != 2)
                return;

            int id = 1000 * (s[1] - '0');

            UnlinkElement(handleTarget);

            handleTarget.Tag = FindFreeIdFromStart(id);

            SaveFrameworkElement(handleTarget);

            canvas.Children.Remove(handleTarget);
            AddCanvasChild(handleTarget);
        }


        void Group_Click(object sender, RoutedEventArgs e)
        {
            if (handleTarget != null)
            {
                selectedList = new List<FrameworkElement>();
                selectedList.Add(handleTarget);
            }

            if (selectedList == null)
                return;

            BeginUndoUnit();

            var th = new Thickness(Double.PositiveInfinity, Double.PositiveInfinity,
                    Double.NegativeInfinity, Double.NegativeInfinity);

            Rectangle rect = new Rectangle();

            foreach (var el in selectedList)
            {
                ComputeBoundingRect(el, rect);

                if (rect.LayoutTransform != Transform.Identity && rect.LayoutTransform != null)
                {
                    Rect r = new Rect(0, 0, rect.Width, rect.Height);

                    r = rect.LayoutTransform.TransformBounds(r);

                    var rect2 = new Rectangle();
                    rect2.Margin = rect.Margin;
                    rect2.Height = r.Height;
                    rect2.Width = r.Width;

                    rect = rect2;
                }

                if (rect.Margin.Left < th.Left)
                    th.Left = rect.Margin.Left;

                if (rect.Margin.Top < th.Top)
                    th.Top = rect.Margin.Top;

                if (rect.Margin.Left + rect.Width > th.Right)
                    th.Right = rect.Margin.Left + rect.Width;

                if (rect.Margin.Top + rect.Height > th.Bottom)
                    th.Bottom = rect.Margin.Top + rect.Height;
            }

            var g = new Grid();
            g.VerticalAlignment = VerticalAlignment.Top;
            g.HorizontalAlignment = HorizontalAlignment.Left;
            g.Margin = RoundMargin(th.Left + 1, th.Top + 1, 0, 0);
            g.Height = RoundCoordinate(th.Bottom - th.Top - 2);
            g.Width = RoundCoordinate(th.Right - th.Left - 2);
            g.ClipToBounds = true;

            var q = from el in selectedList
                    orderby el.Tag
                    select el;

            foreach (var el in q.ToList())
            {
                UnlinkElement(el);
                UnWireObject(el);
                MoveElement(el, -g.Margin.Left, -g.Margin.Top);
                canvas.Children.Remove(el);
                g.Children.Add(el);
            }

            if (IsShiftDown() || IsCtrlDown())
            {
                g.Background = this.FindResource("Shine1") as Brush;
            }

            WireObject(g);
            AddCanvasChild(g);
            SaveFrameworkElement(g);

            ClearHandles();
            AnimateAppearance(g, true);
        }

        void Ungroup_Click(object sender, RoutedEventArgs e)
        {
            if (handleTarget != null)
            {
                selectedList = new List<FrameworkElement>();
                selectedList.Add(handleTarget);
            }

            if (selectedList == null)
                return;

            BeginUndoUnit();

            List<FrameworkElement> newSelection = new List<FrameworkElement>();

            foreach (var el in selectedList)
            {
                Grid g = el as Grid;

                if (g == null)
                    continue;

                var margin = g.Margin;

                // copy the children to a seperate array because we are going to be
                // iterating over the element contents and removing them and
                // we do not want to be mutating the thing we are iterating.

                var array = new UIElement[g.Children.Count];
                g.Children.CopyTo(array, 0);

                // now enumerate that nice, stable array
                foreach (var child in array)
                {
                    // anything that isn't a framework element, ignore it...
                    FrameworkElement elChild = child as FrameworkElement;

                    if (elChild == null)
                        continue;

                    // adjust the element so that it is relative to the top/left of the group
                    // it has to stand alone now.
                    MoveElement(elChild, margin.Left, margin.Top);

                    // remove the child from the grouping grid
                    g.Children.Remove(child);

                    // turn on its events
                    WireObject(elChild);

                    // add it to the canvas
                    AddCanvasChild(elChild);

                    // its old tag cannot be used... it might be in use already
                    elChild.ClearValue(TagProperty);

                    // and save it
                    SaveFrameworkElement(elChild);

                    // then add it to the selection
                    newSelection.Add(elChild);
                    AnimateAppearance(elChild, addHandles:false);
                }

                canvas.Children.Remove(g);
                UnlinkElement(g);
            }

            // now recreate the selection boxes for the now current selection
            ClearHandles();
            selectedList = newSelection;
            AddBoundingBoxesForSelection();
        }

        void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (handleTarget == null)
                return;

            string id = handleTarget.Tag as string;

            if (id == null)
            {
                menuDictionary["Lock"].IsEnabled = false;
                menuDictionary["Unlock"].IsEnabled = false;

                for (int i = 0; i <= 8; i++)
                {
                    string lcode = "l" + i.ToString();
                    menuDictionary[lcode].IsChecked = false;
                    menuDictionary[lcode].IsCheckable = true;
                    menuDictionary[lcode].IsEnabled = false;
                }

                return;
            }

            menuDictionary["Tag"].Header = id;
            menuDictionary["Tag"].IsEnabled = false;

            bool moveable = !IsUnmoveable(handleTarget);

            for (int i = 0; i <= 8; i++)
            {
                string lcode = "l" + i.ToString();
                menuDictionary[lcode].IsChecked = false;
                menuDictionary[lcode].IsCheckable = true;
                menuDictionary[lcode].IsEnabled = true;
            }

            if (id.Length > 3)
            {
                string lcode = "l" + id[2];
                if (menuDictionary.ContainsKey(lcode))
                    menuDictionary[lcode].IsChecked = true;
            }

            if (IsUnmoveable(handleTarget))
            {
                menuDictionary["Lock"].IsEnabled = false;
                menuDictionary["Unlock"].IsEnabled = true;
                return;
            }
            else
            {
                menuDictionary["Lock"].IsEnabled = true;
                menuDictionary["Unlock"].IsEnabled = false;
                return;
            }
        }

        void UnWireObject(FrameworkElement el)
        {
            if (el == null)
                return;

            if (el is MenuItem)
                return;

            // this nukes all handlers, including general_MouseEnter
            UnwireObjectUsingReflection(el);
        }

        // these are the fields we need to clear the event handler store in the object
        static PropertyInfo storeProperty;
        static FieldInfo entriesField;
        static FieldInfo mapStoreField;

        static void UnwireObjectUsingReflection(FrameworkElement handle)
        {
            const BindingFlags bf = BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Instance;

            if (storeProperty == null)
            {
                storeProperty = handle.GetType().GetProperty("EventHandlersStore", bf | BindingFlags.GetProperty);
            }

            // find EventHandlerStore
            var store = storeProperty.GetValue(handle, null);

            if (entriesField == null)
            {
                entriesField = store.GetType().GetField("_entries", bf | BindingFlags.GetField);
            }

            // find _entries --> this is a valuetype so we're getting a boxed copy
            var entries = entriesField.GetValue(store);

            if (mapStoreField == null)
            {
                mapStoreField = entries.GetType().GetField("_mapStore", bf | BindingFlags.GetField);
            }

            // clobber the map store field so it's empty
            mapStoreField.SetValue(entries, null);

            // replace the value field entries in the event handlers store, this is the actual mutation
            entriesField.SetValue(store, entries);
        }

        void WireObject(FrameworkElement el)
        {
            if (el == null)
                return;

            if (el is MenuItem)
                return;

            el.MouseEnter += new MouseEventHandler(general_MouseEnter);

            WireObjectForMove(el);
        }

        void WireObjectForMove(FrameworkElement el)
        {
            // these variable capture the needed state to do the drag operation
            Point pointStart = new Point(0, 0);
            Point refStart = new Point(0, 0);
            bool dirty = false;

            MouseButtonEventHandler existing_MouseDown = (sender, e) =>
            {
                if (TryEarlyOutMouseDown(el, e))
                    return;

                // we remember where the mouse went down and where a certain reference point on the object is
                pointStart = e.GetPosition(canvas);
                refStart = ReferencePoint(el);

                if (el is Path)
                {
                    pointPathLastClick = pointStart;
                }

                BeginMouseCapture(sender, e);
            };

            MouseEventHandler existing_MouseMove = (sender, e) =>
            {
                if (moving != sender)
                    return;

                if (IsUnmoveable(el))
                    return;

                // now figure out where the mouse is
                var pointEnd = e.GetPosition(canvas);

                // compare how much the mouse has moved with how much the reference point has moved
                Vector deltaRequired = pointEnd - pointStart;
                Vector deltaNow = ReferencePoint(el) - refStart;

                // move the object whatever the difference is
                Vector delta = deltaRequired - deltaNow;
             
                if (delta.X != 0 || delta.Y != 0)
                    dirty = true;

                MoveElement(el, delta.X, delta.Y); 
            };

            MouseButtonEventHandler existing_MouseUp = (sender, e) =>
            {
                DragEditCompleted(handle:el, target:el, dirty:dirty);
            };

            el.MouseDown += existing_MouseDown;
            el.MouseUp += existing_MouseUp;
            el.MouseMove += existing_MouseMove;
        }

        Rectangle AddBoundingRectangleForElement(FrameworkElement elBase, bool animate)
        {
            Rectangle boundingRect = new Rectangle();

            if (IsUnmoveable(elBase))
                boundingRect.Stroke = Brushes.Red;
            else
                boundingRect.Stroke = Brushes.Blue;

            ComputeBoundingRect(elBase, boundingRect);

            canvas.Children.Add(boundingRect);

            if (animate)
                AnimateAppearance(boundingRect, addHandles: false);

            return boundingRect;
        }

        void AddFormation_Click(object sender, RoutedEventArgs e)
        {
            if (selectedList == null)
                return;

            if (selectedList.Count < 2)
                return;

            List<FrameworkElement> fList;
            Thickness thickness;
            ComputeSelectedPlayTiles(out fList, out thickness);

            Grid formation = new Grid();

            foreach (FrameworkElement el in fList)
            {
                System.IO.StringWriter sw = new System.IO.StringWriter();
                System.Windows.Markup.XamlWriter.Save(el, sw);
                string s = sw.ToString();

                byte[] byteData = Encoding.ASCII.GetBytes(s);
                FrameworkElement elNew = (FrameworkElement)System.Windows.Markup.XamlReader.Load(new System.IO.MemoryStream(byteData));

                elNew.Margin = RoundMargin(elNew.Margin.Left - thickness.Left, elNew.Margin.Top - thickness.Top, 0, 0);

                formation.Children.Add(elNew);
            }

            BeginUndoUnit();

            MenuItem m = new MenuItem();
            m.Header = formation;
            m.Click += new RoutedEventHandler(Formation_Click);
            formations.Items.Add(new Separator());
            formations.Items.Add(m);
            SaveFrameworkElement(formations);
        }

        void ComputeSelectedPlayTiles(out List<FrameworkElement> fList, out Thickness th)
        {
            fList = new List<FrameworkElement>();

            th = new Thickness(Double.PositiveInfinity, Double.PositiveInfinity,
                                Double.NegativeInfinity, Double.NegativeInfinity);

            if (selectedList == null)
                return;

            foreach (FrameworkElement el in selectedList)
            {
                if (IsUnmoveable(el))
                    continue;

                if (el is Image || el is Border || el is Grid)
                {
                    if (el.Width > 100 || el.Width < 6 || el.Width > 100 || el.Width < 6)
                        continue;

                    fList.Add(el);
                }
            }

            if (fList.Count < 2)
            {
                fList.Clear();
                return;
            }

            foreach (FrameworkElement el in fList)
            {
                if (el.Margin.Left < th.Left)
                    th.Left = el.Margin.Left;

                if (el.Margin.Top < th.Top)
                    th.Top = el.Margin.Top;

                if (el.Margin.Left + el.ActualWidth > th.Right)
                    th.Right = el.Margin.Left + el.ActualWidth;

                if (el.Margin.Top + el.ActualHeight > th.Bottom)
                    th.Bottom = el.Margin.Top + el.ActualHeight;
            }
        }

        void XFlipFormation_Click(object sender, RoutedEventArgs e)
        {
            List<FrameworkElement> fList;
            Thickness thickness;
            ComputeSelectedPlayTiles(out fList, out thickness);

            ClearHandles();

            BeginUndoUnit();

            foreach (FrameworkElement el in fList)
            {
                Thickness tNew = el.Margin;
                double dx = tNew.Left - thickness.Left;
                tNew.Left = thickness.Right - dx - el.ActualWidth;

                AnimateElementLocation(el, tNew);
            }
        }

        void YFlipFormation_Click(object sender, RoutedEventArgs e)
        {
            List<FrameworkElement> fList;
            Thickness thickness;
            ComputeSelectedPlayTiles(out fList, out thickness);

            ClearHandles();

            BeginUndoUnit();

            foreach (FrameworkElement el in fList)
            {
                Thickness tNew = el.Margin;
                double dy = tNew.Top - thickness.Top;
                tNew.Top = thickness.Bottom - dy - el.ActualHeight;

                AnimateElementLocation(el, tNew);
            }
        }

        void XYFlipFormation_Click(object sender, RoutedEventArgs e)
        {
            List<FrameworkElement> fList;
            Thickness thickness;
            ComputeSelectedPlayTiles(out fList, out thickness);

            ClearHandles();

            BeginUndoUnit();

            foreach (FrameworkElement el in fList)
            {
                Thickness tNew = el.Margin;

                double dx = tNew.Left - thickness.Left;
                double dy = tNew.Top - thickness.Top;

                tNew.Top = thickness.Top + dx;
                tNew.Left = thickness.Left + dy;

                AnimateElementLocation(el, tNew);
            }
        }


        void Formation_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            Grid formation = m.Header as Grid;

            if (formation == null)
                return;

            if (Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                int idx = formations.Items.IndexOf(m);

                // seperator and the item
                formations.Items.RemoveAt(idx - 1);
                formations.Items.RemoveAt(idx - 1);
                SaveFrameworkElement(formations);
                return;
            }

            List<FrameworkElement> fList;
            Thickness thickness;
            ComputeSelectedPlayTiles(out fList, out thickness);

            ClearHandles();

            BeginUndoUnit();

            foreach (FrameworkElement el in fList)
            {
                foreach (UIElement ui in formation.Children)
                {
                    FrameworkElement elSaved = ui as FrameworkElement;

                    if (elSaved == null)
                        continue;

                    if (elSaved.ToolTip as String == el.ToolTip as String)
                    {
                        Thickness tNew = RoundMargin(thickness.Left + elSaved.Margin.Left, thickness.Top + elSaved.Margin.Top, 0, 0);

                        AnimateElementLocation(el, tNew);
                        break;
                    }
                }
            }
        }

        string imageText;
        double imageWidth;

        public void AccessLibrary_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AccessLibraryDlg(fFiles: false);

            string textBase = dlg.GetLibraryResult();

            if (textBase == null)
                return;

            Grid animContainer = (Grid)((Grid)this.Parent).Parent;
            var p1 = new Point(animContainer.ActualWidth / 2, animContainer.ActualHeight / 8);
            var pLocal = animContainer.TranslatePoint(p1, canvas);

            Thickness margin = new Thickness(pLocal.X, pLocal.Y, 0, 0);

            var text = Config.uploads_mapping + textBase;

            BeginUndoUnit();
            imageText = text;
            imageWidth = dlg.ImageWidth;
            CreateImageFromPath(text, ref margin, width: dlg.ImageWidth, tooltip: "");
            ClearHandles();
        }

        void UploadImages_Click(object sender, RoutedEventArgs e)
        {
            OpenAndIterateImages((string file, StringBuilder errors) => UploadImage(file, errors));
        }

        void OpenAndIterateImages(Action<string, StringBuilder> action)
        {
            StringBuilder errors = new StringBuilder();

            // Configure open file dialog box
            var dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.Title = "Select Image";
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".jpg"; // Default file extension
            dlg.Filter = "Images (.jpg)|*.jpg"; // Filter files by extension
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.Multiselect = true;

            // Show open file dialog box
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                foreach (var file in dlg.FileNames)
                {
                    action(file, errors);
                }

                var results = new ResultSummary();
                results.Owner = Main;
                results.HorizontalAlignment = HorizontalAlignment.Center;
                results.VerticalAlignment = VerticalAlignment.Center;
                results.CustomInit(errors);
                results.ShowDialog();
            }
        }

        void UploadImage(string path, StringBuilder errors)
        {
            byte[] bytes = null;
            try
            {
                bytes = System.IO.File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                errors.AppendLine(e.Message);
                return;
            }

            string file = System.IO.Path.GetFileName(path).Replace(" ", "_");
            Main.SendUpload("gameaid", bytes, file);

            errors.AppendFormat("{0} uploaded to {1}.\n", path, "gameaid");
        }

        public void AccessUploadedImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AccessLibraryDlg(fFiles: true);
            dlg.Title = "Access Uploaded Image";

            string textBase = dlg.GetLibraryResult();

            if (textBase == null)
                return;

            Grid animContainer = (Grid)((Grid)this.Parent).Parent;
            var p1 = new Point(animContainer.ActualWidth / 2, animContainer.ActualHeight / 8);
            var pLocal = animContainer.TranslatePoint(p1, canvas);

            Thickness margin = new Thickness(pLocal.X, pLocal.Y, 0, 0);

            string text = Config.uploads_gameaid + textBase;

            BeginUndoUnit();
            imageText = text;
            imageWidth = dlg.ImageWidth;
            CreateImageFromPath(text, ref margin, width: imageWidth, tooltip: textBase);
            ClearHandles();
        }

        public void AccessMiscImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AccessMiscImageDlg();

            if (dlg.ShowDialog() != true)
                return;            

            if (dlg.m_results.Children.Count != 1)
                return;

            var panel = dlg.m_results.Children[0] as StackPanel;

            if (panel == null || panel.Children.Count < 2)
                return;

            var textBlock = panel.Children[0] as TextBlock;

            if (textBlock == null)
                return;

            var text = textBlock.Text;

            Grid animContainer = (Grid)((Grid)this.Parent).Parent;
            var p1 = new Point(animContainer.ActualWidth / 2, animContainer.ActualHeight / 8);
            var pLocal = animContainer.TranslatePoint(p1, canvas);

            Thickness margin = new Thickness(pLocal.X, pLocal.Y, 0, 0);

            BeginUndoUnit();
            imageText = text;
            imageWidth = dlg.ImageWidth;
            CreateImageFromPath(text, ref margin, width: dlg.ImageWidth, tooltip: "");
            ClearHandles();
        }

        public void ImportImages_Click(object sender, RoutedEventArgs e)
        {
            string all = null;

            var dlg = new ImportImageDialog();
            if (dlg.ShowDialog() != true)
                return;

            string path = dlg.url.Text;
            double width = 0;
            Double.TryParse(dlg.width.Text, out width);

            if (!path.StartsWith("http://"))
                return;

            if (!path.EndsWith("/"))
                path = path + '/';

            int iSlash = path.IndexOf('/', 7);
            if (iSlash < 0)
                return;

            string urlBase = path.Substring(0, iSlash + 1);

            try
            {
                Uri targetUri = new Uri(path);
                var fr = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(targetUri);

                var resp = fr.GetResponse();
                var stm = resp.GetResponseStream();
                var sr = new System.IO.StreamReader(stm);
                all = sr.ReadToEnd();
            }
            catch (System.Net.WebException)
            {
                return;
            }

            BeginUndoUnit();

            int ich = 0;

            Thickness margin = RoundMargin(0, 0, 0, 0);

            double d = 0;
            Double.TryParse(dlg.startX.Text, out d);
            margin.Left = d;

            d = 0;
            Double.TryParse(dlg.startY.Text, out d);
            margin.Top = d;

            double widthSkip = 100;
            if (width > 0)
                widthSkip = 10 + width;

            for (; ; )
            {
                int ichT = all.IndexOf("HREF=\"", ich);

                if (ichT < 0)
                    break;

                int ichE = all.IndexOf('"', ichT + 6);

                if (ichE < 0)
                    break;

                ich = ichE + 1;

                string href = all.Substring(ichT + 6, ichE - ichT - 6);

                if (href.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                    href.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    href.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    href.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                {
                    if (href.StartsWith("http://"))
                    {
                        // nothing needed
                    }
                    else if (href.StartsWith("/"))
                    {
                        href = urlBase + href;
                    }
                    else
                    {
                        href = path + href;
                    }

                    int iEnd = href.LastIndexOf('/');
                    string tooltip = "";

                    if (iEnd >= 0)
                    {
                        tooltip = href.Substring(iEnd + 1, href.Length - iEnd - 1 - 4);
                    }

                    CreateImageFromPath(href, ref margin, width, tooltip);

                    margin.Left = margin.Left + widthSkip;
                }
            }
        }

        internal void SetGiveTarget(GameMap map1)
        {
            buttonGive.Visibility = Visibility.Visible;
            giveTarget = map1;
        }

        void GiveElement(FrameworkElement elGiving)
        {
            if (elGiving == null)
                return;

            Grid animContainer = (Grid)((Grid)this.Parent).Parent;
            Point pStartLocal;
            Point pStartAnimContainer;

            if (!GetStartPoints(elGiving, animContainer, out pStartLocal, out pStartAnimContainer))
                return;

            Point pEndAnimContainer;
            Point pEndLocal;
            GetEndPoints(animContainer, out pEndAnimContainer, out pEndLocal, giveTarget);

            string s = SerializeForEndLocal(elGiving, pStartLocal, pEndLocal);

            FrameworkElement elCloned = CloneElementForUseInAnimation(elGiving, animContainer, pStartAnimContainer, s);

            AnimateElementToFinalPosition(animContainer, pStartAnimContainer, pEndAnimContainer, s, elCloned, giveTarget);
        }

        internal void AcceptLibraryToken(string hitloc, string siz, string number, string path, int index)
        {
            string tooltip = String.Format("#{0} Body Type: {1} Size: {2}", index + 1, hitloc, siz);

            double scale = 1;

            int size = 15;
            if (!Int32.TryParse(siz, out size))
                size = 15;

            if (size <= 5)
            {
                scale = .75;
            }

            if (size >= 20)
            {
                int c = size / 5 - 3;
                scale = 1 + .25 * c;
            }

            int width = (int)(18 * scale);

            Grid animContainer = (Grid)((Grid)this.Parent).Parent;
            var p1 = new Point(animContainer.ActualWidth / 2, animContainer.ActualHeight / 8);

            var pLocal = animContainer.TranslatePoint(p1, canvas);
            double dx = -animContainer.ActualWidth / 4 + 40 * (index % 20);
            double dy = 40 * (index / 20);

            Thickness margin = new Thickness(pLocal.X + dx, pLocal.Y + dy, 0, 0);

            CreateImageFromPath(path, ref margin, width: width, tooltip: tooltip);
            ClearHandles();
        }

        internal void AcceptGeneratedToken(string hitloc, string siz, string number, Brush fill, int index)
        {
            string baseXaml = "<Border BorderThickness=\"0.1,0.1,1,1\" Padding=\"1,0,1,0\" CornerRadius=\"9,9,9,9\" BorderBrush=\"{DynamicResource ShadowBrush}\" Width=\"18\" Height=\"18\" Margin=\"0,0,0,0\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" ToolTip=\"ToolTipSlug\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><TextBlock Text=\"TextSlug\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\" /></Border>";

            string name = String.Format("#{0} Body Type: {1} Size: {2}", index + 1, hitloc, siz);

            baseXaml = baseXaml.Replace("ToolTipSlug", name);
            baseXaml = baseXaml.Replace("TextSlug", number);

            FrameworkElement el = TryParseXaml(baseXaml);
            if (el == null)
                return;

            Border b = el as Border;
            b.Background = fill;

            Grid animContainer = (Grid)((Grid)this.Parent).Parent;
            Point pStartLocal;
            Point pStartAnimContainer;

            int size = 15;
            if (!Int32.TryParse(siz, out size))
                size = 15;

            if (size <= 5)
            {
                el.LayoutTransform = new ScaleTransform(.75, .75);
            }

            if (size >= 20)
            {
                int c = size / 5 - 3;
                el.LayoutTransform = new ScaleTransform(1 + .25 * c, 1 + .25 * c);
            }

            animContainer.Children.Add(el);

            if (!GetStartPoints(el, animContainer, out pStartLocal, out pStartAnimContainer))
                return;

            Point pEndAnimContainer;
            Point pEndLocal;
            GetEndPoints(animContainer, out pEndAnimContainer, out pEndLocal, this);

            double dx = -animContainer.ActualWidth / 4 + 40 * (index % 20);
            double dy = 40 * (index / 20);

            pEndLocal = new Point(pEndLocal.X + dx, pEndLocal.Y + dy);

            string s = SerializeForEndLocal(el, pStartLocal, pEndLocal);

            // AnimateElementToFinalPosition(animContainer, pStartAnimContainer, pEndAnimContainer, s, f, this);
            animContainer.Children.Remove(el);
            AcceptElement(s);
        }

        internal static bool GetStartPoints(FrameworkElement el, FrameworkElement animContainer, out Point pStartLocal, out Point pStartAnimContainer)
        {
            pStartLocal = new Point();
            pStartAnimContainer = new Point();
            Line l = el as Line;
            Path p = el as Path;

            if (l != null)
            {
                pStartLocal = new Point(l.X1, l.Y1);
            }
            else if (p != null)
            {
                PathFigure pf;
                if (!TryExtractPathFigure(p, out pf))
                {
                    pStartLocal = pf.StartPoint;
                    return false;
                }
            }
            else
                pStartLocal = new Point(el.Margin.Left, el.Margin.Top);

            var local = el.Parent as FrameworkElement;

            if (local == null)
                return false;

            pStartAnimContainer = local.TranslatePoint(pStartLocal, animContainer);
            return true;
        }

        internal void GetEndPoints(Grid animContainer, out Point pEndAnimContainer, out Point pEndLocal, GameMap giveTarget)
        {
            pEndAnimContainer = new Point(animContainer.ActualWidth / 2, animContainer.ActualHeight / 8);
            pEndLocal = animContainer.TranslatePoint(pEndAnimContainer, giveTarget.canvas);
        }

        internal static string SerializeForEndLocal(FrameworkElement el, Point pStartLocal, Point pEndLocal)
        {
            Line l = el as Line;
            Path p = el as Path;

            if (l != null)
            {
                MoveLineNoSnap(l, pEndLocal);
            }
            else if (p != null)
            {
                MovePath(p, pEndLocal);
            }
            else
            {
                el.Margin = RoundMargin(pEndLocal.X, pEndLocal.Y, 0, 0);
            }

            System.IO.StringWriter sw = new System.IO.StringWriter();
            System.Windows.Markup.XamlWriter.Save(el, sw);

            if (l != null)
            {
                MoveLineNoSnap(l, pStartLocal);
            }
            else if (p != null)
            {
                MovePath(p, pStartLocal);
            }
            else
            {
                el.Margin = RoundMargin(pStartLocal.X, pStartLocal.Y, 0, 0);
            }

            return sw.ToString();
        }

        internal void AnimateElementToFinalPosition(Grid parent, Point pStartAnimContainer, Point pEndAnimContainer, string s, FrameworkElement el, GameMap giveTarget)
        {
            Line l = el as Line;
            Path p = el as Path;

            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(MainWindow.mainWindow, new NameScope());

            var rot = new RotateTransform();
            var trans = new TranslateTransform();
            var scale = new ScaleTransform();
            var group = new TransformGroup();

            group.Children.Add(rot);
            group.Children.Add(scale);
            group.Children.Add(trans);
            el.RenderTransform = group;

            // Register the name with the page to which the element belongs.
            MainWindow.mainWindow.RegisterName("rot", rot);
            MainWindow.mainWindow.RegisterName("trans", trans);
            MainWindow.mainWindow.RegisterName("scale", scale);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(1000));

            Anim2Point(story, dur, "rot", RotateTransform.AngleProperty, 0, 360);
            Anim2Point(story, dur, "trans", TranslateTransform.XProperty, 0, pEndAnimContainer.X - pStartAnimContainer.X);
            Anim2Point(story, dur, "trans", TranslateTransform.YProperty, 0, pEndAnimContainer.Y - pStartAnimContainer.Y);

            if (l != null)
            {
                rot.CenterX = (l.X1 + l.X2) / 2;
                rot.CenterY = (l.Y1 + l.Y2) / 2;
                scale.CenterX = rot.CenterX;
                scale.CenterY = rot.CenterY;
            }
            else if (p != null)
            {
                rot.CenterX = pStartAnimContainer.X;
                rot.CenterY = pStartAnimContainer.Y;
                scale.CenterX = rot.CenterX;
                scale.CenterY = rot.CenterY;
            }

            if (l == null)
            {
                double z1 = GetCurrentZoom();
                double z2 = z1 * 2;
                double z3 = giveTarget.GetCurrentZoom();

                var kt0 = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0));
                var kt1 = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500));
                var kt2 = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000));

                Anim3Point(story, z1, z2, z3, ref kt0, ref kt1, ref kt2, "scale", ScaleTransform.ScaleXProperty);
                Anim3Point(story, z1, z2, z3, ref kt0, ref kt1, ref kt2, "scale", ScaleTransform.ScaleYProperty);
            }

            story.Completed += new EventHandler(
                (object sender, EventArgs e) =>
                {
                    giveTarget.AcceptElement(s);
                    parent.Children.Remove(el);
                });

            story.Begin(MainWindow.mainWindow);
        }

        internal static FrameworkElement CloneElementForUseInAnimation(FrameworkElement elSource, Grid animContainer, Point pStartAnimContainer, string s)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(s);
            FrameworkElement elClone = (FrameworkElement)System.Windows.Markup.XamlReader.Load(new System.IO.MemoryStream(byteData));

            Line l = elClone as Line;
            Path p = elClone as Path;

            if (l != null)
            {
                MoveLineNoSnap(l, pStartAnimContainer);
            }
            else if (p != null)
            {
                MovePath(p, pStartAnimContainer);
                elClone.VerticalAlignment = VerticalAlignment.Top;
                elClone.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                elClone.Margin = RoundMargin(pStartAnimContainer.X, pStartAnimContainer.Y, 0, 0);
                elClone.VerticalAlignment = VerticalAlignment.Top;
                elClone.HorizontalAlignment = HorizontalAlignment.Left;
                elClone.Width = elSource.ActualWidth;
                elClone.Height = elSource.ActualHeight;
            }

            animContainer.Children.Add(elClone);
            return elClone;
        }

        void AcceptElement(string v)
        {
            FrameworkElement element = TryParseXaml(v);
            if (element == null)
                return;

            BeginUndoUnit();

            string tag = element.Tag as String;
            element.ClearValue(TagProperty);
            element.ClearValue(ContextMenuProperty);

            int id;

            if (tag != null && Int32.TryParse(tag, out id))
            {
                // keep same layer
                element.Tag = FindFreeIdFromStart(id / 1000 * 1000);
            }

            WireObject(element);
            SaveFrameworkElement(element);
            AddCanvasChild(element);
        }

        internal static FrameworkElement TryParseXaml(string v)
        {
            v = v.Replace("<Separator Style=\"{DynamicResource {x:Static MenuItem.SeparatorStyleKey}}\" />", "");

            byte[] byteData = Encoding.ASCII.GetBytes(v);

            object el = null;

            try
            {
                el = System.Windows.Markup.XamlReader.Load(new System.IO.MemoryStream(byteData));
            }
            catch (Exception)
            {
                return null;
            }

            return el as FrameworkElement;
        }

        void ItemLock_Click(object sender, RoutedEventArgs e)
        {
            BeginUndoUnit();

            if (selectedList != null)
                foreach (FrameworkElement el in selectedList)
                    LockElement(el);

            if (handleTarget != null)
                LockElement(handleTarget);
        }

        void ItemUnlock_Click(object sender, RoutedEventArgs e)
        {
            BeginUndoUnit();

            bool fResetZOrder = Keyboard.IsKeyDown(Key.RightShift) || Keyboard.IsKeyDown(Key.LeftShift);

            if (selectedList != null)
                foreach (FrameworkElement el in selectedList)
                    UnlockElement(el, fResetZOrder);

            if (handleTarget != null)
                UnlockElement(handleTarget, fResetZOrder);
        }

        void ItemSnap_Click(object sender, RoutedEventArgs e)
        {
            BeginUndoUnit();

            if (selectedList != null)
                foreach (FrameworkElement el in selectedList)
                    SnapElement(el);

            if (handleTarget != null)
                SnapElement(handleTarget);
        }

        const double PathTolerance = 40 * 40;

        void CleanupLines_Click(object sender, RoutedEventArgs e)
        {
            if (selectedList == null)
                return;

            BeginUndoUnit();

            var lines = new List<Line>();

            foreach (FrameworkElement el in selectedList)
            {
                Line l = el as Line;
                if (l != null)
                    lines.Add(l);
            }

            if (lines.Count < 2)
                return;

            for (int i = 0; i < lines.Count; i++)
            {
                Line l = lines[i];
                Point p1 = new Point(lines[i].X1, lines[i].Y1);
                AdjustLinesToPoint(lines, p1);
                Point p2 = new Point(lines[i].X2, lines[i].Y2);
                AdjustLinesToPoint(lines, p2);
            }
        }

        void AdjustLinesToPoint(List<Line> lines, Point p)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                Line l = lines[i];

                double d1 = DistanceSquared(p.X, l.X1, p.Y, l.Y1);
                double d2 = DistanceSquared(p.X, l.X2, p.Y, l.Y2);

                int line_end = 1;
                double d = d1;

                if (d2 < d) { d = d2; line_end = 2; }

                if (d == 0)
                {
                    continue;
                }

                if (d < PathTolerance)
                {
                    if (line_end == 1)
                    {
                        l.X1 = p.X;
                        l.Y1 = p.Y;

                    }
                    else
                    {
                        l.X2 = p.X;
                        l.Y2 = p.Y;
                    }

                    SaveFrameworkElement(l);
                }
            }
        }


        void MakePath_Click_BeeOptimized(object sender, RoutedEventArgs e)
        {
            if (selectedList == null)
                return;

            BeginUndoUnit();

            var lines = new List<Line>();

            foreach (FrameworkElement el in selectedList)
            {
                Line l = el as Line;
                if (l != null)
                    lines.Add(l);
            }

            if (lines.Count < 2)
                return;

            Path path = LineToPath.FindOptimalPath(lines);

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            path.Fill = fillBrush;
            path.Stroke = strokeBrush;
            path.StrokeThickness = thickness;
            path.Opacity = opacity;
            path.HorizontalAlignment = HorizontalAlignment.Left;
            path.VerticalAlignment = VerticalAlignment.Top;

            WireObject(path);
            AddCanvasChild(path);
            AnimateAppearance(path, true);

            foreach (Line l in lines)
            {
                DeleteElement(l);
            }
        }

        void MakePath_Click(object sender, RoutedEventArgs e)
        {
            ConvertSelectionToPath();
        }

        Path ConvertLineToPath(Line line)
        {
            BeginUndoUnit();

            // Create a path to draw a geometry with.
            Path path = new Path();
            PathGeometry pg = new PathGeometry();
            PathFigure pf = new PathFigure();
            pf.StartPoint = new Point(line.X1, line.Y1);
            var seg = new LineSegment(new Point(line.X2, line.Y2), true);
            pf.Segments.Add(seg);
            pg.Figures.Add(pf);
            pg.FillRule = FillRule.EvenOdd;
            path.Data = pg;

            path.Stroke = line.Stroke;
            path.StrokeThickness = line.StrokeThickness;
            path.Opacity = line.Opacity;

            path.HorizontalAlignment = HorizontalAlignment.Left;
            path.VerticalAlignment = VerticalAlignment.Top;

            WireObject(path);
            AddCanvasChild(path);

            DeleteElement(line);

            return path;
        }
        
        Path ConvertSelectionToPath()
        {
            if (selectedList == null)
                return null;

            BeginUndoUnit();

            var lines = new List<Line>();
            var toDelete = new List<Line>();

            foreach (FrameworkElement el in selectedList)
            {
                Line l = el as Line;
                if (l != null)
                    lines.Add(l);
            }

            var points = new List<Point>();

            points.Add(new Point(lines[0].X1, lines[0].Y1));
            points.Add(new Point(lines[0].X2, lines[0].Y2));

            toDelete.Add(lines[0]);
            lines.RemoveAt(0);

            for (;;)
            {
                int line_end_min = -1;
                int iline_min = -1;
                double dMin = Double.PositiveInfinity;

                for (int i = 0; i < lines.Count; i++)
                {
                    Line l = lines[i];
                    int c = points.Count - 1;

                    double d1 = DistanceSquared(points[0].X, l.X1, points[0].Y, l.Y1);
                    double d2 = DistanceSquared(points[c].X, l.X1, points[c].Y, l.Y1);
                    double d3 = DistanceSquared(points[0].X, l.X2, points[0].Y, l.Y2);
                    double d4 = DistanceSquared(points[c].X, l.X2, points[c].Y, l.Y2);

                    int line_end = 1;
                    double d = d1;
                    if (d2 < d) { d = d2; line_end = 2; }
                    if (d3 < d) { d = d3; line_end = 3; }
                    if (d4 < d) { d = d4; line_end = 4; }

                    if (d < dMin)
                    {
                        dMin = d;
                        line_end_min = line_end;
                        iline_min = i;
                    }
                }

                if (dMin < PathTolerance)
                {
                    Line l = lines[iline_min];
                    toDelete.Add(lines[iline_min]);
                    lines.RemoveAt(iline_min);

                    Point p = new Point();
                    switch (line_end_min)
                    {
                        case 1:
                            p.X = l.X2;
                            p.Y = l.Y2;
                            points.Insert(0, p);
                            break;
                        case 2:
                            p.X = l.X2;
                            p.Y = l.Y2;
                            points.Add(p);
                            break;
                        case 3:
                            p.X = l.X1;
                            p.Y = l.Y1;
                            points.Insert(0, p);
                            break;
                        case 4:
                            p.X = l.X1;
                            p.Y = l.Y1;
                            points.Add(p);
                            break;
                    }
                }
                else
                {
                    break;
                }
            }

            // Create a path to draw a geometry with.
            Path path = new Path();
            PathGeometry pg = new PathGeometry();
            PathFigure pf = new PathFigure();
            pf.StartPoint = points[0];

            for (int i = 1; i < points.Count; i++)
            {
                if (i == points.Count - 1)
                {
                    if (DistanceSquared(points[i], points[0]) < PathTolerance)
                    {
                        pf.IsClosed = true;
                        pf.IsFilled = true;
                        path.Fill = fillBrush;
                        break;
                    }
                }

                var seg = new LineSegment(points[i], true);
                pf.Segments.Add(seg);
            }

            pg.Figures.Add(pf);
            pg.FillRule = FillRule.EvenOdd;
            path.Data = pg;

            path.Stroke = strokeBrush;

            double thickness;
            double opacity;
            GetThicknessAndOpacity(out thickness, out opacity);

            path.StrokeThickness = thickness;
            path.Opacity = opacity;
            path.HorizontalAlignment = HorizontalAlignment.Left;
            path.VerticalAlignment = VerticalAlignment.Top;

            WireObject(path);
            AddCanvasChild(path);
            AnimateAppearance(path, true);

            foreach (Line l in toDelete)
            {
                DeleteElement(l);
            }

            return path;
        }

        void AddGrid_Click(object sender, RoutedEventArgs e)
        {
            BeginUndoUnit();

            var grid = new Tile();

            grid.IsHitTestVisible = false;
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.VerticalAlignment = VerticalAlignment.Top;
            grid.Margin = RoundMargin(0, 0, 0, 0);
            grid.Rows = 50;
            grid.Columns = 50;

            grid.Height = 2000;
            grid.Width = 2000;
            grid.Tag = FindFreeIdFromStart(0);

            WireObject(grid);
            AddCanvasChild(grid);
            SaveFrameworkElement(grid);
        }

        void MakeRectTile_Click(object sender, RoutedEventArgs e)
        {
            BeginUndoUnit();
            MakeTiles(18, 4);
        }

        void MakeRoundTile_Click(object sender, RoutedEventArgs e)
        {
            BeginUndoUnit();
            MakeTiles(18, 9);
        }

        void MakeTiles(double width, double corner)
        {
            if (selectedList != null)
                foreach (FrameworkElement el in selectedList)
                    WrapInBorder(handleTarget, width, corner);

            if (handleTarget != null)
                WrapInBorder(handleTarget, width, corner);

            ClearHandles();
        }

        void WrapInBorder(FrameworkElement handleTarget, double width, double corner)
        {
            BeginUndoUnit();

            if (handleTarget is TextBlock)
                MakeImageTile(handleTarget, 0, corner); // do not force the size for text
            else
                MakeImageTile(handleTarget, width, corner);
        }

        void MakeImageTile(FrameworkElement handleTarget, double width, double radius)
        {
            Image im = handleTarget as Image;
            Grid g = handleTarget as Grid;
            TextBlock text = handleTarget as TextBlock;
            if (im == null && g == null && text == null)
                return;

            var b = new Border();
            b.IsHitTestVisible = true;
            b.BorderBrush = this.FindResource("ShadowBrush") as Brush;
            b.BorderThickness = new Thickness(.1, .1, 1, 1);
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.VerticalAlignment = VerticalAlignment.Top;
            b.CornerRadius = new CornerRadius(radius);
            b.Margin = handleTarget.Margin;

            double scale = 0;

            if (width == 0)
            {
                b.Width = handleTarget.ActualHeight;
                b.Height = handleTarget.ActualHeight;
            }
            else if (handleTarget.ActualWidth > handleTarget.ActualHeight)
            {
                scale = width / handleTarget.ActualWidth;
                b.Width = width;
                b.Height = width * (handleTarget.ActualHeight / handleTarget.ActualWidth);
            }
            else
            {
                scale = width / handleTarget.ActualHeight;
                b.Height = width;
                b.Width = width * (handleTarget.ActualWidth / handleTarget.ActualHeight);
            }

            canvas.Children.Remove(handleTarget);

            if (im != null)
            {
                b.Background = new ImageBrush(im.Source);
            }
            else
            {
                if (g != null)
                {
                    if (g.Background == null)
                        g.Background = this.FindResource("Shine1") as Brush;
                    b.Background = new VisualBrush(g);
                }
                else
                {
                    if (text.Background == null)
                        text.Background = this.FindResource("Shine1") as Brush;
                    b.Background = new VisualBrush(text);
                }

                b.Padding = new Thickness(2, 2, 2, 2);
                b.Width += 4;
                b.Height += 4;
            }

            var s = handleTarget.ToolTip as string;

            if (s != null && s != "")
                b.ToolTip = handleTarget.ToolTip;

            b.Tag = handleTarget.Tag;

            WireObject(b);
            AddCanvasChild(b);
            SaveFrameworkElement(b);
        }

        void SnapElement(FrameworkElement el)
        {
            if (el is Line)
            {
                Line l = el as Line;

                if (IsHardSnapEnabled())
                {
                    l.X1 = HardSnapCoordinate(l.X1);
                    l.X2 = HardSnapCoordinate(l.X2);
                    l.Y1 = HardSnapCoordinate(l.Y1);
                    l.Y2 = HardSnapCoordinate(l.Y2);
                }
                else
                {
                    l.X1 = SoftSnapCoordinate(l.X1);
                    l.X2 = SoftSnapCoordinate(l.X2);
                    l.Y1 = SoftSnapCoordinate(l.Y1);
                    l.Y2 = SoftSnapCoordinate(l.Y2);
                }
            }
            else if (el is Path)
            {
                Path p = el as Path;
                PathFigure pf;
                ArcSegment arc;
                if (TryExtractArc(p, out pf, out arc))  // don't snap arcs
                    return;

                foreach (var pp in EnumeratePathPoints(p))
                {
                    Point point = pp.v;

                    if (IsHardSnapEnabled())
                    {
                        HardSnapPoint(ref point);
                    }
                    else
                    {
                        SoftSnapPoint(ref point);
                    }

                    if (pp.lineseg != null)
                    {
                        pp.lineseg.Point = point;
                    }
                    else if (pp.bezseg != null)
                    {
                        pp.bezseg.Point3 = point;
                    }
                    else if (pp.polybezseg != null)
                    {
                        pp.polybezseg.Points[pp.ipoint] = point;
                    }
                    else if (pp.polyseg != null)
                    {
                        pp.polyseg.Points[pp.ipoint] = point;
                    }
                    else
                    {
                        pp.fig.StartPoint = point;
                    }
                }
            }
            else
            {
                return;
            }

            SaveFrameworkElement(el);
        }

        void SetOwnFontName()
        {
            fontSampleText.Text = fontSampleText.FontFamily.ToString() + " " + (fontSampleText.FontSize / 1.333333333333333).ToString();
        }

        void colorsample_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            Rectangle r = sender as Rectangle;

            var picker = new ColorPickerDialog();

            Brush brush;

            if (r == strokeRect)
                brush = strokeBrush;
            else
                brush = fillBrush;


            SolidColorBrush solid = brush as SolidColorBrush;

            if (solid != null)
            {
                picker.StartingColor = solid.Color;
            }
            else
            {
                picker.StartingColor = Colors.White;
            }

            if (picker.ShowDialog() == true)
            {
                if (r == strokeRect)
                    strokeBrush = new SolidColorBrush(picker.SelectedColor);
                else
                    fillBrush = new SolidColorBrush(picker.SelectedColor);

                r.Fill = new SolidColorBrush(picker.SelectedColor);
            }
        }

        HandleInfo GetHandleInfoByTag(string tag)
        {
            if (handles != null)
            {
                HandleInfo handle;
                if (handles.TryGetValue(tag, out handle))
                    return handle;
            }

            return null;
        }

        Shape GetHandleByTag(string tag)
        {
            if (handles != null)
            {
                HandleInfo handle;
                if (handles.TryGetValue(tag, out handle))
                    return handle.shape;
            }

            return null;
        }

        void RemoveHandleByTag(string tag)
        {
            Shape s = GetHandleByTag(tag);
            if (s != null)
            {
                canvas.Children.Remove(s);
                handles.Remove(tag);
            }
        }

        void AddHandle(string tag, Shape s)
        {
            var handle = new HandleInfo();
            handle.tag = tag;
            handle.shape = s;
            handles.Add(tag, handle);
        }

        void CreateHandles(FrameworkElement el)
        {
            // compute this now before we lose the old handleTarget
            bool changingElements = (handleTarget != el);
            
            // does not clear pathPointIndex
            ClearHandles();

            Line l = el as Line;
            Path p = el as Path;

            // The index has to survive because we often recreate the handle list
            // without the path object, just moving the index for instance
            // and recreating the handles with this method.
            // if we changed elements or we're somehow no long on a path, then nuke it
            if (changingElements || p == null)
                pathPointIndex = -1;

            if (p != null)
            {
                ArcSegment arc;
                PathFigure pf;

                if (TryExtractArc(p, out pf, out arc))
                {
                    CreateArcHandles(arc, pf);
                }
                else
                {
                    CreatePathHandles(p);
                }
            }
            else if (l != null)
            {
                CreateLineHandles(l);
            }
            else
            {
                CreateSizeAndRotateHandles(el);
            }

            handleTarget = el;

            SetPropertiesForHandleTarget();

            AddHandleContextMenus();

            if (buttonActive != null)
                buttonActive.Focus();
        }

        void AddHandleContextMenus()
        {
            // by convention the main handles are the first two and they get the context menu

            foreach (var tag in handles.Keys)
            {
                switch (tag)
                {
                    case "size":
                    case "rot":
                    case "arc_start":
                    case "arc_end":
                    case "arc_peak":
                    case "arc_center":
                    case "line_start":
                    case "line_end":
                        handles[tag].shape.ContextMenu = handleMenu;
                        break;
                }
            }
        }

        void CreatePathHandles(Path p)
        {
            // find the last mouse click position in this path
            Point mousePoint = canvas.TranslatePoint(pointPathLastClick, p);

            CreatePathPointList(p, mousePoint);

            CreatePathVertexHandles(p);

            // no extension handles for closed paths
            if (!pathPointList[0].fig.IsClosed)
            {
                CreatePathExtensionHandles(p);
            }

            if (buttonActive != null)
                buttonActive.Focus();

            // create handles for the current edge and the next
            CreatePathMgmtHandles(p, pathPointIndex, "");
            CreatePathMgmtHandles(p, pathPointIndex + 1, "_1");
        }

        class PathHandles
        {
            public FrameworkElement control1;
            public FrameworkElement control2;
            public FrameworkElement toggleSide;
            public FrameworkElement toggleCurve;
            public FrameworkElement splitSide;
            public FrameworkElement deleteNode;
        };

        void CreatePathMgmtHandles(Path path, int ppIndex, string suffix)
        {
            // if the path setup is not good, then forget the whole thing
            if (pathPointList == null || ppIndex < 0 || ppIndex >= pathPointList.Count)
                return;

            var handles = PlacePathMgmtHandles(ppIndex, suffix);

            if (handles.deleteNode != null)
                handles.deleteNode.MouseUp += (sender, e) =>
                {
                    // delete a node
                    DeletePathNode(ppIndex);
                    InstantEditCompleted(path);
                };

            if (handles.toggleCurve != null)
                handles.toggleCurve.MouseUp += (sender, e) =>
                {
                    // convert lines to curves and curves to lines
                    ToggleSegmentCurvedness(ppIndex);
                    InstantEditCompleted(path);
                };


            if (handles.toggleSide != null)
                handles.toggleSide.MouseUp += (sender, e) =>
                {
                    // hide one edge
                    TogglePathSide(ppIndex);
                    InstantEditCompleted(path);
                };

            if (handles.splitSide != null)
                handles.splitSide.MouseDown += (sender, e) =>
                {
                    if (TryEarlyOutMouseDownBasic(sender, e))
                        return;

                    Point pointEnd = e.GetPosition(canvas);

                    // try add a node in the middle, if we can't then ignore the whole thing
                    var newHandle = TryAddPathSideNode(ppIndex, pointEnd);

                    if (newHandle == null)
                        return;

                    // hand off to the path handling system, we move the node from here
                    e.Handled = false;
                    newHandle.RaiseEvent(e);
                };

            if (handles.control1 != null)
            {
                WireControlHandle(handles.control1, path, (pt) => SetBezControl1(ppIndex, pt));
            }

            if (handles.control2 != null)
            {
                WireControlHandle(handles.control2, path, (pt) => SetBezControl2(ppIndex, pt));
            }
        }

        void WireControlHandle(FrameworkElement handle, FrameworkElement target, Action<Point> Setter)
        {
            bool dirty = false;
            Shape snapHint = null;

            handle.MouseDown += (object sender, MouseButtonEventArgs e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                BeginMouseCapture(sender, e);
            };

            handle.MouseMove += (object sender, MouseEventArgs e) =>
            {
                if (moving != sender)
                    return;

                Point pointEnd = e.GetPosition(canvas);
                SnapAndTrack(ref pointEnd, ref snapHint);
                Setter(pointEnd);
                MoveHandle(sender, pointEnd);

                dirty = true;
            };

            handle.MouseUp += (object sender, MouseButtonEventArgs e) =>
            {
                if (moving != sender)
                    return;

                // this will recreate the handles for the edited item
                DragEditCompleted(handle: sender, target: target, dirty: dirty);
            };
        }

        PathHandles PlacePathMgmtHandles(int ppIndex, string suffix)
        {
            var handles = new PathHandles();

            PathPoint pp = pathPointList[ppIndex];
            Vector vector = GetDirectionVector(pp);
            Vector curveTangent;
            Point ptToggleSide;

            var hull = GetBezHull(pp);
            if (hull != null)
            {
                // for a bezier segment we'll want to create the control points in addition to the other stuff
                handles.control1 = CreateHandleCircle(hull.Control1, "pp_bez_control1" + suffix);
                handles.control2 = CreateHandleCircle(hull.Control2, "pp_bez_control2" + suffix);

                // we put the triangle icon half way through the curve
                ptToggleSide = hull.Triangle;

                // the tangent at that point is given by the hull points 4 and 5
                curveTangent = hull.Mid5 - hull.Mid4;
                NormalizeVector(ref curveTangent);
            }
            else
            {
                // for a straight line segment the triangle goes in the middle and the tangent at that point is of course the direction vector
                // of the line since it's a straight line
                ptToggleSide = Midpoint(pp.v, GetPreviousPoint(pp));
                curveTangent = vector;
            }

            // any sort of segment gets the side management handles to toggle curvature and split
            if (pp.lineseg != null || pp.polybezseg != null || pp.polyseg != null || pp.bezseg != null)
            {
                var ptToggleCurve = new Point(ptToggleSide.X - curveTangent.Y * 15, ptToggleSide.Y + curveTangent.X * 15);
                var ptSplitSide = new Point(ptToggleSide.X + curveTangent.Y * 15, ptToggleSide.Y - curveTangent.X * 15);

                handles.toggleCurve = CreateHandleSine(ptToggleCurve, "pp_togglecurve" + suffix);
                handles.splitSide = CreateHandlePlus(ptSplitSide, "pp_splitside" + suffix);

                if (suffix == "")
                {
                    // only the primary nodes gets a delete option, just for visual clarity
                    var ptDeleteNode = new Point(pp.v.X - vector.Y * 15, pp.v.Y + vector.X * 15);
                    handles.deleteNode = CreateHandleCross(ptDeleteNode, "pp_deletenode" + suffix);
                }
            }

            // all nodes get a side to toggle, even the start node, which is used to make a closed figure
            handles.toggleSide = CreateHandleTriangle(ptToggleSide, "pp_toggleside" + suffix);

            return handles;
        }
        
        void CreatePathExtensionHandles(Path p)
        {
            var startHandle = PlacePathExtensionHandle(0, "path_extend_start");
            var endHandle = PlacePathExtensionHandle(pathPointList.Count - 1, "path_extend_end");

            MouseButtonEventHandler pathExtendStartHandle_MouseDown = (sender, e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender,e))
                    return;

                // at start
                ExtendPathAtMouse(0, e);
            };

            MouseButtonEventHandler pathExtendEndHandle_MouseDown = (sender, e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                // at end
                ExtendPathAtMouse(pathPointList.Count - 1, e);
            };

            startHandle.MouseDown += pathExtendStartHandle_MouseDown;
            endHandle.MouseDown += pathExtendEndHandle_MouseDown;
        }

        void ExtendPathAtMouse(int ppIndex, MouseButtonEventArgs e)
        {
            pathPointIndex = ppIndex;

            // instantaneously select the correct endpoint and begin an extension if possible
            var newHandle = TryAddPathEndNode(e.GetPosition(canvas));

            if (newHandle == null)
                return;

            // transfer the mouse down to the new handle
            e.Handled = false;
            newHandle.isNew = true;
            newHandle.shape.RaiseEvent(e);
        }

        void CreatePathVertexHandles(Path p)
        {
            Point nodeStart = new Point(0, 0);
            bool dirty = false;
            Shape snapHint = null;

            MouseButtonEventHandler pathHandle_MouseDown = (object sender, MouseButtonEventArgs e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                var handle = (FrameworkElement)sender;
                string tagString = handle.Tag as String;

                // clicked on a new node, changing pathPointIndex
                if (!Int32.TryParse(tagString.Substring(10), out pathPointIndex))
                {
                    pathPointIndex = -1; // no node selected
                    return;
                }

                var pp = pathPointList[pathPointIndex];

                nodeStart = pp.v;

                if (IsAltDown()) // check to see if we're doing scale/rotate
                {
                    var newHandle = SetupPathScaleAndRotate(pp);

                    // and link it up to start dragging
                    e.Handled = false;
                    newHandle.RaiseEvent(e);                  
                    return;
                }

                // we're moving a single path point with the standard endpoint handle
                RemoveNonPathNodeHandles();

                BeginMouseCapture(sender, e);
            };

            MouseEventHandler pathHandle_MouseMove = (object sender, MouseEventArgs e) =>
            {
                if (moving != sender)
                    return;

                Point pointEnd = e.GetPosition(canvas);

                if (IsCtrlDown())
                {
                    RightAngleAdjustMovingPathNode(ref pointEnd);
                }

                SnapAndTrack(ref pointEnd, ref snapHint);
                MovePathNode(pointEnd);
                
                MoveHandle(sender, pointEnd);

                dirty = true;
            };

            MouseButtonEventHandler pathHandle_MouseUp = (object sender, MouseButtonEventArgs e) =>
            {
                if (moving != sender)
                    return;

                // get the information for the handle that we used for the drag
                HandleInfo info = GetHandleInfoFromSender(sender);

                Point pointEnd = e.GetPosition(canvas);

                // this will recreate the handles for the edited item
                DragEditCompleted(handle: sender, target: p, dirty: dirty);

                string tag = pathPointIndex != 0 ? "path_extend_end" : "path_extend_start";

                if (info.isNew)
                {
                    // we remove the path end point when we extend the path because we do not want
                    // to create a situation where you can't start a new line near the old end-point
                    // simply because the handle is in the way.
                    RemoveHandleByTag(GetHandleInfoFromPathPoint(pathPointIndex).tag);

                    AdjustToBigExtensionHandle(pointEnd, tag);
                }
            };

            for (int i = 0; i < pathPointList.Count; i++)
            {
                var pp = pathPointList[i];
                var handle = CreateHandleRectMisc(pp.v);
                handle.ContextMenu = handleMenu;
                string tag = String.Format("path_node:{0}", i);
                handle.Tag = tag;

                handle.MouseDown += pathHandle_MouseDown;
                handle.MouseMove += pathHandle_MouseMove;
                handle.MouseUp += pathHandle_MouseUp;

                AddHandle(tag, handle);
            }
        }

        // make the extension handle bigger and move it to the end of the line
        // so that the natural extension operation tends to create a path
        void AdjustToBigExtensionHandle(Point pointEnd, string tag)
        {
            var extendHandle = GetHandleByTag(tag);
            double scale = Math.Sqrt(3.0 / GetCurrentZoom());
            extendHandle.LayoutTransform = new ScaleTransform(scale, scale);
            extendHandle.Opacity = 0.75;
            MoveHandle(extendHandle, pointEnd);
        }

        HandleInfo GetHandleInfoFromSender(object sender)
        {
            Shape shape = sender as Shape;
            string tag = shape.Tag as string;
            return GetHandleInfoByTag(tag);
        }

        void RightAngleAdjustMovingPathNode(ref Point pointEnd)
        {
            // get the count and index for easy access
            int c = pathPointList.Count;
            int i = pathPointIndex;
            bool isClosed = pathPointList[i].fig.IsClosed;

            // we begin at the original end point
            var t1 = pointEnd;
            var t2 = pointEnd;

            // and with no useful snap
            double d1 = double.PositiveInfinity;
            double d2 = double.PositiveInfinity;

            // if we can go forward then try to right angle snap forward
            int i1 = i + 1;
            if (i1 < c || isClosed)
            {
                i1 %= c;
                var ref1 = pathPointList[i1].v;
                RightAngleAdjustMousePosition(ref1, ref t1);
                d1 = DistanceSquared(pointEnd, t1);
            }

            // if we can go backward then try to right angle snap backward
            int i2 = i - 1;
            if (i2 >= 0 || isClosed)
            {
                if (i2 < 0) i2 = c - 1;
                var ref2 = pathPointList[i2].v;
                RightAngleAdjustMousePosition(ref2, ref t2);
                d2 = DistanceSquared(pointEnd, t2);
            }

            // pick whichever one was the least disturbance
            if (d1 < d2)
                pointEnd = t1;
            else
                pointEnd = t2;            
        }

        // recreate the path point list from the path object, if there is no selected node
        // then try to establish one from the given mouse point
        void CreatePathPointList(Path p, Point mousePoint)
        {
            pathPointList = new List<PathPoint>();

            // try to find a node to select based on mouse position
            double closestDistance = double.MaxValue;
            int closestIndex = -1;

            foreach (PathPoint pp in EnumeratePathPoints(p))
            {
                double d = DistanceSquared(pp.v, mousePoint);
                if (d < closestDistance)
                {
                    closestIndex = pathPointList.Count;
                    closestDistance = d;
                }

                pathPointList.Add(pp);
            }

            // if something has already been selected, then leave it
            // but on first select we can go with the closest point
            if (pathPointIndex == -1)
            {
                pathPointIndex = closestIndex;
            }
        }

        void CreateSizeAndRotateHandles(FrameworkElement el)
        {
            // this is the captured state we need to do size and rotate generally
            double scaleX;
            double scaleY; 
            double rotateAngle;
            ExtractScaleAndRotate(el, out scaleX, out scaleY, out rotateAngle);

            // we also need these attributes from the element
            double widthStart = el.ActualWidth;
            double heightStart = el.ActualHeight;
            double cornerStart = 0;
            Point pointCenterOriginal = el.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
            Point pointCornerOriginal = el.TranslatePoint(new Point(widthStart, heightStart), canvas);
            Point point3PMOriginal = el.TranslatePoint(new Point(widthStart, heightStart / 2), canvas);

            if (el is Border)
            {
                var b = el as Border;
                cornerStart = b.CornerRadius.BottomRight;
            }

            bool dirty = false;

            MouseButtonEventHandler sizeHandle_MouseDown = (sender, e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                RemoveHandleByTag("rot");

                BeginMouseCapture(sender, e);
            };

            MouseButtonEventHandler rotateHandle_MouseDown = (sender, e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                RemoveHandleByTag("size");

                BeginMouseCapture(sender, e);
            };

            MouseEventHandler sizeHandle_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                Point pointEnd = e.GetPosition(canvas);

                if (IsCtrlDown())
                {
                    RightAngleAdjustMousePosition(pointCornerOriginal, ref pointEnd);
                }

                if (IsHardSnapEnabled())
                {
                    pointEnd = new Point(HardSnapCoordinate(pointEnd.X), HardSnapCoordinate(pointEnd.Y));
                }

                // this is the mouse point in coordinates relative to the object origin
                var pointObject = canvas.TranslatePoint(pointEnd, el);

                if (pointObject.X < 5 || pointObject.Y < 5)
                {
                    // the thing is too small, don't try to change it anymore, just stop there
                }
                else if (el is Grid || el is TextBlock)
                {
                    ResizeViaScaling(el, widthStart, heightStart, ref pointObject);
                    FixupMarginAfterTransformChange(el, widthStart, heightStart, pointCenterOriginal);
                }
                else if (el is Border)
                {
                    ResizeBorder(el, cornerStart, widthStart, heightStart, ref pointObject);
                    FixupMarginAfterDimensionChange(el);
                }
                else
                {
                    el.Width = RoundCoordinate(pointObject.X);
                    el.Height = RoundCoordinate(pointObject.Y);
                    FixupMarginAfterDimensionChange(el);
                }

                dirty = true;

                MoveHandle(sender, pointEnd);
            };


            MouseEventHandler rotateHandle_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                Point pointEnd = e.GetPosition(canvas);

                if (IsCtrlDown())
                {
                    RightAngleAdjustMousePosition(pointCenterOriginal, ref pointEnd);
                }

                double angle = Math.Atan2(pointEnd.Y - pointCenterOriginal.Y, pointEnd.X - pointCenterOriginal.X);

                angle = Math.Floor(angle * 360 / Math.PI / 2);

                rotateAngle = angle;
                double rots = Math.Floor(rotateAngle/ 360);
                rotateAngle -= rots * 360;

                rotateAngle = RoundCoordinate(rotateAngle);

                el.LayoutTransform = MakeCombinationTransform(scaleX, scaleY, rotateAngle);

                Point pc = el.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);

                el.Margin = RoundMargin(el.Margin.Left + pointCenterOriginal.X - pc.X, el.Margin.Top + pointCenterOriginal.Y - pc.Y, 0, 0);

                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            MouseButtonEventHandler anyHandle_MouseUp = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                // this will recreate the handles for the edited item
                DragEditCompleted(handle: sender, target: handleTarget, dirty: dirty);
            };

            Shape sizeHandle = CreateHandleRect(pointCornerOriginal, "size");
            Shape rotateHandle = CreateHandleCircle(point3PMOriginal, "rot");
            
            sizeHandle.MouseDown += sizeHandle_MouseDown;
            sizeHandle.MouseMove += sizeHandle_MouseMove;
            sizeHandle.MouseUp += anyHandle_MouseUp;

            rotateHandle.MouseDown += rotateHandle_MouseDown;
            rotateHandle.MouseMove += rotateHandle_MouseMove;
            rotateHandle.MouseUp += anyHandle_MouseUp;
        }

        void FixupMarginAfterDimensionChange(FrameworkElement el)
        {
            // Note:  The size of the object changes but its margin does not, there is a rotation in effect 
            // it appears to grow in the wrong direction -- it must stay below and to the right
            // of its margin... so we fix up the margins so that the object will once again be adjacent

            Point pTopLeft = el.TranslatePoint(new Point(0, 0), canvas);
            Point pTopRight = el.TranslatePoint(new Point(el.Width, 0), canvas);
            Point pBottomLeft = el.TranslatePoint(new Point(0, el.Height), canvas);
            Point pBottomRight = el.TranslatePoint(new Point(el.Width, el.Height), canvas);

            double left = Math.Min(Math.Min(pTopLeft.X, pTopRight.X), Math.Min(pBottomLeft.X, pBottomRight.X));
            double top = Math.Min(Math.Min(pTopLeft.Y, pTopRight.Y), Math.Min(pBottomLeft.Y, pBottomRight.Y));

            el.Margin = new Thickness(left, top, 0, 0);
        }

        void FixupMarginAfterTransformChange(FrameworkElement el, double widthStart, double heightStart, Point pointCenterOriginal)
        {
            // we changed the scale and we want to keep the item center in the same place, adjust the margin so that it does not move

            Point pc = el.TranslatePoint(new Point(widthStart / 2, heightStart / 2), canvas);
            el.Margin = RoundMargin(el.Margin.Left + pointCenterOriginal.X - pc.X, el.Margin.Top + pointCenterOriginal.Y - pc.Y, 0, 0);            
        }

        void ResizeBorder(FrameworkElement el, double cornerStart, double widthStart, double heightStart, ref Point pointObject)
        {
            Border b = (Border)el;

            double w, h, scale;

            if (IsCtrlDown())
            {
                w = RoundCoordinate(pointObject.X);
                h = RoundCoordinate(pointObject.Y);

                scale = RoundScale(w / widthStart + h / heightStart) / 2;
            }
            else
            {
                if (pointObject.X > pointObject.Y)
                    scale = pointObject.Y / widthStart;
                else
                    scale = pointObject.X / heightStart;

                w = RoundCoordinate(widthStart * scale);
                h = RoundCoordinate(heightStart * scale);
            }

            if (w < 10 || h < 10)
                return;

            b.CornerRadius = new CornerRadius(RoundScale(cornerStart * scale));
 
            el.Width = w;
            el.Height = h;
        }

        void ResizeViaScaling(FrameworkElement el, double widthStart, double heightStart, ref Point pointObject)
        {
            double scaleX, scaleY, rotateAngle;
            ExtractScaleAndRotate(el.LayoutTransform, out scaleX, out scaleY, out rotateAngle);

            scaleX = RoundScale(scaleX * pointObject.X / widthStart);
            scaleY = RoundScale(scaleY * pointObject.Y / heightStart);

            el.LayoutTransform = MakeCombinationTransform(scaleX, scaleY, rotateAngle);
        }

        // this method creates the handles for an arc but also serves as the central place where the line handle state is held
        // in the form of captured local variables.  In a real sense this function is it's a line handle class, much like
        // the javascript "a delegate and its captured state is a class" model.
        void CreateArcHandles(ArcSegment arc, PathFigure figure)
        {
            // these are the captured original end points of the line and the original mouse down coordinate
            Point p1 = new Point(0, 0);
            Point p2 = new Point(0, 0);
            Point pointStart = new Point(0, 0);
            bool dirty = false;
            Size sizeRef = new Size(0,0);
            Size sizeStart = new Size(0, 0);

            Point center = LocateArcCenter(figure, arc);
            Point peak = LocateArcPeak(figure, arc);

            Shape startHandle = CreateHandleRect(figure.StartPoint, "arc_start");
            Shape endHandle = CreateHandleRect(arc.Point, "arc_end");
            Shape peakHandle = CreateHandleRect(peak, "arc_peak");
            Shape centerHandle = CreateHandleRect(center, "arc_center");

            Shape snapHint = null;

            MouseButtonEventHandler arcAny_MouseDown = (sender, e) =>
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                 pointStart = e.GetPosition(canvas);

                 p1 = figure.StartPoint;
                 p2 = arc.Point;
                 sizeStart = arc.Size;
                 sizeRef = (Size)(p1 - p2);
                 sizeRef = new Size(Math.Abs(sizeRef.Width), Math.Abs(sizeRef.Height));

                 BeginMouseCapture(sender, e);
            };

            MouseEventHandler arcStart_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);

                var pointNew = p1 + (pointEnd - pointStart);
                SnapAndTrack(ref pointNew, ref snapHint);

                if (DistanceSquared(pointNew, p2) < 4)
                    return;

                figure.StartPoint = pointNew;

                AdjustArcSize(figure, arc, sizeStart, sizeRef);
                PlacePeakHandle(figure, arc, peakHandle);
                PlaceCenterHandle(figure, arc, centerHandle);
                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            MouseEventHandler arcEnd_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);

                var pointNew = p2 + (pointEnd - pointStart);
                SnapAndTrack(ref pointNew, ref snapHint);

                if (DistanceSquared(pointNew, p1) < 4)
                    return;

                arc.Point = pointNew;

                AdjustArcSize(figure, arc, sizeStart, sizeRef);
                PlacePeakHandle(figure, arc, peakHandle);
                PlaceCenterHandle(figure, arc, centerHandle);
                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            MouseEventHandler arcPeak_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);
                AdjustArcPeak(figure, arc, pointEnd);
                PlaceCenterHandle(figure, arc, centerHandle);
                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            MouseEventHandler arcCenter_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);
                AdjustArcCenter(figure, arc, pointEnd);
                PlacePeakHandle(figure, arc, peakHandle);
                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            MouseButtonEventHandler arcAny_MouseUp = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                // arc segment is too small now, just nuke it
                if (DistanceSquared(figure.StartPoint, arc.Point) <= 16)
                {
                    DeleteElement(handleTarget);
                    EndMouseCapture(sender);
                    return;
                }

                // this will recreate the handles for the edited item
                DragEditCompleted(handle:sender, target:handleTarget, dirty:dirty);
            };

            // wire the remaining handlers
            startHandle.MouseDown += arcAny_MouseDown;
            endHandle.MouseDown += arcAny_MouseDown;
            peakHandle.MouseDown += arcAny_MouseDown;
            centerHandle.MouseDown += arcAny_MouseDown;

            startHandle.MouseUp += arcAny_MouseUp;
            endHandle.MouseUp += arcAny_MouseUp;
            peakHandle.MouseUp += arcAny_MouseUp;
            centerHandle.MouseUp += arcAny_MouseUp;

            startHandle.MouseMove += arcStart_MouseMove;
            endHandle.MouseMove += arcEnd_MouseMove;
            peakHandle.MouseMove += arcPeak_MouseMove;
            centerHandle.MouseMove += arcCenter_MouseMove;
        }

        // this method creates the handles for a line but also serves as the central place where the line handle state is held
        // in the form of captured local variables.  In a real sense this function is it's a line handle class, much like
        // the javascript "a delegate and its captured state is a class" model.
        void CreateLineHandles(Line line)
        {
            // these are the captured original end points of the line and the original mouse down coordinate
            Point p1 = new Point(0, 0);
            Point p2 = new Point(0, 0);
            Point pointStart = new Point(0, 0);
            bool dirty = false;
            Shape snapHint = null;


            MouseButtonEventHandler lineHandle_MouseDown = (sender, e) => 
            {
                if (TryEarlyOutMouseDownBasic(sender, e))
                    return;

                p1 = new Point(line.X1, line.Y1);
                p2 = new Point(line.X2, line.Y2);

                pointStart = e.GetPosition(canvas);

                BeginMouseCapture(sender, e);

                // we don't need to see these while we're dragging, we will regenerate handles when the drag is done
                RemoveHandleByTag("line_extend_end");
                RemoveHandleByTag("line_extend_start");
            };

            MouseButtonEventHandler lineExtendStartHandle_MouseDown = (sender, e) =>
            {
                if (!TryEarlyOutMouseDownBasic(sender, e))
                    ExtendLineAtMouse(0, line, e); // extend at start
            };

            MouseButtonEventHandler lineExtendEndHandle_MouseDown = (sender, e) =>
            {
                if (!TryEarlyOutMouseDownBasic(sender, e))
                    ExtendLineAtMouse(1, line, e); // extend at end
            };

            // when the line start handle moves we adjust the x1 and y1 of the line
            MouseEventHandler lineStart_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);

                if (IsCtrlDown())
                {
                    RightAngleAdjustMousePosition(p2, ref pointEnd);
                }

                SnapAndTrack(ref pointEnd, ref snapHint);

                line.X1 = pointEnd.X;
                line.Y1 = pointEnd.Y;
                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            // when the line end handle moves we adjust the x2 and y2 of the line
            MouseEventHandler lineEnd_MouseMove = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                var pointEnd = e.GetPosition(canvas);

                if (IsCtrlDown())
                {
                    RightAngleAdjustMousePosition(p1, ref pointEnd);
                }

                SnapAndTrack(ref pointEnd, ref snapHint);

                line.X2 = pointEnd.X;
                line.Y2 = pointEnd.Y;
                dirty = true;

                MoveHandle(sender, pointEnd);
            };

            MouseButtonEventHandler lineHandle_MouseUp = (sender, e) =>
            {
                // drag not in progress, ignore it
                if (moving != sender)
                    return;

                // if this line is too short, it's just junk now and it was probably a mistake
                if (DistanceSquared(line.X1, line.X2, line.Y1, line.Y2) <= 16)
                {
                    DeleteElement(handleTarget);
                    EndMouseCapture(sender);
                    return;
                }

                // get the information for the handle that we used for the drag
                HandleInfo info = GetHandleInfoFromSender(sender);

                // this will recreate the handles for the edited item
                DragEditCompleted(handle: sender, target: line, dirty: dirty);

                // we remove the line end point when we first create a line because we do not want
                // to create a situation where you can't start a new line near the old end-point
                // simply because the handle is in the way.
                if (info.isNew)
                {
                    RemoveHandleByTag("line_end");

                    if (info.isNew)
                    {
                        AdjustToBigExtensionHandle(new Point(line.X2, line.Y2), "line_extend_end");
                    }
                }
            };

            // line start and end handles
            Shape startHandle = CreateHandleRect(new Point(line.X1, line.Y1), "line_start");
            Shape endHandle = CreateHandleRect(new Point(line.X2, line.Y2), "line_end");

            // create line extension handles
            Vector v = GetDirectionVector(line);
            var exHandle1 = CreateHandlePlus(new Point(line.X1 - v.X * 15, line.Y1 - v.Y * 15), "line_extend_start");
            var exHandle2 = CreateHandlePlus(new Point(line.X2 + v.X * 15, line.Y2 + v.Y * 15), "line_extend_end");

            // wire the remaining handlers
            exHandle1.MouseDown += lineExtendStartHandle_MouseDown;
            exHandle2.MouseDown += lineExtendEndHandle_MouseDown;
            startHandle.MouseDown += lineHandle_MouseDown;
            startHandle.MouseUp += lineHandle_MouseUp;
            startHandle.MouseMove += lineStart_MouseMove;
            endHandle.MouseDown += lineHandle_MouseDown;
            endHandle.MouseUp += lineHandle_MouseUp;
            endHandle.MouseMove += lineEnd_MouseMove;

            // note that we initially create the line_end handle to allow new lines to be dragged open
            // the new operation is basically a create line and size it in one step
            // however once the line is created but just as it's about to be saved (i.e. on the mouse up)
            // the end handle has to go away so that it isn't in the way
            // hence in general we create the handle but in that one case we have to delete it
            // we can't simply avoid creating the end handle here for new lines
        }


        enum SnapAction
        {
            None,  // did not snap
            Coordinate1, // snapped to 1 round coordinate
            Coordinate2, // snapped to 2 round coordinates
            Object // snapped to an object
        }

        void SnapAndTrack(ref Point point, ref Shape snapHint)
        {
            // snap the point
            var snapaction = SnapPoint(ref point);

            // display a hollow helper handle when snapping is occurring
            if (snapaction != SnapAction.None)
            {
                if (snapHint == null)
                {
                    // the ellipse needs creating, make it now
                    snapHint = CreateHandleHollow(point, "snaphint");
                }
                else
                {
                    // we already have it, just move it, and show it
                    snapHint.Visibility = Visibility.Visible;
                    MoveHandle(snapHint, point);
                }

                // set the thickness and opacity to reflect goodness of snap
                switch (snapaction)
                {
                    case SnapAction.Coordinate1:
                        snapHint.StrokeThickness = 1;
                        snapHint.Opacity = .5;
                        break;
                    case SnapAction.Coordinate2:
                        snapHint.StrokeThickness = 1;
                        snapHint.Opacity = 1;
                        break;
                    case SnapAction.Object:
                        snapHint.StrokeThickness = 3;
                        snapHint.Opacity = 1;
                        break;
                }
            }
            else
            {
                // hide it if we have it
                if (snapHint != null)
                {
                    snapHint.Visibility = Visibility.Hidden;
                }
            }
        }

        void ExtendLineAtMouse(int ppIndex, Line line, MouseButtonEventArgs e)
        {
            // make a new path from the line
            Path path = ConvertLineToPath(line);

            // set up the handles
            CreateHandles(path);

            // hand off to the path handling system
            ExtendPathAtMouse(ppIndex, e);
        }

        void DragEditCompleted(object handle, FrameworkElement target, bool dirty)
        {
            // supurious mouse up from something that isn't what we have captured, drop it on the floor
            if (moving != handle)
                return;

            // now ok to end the operation and save the result
            EndMouseCapture(handle);
            InstantEditCompleted(target, dirty);
        }

        void InstantEditCompleted(FrameworkElement target, bool dirty=true)
        {
            CreateHandles(target);

            if (dirty)
            {
                BeginUndoUnit();
                SaveFrameworkElement(target);
            }
        }

        void EndMouseCapture(object sender)
        {
            var handle = (FrameworkElement)sender;
            handle.ReleaseMouseCapture();
            moving = null;
        }

        static string GetHandleTag(object sender)
        {
            return (sender as FrameworkElement).Tag as string;
        }

        void BeginMouseCapture(object s, MouseButtonEventArgs e)
        {
            var sender = s as FrameworkElement;

            if (sender != null)
                sender.CaptureMouse();

            moving = sender;
            buttonStarted = e.ChangedButton;
            e.Handled = true;
        }

        void AdjustArcPeak(PathFigure pf, ArcSegment arc, Point mouse)
        {
            var r_rev = new RotateTransform(-arc.RotationAngle);

            SnapPoint(ref mouse);

            Point pz = r_rev.Transform((Point)(mouse - pf.StartPoint));

            Point pc = LocateArcCenter(pf, arc);

            pc = (Point)(pc - pf.StartPoint);
            pc = r_rev.Transform(pc);

            pz.X = pz.X - pc.X;
            pz.Y = pz.Y - pc.Y;

            arc.Size = new Size(RoundScale(arc.Size.Width), RoundScale(Math.Abs(pz.Y)));
            arc.IsLargeArc = (pc.Y < 0);
        }

        void AdjustArcCenter(PathFigure pf, ArcSegment arc, Point mouse)
        {
            var r_rev = new RotateTransform(-arc.RotationAngle);

            SnapPoint(ref mouse);

            Point pc = (Point)(mouse - pf.StartPoint);

            pc = r_rev.Transform(pc);

            Point p1 = new Point(0, 0);
            Point p2 = (Point)(arc.Point - pf.StartPoint);

            p2 = r_rev.Transform(p2);

            pc.X = p2.X / 2;

            p1.X = p1.X - pc.X;
            p2.X = p2.X - pc.X;
            p1.Y = p1.Y - pc.Y;
            p2.Y = p2.Y - pc.Y;

            double ratio = arc.Size.Height / arc.Size.Width;

            p1.Y = p1.Y / ratio;
            p2.Y = p2.Y / ratio;


            var theta = Math.Atan2(p1.X, p1.Y);
            var phi = Math.PI / 2 - theta;

            var cosphi = Math.Cos(phi);

            if (Math.Abs(cosphi) < 0.0001)
                return;

            double dx = Math.Abs(p1.X / cosphi);

            arc.Size = new Size(RoundScale(dx), RoundScale(dx * ratio));
            arc.IsLargeArc = (pc.Y < 0);
        }

        static void PlaceCenterHandle(PathFigure pf, ArcSegment arc, FrameworkElement center)
        {
            Point ptCenter = LocateArcCenter(pf, arc);

            if (Double.IsNaN(ptCenter.X) || Double.IsNaN(ptCenter.Y))
                return;

            center.Margin = RoundMargin(ptCenter.X - HandleSize / 2, ptCenter.Y - HandleSize / 2, 0, 0);
            return;
        }

        static void PlacePeakHandle(PathFigure pf, ArcSegment arc, FrameworkElement peak)
        {
            Point ptPeak = LocateArcPeak(pf, arc);

            if (Double.IsNaN(ptPeak.X) || Double.IsNaN(ptPeak.Y))
                return;

            peak.Margin = RoundMargin(ptPeak.X - HandleSize / 2, ptPeak.Y - HandleSize / 2, 0, 0);
            return;
        }

        static void AdjustArcSize(PathFigure pf, ArcSegment arc, Size sizeStart, Size sizeRef)
        {
            Size sizeNew = new Size(Math.Abs(pf.StartPoint.X - arc.Point.X), Math.Abs(pf.StartPoint.Y - arc.Point.Y));

            double ratio = arc.Size.Height / arc.Size.Width;

            var radians = Math.Atan2(arc.Point.Y - pf.StartPoint.Y, arc.Point.X - pf.StartPoint.X);

            arc.RotationAngle = RoundCoordinate(radians / 2 / Math.PI * 360);

            double dOld = Math.Sqrt(sizeRef.Height * sizeRef.Height + sizeRef.Width * sizeRef.Width);
            double dNew = Math.Sqrt(sizeNew.Height * sizeNew.Height + sizeNew.Width * sizeNew.Width);
            
            double w = RoundScale(sizeStart.Width * (dNew / dOld));
            double h = RoundScale(sizeStart.Height * (dNew / dOld));

            arc.Size = new Size(w, h);
        }

        void currentPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentPath.SelectedIndex == -1)
                SetMapPath(defaultMap);
            else
                SetMapPath(currentPath.Items[currentPath.SelectedIndex] as string);
        }

        FrameworkElement SetupPathScaleAndRotate(PathPoint ppHandle)
        {
            // we're setting up to do a full scale rotate of the entire path
            Point pointStart = ppHandle.v;
            Point pointCenter;

            // we're going to pivot around index zero, unless we're on index zero
            // in which case we'll pivot around the last index
            if (pathPointIndex != 0)
            {
                pointCenter = pathPointList[0].v;
            }
            else
            {
                pointCenter = pathPointList[pathPointList.Count - 1].v;
            }

            // stash these so we know where we are
            var pathPointIndexSaved = pathPointIndex;
            var pathPointListSaved = pathPointList;

            ClearHandles();

            // this handle is for show
            CreateHandleRect(pointCenter, "pivot_center");

            // this handle is the one that is gonna move
            var handleEnd = CreateHandleRect(pointStart, "pivot_end");

            // and set up the path point list again so we're ok to go on recieve path updates
            handleTarget = ppHandle.path;
            pathPointList = pathPointListSaved;
            pathPointIndex = pathPointIndexSaved;

            Action<Point> ScaleAndRotatePath = (Point pointNew) =>
            {
                double xNew = pointNew.X;
                double yNew = pointNew.Y;

                // compute the scale and rotation matrices required to perform the transform 
                // from the start orientation to the end orientation

                // we are rotating around pointCenter and our original position was pointStart
                var radiusOld = pointStart - pointCenter;
                var radiusNew = pointNew - pointCenter;
                var radiansOld = Math.Atan2(radiusOld.Y, radiusOld.X);
                var radiansNew = Math.Atan2(radiusNew.Y, radiusNew.X);

                var angle1 = radiansOld / 2 / Math.PI * 360;
                var angle2 = radiansNew / 2 / Math.PI * 360;

                // this is the net rotation in degrees
                var rotation = angle2 - angle1;

                // this is the length of the orignal segment we are rotating and the length of the current segment
                var d1 = Math.Sqrt(DistanceSquared(pointCenter, pointStart));
                var d2 = Math.Sqrt(DistanceSquared(pointCenter, pointNew));

                // this is the net scale
                var scale = d2 / d1;

                // and now we create transforms to rotate and scale about the center
                var t1 = new ScaleTransform(scale, scale, pointCenter.X, pointCenter.Y);
                var t2 = new RotateTransform(rotation, pointCenter.X, pointCenter.Y);

                // Apply the transform to every point in the path, including control points
                foreach (var pp in pathPointList)
                {
                    // Apply the transforms to scale and rotate
                    var point = pp.v;
                    point = t1.Transform(point);
                    point = t2.Transform(point);

                    // update pathpoint, then update the correct path segment, 4 cases
                    pp.v = point;

                    if (pp.lineseg != null)
                    {
                        // line segments only the one point, update it
                        pp.lineseg.Point = point;
                    }
                    else if (pp.bezseg != null)
                    {
                        // bezier segment, we already have the correct end point, now do the control points.
                        pp.bezseg.Point3 = point;

                        // first control point
                        point = pp.bezseg.Point1;
                        point = t1.Transform(point);
                        point = t2.Transform(point);
                        pp.bezseg.Point1 = point;

                        // second control point
                        point = pp.bezseg.Point2;
                        point = t1.Transform(point);
                        point = t2.Transform(point);
                        pp.bezseg.Point2 = point;
                    }
                    else if (pp.polybezseg != null)
                    {
                        // poly bez seg just is stored differently, otherwise the same
                        // we already have the correct end point, now do the control points.
                        var i = pp.ipoint;
                        pp.polybezseg.Points[i] = point;

                        // control point 2
                        point = pp.polybezseg.Points[i - 1];
                        point = t1.Transform(point);
                        point = t2.Transform(point);
                        pp.polybezseg.Points[i - 1] = point;

                        // control point 1
                        point = pp.polybezseg.Points[i - 2];
                        point = t1.Transform(point);
                        point = t2.Transform(point);
                        pp.polybezseg.Points[i - 2] = point;

                    }
                    else if (pp.polyseg != null)
                    {
                        // poly line -- there is only one point, just store it
                        var i = pp.ipoint;
                        pp.polyseg.Points[i] = point;
                    }
                    else
                    {
                        // otherwise it's the start point, already transformed, just store it
                        pp.fig.StartPoint = point;
                    }
                };

                // update the new current position to be where the rotation point has ended up
                // this will be correct for the next mousemove
                pointStart = pathPointList[pathPointIndex].v;
            };

            WireControlHandle(handleEnd, ppHandle.path, (pt) => ScaleAndRotatePath(pt));

            return handleEnd;
        }

        FrameworkElement PlacePathExtensionHandle(int index, string tag)
        {
            PathPoint pp = pathPointList[index];
            Vector v = GetDirectionVector(pp) * 15;

            return CreateHandlePlus(pp.v + v, tag);
        }

        void ToggleSegmentCurvedness(int ppIndex)
        {
            PathPoint pp = pathPointList[ppIndex];

            // bezier segments will become lines, line segments will become straight bezier curves in the same place
            Point prev = GetPreviousPoint(pp);

            BezierSegment bezseg = null;

            if (pp.lineseg != null)
            {
                // this is the first easy case, if it's a lineseg then it can simply be replaced with a bez seg that is straight
                var index = pp.fig.Segments.IndexOf(pp.lineseg);

                bezseg = MakeStraightBezier(prev.X, prev.Y, pp.v.X, pp.v.Y); // always stroked, unstroked bezier is just goofy

                pp.fig.Segments[index] = bezseg;
            }
            else if (pp.bezseg != null)
            {
                // this is the second easy case, if it's a bezseg then it can simply be replaced with a lineseg with the same endpoint
                var index = pp.fig.Segments.IndexOf(pp.bezseg);

                var lineseg = new LineSegment(pp.bezseg.Point3, isStroked: true);

                pp.fig.Segments[index] = lineseg;
            }
            else if (pp.polyseg != null)
            {
                // this is an unfortunate case, we have an array of polygon points, we're going to have to
                // convert them all into segments because now we have one curve in them
                // oh well... we need the curve

                var index = pp.fig.Segments.IndexOf(pp.polyseg);

                pp.fig.Segments.RemoveAt(index);

                for (int i = 0; i < pp.polyseg.Points.Count; i++)                
                {
                    // everything is converting into a line segment except the one place we're making
                    // the change, the one is going to convert into a bezier segment
                    if (i != pp.ipoint)
                    {
                        pp.fig.Segments.Insert(index++, new LineSegment(pp.polyseg.Points[i], isStroked:true));
                    }
                    else
                    {
                        bezseg = MakeStraightBezier(prev.X, prev.Y, pp.v.X, pp.v.Y);
                        pp.fig.Segments.Insert(index++, bezseg);
                    }
                }
            }
            else if (pp.polybezseg != null)
            {
                // this case is nearly as unfortunate, now we have a bunch of bezier control points and we have to put
                // a line in the middle of them... we'll need to split it into two pieces and insert in the middle

                var ipoint = pp.ipoint - 2; // safe because this array has point triples and ipoint is the last of a triple
                var index = pp.fig.Segments.IndexOf(pp.polybezseg);
                Point[] points = pp.polybezseg.Points.ToArray(); // stash the points

                // the existing points will be split into two parts with a line between them
                // let's call those parts  A line B

                // this is for A, we have to see if we can keep any points in the existing polybezseg
                // to construct A
                if (ipoint > 0)
                {
                    // keep deleting anything after ipoint, until there's nothing left
                    while (pp.polybezseg.Points.Count > ipoint)
                        pp.polybezseg.Points.RemoveAt(ipoint);
                }
                else
                {
                    // ipoint was the front, there is no "A" segment, delete what we got
                    pp.fig.Segments.RemoveAt(index);
                    index--;
                }

                // now insert the line segment after the current index
                var lineseg = new LineSegment(points[ipoint + 2], isStroked: true);
                pp.fig.Segments.Insert(++index, lineseg);


                // we've now effectively moved past the point of the split
                // skip the 3 control points for the bezier we just converted to a line
                ipoint += 3;

                // if there is anything in the B segment then add it
                if (ipoint < points.Length)
                {
                    // make a new polybezsegment
                    var polybezseg = new PolyBezierSegment();
                    polybezseg.IsStroked = pp.polybezseg.IsStroked;

                    // play all the points into it
                    for (int i = ipoint; i < points.Length; i++)
                    {
                        polybezseg.Points.Add(points[i]);
                    }

                    // and add it after the line segment we just made
                    pp.fig.Segments.Insert(++index, polybezseg);
                }
            }

            CreateHandles(pp.path);
        }

        // add a new node to the path at one of its two ends, returns the handle for the created node
        HandleInfo TryAddPathEndNode(Point point)
        {
            // only at the ends
            if (pathPointIndex != 0 && pathPointIndex != pathPointList.Count - 1)
                return null;

            var pp = pathPointList[pathPointIndex];

            // only open figures
            if (pp.fig.IsClosed)
                return null;

            int pathPointIndexNext;

            // insert the line segment at the front or the end
            if (pathPointIndex == 0)
            {
                var lineseg = new LineSegment(pp.fig.StartPoint, true);
                pp.fig.Segments.Insert(0, lineseg);
                pp.fig.StartPoint = point;
                pathPointIndexNext = 0;
            }
            else
            {
                var lineseg = new LineSegment(point, true);
                pp.fig.Segments.Add(lineseg);
                pathPointIndexNext = pathPointList.Count;
            }

            CreateHandles(pp.path);

            // and now we just set up for the normal move case
            pathPointIndex = pathPointIndexNext;
            return GetHandleInfoFromPathPoint(pathPointIndex);
        }

        HandleInfo GetHandleInfoFromPathPoint(int ppIndex)
        {
            string tag = String.Format("path_node:{0}", ppIndex);

            return GetHandleInfoByTag(tag);
        }

        class BezierHull
        {
            public Point Start;
            public Point Control1;
            public Point Control2;
            public Point End;
            
            public Point Mid1;  // between start and control1    
            public Point Mid2;  // between control2 and end;
            public Point Mid3;  // between control1 and control2;
            
            public Point Mid4;  // between Mid1 and Mid3    Mid4->Mid5 is tangent to Triangle
            public Point Mid5;  // between Mid2 and Mid3

            public Point Triangle; // between Mid4 and Mid5;  This is half way on the bezier path

            public void Compute()
            {
                Mid1 = Midpoint(Start, Control1);
                Mid2 = Midpoint(Control2, End);
                Mid3 = Midpoint(Control1, Control2);

                Mid4 = Midpoint(Mid1, Mid3);
                Mid5 = Midpoint(Mid2, Mid3);
                Triangle = Midpoint(Mid4, Mid5);
            }

            Point Midpoint(Point p1, Point p2)
            {
                return new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            }
        }

        FrameworkElement TryAddPathSideNode(int ppIndex, Point pointMouse)
        {
            var pp = pathPointList[ppIndex];

            BezierSegment bezseg = null;

            var pathPointIndexNext = ppIndex;

            if (pp.lineseg != null)
            {
                var index = pp.fig.Segments.IndexOf(pp.lineseg);

                var lineseg = new LineSegment(pointMouse, true);

                pp.fig.Segments.Insert(index, lineseg);
            }
            else if (pp.bezseg != null)
            {
                // extract the curve segment and control points
                var index = pp.fig.Segments.IndexOf(pp.bezseg);

                // make the hull for bezier curves
                var hull = GetBezHull(pp);

                // now use those points to create new control points with the same slope contraints but half the magnitude

                // X B1 B2 Y becomes  X mid1 mid4 triangle then mid5 mid2 Y

                // this is the second segment, add it after the current one
                bezseg = new BezierSegment(hull.Mid5, hull.Mid2, hull.End, true);
                pp.fig.Segments.Insert(index+1, bezseg);

                // now adjust the current one to stop at triangle, note mid1 is on the same line as B1 was
                pp.bezseg.Point1 = hull.Mid1;
                pp.bezseg.Point2 = hull.Mid4;
                pp.bezseg.Point3 = hull.Triangle;
            }
            else if (pp.polyseg != null)
            {
                pp.polyseg.Points.Insert(pp.ipoint, pointMouse);
            }
            else if (pp.polybezseg != null)
            {
                // extract the curve segment and control points
                int index = pp.ipoint;

                // make the hull for bezier curves
                var hull = GetBezHull(pp);

                // now use those points to create new control points with the same slope contraints but half the magnitude
                // X B1 B2 Y becomes  X mid1 mid4 triangle then mid5 mid2 Y

                // index points to Y so these insert in front of B2
                pp.polybezseg.Points.Insert(index - 1, hull.Mid5);
                pp.polybezseg.Points.Insert(index - 1, hull.Triangle);
                pp.polybezseg.Points.Insert(index - 1, hull.Mid4);

                // we now have X B1 mid4 triangle mid5 B2 Y
                // pp.polybezseg.Points[index] is now triangle
                // we have to fix up B1 and B2, replacing them with mid1 and mid2

                pp.polybezseg.Points[index - 2] = hull.Mid1;
                pp.polybezseg.Points[index + 2] = hull.Mid2;
            }
            else
            {
                var lineseg = new LineSegment(pointMouse, true);
                pp.fig.Segments.Add(lineseg);
                pathPointIndexNext = pathPointList.Count;
            }

            CreateHandles(pp.path);

            // the newly added node becomes the current node
            pathPointIndex = pathPointIndexNext;

            string tag = String.Format("path_node:{0}", pathPointIndexNext);
            return GetHandleByTag(tag);
        }

        void DeletePathNode(int ppIndex)
        {
            var pp = pathPointList[ppIndex];

            if (pp.lineseg != null)
            {
                var index = pp.fig.Segments.IndexOf(pp.lineseg);

                pp.fig.Segments.RemoveAt(index);
            }
            else if (pp.bezseg != null)
            {
                SetNextBezierControl1(pp.bezseg.Point1);
                var index = pp.fig.Segments.IndexOf(pp.bezseg);
                pp.fig.Segments.RemoveAt(index);
            }
            else if (pp.polyseg != null)
            {
                // remove a point
                pp.polyseg.Points.RemoveAt(pp.ipoint);
            }
            else if (pp.polybezseg != null)
            {
                // hand off control point 1
                SetNextBezierControl1(pp.polybezseg.Points[pp.ipoint - 2]);

                // remove 3 points starting at point1 offset
                pp.polybezseg.Points.RemoveAt(pp.ipoint - 2);
                pp.polybezseg.Points.RemoveAt(pp.ipoint - 2);
                pp.polybezseg.Points.RemoveAt(pp.ipoint - 2);
            }

            CreateHandles(pp.path);
        }

        void SetNextBezierControl1(Point point)
        {
            // nothing to do
            if (pathPointList.Count <= pathPointIndex + 1)
                return;

            var pp = pathPointList[pathPointIndex + 1];

            // if the next segment is bezier then fix up its first control point, using the first control
            // point of the previous segment which is provided, if next segment is not bezier then forget it

            if (pp.bezseg != null)
            {
                pp.bezseg.Point1 = point;
            }
            else if (pp.polybezseg != null)
            {
                pp.polybezseg.Points[pp.ipoint - 2] = point;
            }            
        }

        bool TryGetNextBezierControl1(out Point point)
        {
            point = new Point(0, 0);

            // if path not active
            if (pathPointList == null || pathPointIndex < 0 || pathPointIndex >= pathPointList.Count)
                return false;

            // nothing to do if at the end
            if (pathPointIndex + 1 >= pathPointList.Count)
                return false;

            var pp = pathPointList[pathPointIndex + 1];

            // if the next segment is bezier then fix up its first control point, using the first control
            // point of the previous segment which is provided, if next segment is not bezier then forget it

            if (pp.bezseg != null)
            {
                point = pp.bezseg.Point1;
                return true;
            }
            else if (pp.polybezseg != null)
            {
                point = pp.polybezseg.Points[pp.ipoint - 2];
                return true;
            }            

            return false;
        }

        void ApplyDeltaNextBezierControl1(Vector delta)
        {
            Point pt;
            if (!TryGetNextBezierControl1(out pt))
                return;

            SetNextBezierControl1(pt + delta);
        }

        void TogglePathSide(int ppIndex)
        {
            // hide or reveal a side
            var pp = pathPointList[ppIndex];

            if (pp.lineseg != null)
            {
                pp.lineseg.IsStroked = !pp.lineseg.IsStroked;
            }
            else if (pp.bezseg != null)
            {
                pp.bezseg.IsStroked = !pp.bezseg.IsStroked;
            }
            else if (pp.polyseg != null)
            {
                var ipoint = pp.ipoint;
                var index = pp.fig.Segments.IndexOf(pp.polyseg);
                Point[] points = pp.polyseg.Points.ToArray();

                if (pp.ipoint > 0)
                {
                    while (pp.polyseg.Points.Count > ipoint)
                        pp.polyseg.Points.RemoveAt(ipoint);
                }
                else
                {
                    pp.fig.Segments.RemoveAt(index);
                    index--;
                }

                var lineseg = new LineSegment(points[ipoint], !pp.polyseg.IsStroked);
                pp.fig.Segments.Insert(++index, lineseg);

                if (ipoint + 1 < points.Length)
                {
                    var polylineseg = new PolyLineSegment();
                    polylineseg.IsStroked = pp.polyseg.IsStroked;

                    for (int i = ipoint + 1; i < points.Length; i++)
                    {
                        polylineseg.Points.Add(points[i]);
                    }
                    pp.fig.Segments.Insert(++index, polylineseg);
                }
            }
            else if (pp.polybezseg != null)
            {
                var ipoint = pp.ipoint - 2;
                var index = pp.fig.Segments.IndexOf(pp.polybezseg);
                Point[] points = pp.polybezseg.Points.ToArray();

                if (ipoint > 0)
                {
                    while (pp.polybezseg.Points.Count > ipoint)
                        pp.polybezseg.Points.RemoveAt(ipoint);
                }
                else
                {
                    pp.fig.Segments.RemoveAt(index);
                    index--;
                }

                var bezseg = new BezierSegment(points[ipoint], points[ipoint + 1], points[ipoint + 2], !pp.polybezseg.IsStroked);
                pp.fig.Segments.Insert(++index, bezseg);

                ipoint += 3;

                if (ipoint < points.Length)
                {
                    var polybezseg = new PolyBezierSegment();
                    polybezseg.IsStroked = pp.polybezseg.IsStroked;

                    for (int i = ipoint; i < points.Length; i++)
                    {
                        polybezseg.Points.Add(points[i]);
                    }
                    pp.fig.Segments.Insert(++index, polybezseg);
                }
            }
            else
            {
                pp.fig.IsClosed = !pp.fig.IsClosed;
            }
        }

        Point GetPreviousPoint(PathPoint pp)
        {
            int index = pathPointList.IndexOf(pp);

            if (index == 0)
            {
                return pathPointList[pathPointList.Count - 1].v;
            }
            else
            {
                return pathPointList[index - 1].v;
            }
        }

        BezierHull GetBezHull(PathPoint pp)
        {
            Point pointPrev = GetPreviousPoint(pp); 

            if (pp.bezseg != null)
            {
                // make the hull for bezier curves
                var hull = new BezierHull();

                hull.Start = pointPrev;
                hull.Control1 = pp.bezseg.Point1;
                hull.Control2 = pp.bezseg.Point2;
                hull.End = pp.bezseg.Point3;
                hull.Compute();

                return hull;
            }
            else if (pp.polybezseg != null)
            {
                // make the hull for bezier curves
                var hull = new BezierHull();
                int ipoint = pp.ipoint;

                hull.Start = pointPrev;
                hull.Control1 = pp.polybezseg.Points[ipoint - 2];
                hull.Control2 = pp.polybezseg.Points[ipoint - 1];
                hull.End = pp.polybezseg.Points[ipoint];
                hull.Compute();

                return hull;
            }
            else
            {
                return null;
            }
        }

        Vector GetDirectionVector(Line line)
        {
            Vector vector = new Vector(line.X2 - line.X1, line.Y2 - line.Y1);

            NormalizeVector(ref vector);

            return vector;
        }

        static void NormalizeVector(ref Vector vector)
        {
            double length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);

            if (length == 0)
            {
                length = 1;
                vector.X = 1;
                vector.Y = 1;
            }

            vector.X /= length;
            vector.Y /= length;
        }

        Vector GetDirectionVector(PathPoint pp)
        {
            Vector vector = new Vector();
            int index = pathPointList.IndexOf(pp);

            if (index != 0)
            {
                var hull = GetBezHull(pp);

                if (hull != null)
                {
                    // for beziers use the line from control2 to the endpoint as the direction vector
                    // that's incoming tangent at the vertex moving in the natural direction of the path
                    vector = hull.End - hull.Control2;
                }
                else
                {
                    // for non beziers, the direction vector is simply previous point to here
                    vector = pp.v - GetPreviousPoint(pp);
                }
            }
            else
            {
                // index is zero, the direction vector we want is from node 1 to zero, the reverse of the normal direction
                Point control1;
                if (TryGetNextBezierControl1(out control1))
                {
                    // use the incoming tangent if there is one
                    vector = pp.v - control1;
                }
                else
                {
                    // otherwise use the position of node 1 (for straight line segments)
                    vector = pp.v - pathPointList[1].v;
                }
            }

            NormalizeVector(ref vector);

            return vector;
        }

        Point Midpoint(Point p1, Point p2)
        {
            return new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        }

        static void MoveHandle(object sender, Point point)
        {
            FrameworkElement handle = sender as FrameworkElement;

            if (Double.IsNaN(handle.Width) || Double.IsNaN(handle.Height))
                handle.Margin = RoundMargin(point.X, point.Y, 0, 0);
            else
                handle.Margin = RoundMargin(point.X - handle.Width / 2, point.Y - handle.Height / 2, 0, 0);
        }

        void SetBezControl1(int ppIndex, Point target)
        {
            var pp = pathPointList[ppIndex];

            if (pp.bezseg != null)
            {
                pp.bezseg.Point1 = target;
            }
            else if (pp.polybezseg != null)
            {
                pp.polybezseg.Points[pp.ipoint - 2] = target;
            }
        }

        void SetBezControl2(int ppIndex, Point target)
        {
            var pp = pathPointList[ppIndex];

            if (pp.bezseg != null)
            {
                pp.bezseg.Point2 = target;
            }
            else if (pp.polybezseg != null)
            {
                pp.polybezseg.Points[pp.ipoint - 1] = target;
            }
        }

        Point GetBezControl1(int ppIndex)
        {
            var pp = pathPointList[ppIndex];

            if (pp.bezseg != null)
            {
                return pp.bezseg.Point1;
            }
            else if (pp.polybezseg != null)
            {
                return pp.polybezseg.Points[pp.ipoint - 2];
            }

            return new Point(0, 0);
        }

        Point GetBezControl2(int ppIndex)
        {
            var pp = pathPointList[ppIndex];

            if (pp.bezseg != null)
            {
                return pp.bezseg.Point2;
            }
            else if (pp.polybezseg != null)
            {
                return pp.polybezseg.Points[pp.ipoint - 1];
            }

            return new Point(0, 0);
        }

        void MovePathNode(Point pointNew)
        {
            var pp = pathPointList[pathPointIndex];

            if (pp.lineseg != null)
            {
                // line seg has only one position
                pp.lineseg.Point = pointNew;
            }
            else if (pp.polyseg != null)
            {
                // poly seg has only one position, set it
                int ipoint = pp.ipoint;
                pp.polyseg.Points[ipoint] = pointNew;
            }
            else if (pp.polybezseg != null)
            {
                int ipoint = pp.ipoint;
                Vector delta = pointNew - pp.polybezseg.Points[ipoint];

                // move the end point to the indicated location
                // adjust the control points around this point so that they move an equal amount
                pp.polybezseg.Points[ipoint] = pointNew;
                pp.polybezseg.Points[ipoint - 1] += delta;
                ApplyDeltaNextBezierControl1(delta);
            }
            else if (pp.bezseg != null)
            {
                Vector delta = pointNew - pp.bezseg.Point3;

                // move the end point to the indicated location
                // adjust the control points around this point so that they move an equal amount
                pp.bezseg.Point2 += delta;
                pp.bezseg.Point3 = pointNew;
                ApplyDeltaNextBezierControl1(delta);
            }
            else
            {
                // if moving the start node, just move it
                pp.fig.StartPoint = pointNew;
            }
        }

        void RightAngleAdjustMousePosition(Point pointReference, ref Point pointMouseCur)
        {
            double xRef = pointReference.X;
            double yRef = pointReference.Y;

            double dx = RoundCoordinate(Math.Abs(pointMouseCur.X - xRef));
            double dy = RoundCoordinate(Math.Abs(pointMouseCur.Y - yRef));

            if (dx < dy / 2)
            {
                // vertical snap, dx is pretty small
                pointMouseCur.X = xRef;
            }
            else if (dy < dx / 2)
            {
                // horizontal snap, dy is pretty small
                pointMouseCur.Y = yRef;
            }
            else
            {
                // snapping to 45 degrees, big the bigger delta
                dy = dx = Math.Max(dx, dy);

                // move the indicated delta in the direction that the mouse has moved, same for x and y
                if (pointMouseCur.X > xRef)
                    pointMouseCur.X = xRef + dx;
                else
                    pointMouseCur.X = xRef - dx;

                if (pointMouseCur.Y > yRef)
                    pointMouseCur.Y = yRef + dy;
                else
                    pointMouseCur.Y = yRef - dy;
            }
        }

        SnapAction SnapPoint(ref Point pt)
        {
            const double near = 36;

            // right angle snapping -- forget the whole grid thing
            if (IsCtrlDown())
                return SnapAction.None;

            // hard grid snapping, forget the soft snap
            if (IsHardSnapEnabled())
            {
                return HardSnapPoint(ref pt);
            }

            // drop a lot of digits
            pt = RoundPoint(pt);

            foreach (UIElement ui in canvas.Children)
            {
                Path p = ui as Path;
                Line l = ui as Line;

                if (p != null)
                {
                    PathFigure pf;
                    ArcSegment arc;

                    if (TryExtractArc(p, out pf, out arc))
                    {
                        if (ui == handleTarget)
                            continue;

                        if (DistanceSquared(pf.StartPoint, pt) <= near)
                        {
                            pt = pf.StartPoint;
                            return SnapAction.Object;
                        }

                        if (DistanceSquared(arc.Point, pt) <= near)
                        {
                            pt = arc.Point;
                            return SnapAction.Object;
                        }
                    }
                    else
                    {
                        int ppIndex = 0;
                        foreach (var pp in EnumeratePathPoints(p))
                        {
                            // path can snap to any vertex other than the current vertex
                            if (ui != handleTarget || ppIndex != pathPointIndex)
                            {
                                if (DistanceSquared(pp.v, pt) <= near)
                                {
                                    pt = pp.v;
                                    return SnapAction.Object;
                                }
                            }
                            ppIndex++;
                        }
                    }
                }
                else if (l != null)
                {
                    if (ui == handleTarget)
                        continue;

                    if (DistanceSquared(l.X1, pt.X, l.Y1, pt.Y) <= near)
                    {
                        pt = new Point(l.X1, l.Y1);
                        return SnapAction.Object;
                    }


                    if (DistanceSquared(l.X2, pt.X, l.Y2, pt.Y) <= near)
                    {
                        pt = new Point(l.X2, l.Y2);
                        return SnapAction.Object;
                    }
                }
            }

            return SoftSnapPoint(ref pt);
        }

        void AnimateDisappearance(FrameworkElement el)
        {
            Grid parent = (Grid)((Grid)this.Parent).Parent;
            Line l = el as Line;

            double cx = 0;
            double cy = 0;

            if (el is Line)
            {
                cx = (l.X1 + l.X2) / 2;
                cy = (l.Y1 + l.Y2) / 2;
            }
            else if (el is Path)
            {
                Path path = el as Path;
                ArcSegment arc;
                PathFigure pf;

                if (TryExtractArc(path, out pf, out arc))
                {
                    Point p = LocateArcCenter(pf, arc);
                    cx = p.X;
                    cy = p.Y;
                }
                else
                {
                    ComputeElementBoxCenter(el, out cx, out cy);
                }
            }
            else
            {
                cx = el.ActualWidth / 2;
                cy = el.ActualHeight / 2;
            }

            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(el, new NameScope());

            var scale = new ScaleTransform();
            el.RenderTransform = scale;

            // Register the name with the page to which the element belongs.
            el.RegisterName("scale", scale);
            scale.CenterX = cx;
            scale.CenterY = cy;

            Duration dur = new Duration(TimeSpan.FromMilliseconds(250));

            Anim2Point(story, dur, "scale", ScaleTransform.ScaleXProperty, 1, 0);
            Anim2Point(story, dur, "scale", ScaleTransform.ScaleYProperty, 1, 0);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    el.ClearValue(RenderTransformProperty);
                    canvas.Children.Remove(el);
                });

            story.Begin(el);
        }

        void ComputeElementBoxCenter(FrameworkElement el, out double cx, out double cy)
        {
            Rectangle boundingRect = new Rectangle();
            ComputeBoundingRect(el, boundingRect);
            cx = boundingRect.Margin.Left;
            cy = boundingRect.Margin.Top;
            cx += boundingRect.Width / 2;
            cy += boundingRect.Height / 2;
        }

        void AnimatePolygonAppearance(FrameworkElement el)
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(el, new NameScope());

            var scale = new ScaleTransform();
            el.RenderTransform = scale;

            // Register the name with the page to which the element belongs.
            el.RegisterName("scale", scale);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(250));

            Anim2Point(story, dur, "scale", ScaleTransform.ScaleXProperty, 1.5, 1);
            Anim2Point(story, dur, "scale", ScaleTransform.ScaleYProperty, 1.5, 1);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    el.ClearValue(RenderTransformProperty);
                });

            story.Begin(el);
        }

        void AnimateAppearance(FrameworkElement el, bool addHandles, int durationMS = 250)
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(el, new NameScope());
            
            Duration dur = new Duration(TimeSpan.FromMilliseconds(durationMS));

            if (el is Line)
                AddLineEndAnimation(el as Line, story, dur);
            else
                AddScaleAnimation(el, story, dur);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    el.ClearValue(RenderTransformProperty);
                    if (addHandles)
                        CreateHandles(el);
                });

            story.Begin(el);
        }

        static void AddLineEndAnimation(Line line, Storyboard story, Duration dur)
        {
            // Register the name with the page to which the element belongs.
            line.RegisterName("line", line);

            Anim2Point(story, dur, "line", Line.X2Property, line.X1, line.X2);
            Anim2Point(story, dur, "line", Line.Y2Property, line.Y1, line.Y2);
        }

        static void AddScaleAnimation(FrameworkElement el, Storyboard story, Duration dur)
        {
            var scale = new ScaleTransform();
            el.RenderTransform = scale;

            // Register the name with the page to which the element belongs.
            el.RegisterName("scale", scale);

            scale.CenterX = el.Width / 2;
            scale.CenterY = el.Height / 2;

            Anim2Point(story, dur, "scale", ScaleTransform.ScaleXProperty, 1.5, 1);
            Anim2Point(story, dur, "scale", ScaleTransform.ScaleYProperty, 1.5, 1);
        }

        void RemoveNonPathNodeHandles()
        {
            var q = from tag in handles.Keys where !tag.StartsWith("path_node:") select tag;
            var tags = q.ToArray();
            foreach (var tag in tags)
            {
                RemoveHandleByTag(tag);
            }
        }

        void ClearHandles()
        {
            if (handles != null)
            {
                foreach (HandleInfo handle in handles.Values)
                    canvas.Children.Remove(handle.shape);

                handles.Clear();
            }

            pathPointList = null;
            handleTarget = null;

            if (selectedListBoxes != null)
            {
                foreach (FrameworkElement el in selectedListBoxes)
                {
                    canvas.Children.Remove(el);
                }
            }

            selectedListBoxes = null;
            selectedList = null;

            if (MainWindow.propertyWindow != null && MainWindow.propertyGameMap == this)
            {
                MainWindow.propertyWindow.ClearRows();
            }
        }

        bool configuredFont = false;

        void buttonChangeFont_Click(object sender, RoutedEventArgs e)
        {
            var fontChooser = new FontDialogSample.FontChooser();
            fontChooser.Owner = MainWindow.mainWindow;

            fontChooser.SetPropertiesFromObject(fontSampleText);
            fontChooser.PreviewSampleText = "The quick brown fox jumps over the lazy white dog.";

            if (fontChooser.ShowDialog().Value)
            {
                fontChooser.ApplyPropertiesToObject(fontSampleText);
                configuredFont = true;

                SetOwnFontName();
            }
        }

        void massmove_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (selectedList == null || selectedListBoxes == null)
            {
                return;
            }

            var el = (Rectangle)selectedListBoxes[0];

            WireRectangleForMassmove(el, e.GetPosition(canvas));
            BeginMouseCapture(el, e);
            e.Handled = true;
        }

        void WireRectangleForMassmove(Rectangle rect, Point mousePoint)
        {
            Point pointStart = mousePoint;
            Point originalReference = GetReferencePointForSelection();
            bool dirty = false;

            MouseEventHandler massmove_MouseMove = (sender, e) =>
            {
                if (moving != sender)
                    return;

                if (selectedList == null || selectedListBoxes == null)
                {
                    return;
                }

                Point pointEnd = e.GetPosition(canvas);

                if (IsHardSnapEnabled())
                {
                    // the effective end point of the move is adjusted so that it is on a hard snap point relative to its origin, that is
                    // we are moving by a hard snap amount
                    pointEnd = pointStart + HardSnapVector(pointEnd - pointStart);
                }
                else if (!IsCtrlDown())
                {
                    // the effective end point of the move is adjusted so that it is on a hard snap point relative to its origin, that is
                    // we are moving by a soft snap amount
                    pointEnd = pointStart + SoftSnapVector(pointEnd - pointStart);
                }

                Point currentReference = GetReferencePointForSelection();

                // now we look at how far we have moved from the original starting point
                // we must move that amount, less the amount we have already moved
                Vector delta = (pointEnd - pointStart) - (currentReference - originalReference);

                MoveSelectedElements(delta.X, delta.Y);

                dirty = true;
            };

            MouseButtonEventHandler massmove_MouseUp = (sender, e) =>
            {
                if (moving != sender)
                    return;

                if (e.ChangedButton != MouseButton.Left)
                    return;

                if (dirty)
                {
                    BeginUndoUnit();

                    foreach (FrameworkElement el in selectedList)
                        SaveFrameworkElement(el);
                }

                EndMouseCapture(sender);
                UnwireObjectUsingReflection(rect);
            };
        
            rect.MouseMove += massmove_MouseMove;
            rect.MouseUp += massmove_MouseUp;
        }

        Point GetReferencePointForSelection()
        {
            if (selectedList != null)
                for (int i = 0; i < selectedList.Count; i++)
                {
                    if (!IsUnmoveable(selectedList[i]))
                    {
                        return ReferencePoint(selectedList[i]);
                    }
                }

            if (handleTarget != null && !IsUnmoveable(handleTarget))
            {
                return ReferencePoint(handleTarget);
            }

            return new Point(0, 0);
        }

        void BulkMove_Click(object sender, RoutedEventArgs e)
        {
            ManyKey dlg = new ManyKey("Bulk Move Selection", "Tiles Down (+/-):", "0", "Tiles Right (+/-):", "0");
            if (dlg.ShowDialog() != true)
                return;

            int y;               
            if (!int.TryParse(dlg.Results[0], out y))
                return;

            int x;               
            if (!int.TryParse(dlg.Results[1], out x))
                return;

            double dx = x * Tile.MajorGridSize;
            double dy = y * Tile.MajorGridSize;

            MoveSelectedElements(dx, dy, moveLockedAlso: true);

            BeginUndoUnit();

            if (selectedList != null)
            {
                foreach (FrameworkElement el in selectedList)
                    SaveFrameworkElement(el);
            }
            else if (handleTarget != null)
            {
                SaveFrameworkElement(handleTarget);
            }
        }

        void MoveSelectedElements(double dx, double dy, bool moveLockedAlso = false)
        {
            if (selectedList != null)
                for (int i = 0; i < selectedList.Count; i++)
                {
                    if (moveLockedAlso || !IsUnmoveable(selectedList[i]))
                    {
                        MoveElement(selectedList[i], dx, dy);
                        MoveElement(selectedListBoxes[i], dx, dy);
                    }
                }

            if (handleTarget != null && (moveLockedAlso || !IsUnmoveable(handleTarget)))
            {
                MoveElement(handleTarget, dx, dy);
                if (handles != null)
                {
                    foreach (HandleInfo handle in handles.Values)
                        MoveElement(handle.shape, dx, dy);
                }
            }
        }

        FrameworkElement ReplaceElement(string k, string v)
        {
            foreach (var c in canvas.Children)
            {
                FrameworkElement child = c as FrameworkElement;

                if (child == null)
                    continue;

                if (child.Tag != null && child.Tag.ToString() == k)
                {
                    UnlinkElement(child);
                    AnimateDisappearance(child);
                }
            }

            if (v == "")
                return null;

            byte[] byteData = Encoding.ASCII.GetBytes(v);

            Object el = null;

            try
            {
                el = System.Windows.Markup.XamlReader.Load(new System.IO.MemoryStream(byteData));
            }
            catch (Exception)
            {
                return null;
            }

            if (!(el is FrameworkElement))
                return null;

            FrameworkElement element = (FrameworkElement)el;

            element.Tag = k;
            WireObject(element);
            SaveFrameworkElement(element);
            AddCanvasChild(element);

            return element;
        }

        void Undo_Click(object sender, RoutedEventArgs e)
        {
            ClearHandles();
            int c = undoHistory.Count;

            while (c > 0)
            {
                c--;
                var undo = undoHistory[c];
                undoHistory.RemoveAt(c);
                redoHistory.Insert(0, undo);

                if (undo.Key == "")
                {
                    break;
                }

                suspendUndo = true;
                var el = ReplaceElement(undo.Key, undo.Before);
                suspendUndo = false;

                if (el != null)
                {
                    if (selectedList == null)
                        selectedList = new List<FrameworkElement>();

                    selectedList.Add(el);
                }
            }

            var sel = selectedList;

            Main.DelayAction(300, () =>
            {
                if (sel == selectedList)
                    AddBoundingBoxesForSelection();
            });
        }

        void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (redoHistory.Count == 0)
                return;

            if (redoHistory[0].Key != "")
                return;

            undoHistory.Add(redoHistory[0]);
            redoHistory.RemoveAt(0);

            ClearHandles();
            while (redoHistory.Count > 0)
            {
                var undo = redoHistory[0];
                if (undo.Key == "")
                {
                    break;
                }

                redoHistory.RemoveAt(0);
                undoHistory.Add(undo);

                suspendUndo = true;
                var el = ReplaceElement(undo.Key, undo.After);
                suspendUndo = false;

                if (el != null)
                {
                    if (selectedList == null)
                        selectedList = new List<FrameworkElement>();

                    selectedList.Add(el);
                }
            }

            var sel = selectedList;

            Main.DelayAction(300, () =>
            {
                if (sel == selectedList)
                    AddBoundingBoxesForSelection();
            });
        }

        struct UndoRecord
        {
            internal string Key;
            internal string Before;
            internal string After;
        }

        List<UndoRecord> undoHistory = new List<UndoRecord>();
        List<UndoRecord> redoHistory = new List<UndoRecord>();

        bool suspendUndo = false;

        void BeginUndoUnit()
        {
            if (suspendUndo)
                return;

            // don't add a marker if we are already at one
            int c = undoHistory.Count;
            if (c > 0 && undoHistory[c - 1].Key == "")
                return;

            AddUndoRecord("", "", "");
            redoHistory.Clear();
        }

        void AddUndoRecord(string key, string before, string after)
        {
            if (suspendUndo)
                return;

            if (undoHistory.Count > 1000)
            {
                int index = undoHistory.FindIndex(20, (UndoRecord item) => item.Key == "");
                if (index >= 0)
                {
                    undoHistory.RemoveRange(0, index);
                }
            }
            undoHistory.Add(new UndoRecord() { Key = key, Before = before, After = after });
        }

        Style redStyle;
        Style blueStyle;
        bool[] disableLayer = new bool[10];

        void layer_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;

            if (b == null)
                return;

            if (redStyle == null)
            {
                redStyle = Main.FindResource("RedButton") as Style;
            }

            if (blueStyle == null)
            {
                blueStyle = b.Style;
            }

            if (b.Style == redStyle)
            {
                b.Style = blueStyle;
            }
            else
            {
                b.Style = redStyle;
            }

            string s = b.Content as String;

            if (s == null)
                return;

            if (s == "c")
            {
                StackPanel p = b.Parent as StackPanel;
                foreach (var child in p.Children)
                {
                    var but = child as Button;
                    if (but != null)
                        but.Style = blueStyle;
                }

                for (int i = 0; i < disableLayer.Length; i++)
                {
                    disableLayer[i] = false;
                }
            }
            else if (IsCtrlDown())
            {
                int layer = (b.Content as string)[0] - '0';

                StackPanel p = b.Parent as StackPanel;
                foreach (var child in p.Children)
                {
                    var but = child as Button;
                    if (but != null)
                    {
                        string str = but.Content as String;
                        if (str != null && str != "c")
                            but.Style = redStyle;
                    }
                }

                for (int i = 0; i < disableLayer.Length; i++)
                {
                    disableLayer[i] = true;
                }

                disableLayer[layer] = false;
                b.Style = blueStyle;
            }
            else
            {
                int layer = (b.Content as string)[0] - '0';

                disableLayer[layer] = !disableLayer[layer];
            }

            ReplaceCanvasContents(dictCurrent);
        }

        static List<string> clipBoardData = new List<string>();
        double pasteDelta;
        string defaultMap = "default";        

        void Copy_Click(object sender, RoutedEventArgs e)
        {
            pasteDelta = 0;
            clipBoardData.Clear();

            if (selectedList != null)
            {
                foreach (var el in selectedList)
                {
                    CopyElement(el);
                }
            }

            if (handleTarget != null)
            {
                CopyElement(handleTarget);
            }
        }

        void CopyElement(FrameworkElement el)
        {
            var tag = el.Tag as string;
            if (tag == null)
            {
                return;
            }

            string value;
            if (!dictCurrent.TryGetValue(tag, out value))
            {
                return;
            }

            clipBoardData.Add(tag);
            clipBoardData.Add(value);
        }

        void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (clipBoardData.Count % 2 != 0)
            {
                return;
            }

            BeginUndoUnit();
            ClearHandles();

            pasteDelta += 10;

            selectedList = new List<FrameworkElement>();

            bool first = true;
            Point p0 = new Point();
            Point p1 = new Point();

            ScrollViewer parent = (ScrollViewer)(canvas.Parent);
            var ptParent = new Point(parent.ActualWidth / 4, parent.ActualHeight / 4);
            var ptNewOrigin = parent.TranslatePoint(ptParent, canvas);

            for (int i = 0; i < clipBoardData.Count; i += 2)
            {
                var k = clipBoardData[i];
                var v = clipBoardData[i + 1];

                int idStart = (Int32.Parse(k) & ~1);

                byte[] byteData = Encoding.ASCII.GetBytes(v);
                FrameworkElement el = (FrameworkElement)System.Windows.Markup.XamlReader.Load(new System.IO.MemoryStream(byteData));

                selectedList.Add(el);

                var tag = FindFreeIdFromStart(idStart);
                el.Tag = tag;
                WireObject(el);
                AddCanvasChild(el);

                Line l = el as Line;
                Path p = el as Path;

                if (l != null)
                {
                    p1 = new Point(l.X1, l.Y1);
                }
                else if (p != null)
                {
                    PathFigure pf;
                    if (!TryExtractPathFigure(p, out pf))
                        return;

                    p1 = pf.StartPoint;
                }
                else
                    p1 = new Point(el.Margin.Left, el.Margin.Top);

                if (first)
                {
                    p0 = p1;
                    first = false;
                }

                Point p4 = new Point(ptNewOrigin.X + p1.X - p0.X + pasteDelta, ptNewOrigin.Y + p1.Y - p0.Y + pasteDelta);

                if (l != null)
                {
                    MoveLineNoSnap(l, p4);
                }
                else if (p != null)
                {
                    MovePath(p, p4);
                }
                else
                {
                    el.Margin = RoundMargin(p4.X, p4.Y, 0, 0);
                }

                SaveFrameworkElement(el);
            }

            var sel = selectedList;

            Main.DelayAction(300, () =>
            {
                if (sel == selectedList)
                    AddBoundingBoxesForSelection();
            });
        }

        void Cut_Click(object sender, RoutedEventArgs e)
        {
            Copy_Click(sender, e);
            DeleteSelection();
        }

        void AddParty_Click(object sender, RoutedEventArgs e)
        {
            string args;
            if (this == Main.map1)
            {
                args = "map1";
            }
            else
            {
                args = "map2";
            }

            Main.SendHost(String.Format("icons {0}", args));
        }

        void Button_Click(object sender, RoutedEventArgs e)
        {
            Main.SendChat(String.Format("!gameaid map {0}", currentPath.Text));
            Main.SendHost(String.Format("!gameaid map {0}", currentPath.Text));
        }

        void DoubleGrid_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in canvas.Children)
            {
                var el = child as FrameworkElement;
                if (el == null)
                    continue;

                if (el.Tag as String != "000000")
                    continue;

                BeginUndoUnit();
                el.Width = el.Width * 2;
                el.Height = el.Height * 2;
                SaveFrameworkElement(el);
                return;
            }
        }
    }
}
