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
using System.Text;

namespace GameHost
{
    public class Dict
    {
        static Dictionary<string, Mods> toonMods = new Dictionary<string, Mods>();
        static Mods zeroMod = new Mods("");
        public static char[] plusminus = new char[] { '+', '-' };

        public static bool suppressBuffs = false;

        string path;
        string toon;
        bool isToonFolder;
        bool isManaFolder;
        bool isMagicFolder;
        bool isRunemagicFolder;
        bool isSpiritFolder;
        bool isOneuseFolder;
        bool isMiscFolder;
        bool isHitLocFolder;

        Func<string, string> adjuster;
        Mods modLazyWrite;

        static int ROUND(double x) { return (int)Math.Round(x); }

        Dictionary<string, string> data;
        public Dict(string p)
        {
            data = new Dictionary<string, string>();
            path = p;

            if (!path.StartsWith("_"))
            {
                string tail;

                Worker.Parse2Ex(p, out toon, out tail, "/");

                if (tail.StartsWith("_wpn/"))
                {
                    adjuster = (string _key) => AdjustWeaponFolder(_key);
                }
                else if (tail.EndsWith("_school") && tail.StartsWith("_"))
                {
                    string school = tail.Substring(1);
                    school = school.Substring(0, school.Length - 7).ToLower();
                    adjuster = (string _key) => AdjustShugenjaFolder(_key, school);
                }
                else switch (tail)
                {
                    case "":
                        adjuster = (string _key) => AdjustMainFolder(_key);
                        isToonFolder = true;
                        break;

                    case "_misc":
                        adjuster = (string _key) => AdjustMiscFolder(_key);
                        isMiscFolder = true;
                        break;

                    case "_armor":
                        adjuster = (string _key) => AdjustArmorFolder(_key);
                        break;

                    case "_hit_location":
                        isHitLocFolder = true;
                        adjuster = (string _key) => AdjustHitLocationFolder(_key);
                        break;

                    case "agility":
                        adjuster = (string _key) => AdjustAgilityFolder(_key);
                        break;

                    case "communication":
                        adjuster = (string _key) => AdjustCommunicationFolder(_key);
                        break;

                    case "knowledge":
                        adjuster = (string _key) => AdjustKnowledgeFolder(_key);
                        break;

                    case "alchemy":
                        adjuster = (string _key) => AdjustAlchemyFolder(_key);
                        break;

                    case "manipulation":
                        adjuster = (string _key) => AdjustManipulationFolder(_key);
                        break;

                    case "perception":
                        adjuster = (string _key) => AdjustPerceptionFolder(_key);
                        break;

                    case "stealth":
                        adjuster = (string _key) => AdjustStealthFolder(_key);
                        break;

                    case "_battlemagic":
                        adjuster = (string _key) => AdjustBattlemagicFolder(_key);
                        break;

                    case "mana":
                        isManaFolder = true;
                        adjuster = (string _key) => AdjustManaFolder(_key);
                        break;

                    case "_one_use":
                        isOneuseFolder = true;
                        break;

                    case "_runemagic":
                        isRunemagicFolder = true;
                        break;

                    case "_spirits":
                        isSpiritFolder = true;
                        break;

                    case "magic":
                        isMagicFolder = true;
                        adjuster = (string _key) => AdjustMagicFolder(_key);
                        break;

                    case "_spells":
                    case "_stored_spells":
                        adjuster = (string _key) => AdjustSpells(_key);
                        break;

                    case "_others_spells":
                        adjuster = (string _key) => AdjustLuckOnly(_key);
                        break;

                }
            }
            else if (path == "_mana")
            {
                adjuster = (string _key) => AdjustManaUsedFolder(_key);
            }
            else if (path == "_wounds")
            {
                adjuster = (string _key) => AdjustWoundsFolder(_key);
            }
            else if (path == "_runemagic")
            {
                adjuster = (string _key) => AdjustRunemagicUsedFolder(_key);
            }
            else if (path == "_spiritmana")
            {
                adjuster = (string _key) => AdjustSpiritManaUsedFolder(_key);
            }
            
        }

        public void Set(string key, string val)
        {
            data[key] = val;
        }

        public string GetRaw(string key)
        {
            return data[key];
        }

        internal bool ContainsKey(string key)
        {
            return data.ContainsKey(key);
        }

        internal void Remove(string key)
        {
            data.Remove(key);
            Trigger(key, null);
        }

        internal string this[string key]
        {
            get
            {
                if (suppressBuffs) 
                    return data[key];

                var v = (adjuster == null) ? data[key] : adjuster(key);
                if (toon == null)
                    return v;

                var mods = GetLazyModstate();

                if (mods == zeroMod)
                    return v;

                return mods.ApplyMiscBuff(path + "/" + key, v);
            }
            set
            {
                data[key] = value;
                Trigger(key, value);
            }
        }

        internal Dictionary<string,string>.KeyCollection Keys { get { return data.Keys; } }
      
        bool TryParse(string data, out int val)
        {
            val = 0;
            int sign = 1;

            if (data == null || data.Length == 0) return false;

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == '-' && i == 0)
                {
                    sign = -1;
                    continue;
                }

                if (data[i] == '+' && i == 0)
                {
                    continue;
                }

                if (data[i] < '0' || data[i] > '9')
                    return false;

                val = val * 10 + data[i] - '0';
            }

            val *= sign;
            return true;
        }

        Mods GetLazyModstate()
        {
            if (modLazyWrite != null)
                return modLazyWrite;

            Mods modstate;
            if (toonMods.TryGetValue(toon, out modstate))
            {
                modLazyWrite = modstate;
                return modstate;
            }
            else
            {
                return zeroMod;
            }
        }

        string AdjustKey(string key, int bonus)
        {
            var raw = data[key];
            if (bonus != 0)
            {
                int val;
                if (TryParse(raw, out val))
                {
                    val += bonus;
                    return val.ToString();
                }
            }
            return raw;
        }

        string AdjustHitLocation(string key, double fraction)
        {
            var raw = data[key];
            var mod = GetLazyModstate();

            if (mod.lifeScraped == mod.lifeAdjusted)
                return raw;

            int val;
            if (!TryParse(raw, out val))
                return raw;

            int nominalHitPoints = (int)Math.Floor(mod.lifeScraped * fraction);
            int sheetBonus = val - nominalHitPoints;
            int adjustedHitPoints = (int)Math.Floor(mod.lifeAdjusted * fraction);

            adjustedHitPoints += sheetBonus;

            return adjustedHitPoints.ToString();
        }

        string AdjustWeaponFolder(string key)
        {
            var mod = GetLazyModstate();

            switch (key)
            {
                case "sr":
                    return AdjustKey(key, mod.delta.DexSRM);
                case "attack":
                    return AdjustKey(key, mod.delta.Attack);
                case "parry":
                    return AdjustKey(key, mod.delta.Parry);
                case "dmg":
                    if (path.Contains("tannenheim")) // tannenhiem is double str damage
                        return AdjustDamage(key, mod.stdDervs.DoubleDamage, mod.adjDervs.DoubleDamage);
                    else
                        return AdjustDamage(key, mod.stdDervs.Damage, mod.adjDervs.Damage);
                default:
                    return data[key];
            }
        }

        string AdjustDamage(string key, string stdDamage, string adjDamage)
        {
            var dmg = data[key];

            // if the end matches then replace it, note this works if either std or adj is empty
            if (dmg.EndsWith(stdDamage))
            {
                dmg = dmg.Substring(0, dmg.Length - stdDamage.Length);
                dmg += adjDamage;
            }
            return dmg;
        }

        string AdjustArmorFolder(string key)
        {
            var mod = GetLazyModstate();

            return AdjustKey(key, mod.buffs.PROT);
        }

        string AdjustHitLocationFolder(string key)
        {
            if (key.Contains("chest"))
            {
                return AdjustHitLocation(key, .4);
            }
            else if (key.Contains("abd"))
            {
                return AdjustHitLocation(key, .33);
            }
            else if (key.Contains("leg"))
            {
                return AdjustHitLocation(key, .33);
            }
            else if (key.Contains("arm"))
            {
                return AdjustHitLocation(key, .25);
            }
            else if (key.Contains("wing"))
            {
                return AdjustHitLocation(key, .16);
            }
            else if (key.Contains("head"))
            {
                return AdjustHitLocation(key, .33);
            }
            else if (key.Contains("life"))
            {
                return AdjustKey(key, GetLazyModstate().delta.Life);
            }
            else
            {
                return data[key];
            }
        }
        string AdjustBattlemagicFolder(string key)
        {
            var mods = GetLazyModstate();

            return AdjustKey(key, mods.delta.Magic + mods.buffs.POW * 5 - mods.buffs.ENC);
        }

        string AdjustWoundsFolder(string key)
        {
            // this is what we're going to use if anything goes wrong, and a lot can go wrong
            string result = data[key];

            // first let's figure out who this is
            string who, entry;
            Worker.Parse2Ex(key, out who, out entry, "|");

            // if we can't figure that out, then forget the whole thing
            if (entry == "")
                return result;

            // all entries look like this someone|location

            // we have a hope of doing this so get our mods
            var mods = LazyInitMods(who);

            // we'll need the hitlocation dict 
            if (mods.hitlocDict == null)
                return result;

            // there should be two pieces in the value, used and max
            string dmg, max;
            Worker.Parse2(result, out dmg, out max);

            // if either is missing forget it
            if (dmg == "" || max == "")
                return result;

            // used needs to start with used:
            if (!dmg.StartsWith("damage:"))
                return result;

            // and max likewise must have the right prefix
            if (!max.StartsWith("max:"))
                return result;

            // convert max to an integer
            int nMax;
            if (!int.TryParse(max.Substring(4), out nMax))
                return result;

            // check for the hit location
            if (!mods.hitlocDict.ContainsKey(entry))
                return result;

            // use the hit location for the max
            if (!int.TryParse(mods.hitlocDict[entry], out nMax))
                return result;

            result = String.Format("{0} max:{1}", dmg, nMax);

            return result;
        }

        string AdjustSpiritManaUsedFolder(string key)
        {
            // this is what we're going to use if anything goes wrong, and a lot can go wrong
            string result = data[key];

            // first let's figure out who this is
            string who, effect;
            Worker.Parse2Ex(key, out who, out effect, "|");

            // if we can't figure that out, then forget the whole thing
            if (effect == "")
                goto Error;

            // we have a hope of doing this so get our mods
            var mods = LazyInitMods(who);

            Dict dict = mods.spiritDict;

            if (dict == null)
                goto Error;

            // anything else might be a school type, use that.
            if (!dict.ContainsKey(effect))
                goto Error;

            var value = dict[effect];

            string sc;
            string pow;
            string stored;
            GameHost.Dict.ExtractSpiritInfoParts(value, out sc, out pow, out stored);

            return String.Format("used:{0} max:{1}", result, pow);

        Error:
            return String.Format("used:{0} max:?", result);
        }

        public static void ExtractSpiritInfoParts(string value, out string sc, out string pow, out string stored)
        {
            string ctr;
            Worker.Parse2(value, out sc, out ctr);
            Worker.Parse2(ctr, out pow, out stored);

            pow = pow.Substring(4);
            sc = sc.Substring(3);
            stored = stored.Substring(7);
        }

        string AdjustRunemagicUsedFolder(string key)
        {
            // this is what we're going to use if anything goes wrong, and a lot can go wrong
            string result = data[key];

            // first let's figure out who this is
            string who, effect;
            Worker.Parse2Ex(key, out who, out effect, "|");

            // if we can't figure that out, then forget the whole thing
            if (effect == "")
                goto Error;

            // we have a hope of doing this so get our mods
            var mods = LazyInitMods(who);

            Dict dict = mods.runemagicDict;

            if (effect.StartsWith("one_use_"))
            {
                effect = effect.Substring(8);
                dict = mods.oneuseDict;
            }

            if (dict == null)
                goto Error;

            // anything else might be a school type, use that.
            if (!dict.ContainsKey(effect))
                goto Error;

            int nMax;
            if (!int.TryParse(dict[effect], out nMax))
                goto Error;

            return String.Format("used:{0} max:{1}", result, nMax);

        Error:
            return String.Format("used:{0} max:?", result);
        }

        string AdjustManaUsedFolder(string key)
        {
            // this is what we're going to use if anything goes wrong, and a lot can go wrong
            string result = data[key];

            // first let's figure out who this is
            string who, entry;
            Worker.Parse2Ex(key, out who, out entry, "|");

            // if we can't figure that out, then forget the whole thing
            if (entry == "")
                return result;

            // all entries look like this someone|mana|type or someone|mana for standard mana
            string mana;
            string manatype;
            Worker.Parse2Ex(entry, out mana, out manatype, "|");

            // mana must be the first word
            if (mana != "mana")
                return result;

            // we have a hope of doing this so get our mods
            var mods = LazyInitMods(who);

            // we'll need the magic dict and mana dict
            if (mods.magicDict == null || mods.manaDict == null)
                return result;

            // there should be two pieces in the value, used and max
            string used, max;
            Worker.Parse2(result, out used, out max);

            // if either is missing forget it
            if (used == "" || max == "")
                return result;

            // used needs to start with used:
            if (!used.StartsWith("used:"))
                return result;

            // and max likewise must have the right prefix
            if (!max.StartsWith("max:"))
                return result;

            // convert max to an integer
            int nMax;
            if (!int.TryParse(max.Substring(4), out nMax))
                return result;

            // three cases, standard mana first
            if (manatype == "")
            {
                // check for the standard total_magic_points
                if (!mods.manaDict.ContainsKey("total_magic_points"))
                    return result;

                // standard mana uses total_magic_points as the max
                if (!int.TryParse(mods.manaDict["total_magic_points"], out nMax))
                    return result;
            }
            else if (manatype == "personal")
            {
                // check for the standard total_magic_points
                if (!mods.manaDict.ContainsKey(manatype))
                    return result;

                // personal mana uses personal key as the max
                if (!int.TryParse(mods.manaDict[manatype], out nMax))
                    return result;
            }
            else
            {
                // anything else might be a school type, use that.
                if (!mods.manaDict.ContainsKey(manatype))
                    return result;

                if (!int.TryParse(mods.manaDict[manatype], out nMax))
                    return result;
            }

            result = String.Format("{0} max:{1}", used, nMax);           

            return result;
        }

        string AdjustManaFolder(string key)
        {
            var mods = GetLazyModstate();

            if (key == "mpts_per_day")
            {
                // these get the power buff
                return AdjustKey(key, mods.buffs.POW);
            }

            if (key == "total_magic_points" || key == "personal")
            {
                // these get the power buff and the max mana points buff
                return AdjustKey(key, mods.buffs.POW + mods.buffs.MPMAX);
            }

            if (mods.magicDict != null && mods.magicDict.ContainsKey(key))
            {
                // for school mana get the actual total from the magic folder which includes the buff adjustments
                return mods.magicDict[key];
            }

            return data[key];
        }

        string AdjustLuckOnly(string key)
        {
            return AdjustKey(key, GetLazyModstate().buffs.LUCK);
        }

        string AdjustSpells(string key)
        {
            var mods = GetLazyModstate();

            return AdjustKey(key, mods.delta.Magic - mods.buffs.ENC);
        }

        string AdjustMagicFolder(string key)
        {
            var mods = GetLazyModstate();

            switch (key)
            {
                case "mysticism":
                    return AdjustKey(key, mods.delta.Mysticism);

                case "mpts_per_day":
                    return AdjustKey(key, mods.buffs.POW);

                case "total_magic_points":
                case "personal":
                    return AdjustKey(key, mods.buffs.POW + mods.buffs.MPMAX);

                default:
                    return AdjustKey(key, mods.delta.Magic);
            }
        }

        string AdjustStealthFolder(string key)
        {
            var mods = GetLazyModstate();

            switch (key)
            {
                case "sneak":
                    return AdjustKey(key, mods.delta.Stealth - mods.buffs.ENC);

                default:
                    return AdjustKey(key, mods.delta.Stealth);
            }
        }

        string AdjustPerceptionFolder(string key)
        {
            return AdjustKey(key, GetLazyModstate().delta.Perception);
        }

        string AdjustManipulationFolder(string key)
        {
            return AdjustKey(key, GetLazyModstate().delta.Manipulation);
        }

        string AdjustKnowledgeFolder(string key)
        {
            return AdjustKey(key, GetLazyModstate().delta.Knowledge);
        }

        string AdjustAlchemyFolder(string key)
        {
            return AdjustKey(key, GetLazyModstate().delta.Alchemy);
        }

        string AdjustAgilityFolder(string key)
        {
            var mods = GetLazyModstate();

            switch (key)
            { 
                case "swim":
                    return AdjustKey(key, mods.delta.Agility - mods.buffs.ENC * 5);

                case "dodge":
                case "jump":
                    return AdjustKey(key, mods.delta.Agility - mods.buffs.ENC);

                default:
                    return AdjustKey(key, mods.delta.Agility);
            }
        }

        string AdjustCommunicationFolder(string key)
        {
            return AdjustKey(key, GetLazyModstate().delta.Communication);
        }

        // Shugenja reference:
        //
        // Secondary Stat ("Stat2") for casting chance by school 
        // 
        // APP: Elurae, Trickery, Shapeshift, Illusion, Community, Void, Jealousy
        // CON: Earth, Nature, Stone, Life, Healing, Wood
        // DEX: Sleet, Travel, Night, Water, Mischief
        // INT: Flame, Metal, Illumination, Knowledge, Fire, Dragon
        // POW: Cold, Divination, Fortune, Fury, Celestial, Darkness, Greed
        // SIZ: Taint, Blood, Appetite, Grave, Dragonnewt
        // STR: War, Weapon, Ancestor, Hero, Guardian, Air

        string AdjustShugenjaFolder(string key, string school)
        {
            Mods mods = GetLazyModstate();
            int delta = mods.delta.AnySchoolCasting;  // includes INT buff + school buff (half mysticism and half magic bonus)

            // add school specific delta based on school stat2
            switch (school)
            {
                case "elurae":
                case "trickery":
                case "shapeshift":
                case "illusion":
                case "community":
                case "void":
                case "jealousy":
                    delta += mods.buffs.APP;
                    break;

                case "earth":
                case "nature":
                case "stone":
                case "life":
                case "healing":
                case "wood":
                    delta += mods.buffs.CON;
                    break;

                case "sleet":
                case "travel":
                case "night":
                case "water":
                case "mischief":
                    delta += mods.buffs.DEX;
                    break;

                case "flame":
                case "metal":
                case "illumination":
                case "light":
                case "knowledge":
                case "fire":
                case "dragon":
                    delta += mods.buffs.INT;
                    break;

                case "cold":
                case "divination":
                case "fortune":
                case "fury":
                case "celestial":
                case "darkness":
                case "greed":
                    delta += mods.buffs.POW;
                    break;

                case "taint":
                case "blood":
                case "appetite":
                case "grave":
                case "dragonnewt":
                    delta += mods.buffs.SIZ;
                    break;

                case "war":
                case "weapon":
                case "ancestor":
                case "hero":
                case "guardian":
                case "air":
                    delta += mods.buffs.STR;
                    break;
            }

            return AdjustKey(key, delta);
        }

        string AdjustMainFolder(string key)
        {
            var mods = GetLazyModstate();

            switch (key)
            {
                case "INT": return AdjustKey(key, mods.buffs.INT);
                case "POW": return AdjustKey(key, mods.buffs.POW);
                case "DEX": return AdjustKey(key, mods.buffs.DEX);
                case "SIZ": return AdjustKey(key, mods.buffs.SIZ);
                case "APP": return AdjustKey(key, mods.buffs.APP);
                case "CON": return AdjustKey(key, mods.buffs.CON);
                case "STR": return AdjustKey(key, mods.buffs.STR);
                case "EMP":  return ComputeEffectivePersonalMana(mods);
                case "MPMAX": return AdjustKey("POW", mods.buffs.POW + mods.buffs.MPMAX);
            }

            return data[key];
        }

        string ComputeEffectivePersonalMana(Mods mods)
        {
            string result = GameHost.Worker.readkey("_mana", toon + "|mana|personal");
            int nUsed = 0;

            while (result != null)
            {
                // there should be two pieces in the value, used and max
                string used, max;
                Worker.Parse2(result, out used, out max);

                // if either is missing forget it
                if (used == "" || max == "")
                    break;

                // used needs to start with used:
                if (!used.StartsWith("used:"))
                    break;

                    // convert used to an integer
                int.TryParse(used.Substring(5), out nUsed);
                break;
            }

            return AdjustKey("POW", mods.buffs.POW + mods.buffs.MPMAX + mods.buffs.EMP + mods.buffs.LUCK / 5 - nUsed);
        }

        string AdjustMiscFolder(string key)
        {
            var mods = GetLazyModstate();

            switch (key)
            {
                case "agility": return AdjustKey(key, mods.delta.Agility - mods.buffs.LUCK);
                case "communication": return AdjustKey(key, mods.delta.Communication - mods.buffs.LUCK);
                case "knowledge": return AdjustKey(key, mods.delta.Knowledge - mods.buffs.LUCK);
                case "alchemy": return AdjustKey(key, mods.delta.Alchemy - mods.buffs.LUCK);
                case "manipulation": return AdjustKey(key, mods.delta.Manipulation - mods.buffs.LUCK);
                case "perception": return AdjustKey(key, mods.delta.Perception - mods.buffs.LUCK);
                case "stealth": return AdjustKey(key, mods.delta.Stealth - mods.buffs.LUCK);
                case "magic": return AdjustKey(key, mods.delta.Magic - mods.buffs.LUCK);
                case "attack": return AdjustKey(key, mods.delta.Attack - mods.buffs.LUCK);
                case "parry": return AdjustKey(key, mods.delta.Parry - mods.buffs.LUCK);
                case "life_points": return AdjustKey(key, mods.delta.Life);
                case "free_int": return AdjustKey(key, mods.buffs.INT);
                case "endurance": return AdjustKey(key, mods.delta.Endurance);
                case "fatigue": return AdjustKey(key, mods.delta.Endurance - mods.buffs.ENC); // same buff as endurance
                case "dex_srm": return AdjustKey(key, mods.delta.DexSRM);
                case "vow_presence": return AdjustKey(key, mods.delta.Magic + mods.buffs.INT - mods.buffs.LUCK);
                case "damage_bonus":
                    {
                        var dmg = mods.adjDervs.Damage;
                        if (dmg.StartsWith("+"))
                            return dmg.Substring(1);
                        else
                            return dmg;
                    }
            }

            return data[key];
        }
        void Trigger(string key, string value)
        {
            if (isToonFolder)
            {
                string ev = (value == null) ? "0" : value;

                LazyInitMods();

                int val;
                if (Int32.TryParse(ev, out val))
                {
                    switch (key)
                    {
                        case "INT": modLazyWrite.std.INT = val; modLazyWrite.ComputeAdjustments(); break;
                        case "POW": modLazyWrite.std.POW = val; modLazyWrite.ComputeAdjustments(); break;
                        case "DEX": modLazyWrite.std.DEX = val; modLazyWrite.ComputeAdjustments(); break;
                        case "SIZ": modLazyWrite.std.SIZ = val; modLazyWrite.ComputeAdjustments(); break;
                        case "APP": modLazyWrite.std.APP = val; modLazyWrite.ComputeAdjustments(); break;
                        case "CON": modLazyWrite.std.CON = val; modLazyWrite.ComputeAdjustments(); break;
                        case "STR": modLazyWrite.std.STR = val; modLazyWrite.ComputeAdjustments(); break;
                    }
                }
            }
            else if (isManaFolder)
            {
                LazyInitMods();
                modLazyWrite.manaDict = this;
            }
            else if (isMagicFolder)
            {
                LazyInitMods();
                modLazyWrite.magicDict = this;
            }
            else if (isHitLocFolder)
            {
                LazyInitMods();
                modLazyWrite.hitlocDict = this;
            }
            else if (isRunemagicFolder)
            {
                LazyInitMods();
                modLazyWrite.runemagicDict = this;
            }
            else if (isSpiritFolder)
            {
                LazyInitMods();
                modLazyWrite.spiritDict = this;
            }
            else if (isOneuseFolder)
            {
                LazyInitMods();
                modLazyWrite.oneuseDict = this;
            }
            else if (isMiscFolder)
            {
                if (key == "life_points")
                {
                    LazyInitMods();
                    int val;
                    if (Int32.TryParse(value, out val))
                    {
                        modLazyWrite.lifeScraped = val;
                        modLazyWrite.ComputeAdjustments();
                    }
                }
            } 
            else if (path == "_buffs")
            {
                string p;
                string id;
                Worker.Parse2Ex(key, out p, out id, "|");

                var mods = LazyInitMods(p);

                mods.HarvestBuffs(this);
            }
        }

        void LazyInitMods()
        {
            if (modLazyWrite == null)
            {
                modLazyWrite = LazyInitMods(toon);
            }
        }

        static Mods LazyInitMods(string toon)
        {
            Mods mods;
            if (!toonMods.TryGetValue(toon, out mods))
            {
                mods = new Mods(toon);
                toonMods.Add(toon, mods);
            }

            return mods;
        }
    }

    public class Buffables
    {
        public int INT;
        public int POW;
        public int CON;
        public int SIZ;
        public int STR;
        public int DEX;
        public int APP;
        public int LIFE;
        public int LUCK;
        public int PROT;
        public int SRM;
        public int ENC;
        public int EMP;
        public int MPMAX;

        public int Communication;
        public int Agility;
        public int Manipulation;
        public int Stealth;
        public int Knowledge;
        public int Alchemy;
        public int Perception;
        public int Magic;
        public int Attack;
        public int Parry;

        double MINOR(int x) { return Math.Min(10.0, (x - 10.0) / 2); }
        static int ROUND(double x) { return (int)Math.Round(x); }

        public void UpdateDerivatives(Derivatives derv)
        {
            derv.Communication = ROUND(INT - 10 + MINOR(POW) + MINOR(APP));
            derv.Agility = ROUND(DEX - SIZ + MINOR(STR));
            derv.Manipulation = ROUND(DEX + INT + MINOR(STR) - 20);
            derv.Stealth = DEX + 10 - SIZ - POW;
            derv.Knowledge = INT - 10;
            derv.Alchemy = INT + POW + DEX - 40;
            derv.Perception = ROUND(INT - 10 + MINOR(POW) + MINOR(CON));
            derv.Magic = ROUND(POW + INT + MINOR(DEX) - 20);
            derv.Attack = derv.Manipulation;
            derv.Parry = derv.Agility;
            derv.Life = (SIZ + CON + 1) / 2;
            derv.Mysticism = ROUND(POW/6.0) + 20 - INT;
            derv.AnySchoolCasting = INT + ROUND((derv.Mysticism + derv.Magic) / 2);
            derv.Endurance = STR + CON;  // note some people get SIZ/6 but that's an orlanthi thing, ignoring it for buff purposes
            derv.DexSRM = Math.Max(0, SRM + (DEX < 10 ? 4 : DEX < 16 ? 3 : DEX < 20 ? 2 : 1));

            derv.Damage = DamageBonus(1);
            derv.DoubleDamage = DamageBonus(2);
        }

        public string DamageBonus(int scale)
        {
            int sum = STR + SIZ;

            int count = sum <= 13 ? -1 : 
                        sum <= 16 ? 0 : 
                        sum <= 48 ? 1 : 
                        sum <= 56 ? 2 : (sum - 57) / 16 + 3;

            int dice = sum <= 29 ? (sum - 2) / 3 - 4 :
                       sum <= 32 ? 5 :
                       sum <= 48 ? (sum - 33) / 4 * 2 + 6 :
                       sum <= 52 ? 6 :
                       sum <= 56 ? 8 : 6;

            if (count == 0 || dice == 0)
                return "";                    

            dice = Math.Abs(dice);

            count *= scale;

            string result = count.ToString() + "d" + dice;

            // normalize with leading +
            if (result.StartsWith("-"))
                return result;

            return "+" + result;
        }
    }

    public class Derivatives
    {
        public int Communication;
        public int Agility;
        public int Manipulation;
        public int Stealth;
        public int Knowledge;
        public int Alchemy;
        public int Perception;
        public int Magic;
        public int Attack;
        public int Parry;
        public int Life;
        public int Mysticism;
        public int AnySchoolCasting;
        public int Endurance;
        public int DexSRM;

        public string Damage;
        public string DoubleDamage;
    }

    public class Mods
    {
        public string toon;
        public Buffables std;
        public Buffables buffs;
        public Buffables adj;
        public Derivatives stdDervs; // what normal stats would yield
        public Derivatives adjDervs; // what the adjusted stats would yield
        public Derivatives delta; // how much to add to relevant skills  (adj-std)
        public int lifeScraped;
        public int lifeAdjusted;
        public Dict runemagicDict;
        public Dict oneuseDict;
        public Dict spiritDict;
        public Dict magicDict;
        public Dict manaDict;
        public Dict hitlocDict;
        Dictionary<string, string> miscMods;

        public Mods(string _toon)
        {
            toon = _toon;
            std = new Buffables();
            buffs = new Buffables();
            adj = new Buffables();
            stdDervs = new Derivatives();
            adjDervs = new Derivatives();
            delta = new Derivatives();
            magicDict = null;
            manaDict = null;
            runemagicDict = null;
            oneuseDict = null;
            hitlocDict = null;
            miscMods = new Dictionary<string, string>();
        }
        static int ROUND(double x) { return (int)Math.Round(x); }

        public void ComputeAdjustments()
        {
            adj.INT = std.INT + buffs.INT;
            adj.POW = std.POW + buffs.POW;
            adj.CON = std.CON + buffs.CON;
            adj.SIZ = std.SIZ + buffs.SIZ;
            adj.STR = std.STR + buffs.STR;
            adj.DEX = std.DEX + buffs.DEX;
            adj.APP = std.APP + buffs.APP;
            adj.SRM = std.SRM + buffs.SRM;
            adj.ENC = std.ENC + buffs.ENC;
            adj.EMP = 0;  // special computation
            adj.MPMAX = std.POW + buffs.MPMAX;

            std.UpdateDerivatives(stdDervs);
            adj.UpdateDerivatives(adjDervs);

            delta.Communication = buffs.LUCK + adjDervs.Communication - stdDervs.Communication;
            delta.Agility = buffs.LUCK + adjDervs.Agility - stdDervs.Agility;
            delta.Manipulation = buffs.LUCK + adjDervs.Manipulation - stdDervs.Manipulation;
            delta.Stealth = buffs.LUCK + adjDervs.Stealth - stdDervs.Stealth;
            delta.Knowledge = buffs.LUCK + adjDervs.Knowledge - stdDervs.Knowledge;
            delta.Alchemy = buffs.LUCK + adjDervs.Alchemy - stdDervs.Alchemy;
            delta.Perception = buffs.LUCK + adjDervs.Perception - stdDervs.Perception;
            delta.Magic = buffs.LUCK + adjDervs.Magic - stdDervs.Magic;
            delta.Attack = buffs.LUCK + adjDervs.Attack - stdDervs.Attack;
            delta.Parry = buffs.LUCK + adjDervs.Parry - stdDervs.Parry;
            delta.Life = adjDervs.Life - stdDervs.Life + buffs.LIFE;
            delta.Mysticism = adjDervs.Mysticism - stdDervs.Mysticism;
            delta.AnySchoolCasting = buffs.LUCK + adjDervs.AnySchoolCasting - stdDervs.AnySchoolCasting;
            delta.Endurance = adjDervs.Endurance - stdDervs.Endurance;
            delta.DexSRM = adjDervs.DexSRM - stdDervs.DexSRM;

            delta.Communication += buffs.Communication;
            delta.Agility += buffs.Agility;
            delta.Manipulation += buffs.Manipulation;
            delta.Stealth += buffs.Stealth;
            delta.Knowledge += buffs.Knowledge;
            delta.Alchemy += buffs.Alchemy;
            delta.Perception += buffs.Perception;
            delta.Magic += buffs.Magic;
            delta.Attack += buffs.Attack;
            delta.Parry += buffs.Parry;
            
            lifeAdjusted = lifeScraped + delta.Life;
        }

        internal void HarvestBuffs(Dict buffDict)
        {
            buffs.INT = 0;
            buffs.POW = 0;
            buffs.CON = 0;
            buffs.SIZ = 0;
            buffs.STR = 0;
            buffs.DEX = 0;
            buffs.APP = 0;
            buffs.LIFE = 0;
            buffs.SRM = 0;
            buffs.LUCK = 0;
            buffs.PROT = 0;
            buffs.ENC = 0;
            buffs.MPMAX = 0;
            buffs.EMP = 0;

            buffs.Communication = 0;
            buffs.Agility = 0;
            buffs.Manipulation = 0;
            buffs.Stealth = 0;
            buffs.Knowledge = 0;
            buffs.Alchemy = 0;
            buffs.Perception = 0;
            buffs.Magic = 0;
            buffs.Attack = 0;
            buffs.Parry = 0;

            miscMods.Clear();
            
            var keys = buffDict.Keys;
            var prefix = toon + "|";

            foreach (var k in keys)
            {
                if (!k.StartsWith(prefix))
                    continue;

                string buff = buffDict.GetRaw(k);

                if (buff.Length < 5)
                    continue;

                int index = buff.IndexOfAny(Dict.plusminus);
                if (index < 1)
                    continue;

                int val;
                if (!int.TryParse(buff.Substring(index), out val))
                    continue;

                string item = buff.Substring(0, index);
                switch (item)
                {
                    case "EMP":
                        buffs.EMP += val;
                        break;
                    case "INT":
                        buffs.INT += val;
                        break;
                    case "POW":
                        buffs.POW += val;
                        break;
                    case "CON":
                        buffs.CON += val;
                        break;
                    case "SIZ":
                        buffs.SIZ += val;
                        break;
                    case "STR":
                        buffs.STR += val;
                        break;
                    case "DEX":
                        buffs.DEX += val;
                        break;
                    case "APP":
                        buffs.APP += val;
                        break;
                    case "SRM":
                        buffs.SRM += val;
                        break;
                    case "ENC":
                        buffs.ENC += val;
                        break;
                    case "LIFE":
                        buffs.LIFE += val;
                        break;
                    case "LUCK":
                        buffs.LUCK += val;
                        break;
                    case "PROT":
                        buffs.PROT += val;
                        break;
                    case "MPMAX":
                        buffs.MPMAX += val;
                        break;
                    case "Communication":
                        buffs.Communication += val;
                        break;
                    case "Agility":
                        buffs.Agility += val;
                        break;
                    case "Manipulation":
                        buffs.Manipulation += val;
                        break;
                    case "Stealth":
                        buffs.Stealth += val;
                        break;
                    case "Knowledge":
                        buffs.Knowledge += val;
                        break;
                    case "Alchemy":
                        buffs.Alchemy += val;
                        break;
                    case "Perception":
                        buffs.Perception += val;
                        break;
                    case "Magic":
                        buffs.Magic += val;
                        break;
                    case "Attack":
                        buffs.Attack += val;
                        break;
                    case "Parry":
                        buffs.Parry += val;
                        break;
                    default:
                        // misc buffs, not a special category, a particular skill
                        string key = toon + "/" + item;
                        key = key.Replace("|", "/");
                        key = key.Replace("//", "/");

                        while (key.EndsWith("/"))
                            key = key.Substring(0, key.Length - 1);

                        RecordBuff(key, val.ToString());
                        break;
                }
            }

            ComputeAdjustments();
        }

        static bool TryGetSimpleNumber(string strVal, out int intVal)
        {
            int i = 0;
            intVal = 0;
            int l = strVal.Length;

            if (l == 0)
                return false;

            if (strVal[0] == '+' || strVal[0] == '-')
            {
                i++;
            }

            for (; i < l; i++)
            {
                if (strVal[i] < '0' || strVal[i] > '9')
                {
                    intVal = 0;
                    return false;
                }

                intVal = intVal * 10 + strVal[i] - '0';
            }

            if (strVal[0] == '-')
                intVal = -intVal;

            return true;
        }

        void RecordBuff(string key, string value)
        {
            var v = ApplyMiscBuff(key, value);
            if (!v.StartsWith("+") && !v.StartsWith("-"))
                v = "+" + v;
            miscMods[key] = v;
        }

        public string ApplyMiscBuff(string key, string value)
        {
            if (miscMods.ContainsKey(key))
            {
                int oldVal, newVal;
                if (TryGetSimpleNumber(miscMods[key], out oldVal) && TryGetSimpleNumber(value, out newVal))
                {
                    newVal += oldVal;
                    value = newVal.ToString();
                }
                else
                {
                    int i = value.IndexOfAny(Dict.plusminus);

                    if (i < 0)
                    {
                        value = value + miscMods[key];
                    }
                    else
                    {
                        // put any roll mods in the middle, not at the end
                        value = value.Substring(0, i) + miscMods[key] + value.Substring(i);
                    }
                }
            }

            return value;
        }
    }
}

