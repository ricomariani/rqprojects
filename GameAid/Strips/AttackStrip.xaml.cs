using System;
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for AttackStrip.xaml
    /// </summary>
    public partial class AttackStrip : UserControl
    {
        enum AttackType
        {
            Slash = 0,
            Thrust = 1,
            Crush = 2
        }

        MainWindow Main { get { return MainWindow.mainWindow; } }

        string Character { get; set; }

        string AdjustedName(string s)
        {
            if (!s.StartsWith("#"))
                return s;

            int i = s.IndexOf(' ');
            if (i > 0)
                return s.Substring(0, i);

            return s;
        }

        public UIElementCollection Children { get { return mainGrid.Children; } }

        public AttackStrip()
        {
            InitializeComponent();
        }

        internal void Init(string name, string weapon, string sr, string pct, string damage, string note)
        {
            if (note == null || note == "")
            {
                mainGrid.Children.Remove(this.note);
            }

            if (sr != null && sr != "")
            {
                weapon += " (SR" + sr + ")";
            }

            damage = damage.ToLower();
            Character = name;
            attackWeapon.Text = weapon;
            attackChance.Text = pct;
            attackDamage.Text = damage;
            attackMode.SelectedIndex = (int)SelectAttackType(weapon);
            attackSpecial.Text = ConvertToSpecial(weapon, damage);

            var popupMenu = new ContextMenu();
            var dmgMenu = new MenuItem();
            dmgMenu.Header = "Roll Damage";
            dmgMenu.Click += new RoutedEventHandler(dmgMenu_Click);
            var specialMenu = new MenuItem();
            specialMenu.Header = "Roll Special Damage";
            specialMenu.Click += new RoutedEventHandler(specialMenu_Click);
            popupMenu.Items.Add(dmgMenu);
            popupMenu.Items.Add(specialMenu);

            popupMenu.Items.Add(new Separator());
            for (int i = 1; i <= 10; i++)
            {
                int srx = i;
                MenuItem mi = new MenuItem();
                mi.Header = "Roll on SR" + i.ToString();
                mi.Click += new RoutedEventHandler((x, y) =>
                {
                    if (attackTarget.Text == "")
                        MessageBox.Show("No target text set.  You must set a target to pre-roll.");
                    else
                        Main.readyRolls.PreRoll(srx, () => { buttonAttack_Click(null, null); });
                });

                popupMenu.Items.Add(mi);
            }

            buttonRoll.ContextMenu = popupMenu;
        }

        void specialMenu_Click(object sender, RoutedEventArgs e)
        {
            Main.SendChat(String.Format("{0}, {1} special damage roll", AdjustedName(Character), attackWeapon.Text));
            Main.SendHost(String.Format("!roll dmg:{0}", attackSpecial.Text));
            Main.SendHost(String.Format("!loc {0}", LocText()));
        }

        void dmgMenu_Click(object sender, RoutedEventArgs e)
        {
            Main.SendChat(String.Format("{0}, {1} damage roll", AdjustedName(Character), attackWeapon.Text));
            Main.SendHost(String.Format("!roll dmg:{0}", attackDamage.Text));
            Main.SendHost(String.Format("!loc {0}", LocText()));
        }

        void buttonAttack_Click(object sender, RoutedEventArgs e)
        {
            if (attackTarget.Text != "")
            {
                Main.SendChat(String.Format("{0}, {1} attack target: {2}", AdjustedName(Character), attackWeapon.Text, attackTarget.Text));
            }
            else
            {
                Main.SendChat(String.Format("{0}, {1} attack", AdjustedName(Character), attackWeapon.Text));
            }

            var locText = LocText();

            var cmd = String.Format("!atk wpn:{0} pct:{1} dmg:{2} idmg:{3} loc:{4}",
                attackWeapon.Text,
                attackChance.Text,
                attackDamage.Text,
                attackSpecial.Text,
                locText);

            Main.SendHost(cmd);
        }

        string LocText()
        {
            var locText = comboLoc.Text;
            var rollText = locRoll.Text.Trim().Replace(" ", "");

            if (rollText != "")
            {
                locText += "(" + rollText + ")";
            }

            return locText;
        }

        void attackMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (attackDamage != null && attackDamage.Text != null)
                attackSpecial.Text = ConvertToSpecial(attackWeapon.Text, attackDamage.Text);
        }

        void clear_Click(object sender, RoutedEventArgs e)
        {
            int row = Grid.GetRow(attackWeapon);
            Main.readyRolls.RemoveRows(row, 1);
        }

        static char[] plusorminus = new char[] { '+', '-' };

        internal string ConvertToSpecial(string name, string dmg)
        {
            string wpn = dmg;
            string str = "";
            string sgn = "";

            if (wpn == null || wpn == "")
                return "";

            int i2 = dmg.LastIndexOf('d');
            if (i2 > 0)
            {
                int i3 = dmg.LastIndexOfAny(plusorminus, i2);
                if (i3 > 0)
                {
                    wpn = dmg.Substring(0, i3);
                    str = dmg.Substring(i3 + 1);
                    sgn = dmg.Substring(i3, 1);
                }
            }

            if (name.Contains("rapier"))
            {
                return String.Format("{0}+{0}+{0}{1}{2}", wpn, sgn, str);
            }

            if (name.Contains("atlatl") && wpn == "1d8")
            {
                // this is the case of an atlatl with no str mod
                return dmg + "+" + dmg;
            }

            var mode = (AttackType)attackMode.SelectedIndex;

            if (mode < 0)
                mode = 0;

            switch (mode)
            {
                default:
                case AttackType.Slash:
                    return SlashDamage(wpn, sgn, str);

                case AttackType.Thrust:
                    return ThrustDamage(wpn, sgn, str);

                case AttackType.Crush:
                    return CrushDamage(wpn, sgn, str);
            }
        }

        string CrushDamage(string wpn, string sgn, string str)
        {
            if (str == "")
                return wpn;
            else if (sgn == "+")
                return String.Format("{0}+max({1})", wpn, str);
            else
                return String.Format("{0}-1", wpn);
        }

        string SlashDamage(string wpn, string sgn, string str)
        {
            if (str == "")
                return String.Format("max({0})", wpn, str);
            else
                return String.Format("max({0}){1}{2}", wpn, sgn, str);
        }

        string ThrustDamage(string wpn, string sgn, string str)
        {
            if (str == "")
                return String.Format("{0}+{0}", wpn);
            else
                return String.Format("{0}+{0}{1}{2}", wpn, sgn, str);
        }

        AttackType SelectAttackType(string wpn)
        {
            int thrustLen = -1;
            int crushLen = -1;
            int slashLen = 0; // this will be the default

            wpn = wpn.ToLower();
            GetLengthFirstMatch(wpn, slashing, ref slashLen);
            GetLengthFirstMatch(wpn, crushing, ref crushLen);
            GetLengthFirstMatch(wpn, thrusting, ref thrustLen);

            if (slashLen >= crushLen)
            {
                if (slashLen >= thrustLen)
                    return AttackType.Slash;
            }
            else
            {
                if (crushLen >= thrustLen)
                    return AttackType.Crush;
            }

            return AttackType.Thrust;          
        }

        static void GetLengthFirstMatch(string wpn, string[] wpnNames, ref int wpnLength)
        {
            int i;
            for (i = 0; i < wpnNames.Length; i++)
            {
                if (wpn.Contains(wpnNames[i]))
                {
                    wpnLength = wpnNames[i].Length;
                    return;
                }
            }
        }

        static string[] crushing = new string[] {
            "tannenheim",
	        "bowling ball",
	        "three chain",
            "briarthorn",
	        "sodegarami",
            "shikomezu",
            "constrict",
            "naunchaku",
	        "kawanaga",
	        "nunchaku",
	        "buckler",
	        "hoplite",
	        "tetsubo",
            "trample",
            "grapple",
	        "viking",
            "plunge",
	        "hammer",
	        "heater",
            "charge",
	        "kiseru",
	        "shield",
	        "target",
	        "tessen",
            "punch",
	        "bolas",
	        "chain",
	        "flail",
	        "sling",
	        "staff",
	        "stick",
	        "tonfa",
            "hoof",
            "tail",
	        "club",
            "butt",
	        "kite",
	        "mace",
	        "maul",
            "fist",
            "kick",
            "kiss",
	        "pole",
	        "rock",
	        "rope",
	        "net",
	        "sap"
        };

        static string[] slashing = new string[] {
	        "main gauche",
	        "bagh nakh",
	        "wakizashi",
	        "bardiche",
	        "claymore",
	        "falchion",
	        "guisarme",
	        "masakari",
	        "nagamaki",
	        "naginata",
	        "scimitar",
	        "shuriken",
	        "cutlass",
	        "halberd",
	        "hatchet",
	        "hunting",
	        "jambiya",
	        "khopesh",
	        "ninjato",
	        "nodachi",
	        "poleaxe",
	        "glaive",
	        "gunsen",
	        "katana",
	        "klanth",
	        "sapara",
	        "scythe",
	        "sickle",
	        "spatha",
	        "tulwar",
	        "voulge",
	        "jitte",
	        "katar",
	        "knife",
	        "kukri",
	        "sabre",
	        "spade",
	        "sword",
	        "utuma",
	        "claw",
	        "gami",
	        "kama",
	        "whip",
	        "axe",
	        "hoe",
	        "ono"
	        };

        static string[] thrusting = new string[] {
	        "brandistock",
	        "chijikiri",
	        "dragonewt",
	        "pitchfork",
	        "sang kauw",
	        "repeater",
            "earspoon",
	        "stiletto",
	        "stonebow",
            "partisan",
	        "blowgun",
	        "dai-kyu",
	        "dai kyu",
	        "gladius",
	        "han-kyu",
            "han kyu",
	        "javelin",
	        "trident",
	        "uchi-ne",
            "uchi ne",
	        "atlatl",
	        "cestus",
	        "dagger",
	        "musket",
	        "pistol",
	        "rapier",
            "sting",
	        "ankus",
	        "lance",
	        "pilum",
	        "spear",
	        "tanto",
            "horn",
            "bite",
            "wave",
            "tusk",
	        "dart",
	        "pick",
	        "pike",
	        "yari",
	        "bow",
	        "kyu",
	        "sai"
	        };
    }       
}
