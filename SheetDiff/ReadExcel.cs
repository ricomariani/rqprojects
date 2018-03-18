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
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Xml;

namespace TestApp
{
    class ExcelReader
    {
        public ExcelReader()
        {
            SetExcelColors();
        }

        List<string> strings = new List<string>();
        Dictionary<string, string> values = null;
        Dictionary<string, string> formulas = null;
        Dictionary<string, int> cellstyles = null;

        Dictionary<int, string> colorMap = new Dictionary<int, string>();
        Dictionary<string, int> colorNames = new Dictionary<string, int>();

        Package package = null;

        string path;

        public Dictionary<string, string> Values() { return values; }

        public void ImportSheet(string path, StringBuilder errors)
        {
            this.path = path;

            if (!TryOpenPackage(errors, path))
                return;

            using (package)
            {
                ReadSharedStrings(package);
                ReadStyleSection(package);

                ReadSheet(package, "/xl/worksheets/sheet1.xml");

                if (values == null)
                    return;
            }
        }

        bool TryOpenPackage(StringBuilder errors, string path)
        {
            try
            {
                package = Package.Open(path, FileMode.Open);
            }
            catch (Exception e)
            {
                errors.AppendLine(e.Message);
                return false;
            }

            return true;
        }

        class Font
        {
            public string color;
        }

        class StyleIndex
        {
            public int fontIndex;
        }

        List<Font> fonts = new List<Font>();
        List<StyleIndex> styles = new List<StyleIndex>();

        void ReadStyleSection(Package package)
        {
            PackagePart p = package.GetPart(new Uri("/xl/styles.xml", UriKind.Relative));

            var reader = new XmlTextReader(p.GetStream());

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        if (reader.Name == "fonts")
                        {
                            ReadFonts(reader);
                        }
                        else if (reader.Name == "cellXfs")
                        {
                            ReadStyles(reader);
                        }

                        break;

                    case XmlNodeType.Text: //Display the text in each element.
                        // Console.WriteLine(reader.Value);
                        break;
                    case XmlNodeType.EndElement: //Display the end of the element.
                        // Console.Write("</" + reader.Name);
                        // Console.WriteLine(">");
                        break;
                }
            }

            reader.Close();
        }

        void ReadStyles(XmlTextReader reader)
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        if (reader.Name == "xf")
                        {
                            styles.Add(ReadStyle(reader));
                        }
                        break;

                    case XmlNodeType.Text: //Display the text in each element.
                        // Console.WriteLine(reader.Value);
                        break;
                    case XmlNodeType.EndElement: 
                        if (reader.Name == "cellXfs")
                            return;
                        break;
                }
            }
        }

        void ReadFonts(XmlTextReader reader)
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        if (reader.Name == "font")
                        {
                            fonts.Add(ReadFont(reader));
                        }
                        break;

                    case XmlNodeType.Text: //Display the text in each element.
                        // Console.WriteLine(reader.Value);
                        break;
                    case XmlNodeType.EndElement:
                        if (reader.Name == "fonts")
                            return;
                        break;
                }
            }
        }

        StyleIndex ReadStyle(XmlTextReader reader)
        {
            var style = new StyleIndex();
            style.fontIndex = -1;

            var font = reader.GetAttribute("fontId");
            int fontId = 0;
            if (Int32.TryParse(font, out fontId))
                style.fontIndex = fontId;

            return style;
        }


        Font ReadFont(XmlTextReader reader)
        {
            var font = new Font();
            font.color = "";

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        if (reader.Name == "color")
                        {
                            string color;
                            
                            color = reader.GetAttribute("indexed");
                            if (color != null)
                            {
                                int index;
                                if (Int32.TryParse(color, out index)) {
                                    if (colorMap.ContainsKey(index))
                                        font.color = colorMap[index];
                                }
                            }

                            color = reader.GetAttribute("rgb");
                            if (color != null)
                                font.color = color;                            
                        }
                        break;

                    case XmlNodeType.Text: //Display the text in each element.
                        break;

                    case XmlNodeType.EndElement: //Display the end of the element.
                        if (reader.Name == "font")
                            return font;

                        break;
                }
            }

            return font;
        }
       
        void ReadSharedStrings(Package package)
        {
            PackagePart p = package.GetPart(new Uri("/xl/sharedStrings.xml", UriKind.Relative));

            var reader = new XmlTextReader(p.GetStream());

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        if (reader.Name == "si")
                        {
                            string s = ReadSharedString(reader);
                            strings.Add(s);
                        }
                        break;

                    case XmlNodeType.Text: //Display the text in each element.
                        // Console.WriteLine(reader.Value);
                        break;
                    case XmlNodeType.EndElement: //Display the end of the element.
                        // Console.Write("</" + reader.Name);
                        // Console.WriteLine(">");
                        break;
                }
            }

            reader.Close();
        }

        void ReadSheet(Package package, string sheetPath)
        {
            values = new Dictionary<string, string>();
            formulas = new Dictionary<string, string>();
            cellstyles = new Dictionary<string, int>();

            Uri uri = new Uri(sheetPath, UriKind.Relative);

            if (!package.PartExists(uri))
            {
                values = null;
                formulas = null;
                cellstyles = null;
                return;
            }

            PackagePart p = package.GetPart(uri);

            var reader = new XmlTextReader(p.GetStream());

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "c")
                    {
                        string key;
                        string content;
                        string formula;
                        int cellstyle;
                        ReadCell(reader, out key, out content, out formula, out cellstyle);

                        if (key != "")
                            values[key] = content;

                        if (formula != "")
                            formulas[key] = formula;

                        if (cellstyle != -1)
                            cellstyles[key] = cellstyle;
                    }
                }
            }

            reader.Close();
        }

        public int I(string col, int row)
        {
            return GetIntAt(col, row);
        }

        public double D(string col, int row)
        {
            return GetDoubleAt(col, row);
        }

        public float F(string col, int row)
        {
            return (float)GetDoubleAt(col, row);
        }

        public string S(string col, int row)
        {
            string k = String.Format("{0}{1}", col, row);
            if (values.ContainsKey(k))
                return values[k];
            else
                return "";
        }

        string Formula(string col, int row)
        {
            string k = String.Format("{0}{1}", col, row);
            if (formulas.ContainsKey(k))
                return formulas[k];
            else
                return "";
        }

        int Style(string col, int row)
        {
            string k = String.Format("{0}{1}", col, row );
            if (cellstyles.ContainsKey(k))
                return cellstyles[k];
            else
                return -1;
        }

        string Color(string col, int row)
        {
            int cellstyle = Style(col, row);
            if (cellstyle == -1)
                return "";

            if (cellstyle >= styles.Count)
                return "";

            var s = styles[cellstyle];

            if (s.fontIndex >= fonts.Count)
                return "";

            return fonts[s.fontIndex].color;
        }

        void ReadCell(XmlTextReader reader, out string key, out string content, out string formula, out int cellstyle)
        {
            bool needValue = false;
            bool needFormula = false;
            cellstyle = -1;
   
            key = reader.GetAttribute("r");

            string style = reader.GetAttribute("s");
            if (!Int32.TryParse(style, out cellstyle))
                cellstyle = -1;
                
            string type = reader.GetAttribute("t");
            content = "";
            formula = "";
            string si = "";

            if (reader.IsEmptyElement)
                return;

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                case XmlNodeType.Element:
                        if (reader.Name == "v")
                        {
                            needValue = true;
                            needFormula = false;
                            si = null;
                        }
                        else if (reader.Name == "f")                      
                        {
                            needValue = false;
                            si = reader.GetAttribute("si");

                            if (reader.IsEmptyElement && si != null && si != "")
                            {
                                formula = formulas[si];
                                needFormula = false;
                            }
                            else
                                needFormula = true;
                        }
                    break;

                case XmlNodeType.Text:
                    if (needValue)
                        content = reader.Value;
                    else if (needFormula)
                    {
                        formula = reader.Value;
                        if (si != null && si != "")
                        {
                            if (formula != "")
                                formulas[si] = formula;
                        }
                    }
                    break;

                case XmlNodeType.EndElement:
                    if (reader.Name == "v")
                    {
                        needValue = false;
                        needFormula = false;
                    }
                    else if (reader.Name == "f")
                    {
                        needValue = false;
                        needFormula = false;
                    }
                    else if (reader.Name == "c")
                    {
                        if (type == "s")
                            content = strings[Int32.Parse(content)];

                        return;
                    }
                    
                    break;
                }
            }
            return;
        }

        string ReadSharedString(XmlTextReader reader)
        {
            StringBuilder b = new StringBuilder();
            bool addText = false;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        if (reader.Name == "t")
                            addText = true;
                        break;

                    case XmlNodeType.Text: //Display the text in each element.
                        if (addText)
                            b.Append(reader.Value);
                        break;

                    case XmlNodeType.EndElement: //Display the end of the element.
                        if (reader.Name == "t")
                            addText = false;
                        else if (reader.Name == "si")
                            return b.ToString();
                        break;
                }
            }
            return b.ToString();
        }

        string Keysafe(string col, int row)
        {
            return S(col, row).Trim().Replace(' ', '_');
        }

        string Skillname(string col, int row)
        {
            return NormalizeStringForGameaid(S(col, row));
        }

        string NormalizeStringForGameaid(string s)
        {
            s = s.ToLower().Replace(":", "").Replace("*", "").Trim();

            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case '*':
                    case ' ':
                    case ',':
                    case '/':
                    case '+':
                    case '-':
                    case '(':
                    case ')':
                        chars[i] = '_';
                        break;
                }
            }

            s = new string(chars);
            s = s.Replace(",_", ",");
            var t = s.Replace("__", "_");

            while (s != t) 
            {
                s = t;
                t = s.Replace("__", "_");
            }

            while (s.EndsWith("_"))
            {
                s = s.Substring(0, s.Length - 1);
            }

            return s;
        }

        int GetIntAt(string r, int col)
        {
            string val = S(r, col);
            if (val == "")
                return 0;

            double result;
            if (!Double.TryParse(val, out result))
                return 0;

            double v = Math.Round(result);

            return (int)v;
        }
        double GetDoubleAt(string r, int col)
        {
            string val = S(r, col);
            if (val == "")
                return 0;

            double result;
            if (!Double.TryParse(val, out result))
                return 0;

            return result;
        }

        string GetIntCell(string r, int col)
        {
            return GetIntAt(r, col).ToString();
        }

        void SetExcelColors()
        {
            // some standard excel colors

            // aqua    => 0x0F,
            // cyan    => 0x0F,
            // black   => 0x08,
            // blue    => 0x0C,
            // brown   => 0x10,
            // magenta => 0x0E,
            // fuchsia => 0x0E,
            // gray    => 0x17,
            // grey    => 0x17,
            // green   => 0x11,
            // lime    => 0x0B,
            // navy    => 0x12,
            // orange  => 0x35,
            // purple  => 0x14,
            // red     => 0x0A,
            // silver  => 0x16,
            // white   => 0x09,
            // yellow  => 0x0D,


            /* black  */ SetColorIndex( 8, "FF000000");
            /* blue   */ SetColorIndex(12, "FF0000FF");
            /* cyan   */ SetColorIndex(15, "FF00FFFF");
            /* gray   */ SetColorIndex(23, "FF808080");
            /* green  */ SetColorIndex(17, "FF008000");
            /* magenta*/ SetColorIndex(14, "FFFF00FF");
            /* red    */ SetColorIndex(10, "FFFF0000");
            /* white  */ SetColorIndex( 9, "FFFFFFFF");
            /* yellow */ SetColorIndex(13, "FFFFFF00");

            SetColorName("black",  8);
            SetColorName("blue", 12);
            SetColorName("cyan", 15);
            SetColorName("gray", 23);
            SetColorName("green", 17);
            SetColorName("magenta", 14);
            SetColorName("red", 10);
            SetColorName("white",  9);
            SetColorName("yellow", 13);
        }
        
        void SetColorIndex(int index, string rgb)
        {
            colorMap.Add(index, rgb);
        }

        void SetColorName(string name, int index)
        {
            colorNames.Add(name, index);
        }

        string GetColorByName(string name)
        {
            name = name.ToLower().Trim();

            if (!colorNames.ContainsKey(name))
                return "";

            return colorMap[colorNames[name]];
        }
    }
}
