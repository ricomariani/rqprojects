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

namespace GameAid
{
    class SheetImporter
    {
        public SheetImporter()
        {
            SetExcelColors();
        }

        List<string> strings = new List<string>();
        Dictionary<string, string> values = null;
        Dictionary<string, string> formulas = null;
        Dictionary<string, int> cellstyles = null;

        MainWindow Main { get { return MainWindow.mainWindow; } }

        string name;
        string player;
        string path;

        Dictionary<int, string> colorMapRGB = new Dictionary<int, string>();
        Dictionary<int, string> colorMapFriendly = new Dictionary<int, string>();
        Dictionary<string, int> colorNameIndices = new Dictionary<string, int>();
        Dictionary<int, int> battlemagicRows = new Dictionary<int, int>();

        Package package = null;

        public string ImportPassword(StringBuilder errors, string path)
        {
            if (!TryOpenPackage(errors, path))
                return "";

            using (package)
            {
                ReadSharedStrings(package);

                ReadSheet(package, "/xl/worksheets/sheet1.xml");

                // the PIN/password is stored in cell AA1

                var pin = C("AA", 1);

                var index = pin.IndexOf('\r');
                if (index > 0) pin = pin.Substring(0, index);

                index = pin.IndexOf('\n');
                if (index > 0) pin = pin.Substring(0, index);

                return pin;
            }
        }

        string bonusColor = "";

        internal void SearchSheet(string file, string pattern, StringBuilder errors)
        {
            if (!TryOpenPackage(errors, file))
                return;

            pattern = pattern.ToLower();

            using (package)
            {
                ReadSharedStrings(package);
                ReadStyleSection(package);

                ReadSheet(package, "/xl/worksheets/sheet1.xml");

                if (values == null)
                    return;

                if (!SniffSheet())
                    return;

                name = Keysafe("B", 1);

                if (name == "")
                {
                    errors.AppendFormat("The sheet {0} has no name in cell B1\r\n", file);
                    return;
                }

                rowOffset = 0;
                SearchForPattern(pattern, errors);
            }
        }

        void SearchForPattern(string pattern, StringBuilder errors)
        {
            foreach (string k in values.Keys)
            {
                var v = values[k].ToLower();

                if (v.Contains(pattern))
                {
                    errors.AppendFormat("{0} {1}: {2}\r\n", name, k, v);
                }
            }
        }

        internal void VerifySheet(string file, StringBuilder errors)
        {
            if (!TryOpenPackage(errors, file))
                return;

            using (package)
            {
                ReadSharedStrings(package);
                ReadStyleSection(package);

                ReadSheet(package, "/xl/worksheets/sheet1.xml");

                if (values == null)
                    return;

                name = Keysafe("B", 1);

                if (name == "")
                {
                    errors.AppendFormat("The sheet {0} has no name in cell B1\r\n", file);
                    return;
                }

                rowOffset = 0;
                ValidateFormulae(errors);
                ValidateShugenjaSchools(errors);
                ValidateRunemagic(errors);
                ValidateMdmg(errors);
                ValidateRoundLife(errors);
                ValidateTruncHitloc(errors);

                errors.AppendFormat("{0} checked.\n", name);
            }
        }

        void VerifySpelling(StringBuilder errors, string k, string v, string right, string wrong)
        {
            if (v.Contains(wrong))
            {
                errors.AppendFormat("{0} {1} spelling mistake. Expected: '{2}', Actual: '{3}'\r\n", name, k, right, wrong);
            }
        }

        void ValidateFormulae(StringBuilder errors)
        {
            // sniff test before processing, to make sure it looks like a sheet
            if (C("A", 18) != "STR" ||
                C("A", 19) != "CON" ||
                C("A", 20) != "SIZ")
            {
                errors.AppendFormat("{0} does not seem to have a character in it.\r\n", name);
                return;
            }

            foreach (string k in values.Keys)
            {
                var v = values[k].ToLower();
                VerifySpelling(errors, k, v, "presence", "pressence");
                VerifySpelling(errors, k, v, "allegiance", "alegience");
                VerifySpelling(errors, k, v, "allegiance", "allegience");
            }

            foreach (string k in formulas.Keys)
            {
                var v = formulas[k].ToLower();
                VerifySpelling(errors, k, v, "presence", "pressence");
                VerifySpelling(errors, k, v, "allegiance", "alegience");
                VerifySpelling(errors, k, v, "allegiance", "allegience");
            }

            string dmgcnt = "IF(E18+E20<=13,-1,IF(E18+E20<=16,0,IF(E18+E20<=48,1,IF(E18+E20<=56,2,TRUNC((E18+E20-57)/16,0)+3))))";
            string dmgval = "ABS(IF(E18+E20<=29,TRUNC((E18+E20-2)/3,0)-4,IF(E18+E20<=32,5,IF(E18+E20<=48,TRUNC((E18+E20-33)/4)*2+6,IF(AND(E18+E20>=53,E18+E20<=56),8,6)))))";

            ValidateCell(errors, "E", 41, "DEXSRM", "IF(E23<10,4,IF(E23<16,3,IF(E23<20,2,1)))");
            ValidateCell(errors, "E", 42, "SIZSRM", "IF(E20<10,3,IF(E20<16,2,IF(E20<20,1,0)))");
            ValidateCell(errors, "C", 44, "damage dice", dmgcnt);
            ValidateCell(errors, "E", 44, "damage dice", dmgval);


            for (int i = 31; i <= 67; i += 4)
            {
                ValidateCell(errors, "K", i, "damage", "");
                ValidateCell(errors, "V", i, "damage", "");
            }

            ValidateCell(errors, "G", 164, "pack enc", "Pack Space Used");
            ValidateCell(errors, "K", 164, "pack enc", "Max Pack Space");
            ValidateCell(errors, "R", 164, "pack enc", "Container Weight");

            VerifyTwoChoices(errors, "P", 164, "pack enc", "(E167*K167)+(E168*K168)", "E167*K167+E168*K168");
            VerifyTwoChoices(errors, "V", 164, "pack enc", "(E167*F167)+(E168*F168)", "E167*F167+E168*F168");
            VerifyTwoChoices(errors, "F", 164, "pack enc", "MIN((V83+SUM(G166:G???)-V164),P164)", "MIN((V83+SUM(G166:G209)-V164),P164)");
        }

        void VerifyTwoChoices(StringBuilder errors, string col, int row, string desc, string reqd1, string reqd2)
        {
            var l = errors.Length;
            if (!ValidateCell(errors, col, row, desc, reqd1))
            {
                errors.Length = l;
                ValidateCell(errors, col, row, desc, reqd2);
            }
        }

        public void ImportSpirits(string path, StringBuilder errors)
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

                name = Keysafe("A", 1);

                if (name !="Name:")
                {
                    errors.AppendFormat("The spirit sheet {0} doesn't have Name: in A1; can't import.\n", path);
                    return;
                }

                name = Keysafe("B", 1);

                if (name == "")
                {
                    errors.AppendFormat("The spirit sheet {0} has no name in cell B1; can't import.\n", path);
                    return;
                }

                if (Keysafe("A", 5) != "ID" || Keysafe("B", 5) != "Stored_Where" || Keysafe("C", 5) != "Spirit_Combat" || Keysafe("D", 5) != "POW")
                {
                    errors.AppendFormat("The spirit sheet {0} doesn't look properly formatted; can't import.\n", path);
                    return;
                }


                Main.SendHost(String.Format("del {0}/_spirits", name));

                for (int row = 6; row < 300; row++)
                {
                    var id = Keysafe("A", row);
                    var stored = Keysafe("B", row);
                    var v = C("C", row);
                    
                    int sc;
                    if (!Int32.TryParse(v, out sc))
                        continue;

                    v = C("D", row);

                    int pow;
                    if (!Int32.TryParse(v, out pow))
                        continue;

                    if (id == "End_of_Spirits")
                        break;

                    if (string.IsNullOrWhiteSpace(stored) || stored == "")
                        continue;

                    SendSheetString(name, "_spirits", id, String.Format("sc:{0} pow:{1} stored:{2}", sc, pow, stored));
                }

                errors.AppendFormat("{0} spirits scraped from {1}.\n", name, path);
            }
        }

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

                name = Keysafe("B", 1);

                if (name == "")
                {
                    errors.AppendFormat("The sheet {0} has no name in cell B1; can't import.\n", path);
                    return;
                }

                var p1 = Keysafe("V", 1);
                var p2 = Keysafe("W", 1);

                player = p1 + " " + p2;
                player = player.Trim();

                if (player == "")
                {
                    player = "unknown";
                }

                player = player.Replace(" ", "_");

                if (C("AB", 1) != "")                  
                    bonusColor = GetColorByName(C("AB", 1));

                rowOffset = 0;
                ProcessSheet(errors);

                Main.SendHost(String.Format("!clearsheetbuffs {0}", name));


                for (int i = 2; i <= 8; i++)
                {
                    if (TryReadRunemagicTab(String.Format("/xl/worksheets/sheet{0}.xml", i)))
                        break;
                }

                for (int i = 2; i <= 8; i++)
                {
                    if (TryReadWizardryTab(String.Format("/xl/worksheets/sheet{0}.xml", i)))
                        break;
                }

                for (int i = 2; i <= 8; i++)
                {
                    if (TryReadMusicTab(String.Format("/xl/worksheets/sheet{0}.xml", i)))
                        break;
                }

                for (int i = 2; i <= 8; i++)
                {
                    if (TryReadShugenjaTab(String.Format("/xl/worksheets/sheet{0}.xml", i)))
                        break;

                }

                for (int i = 2; i <= 8; i++)
                {
                    if (TryReadBuffTab(String.Format("/xl/worksheets/sheet{0}.xml", i)))
                        break;

                }

                for (int i = 2; i <= 8; i++)
                {
                    if (TryReadStoredMagicTab(String.Format("/xl/worksheets/sheet{0}.xml", i)))
                        break;
                }
            }
        }

        private bool TryReadRunemagicTab(string path)
        {
            ReadSheet(package, path);

            if (values == null)
                return false;

            for (int r = 1; r < 1000; r++)
            {

                string title = C("A", r);

                if (!title.Contains("Rune"))
                    continue;

                if (!title.Contains("Magic"))
                    continue;

                string spellCol = "";
                string castsCol = "";

                var chars = new char[1];

                for (int i = 0; i < 10; i++)
                {
                    chars[0] = (char)('A' + i);
                    var col = new string(chars);
                    string v = C(col, r + 1);

                    if (v == "Spell")
                        spellCol = col;

                    if (v == "Casts")
                        castsCol = col;
                }

                if (spellCol == "" || castsCol == "")
                    continue;

                ProcessRunemagic(spellCol, castsCol, r + 2);

                return true;
            }

            return false;
        }


        private bool TryReadWizardryTab(string path)
        {
            ReadSheet(package, path);

            if (values == null)
                return false;

            for (int r = 1; r < 1000; r++)
            {

                string title = C("A", r);

                if (!title.Contains("Wizardry"))
                    continue;

                string spellCol = "";
                string pctCol = "";
                string ptsCol = "";

                var chars = new char[1];

                for (int i = 0; i < 10; i++)
                {
                    chars[0] = (char)('A' + i);
                    var col = new string(chars);
                    string v = C(col, r + 1);

                    if (v == "Spell")
                        spellCol = col;

                    if (v == "Pts")
                        ptsCol = col;

                    if (v == "Pct")
                        pctCol = col;
                }

                if (spellCol == "" || ptsCol == "" || pctCol == "")
                    continue;

                ProcessWizardry(spellCol, ptsCol, pctCol, r + 2);

                return true;
            }

            return false;
        }

        private void ProcessWizardry(string spellCol, string ptsCol, string pctCol, int startRow)
        {
            Main.SendHost(String.Format("del {0}/_wizardry", name));

            var dict = new Dictionary<string, int>();

            for (int r = startRow; r < startRow + 1000; r++)
            {
                var c1 = C("A", r).ToLower();
                if (c1 == "end wizardry")
                    break;

                var k = Skillname(spellCol, r);
                var pts = C(ptsCol, r);
                var pct = C(pctCol, r);

                if (k != "" && pts != "" && pct != "")
                {
                    SendSheetNumeric(name, "_wizardry", k + "_" + pts, pct);
                }
            }
        }


        private bool TryReadBuffTab(string path)
        {
            ReadSheet(package, path);

            if (values == null)
                return false;

            for (int r = 1; r < 1000; r++)
            {
                string buffCol = "";
                string notesCol = "";

                var chars = new char[1];

                for (int i = 0; i < 10; i++)
                {
                    chars[0] = (char)('A' + i);
                    var col = new string(chars);
                    string v = C(col, r );

                    if (v == "Buff")
                        buffCol = col;

                    if (v == "Notes")
                        notesCol = col;
                }

                if (buffCol == "" || notesCol == "")
                    continue;

                ProcessSheetBuffs(buffCol, r + 1);

                return true;
            }

            return false;
        }


        private void ProcessSheetBuffs(string buffCol, int startRow)
        {
            var dict = new Dictionary<string, int>();

            for (int r = startRow; r < startRow + 1000; r++)
            {
                var c = C("A", r);
                
                if (c.ToLower() == "end buffs")
                    break;

                var k = c.Replace(" ", "_");

                if (k != "")
                {
                    Main.SendHost(String.Format("!sheetbuff @{0} {1}", name, k));
                }
            }
        }

        private bool TryReadMusicTab(string path)
        {
            ReadSheet(package, path);

            if (values == null)
                return false;

            for (int r = 1; r < 1000; r++)
            {
                string title = C("A", r);

                if (!title.Contains("Musical Magic"))
                    continue;

                string spellCol = "";
                string typeCol = "";
                string pctCol = "";
                
                var chars = new char[1];

                for (int i = 0; i < 10; i++)
                {
                    chars[0] = (char)('A' + i);
                    var col = new string(chars);
                    string v = C(col, r + 1);

                    if (v == "Spell")
                        spellCol = col;

                    if (v == "Type")
                        typeCol = col;

                    if (v == "Pct")
                        pctCol = col;
                }

                if (spellCol == "" || typeCol == "" || pctCol == "")
                    continue;

                ProcessMusic(spellCol, typeCol, pctCol, r + 2);

                return true;
            }

            return false;
        }


        private bool TryReadStoredMagicTab(string path)
        {
            ReadSheet(package, path);

            if (values == null)
                return false;

            for (int r = 1; r < 1000; r++)
            {
                string title = C("A", r);

                if (!title.Contains("Stored Magic"))
                    continue;

                string spellCol = "";
                string ptsCol = "";
                string pctCol = "";
                string nakedCol = "";
                string disableCol = "";

                var chars = new char[1];

                for (int i = 0; i < 10; i++)
                {
                    chars[0] = (char)('A' + i);
                    var col = new string(chars);
                    string v = C(col, r + 1);

                    if (v == "Spell")
                        spellCol = col;

                    if (v == "Pts")
                        ptsCol = col;

                    if (v == "Pct")
                        pctCol = col;

                    if (v == "Disable")
                        disableCol = col;

                    if (v == "Naked")
                        nakedCol = col;
                }

                if (spellCol == "" || ptsCol == "" || pctCol == "" || disableCol == "" || nakedCol == "")
                    continue;

                ProcessStoredMagic(spellCol, ptsCol, pctCol, disableCol, nakedCol, r + 2);

                return true;
            }

            return false;
        }

        private void ProcessStoredMagic(string spellCol, string ptsCol, string pctCol, string disableCol, string nakedCol, int startRow)
        {
            for (int r = startRow; r < startRow + 1000; r++)
            {
                if (C(disableCol, r) != "")
                    continue;

                string k = Skillname(spellCol, r);
                string v = C(pctCol, r);
                string naked = C(nakedCol, r);
                
                int p = GetIntAt(ptsCol, r);

                if (p == 0)
                    continue;

                if (p > 1)
                    k += "_" + p.ToString();

                if (k.StartsWith("herocast_"))
                {
                    SendSheetNumeric(name, "_herocast", k.Substring(9), v);
                }
                else if (k.Contains(";"))
                {
                    SendSheetNumeric(name, "_others_spells", k, v);
                }
                else if (naked == "") 
                { 
                    SendSheetNumeric(name, "_battlemagic", k, v);
                }
                else
                {
                    SendSheetNumeric(name, "_stored_spells", k, v);
                }
            }
        }

        private void ProcessMusic(string spellCol, string typeCol, string pctCol, int startRow)
        {
            Main.SendHost(String.Format("del {0}/_music", name));

            var dict = new Dictionary<string, int>();

            for (int r = startRow; r < startRow + 1000; r++)
            {
                var c1 = C("A", r).ToLower();
                if (c1 == "end musical magic")
                    break;

                var k = Skillname(spellCol, r);
                var type = Skillname(typeCol, r);
                var pct = C(pctCol, r);

                if (k != "" && type != "" && pct != "")
                {
                    SendSheetNumeric(name, "_music", type + "_" + k, pct);
                }
            }
        }

        private bool TryReadShugenjaTab(string path)
        {
            ReadSheet(package, path);

            if (values == null)
                return false;

            for (int r = 1; r < 10; r++)
            {              
                string spellCol = "";
                string schoolCol = "";
                string pctCol = "";
                string costCol = "";

                var chars = new char[1];

                for (int i = 0; i < 10; i++)
                {
                    chars[0] = (char)('A' + i);
                    var col = new string(chars);
                    string v = C(col, r);

                    if (v == "Spell")
                        spellCol = col;

                    if (v == "School")
                        schoolCol = col;

                    if (v == "Pct")
                        pctCol = col;

                    if (v == "Cost")
                        costCol = col;
                }

                if (spellCol == "" || schoolCol == "" || pctCol == "" || costCol == "")
                    continue;

                ProcessShugenja(spellCol, schoolCol, pctCol, costCol, r + 1);

                return true;
            }

            return false;
        }

        private void ProcessShugenja(string spellCol, string schoolCol, string pctCol, string costCol, int startRow)
        {

            string lastSchool = "";

            var dict = new Dictionary<string, int>();

            for (int r = startRow; r < startRow + 1000; r++)
            {
                var k = Skillname(spellCol, r);
                var cost = Skillname(costCol, r);
                var pct = C(pctCol, r);
                var school = Skillname(schoolCol, r);

                if (k != "" && school != "" && pct != "" && cost != "")
                {
                    if (school != lastSchool)
                    {
                        lastSchool = school;
                        if (school != "school")
                        {
                            Main.SendHost(String.Format("del {0}/_" + school + "_school", name));
                            SendSheetNumeric(name, "mana", school, "1"); // placeholder, this is recomputed anyway
                        }
                    }
                    
                    int n;
                    if (!Int32.TryParse(cost, out n))
                        continue;

                    double f;
                    if (!double.TryParse(pct, out f))
                        continue;

                    if (f <= 0)
                        continue;

                    k += "_" + cost;

                    SendSheetNumeric(name, "_" + school + "_school", k, pct);
                }
            }
        }
        
        private void ProcessRunemagic(string spellCol, string castsCol, int startRow)
        {
            Main.SendHost(String.Format("del {0}/_runemagic", name));

            var dict = new Dictionary<string, int>();

            for (int r = startRow; r < startRow + 1000; r++)
            {
                var c1 = C("A", r).ToLower();
                if (c1 == "one use" || c1 == "oneuse")
                    break;

                var k = Skillname(spellCol, r);
                var v = C(castsCol, r);

                int casts;
                if (!Int32.TryParse(v, out casts))
                    continue;

                if (dict.ContainsKey(k))
                {
                    dict[k] = dict[k] + casts;
                }
                else
                {
                    dict[k] = casts;
                }
            }

            foreach (var k in dict.Keys)
            {
                SendSheetNumeric(name, "_runemagic", k, dict[k].ToString());
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

        class Style
        {
            public int fontIndex;
        }

        List<Font> fonts = new List<Font>();
        List<Style> styles = new List<Style>();

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

        Style ReadStyle(XmlTextReader reader)
        {
            var style = new Style();
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
                                    if (colorMapRGB.ContainsKey(index))
                                        font.color = colorMapRGB[index];
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

        int rowOffset = 0;
        private string alchemySkillsBonus = "";

        string C(string col, int row)
        {
            string k = String.Format("{0}{1}", col, row + rowOffset);
            if (values.ContainsKey(k))
                return values[k];
            else
                return "";
        }

        string F(string col, int row)
        {
            string k = String.Format("{0}{1}", col, row + rowOffset);
            if (formulas.ContainsKey(k))
                return formulas[k];
            else
                return "";
        }

        int S(string col, int row)
        {
            string k = String.Format("{0}{1}", col, row + rowOffset);
            if (cellstyles.ContainsKey(k))
                return cellstyles[k];
            else
                return -1;
        }

        string Color(string col, int row)
        {
            int cellstyle = S(col, row);
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

            int row = ParseRowFromKey(key);
                
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
                                formula = formulas[si].Replace("<prev>", (row - 1).ToString());
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
                            {
                                string prev = (row-1).ToString();
                                formulas[si] = formula.Replace(prev, "<prev>");
                            }
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

        int ParseRowFromKey(string key)
        {
            int row = 0;

            foreach (char ch in key)
            {
                if (ch >= '0' && ch <= '9')
                    row = row * 10 + ch - '0';
            }

            return row;
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
            return C(col, row).Trim().Replace(' ', '_');
        }

        string Skillname(string col, int row)
        {
            return NormalizeStringForGameaid(C(col, row));
        }

        string NormalizeStringForGameaid(string s)
        {
            s = s.ToLower().Replace(":", "").Replace("*", "").Trim();

            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case '/':
                    case '*':
                    case ' ':
                    case ',':
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

        string SkillCategory(string col, int row, string assumed)
        {
            var f = F(col, row).Replace("$", "");

            if (alchemySkillsBonus != "" && ContainsWord(f, alchemySkillsBonus)) return "alchemy";
            if (ContainsWord(f, "L16") || ContainsWord(f, "E26")) return "communication";
            if (ContainsWord(f, "I16") || ContainsWord(f, "E27")) return "agility";      
            if (ContainsWord(f, "I29") || ContainsWord(f, "E28")) return "manipulation";        
            if (ContainsWord(f, "I38") || ContainsWord(f, "E29")) return "stealth";
            if (ContainsWord(f, "I45") || ContainsWord(f, "E30")) return "knowledge";
            if (ContainsWord(f, "E45") || ContainsWord(f, "E31")) return "perception";
            if (ContainsWord(f, "E56") || ContainsWord(f, "E32")) return "magic";
            if (ContainsWord(f, "K30") || ContainsWord(f, "E33") || ContainsWord(f, "W30")) return "attack";
            if (ContainsWord(f, "P30") || ContainsWord(f, "E34") || ContainsWord(f, "AC30")) return "parry";
            if (ContainsWord(f, "A1")) return "special";
            if (ContainsWord(f, "A2")) return "mana";

            return assumed;
        }

        public static bool ContainsWord(string item, string word)
        {
            int i = -1;
            while ((i = item.IndexOf(word, i+1)) >= 0)
            {
                if (i > 0 && char.IsLetterOrDigit(item[i - 1]))
                    continue;

                if (i + word.Length < item.Length && char.IsLetterOrDigit(item[i + word.Length]))
                    continue;

                return true;
            }

            return false;
        }

        string GetSkillColor(string col1, string col2, int row)
        {
            string color = Color(col1, row);
            if (color != "")
                return color;
           
            return Color(col2, row);
        }

        void ProcessSheet(StringBuilder errors)
        {
            string k = null;
            string v = null;
            string c = null;
            string f = null;
            string color = "";

            int r = 0;

            if (!SniffSheet())
                return;

            Main.SendHost(String.Format("del {0}/_armor", name));
            Main.SendHost(String.Format("del {0}/_hit_location", name));
            Main.SendHost(String.Format("del {0}/_misc", name));
            Main.SendHost(String.Format("del {0}/_equipment", name));
            Main.SendHost(String.Format("del {0}/_one_use", name));
            Main.SendHost(String.Format("del {0}/_runelevel", name));
            Main.SendHost(String.Format("del {0}/_runemagic", name));
            Main.SendHost(String.Format("del {0}/_herocast", name));
            Main.SendHost(String.Format("del {0}/_battlemagic", name));
            Main.SendHost(String.Format("del {0}/_spells", name));
            Main.SendHost(String.Format("del {0}/_forms", name));
            Main.SendHost(String.Format("del {0}/_stored_spells", name));
            Main.SendHost(String.Format("del {0}/_others_spells", name));
            Main.SendHost(String.Format("del {0}/_wpn", name));
            Main.SendHost(String.Format("del {0}/agility", name));
            Main.SendHost(String.Format("del {0}/communication", name));
            Main.SendHost(String.Format("del {0}/knowledge", name));
            Main.SendHost(String.Format("del {0}/magic", name));
            Main.SendHost(String.Format("del {0}/manipulation", name));
            Main.SendHost(String.Format("del {0}/perception", name));
            Main.SendHost(String.Format("del {0}/stealth", name));
            Main.SendHost(String.Format("del {0}/alchemy", name));

            foreach (var colorName in colorMapFriendly.Values)
            {
                Main.SendHost(String.Format("del {0}/_{1}", name, colorName));
            }

            var species = C("B", 2);
            SendSheetString(name, "_misc", "species", species);

            var religion = C("J", 1);
            SendSheetString(name, "_misc", "religion", religion);

            SendSheetString("_gameaid", "_players", name, player);

            var filename = Path.GetFileName(path);
            SendSheetString("_gameaid", "_filenames", filename, player);

            if (C("AA", 2) == "Presence")
            {
                SendSheetNumeric(name, "_misc", "vow_presence", C("AB", 2));
                SendSheetNumeric(name, "_misc", "casting_presence", C("AC", 2));
            }

            if (C("AA", 3) == "Presence")
            {
                SendSheetNumeric(name, "_misc", "vow_presence", C("AB", 3));
                SendSheetNumeric(name, "_misc", "casting_presence", C("AC", 3));
            }

            // normal location of spell and armor ending
            int last_spell_row = 140;
            int first_armor_row = 144;

            // find the start of the armor section
            int iItem = 139;
            for (; iItem < 160; iItem++)
            {
                if (C("A", iItem) == "Item:")
                {
                    last_spell_row = iItem - 1;
                    first_armor_row = iItem + 1;
                    break;
                }
            }

            int armor_enc = 0;
            for (r = first_armor_row; r <= first_armor_row + 4; r++)
            {
                k = Keysafe("A", r);
                v = C("G", r);
                if (v == "") continue;

                double result;
                if (!Double.TryParse(v, out result))
                    continue;

                armor_enc += (int)(result * 100);

                string parts = "";

                parts += EquippedLoc(r, "K", "Hd");
                parts += EquippedLoc(r, "L", "Ch");
                parts += EquippedLoc(r, "M", "Ab");
                parts += EquippedLoc(r, "N", "Ra");
                parts += EquippedLoc(r, "P", "La");
                parts += EquippedLoc(r, "R", "Rl");
                parts += EquippedLoc(r, "T", "Ll");

                if (parts == "")
                    continue;

                string id = MakeID(r - first_armor_row + 1, "A");

                SendSheetNumeric(name, "_equipment", id + ":" + k + "." + parts, (-r).ToString());
            }

            SendSheetString(name, "_misc", "armor_enc", (armor_enc / 100.0).ToString());

            int first_wpn_row = first_armor_row + 7;
            for (r = first_wpn_row; r <= first_wpn_row + 12; r++)
            {
                k = Keysafe("A", r);

                string parts = EquippedLoc(r, "E", "x");

                if (parts == "")
                    continue;

                string id = MakeID(r - first_wpn_row + 1, "W");

                SendSheetNumeric(name, "_equipment", id + ":" + k, (-r).ToString());
            }

            int first_gear_row = first_armor_row + 22;
            for (r = first_gear_row; r <= first_gear_row + 43; r++)
            {
                k = Keysafe("A", r);

                string parts = EquippedLoc(r, "E", "x");

                if (parts == "")
                    continue;

                string id = MakeID(r - first_gear_row + 1, "G");

                SendSheetNumeric(name, "_equipment", id + ":" + k, (-r).ToString());
            }

            for (r = 18; r <= 24; r++)
            {
                k = Keysafe("A", r);
                v = C("E", r);

                // STR SIZ CON etc.
                SendSheetNumeric(name, "", k, v);

                if (k == "APP")
                {
                    // send performance mana
                    SendSheetNumeric(name, "mana", "performance", v);
                }

                if (k == "POW")
                {
                    // send personal mana
                    SendSheetNumeric(name, "mana", "personal", v);
                    SendSheetNumeric(name, "", "EMP", v);
                    SendSheetNumeric(name, "", "MPMAX", v);
                }

                color = GetSkillColor("A", "E", r);
                SendSkillColorInfo(color, name, "", k);

                // MAX STR CON etc.
                v = C("C", r);
                SendSheetNumeric(name, "_misc", "MAX_" + k, v);
            }

            for (r = 26; r <= 34; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);

                // these are the bonuses communication, agility, manipulation, etc.
                SendSheetNumeric(name, "_misc", k, v);
            }

            for (r = 0; r <= 140; r++)
            {
                k = Skillname("A", r);
                if (String.Compare(k, "alchemy_skills") == 0)
                {
                    alchemySkillsBonus = "E" + r.ToString();
                    v = C("E", r);
                    SendSheetNumeric(name, "_misc", "alchemy", v);
                    break;
                }

                k = Skillname("F", r);
                if (String.Compare(k, "alchemy_skills") == 0)
                {
                    alchemySkillsBonus = "I" + r.ToString();
                    v = C("I", r);
                    SendSheetNumeric(name, "_misc", "alchemy", v);
                    break;
                }
            }


            for (r = 17; r <= 28; r++)
            {
                k = Skillname("M", r);

                if (Skillname("R", r) == "" || Skillname("U", r) == "")
                    break;

                v = C("AB", r);

                double result;
                if (!Double.TryParse(v, out result))
                    break;

                if (result == 0)
                    break;


                // hit locations
                SendSheetNumeric(name, "_hit_location", k, v);

                v = C("W", r);

                // hit locations
                SendSheetNumeric(name, "_armor", k, v);
            }

            v = C("R", 26);
            SendSheetNumeric(name, "_hit_location", "_life", v);

            // these are the combat factors like Endurance, Enc, Fatigue, etc.
            for (r = 36; r <= 44; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);

                if (r < 44)
                {
                    SendSheetNumeric(name, "_misc", k, v);
                }
                else
                {
                    // damage modifier

                    // MAX STR CON etc.
                    var count = C("C", r);
                    SendSheetString(name, "_misc", k, count + "d" + v);
                }
            }

            for (r = 46; r <= 55; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);
                c = SkillCategory("E", r, "perception");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("A", "E", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 57; r <= 65; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);
                c = SkillCategory("E", r, "magic");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("A", "E", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 67; r <= 68; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);

                SendSheetNumeric(name, "mana", k, v);

                color = GetSkillColor("A", "E", r);
                SendSkillColorInfo(color, name, c, k);
            }

            // this is the misc magic info like battemagic, free int, mana points per day, and total mana
            for (r = 69; r <= 70; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);

                if (k.StartsWith("battlemagic"))
                {
                    battlemagicRows.Add(r, 0);
                }

                SendSheetNumeric(name, "_misc", k, v);

                color = GetSkillColor("A", "E", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 17; r <= 28; r++)
            {
                k = Skillname("F", r);
                v = C("I", r);
                c = SkillCategory("I", r, "agility");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("F", "I", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 30; r <= 37; r++)
            {
                k = Skillname("F", r);
                v = C("I", r);
                c = SkillCategory("I", r, "manipulation");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("F", "I", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 39; r <= 44; r++)
            {
                k = Skillname("F", r);
                v = C("I", r);
                c = SkillCategory("I", r, "stealth");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("F", "I", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 46; r <= 70; r++)
            {
                k = Skillname("F", r);
                v = C("I", r);
                c = SkillCategory("I", r, "knowledge");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("F", "I", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 17; r <= 28; r++)
            {
                k = Skillname("J", r);
                v = C("L", r);
                c = SkillCategory("L", r, "communication");

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("J", "L", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 5; r <= 15; r++)
            {
                k = Skillname("A", r);
                v = C("E", r);
                c = SkillCategory("E", r, "");

                if (k == "" || v == "" || c == "")
                    continue;

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("A", "E", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 72; r <= 140; r++)
            {
                k = Skillname("F", r);
                v = C("I", r);
                c = SkillCategory("I", r, "");

                if (k == "" || v == "" || c == "")
                    continue;

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("F", "I", r);
                SendSkillColorInfo(color, name, c, k);
            }

            for (r = 5; r <= 15; r++)
            {
                k = Skillname("F", r);
                v = C("I", r);
                c = SkillCategory("I", r, "");

                if (k == "" || v == "" || c == "")
                    continue;

                SendSheetNumeric(name, c, k, v);

                color = GetSkillColor("F", "I", r);
                SendSkillColorInfo(color, name, c, k);
            }


            // check for the shugenja shape, or else do the normal
            bool fShugenja = false;
            for (r = 72; r <= 140; r++)
            {
                k = Skillname("A", r);
                if (k.IndexOf("_school") > 0)
                {
                    fShugenja = true;
                    break;
                }
            }

            if (!fShugenja)
            {
                for (r = 72; r <= 87; r++)
                {
                    k = Skillname("A", r);
                    v = C("E", r);
                    f = F("E", r);
                    int p = GetIntAt("C", r);

                    if (p == 0)
                    {
                        f = F("D", r);
                        if (!f.Contains("+") && !f.Contains("-"))
                            p = GetIntAt("D", r);
                    }

                    if (p > 1)
                        k += "_" + p.ToString();

                    if (TryAccumulateBattlemagic(f, r))
                    {
                        SendSheetNumeric(name, "_battlemagic", k, v);
                    }
                    else
                    {
                        SendSheetNumeric(name, "_spells", k, v);
                    }
                }

                // this is the stored spell section
                for (r = 91; r <= 139; r++)
                {
                    k = Skillname("A", r);
                    v = C("E", r);
                    f = F("E", r);
                    int p = GetIntAt("C", r);

                    if (p == 0)
                    {
                        f = F("D", r);
                        if (!f.Contains("+") && !f.Contains("-"))
                            p = GetIntAt("D", r);
                    }

                    if (p > 1)
                        k += "_" + p.ToString();

                    if (k == "free_con")
                    {
                        SendSheetNumeric(name, "_misc", k, v);
                    }
                    else if (k.StartsWith("herocast_"))
                    {
                        SendSheetNumeric(name, "_herocast", k.Substring(9), v);
                    }
                    else
                    {
                        string default_category = k.Contains(";") ? "_others_spells" : "_stored_spells";

                        c = SkillCategory("E", r, default_category);

                        if (TryAccumulateBattlemagic(f, r))
                        {
                            SendSheetNumeric(name, "_battlemagic", k, v);
                        }
                        else
                        {
                            SendSheetNumeric(name, c, k, v);
                        }
                    }
                }
            }
            else
            {
                // start with generic spells then switch to school as soon as we see one
                string label = "_spells";

                for (r = 72; r <= last_spell_row; r++)
                {
                    k = Skillname("A", r);
                    v = C("E", r);

                    // this is sometimes in the spell section, skip it
                    if (k == "stored_int")
                        continue;

                    // likewise
                    if (k == "total_stored")
                        continue;

                    int i = k.IndexOf("_school");
                    if (i > 0)
                    {
                        label = "_" + k;
                        Main.SendHost(String.Format("del {0}/{1}", name, label));
                        continue;
                    }

                    if (k.EndsWith("_stored"))
                    {
                        label = "_stored_spells";
                    }

                    // if the school name isn't included in the available annotation, infer it
                    // from the section we are processing
                    if (k == "available")
                    {
                        SendSheetNumeric(name, "mana", label.Replace("_school", "").Replace("_", ""), v);
                        label = "_spells";
                        continue;
                    }

                    if (k.EndsWith("_available"))
                    {
                        k = k.Replace("_available", "");
                        SendSheetNumeric(name, "mana", k, v);
                        label = "_spells";
                        continue;
                    }

                    string witchcraft = C("C", r);
                    if (witchcraft == "&")
                    {
                        SendSheetNumeric(name, "magic", k, v);
                        continue;
                    }

                    f = F("E", r);
                    int p = GetIntAt("C", r);

                    if (p == 0)
                    {
                        f = F("D", r);
                        if (!f.Contains("+") && !f.Contains("-"))
                            p = GetIntAt("D", r);
                    }

                    if (p > 1)
                        k += "_" + p.ToString();

                    if (label == "_spells" && TryAccumulateBattlemagic(f, r))
                    {
                        SendSheetNumeric(name, "_battlemagic", k, v);
                    }
                    else
                    {
                        SendSheetNumeric(name, label, k, v);
                    }
                }
            }

            var dict = new Dictionary<string, int>();

            for (r = 72; r <= 108; r++)
            {
                k = Skillname("J", r);
                v = C("L", r);

                if (k == "tattoos")
                    break;

                int casts;
                if (!Int32.TryParse(v, out casts))
                    continue;

                if (dict.ContainsKey(k))
                {
                    dict[k] = dict[k] + casts;
                }
                else
                {
                    dict[k] = casts;
                }
            }

            foreach (var key in dict.Keys)
            {
                SendSheetNumeric(name, "_runemagic", key, dict[key].ToString());
            }


            if (C("J", 110) == "One Use Spells:")
            {
                dict.Clear();

                for (r = 111; r <= 139; r++)
                {
                    k = Skillname("J", r);
                    v = C("L", r);

                    int casts;
                    if (!Int32.TryParse(v, out casts))
                        continue;

                    if (dict.ContainsKey(k))
                    {
                        dict[k] = dict[k] + casts;
                    }
                    else
                    {
                        dict[k] = casts;
                    }
                }

                foreach (var key in dict.Keys)
                {
                    SendSheetNumeric(name, "_one_use", key, dict[key].ToString());
                }

            }
            else
            {
                for (r = 111; r <= 139; r++)
                {
                    k = Skillname("J", r);
                    v = C("M", r);

                    SendSheetNumeric(name, "_secrets", k, v);
                }
            }

            for (r = 31; r <= 67; r += 4)
            {
                SendWeapon("J", "L", "T", r);
                SendWeapon("U", "W", "AC", r);
            }

            for (r = 200; r <= 500; r++)
            {
                var trigger = C("G", r);
                if (trigger == "Extra Weapon")
                {
                    if (C("L", r).Contains("Dmg:"))
                        SendWeapon("J", "L", "T", r);

                    if (C("W", r).Contains("Dmg:"))
                        SendWeapon("U", "W", "AC", r);
                }
            }

            for (r = 151; r <= 163; r++)
            {
                v = C("X", r);
                int ammo;
                if (Int32.TryParse(v, out ammo) && ammo > 0)
                {
                    string wpn = Skillname("A", r);
                    SendSheetString(name, "_routines", "shoot", wpn);
                    break;
                }
            }


            for (r = 200; r <= 500; r++)
            {
                var form = C("A", r);
                if (form != "Form:")
                    continue;

                form = Skillname("B", r);
                if (form == "")
                    continue;

                // advance to the stat labels
                r += 2;

                if (C("A", r) == "STR" &&
                    C("B", r) == "CON" &&
                    C("C", r) == "SIZ" &&
                    C("E", r) == "INT" &&
                    C("F", r) == "POW" &&
                    C("G", r) == "DEX" &&
                    C("H", r) == "APP")
                {
                    SendSheetAlternateForm(form, r);
                }
            }

            errors.AppendFormat("{0} scraped.\n", name);
        }

        void SendSheetAlternateForm(string form, int r)
        {
            // advance to stat deltas
            r += 1;
            string prefix = "_forms/" + form;

            SendSheetNumeric(name, prefix, "STR", C("A", r));
            SendSheetNumeric(name, prefix, "CON", C("B", r));
            SendSheetNumeric(name, prefix, "SIZ", C("C", r));
            SendSheetNumeric(name, prefix, "INT", C("E", r));
            SendSheetNumeric(name, prefix, "POW", C("F", r));
            SendSheetNumeric(name, prefix, "DEX", C("G", r));
            SendSheetNumeric(name, prefix, "APP", C("H", r));

            // skip to the buffs
            r += 2;

            int rNew = SendFormBuffs(r, prefix, "J", "M");
            SendFormBuffs(r, prefix, "U", "X");

            r = rNew;

            if (Skillname("J", r) != "weapon")
                return;

            // skip weapon header
            r++;
            for (; r < 500; r++)
            {
                var wpn = Skillname("J", r);
                var atk = GetIntCell("M", r);
                var par = GetIntCell("O", r);
                var sr = GetIntCell("Q", r);
                var ap = GetIntCell("S", r);
                var dmg = C("U", r).ToLower();

                if (wpn == "")
                    break;

                bool isNew = dmg != "" && sr != "";

                if (isNew)
                {
                    wpn = form + "_" + wpn;

                    dmg = dmg.Replace("+-", "-");

                    SendWeaponPart(wpn, "dmg", dmg);
                    SendWeaponPart(wpn, "attack", atk);
                    SendWeaponPart(wpn, "parry", par);
                    SendWeaponPart(wpn, "sr", sr);
                    SendWeaponPart(wpn, "ap", ap);
                }
                else
                {
                    SendSheetNumeric(name, prefix + "/attack", wpn, atk);
                    SendSheetNumeric(name, prefix + "/parry", wpn, par);
                }

            }
        }

        private int SendFormBuffs(int r, string prefix, string c1, string c2)
        {
            string category = "";

            for (; r < 500; r++)
            {
                var k = Skillname(c1, r);
                var v = C(c2, r);

                switch (k)
                {
                    case "":
                        if (C(c1, r + 1) == "")
                            return r;
                        break;

                    case "communication":
                    case "agility":
                    case "manipulation":
                    case "stealth":
                    case "knowledge":
                    case "alchemy":
                    case "perception":
                    case "magic":
                        category = k;
                        break;

                    case "attack":
                    case "parry":
                    case "weapon":
                    case "dmg":
                        return r;

                    default:
                        if (category != "" && k != "" && v != "")
                            SendSheetNumeric(name, prefix + "/" + category, k, v);
                        break;
                }
            }
            return 0;
        }

        bool TryAccumulateBattlemagic(string f, int r)
        {
            f = f.Replace("$", "");

            if (f.StartsWith("E") && !f.Contains("+") && !f.Contains("-"))
            {
                int row;
                if (Int32.TryParse(f.Substring(1), out row))
                {
                    if (battlemagicRows.ContainsKey(row))
                    {
                        battlemagicRows.Add(r, 0);
                        return true;
                    }
                }
            }

            return false;
        }

        bool SniffSheet()
        {
            // sniff test before processing, to make sure it looks like a sheet
            return C("A", 18) == "STR" && C("A", 19) == "CON" && C("A", 20) == "SIZ";
        }

        bool ValidateCell(StringBuilder errors, string col, int row, string desc, string reqd)
        {
            string v = F(col, row);
            if (v == "") v = C(col, row);

            reqd = AdjustFormula(reqd);

            if (!ValidateString(reqd, v))
            {
                errors.AppendFormat("{0} {1}{2} has unusual {3}. Expected: '{4}', Actual: '{5}'\r\n", name, col, row, desc, reqd, v);
                return false;
            }

            return true;
        }

        bool ValidateString(string reqd, string actual)
        {
            if (reqd.Length != actual.Length)
                return false;

            for (int i = 0; i < reqd.Length; i++)
            {
                if (reqd[i] == '?')
                    continue;

                if (reqd[i] != actual[i])
                    return false;
            }

            return true;
        }
        
        void ValidateRunemagic(StringBuilder errors)
        {
            string s = C("J", 72);
            if (s == "")
                return;

            int row = 71;
            string col = "M";
            string v = C(col, row);
            if (v == "") return;
            if (v != "Usd" && v != "Used")
            {
                errors.AppendFormat("{0} {1}{2} is not tracking used runemagic. Actual: '{3}'\r\n", name, col, row, v);
            }
        }

        void ValidateShugenjaSchools(StringBuilder errors)
        {
            for (int row = 57; row <= 71; row++)
            {
                string s = C("A", row);

                switch (s)
                {
                case "Air":
                case "Ancestor":
                case "Appetite":
                case "Blood":
                case "Celestial":
                case "Cold":
                case "Community":
                case "Darkness":
                case "Divination":
                case "Dragonewt":
                case "Earth":
                case "Elurae":
                case "Faerie":
                case "Fire":
                case "Flame":
                case "Fortune":
                case "Fury":
                case "Grave":
                case "Guardian":
                case "Healing":
                case "Hero":
                case "Illumination":
                case "Illusion":
                case "Knowledge":
                case "Life":
                case "Maho":
                case "Metal":
                case "Nature":
                case "Night":
                case "Shapeshift":
                case "Sleet":
                case "Stone":
                case "Taint":
                case "Travel":
                case "Trickery":
                case "Void":
                case "War":
                case "Water":
                case "Weapon":
                case "Wood":
                    string v = C("C", row);
                    if (v != "^" && v!= "")
                    {
                        errors.AppendFormat("{0} {1}{2} has a no-encumberance school entry but the schools no longer have entry.  Delete or use '^'. Actual: '{3}'\r\n", name, "C", row, v);
                    }

                    string f = F("E", row).Replace("$","");
                    if (f == "" || f.Contains("E56") || f.Contains("E32"))
                    {
                        // ok
                    }
                    else
                    {
                        errors.AppendFormat("{0} {1}{2} isn't using the magic bonus. Actual: '{3}'\r\n", name, "E", row, f);
                    }

                    break;

                }

            }
        }

        void ValidateRoundLife(StringBuilder errors)
        {
            int row = 39;
            string col = "E";
            string v = F(col, row);
            if (v == "") return;
            if (!v.Contains("ROUND"))
            {
                errors.AppendFormat("{0} {1}{2} is missing ROUND. Actual: '{3}'\r\n", name, col, row, v);
            }
        }

        void ValidateTruncHitloc(StringBuilder errors)
        {
            for (int row = 17; row <= 27; row++)
                ValidateTruncHitloc(errors, row);
        }

        void ValidateTruncHitloc(StringBuilder errors, int row)
        {
            string v = F("AB", row);
            if (v == "") return;
            if (!v.Contains("TRUNC"))
            {
                errors.AppendFormat("{0} {1}{2} is missing TRUNC. Actual: '{3}'\r\n", name, "AB", row, v);
            }
        }

        void ValidateMdmg(StringBuilder errors)
        {
            ValidateMdmg(errors, 157);
            ValidateMdmg(errors, 158);
        }
        
        void ValidateMdmg(StringBuilder errors, int row)
        {
            string v = C("R", row);
            if (v == "") return;
            if (!v.StartsWith("+") || v.Contains("."))
            {
                errors.AppendFormat("{0} {1}{2} has unusual {3}. Actual: '{4}'\r\n", name, "R", row, "MDMG", v);
            }
        }
        
        string AdjustFormula(string formula)
        {
            string STR = "Q_Q" + (18 + rowOffset).ToString();
            string CON = "Q_Q" + (19 + rowOffset).ToString();
            string SIZ = "Q_Q" + (20 + rowOffset).ToString();
            string INT = "Q_Q" + (21 + rowOffset).ToString();
            string POW = "Q_Q" + (22 + rowOffset).ToString();
            string DEX = "Q_Q" + (23 + rowOffset).ToString();
            string APP = "Q_Q" + (24 + rowOffset).ToString();

            return formula.Replace("E18", STR)
                          .Replace("E19", CON)
                          .Replace("E20", SIZ)
                          .Replace("E21", INT)
                          .Replace("E22", POW)
                          .Replace("E23", DEX)
                          .Replace("E24", APP)
                          .Replace("Q_Q", "E");
        }

        static string MakeID(int n, string prefix)
        {
            if (n < 10)
                return prefix + "0" + n.ToString();
            else
                return prefix + n.ToString();
        }

        string EquippedLoc(int r, string col, string part)
        {
            string equipped = C(col, r);
            if (equipped != "" && equipped != "0")
                return part;
            else
                return "";
        }

        void SendWeapon(string p, string p_2, string p_3, int r)
        {
            string color;
            string wpn = Skillname(p, r);

            if (wpn == "")
                return;

            string named_weapon = Skillname(p, r + 1);

            if (named_weapon != "")
            {
                wpn += "_" + named_weapon;
            }

            color = GetSkillColor(p, p_2, r + 2);
            SendSkillColorInfo(color, name, "_wpn|" + wpn, "attack");

            color = GetSkillColor(p, p_2, r + 3);
            SendSkillColorInfo(color, name, "_wpn|" + wpn, "parry");

            string dmg = C(p_2, r);
            if (!dmg.StartsWith(" Dmg: "))
                return;

            dmg = dmg.Replace("+-", "-");

            dmg = dmg.Substring(6);
            if (dmg == "" || dmg[0] == '+')
                return;

            dmg = dmg.ToLower();

            SendWeaponPart(wpn, "dmg", dmg);
            SendWeaponPart(wpn, "attack", GetIntCell(p_2, r + 2));
            SendWeaponPart(wpn, "parry", GetIntCell(p_2, r + 3));
            SendWeaponPart(wpn, "sr", GetIntCell(p_3, r + 1));
            SendWeaponPart(wpn, "ap", GetIntCell(p_3, r + 3));
        }

        void SendWeaponPart(string wpn, string label, string atk)
        {
            Main.SendHost(String.Format("n {0} {1} {2}", name + "/_wpn/" + wpn, label, atk));
        }

        int GetIntAt(string r, int col)
        {
            string val = C(r, col);
            if (val == "")
                return 0;

            double result;
            if (!Double.TryParse(val, out result))
                return 0;

            int v = (int)(result + 0.5);
            return v;
        }

        string GetIntCell(string r, int col)
        {
            return GetIntAt(r, col).ToString();
        }

        void SendSheetNumeric(string name, string grp, string k, string v)
        {
            if (v != "" && k != "")
            {
                double result;
                if (!Double.TryParse(v, out result))
                    return;

                int val = (int)Math.Floor(result + 0.5);

                if (grp != "")
                {
                    CanonicalizeSkillName(ref grp, ref k);
                    Main.SendHost(String.Format("n {0} {1} {2}", name + "/" + grp, k, val));
                }
                else
                {
                    Main.SendHost(String.Format("n {0} {1} {2}", name, k, val));
                }
            }
        }

        // skill names that aren't always the same on the sheet are converted to a canonical form here
        // so that group rolls are consistent
        static void CanonicalizeSkillName(ref string grp, ref string key)
        {
            if (key.StartsWith("lore_"))
                key = key.Substring(5);

            if (key.StartsWith("craft_"))
                key = key.Substring(6);

            if (grp == "magic" || grp == "_spells")
            {
                if (key.StartsWith("white_al") && key.EndsWith("nce"))
                {
                    key = "white_allegiance";
                    grp = "magic";
                    return;
                }

                if (key.StartsWith("black_al") && key.EndsWith("nce"))
                {
                    key = "black_allegiance";
                    grp = "magic";
                    return;
                }

                if (key.StartsWith("grey_al") && key.EndsWith("nce"))
                {
                    key = "grey_allegiance";
                    grp = "magic";
                    return;
                }
            }

            if (grp == "knowledge" && key.Contains("butcher"))
            {
                key = "butchery";
            }

            if (grp == "knowledge" && key.Contains("battlesavvy"))
            {
                key = "battle_savvy";
            }

            if (grp == "mana" && key != "mpts_per_day" && key.EndsWith("_per_day"))
            {
                key = key.Substring(0, key.Length - "_per_day".Length);
            }
        }

        void SendSheetString(string name, string grp, string k, string val)
        {
            if (val != "" && k != "")
            {
                if (grp != "")
                    Main.SendHost(String.Format("n {0} {1} {2}", name + "/" + grp, k, val));
                else
                    Main.SendHost(String.Format("n {0} {1} {2}", name, k, val));
            }
        }

        void SendSkillColorInfo(string color, string name, string grp, string k)
        {
            if (color == "")
                return;

            if (grp != "")
                k = grp + "|" + k;

            int index;
            if (colorNameIndices.TryGetValue(color, out index))
            {
                // convert the index to a friendly name like "red"
                string colorName = colorMapFriendly[index];
                if (colorName != "")
                {
                    SendSheetString(name, "_" + colorName, k, colorName);
                }
            }

            if (color == bonusColor)
                SendSheetString(name, "_runelevel", k, "bonus");
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
            colorMapRGB.Add(index, rgb);
            colorNameIndices.Add(rgb, index);
        }

        void SetColorName(string name, int index)
        {
            colorNameIndices.Add(name, index);
            colorMapFriendly.Add(index, name);
        }

        string GetColorByName(string name)
        {
            name = name.ToLower().Trim();

            if (!colorNameIndices.ContainsKey(name))
                return "";

            return colorMapRGB[colorNameIndices[name]];
        }
    }
}
