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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for SorceryWizard.xaml
    /// </summary>
    public partial class SorceryWizard : Window
    {
        public SorceryWizard()
        {
            InitializeComponent();
        }

        int R(TextBox b)
        {
            int r = 0;
            if (Int32.TryParse(b.Text, out r))
            {
                if (r > 0)
                    return r;
            }

            return 0;
        }

        void m_describe_Click(object sender, RoutedEventArgs e)
        {
            int accuracy = R(m_accuracy);  //
            int intensity = R(m_intensity); //
            int speed = R(m_speed); //
            int range = R(m_range); //
            int targets = R(m_targets); //
            int spells = R(m_spells); //
            int ease = R(m_ease);  //
            int duration = R(m_duration); //
            int force = R(m_force); //
            bool hold = m_hold.IsChecked == true;
            bool perm = m_permanence.IsChecked == true;

            StringBuilder b = new StringBuilder();

            if (intensity <= 0 )
            {
                b.AppendLine("At least one intensity is required.");
                b.AppendLine();
                intensity = 1;
            }

            if (ease > 0 && speed > 0)
            {
                b.AppendLine("Using speed and ease in the same spell is unhelpful.");
                b.AppendLine();
                ease = 0;
                speed = 0;
            }

            if (hold && speed > 0)
            {
                b.AppendLine("Using speed and hold in the same spell is unhelpful.");
                b.AppendLine();
                speed = 0;
            }

            if (perm && speed > 0)
            {
                b.AppendLine("Using speed and permanance in the same spell is unhelpful.");
                b.AppendLine();
                speed = 0;
            }

            if (hold && ease > 0)
            {
                b.AppendLine("Using ease and hold in the same spell is unhelpful.");
                b.AppendLine();
                ease = 0;
            }

            if (perm && ease > 0)
            {
                b.AppendLine("Using ease and permanance in the same spell is unhelpful.");
                b.AppendLine();
                ease = 0;
            }

            if (perm && duration > 0)
            {
                b.AppendLine("Using duration and permanance in the same spell is unhelpful.");
                b.AppendLine();
                duration = 0;
            }

            if (hold && perm)
            {
                b.AppendLine("Hold is not compatible with permanence, ignoring hold.");
                b.AppendLine();
                hold = false;
            }

            if (targets == 1)
            {
                b.AppendLine("No mana is needed for one target, you can leave this blank.");
                b.AppendLine();
                targets = 0;
            }

            if (spells == 1)
            {
                b.AppendLine("No mana is needed for one spell, you can leave this blank.");
                b.AppendLine();
                spells = 0;
            }

            if (range == 1)
            {
                b.AppendLine("No mana is needed for standard range (1), that's free, you can leave this blank.");
                b.AppendLine();
                range = 0;
            }

            if (duration == 1)
            {
                b.AppendLine("No mana is needed for standard duration (1), that's free, you can leave this blank.");
                b.AppendLine();
                duration = 0;
            }

            int basecost = 0;

            b.AppendFormat("{0} presence for the intensity\n", intensity);
            basecost += intensity;

            if (spells > 0)
            {
                b.AppendFormat("{0} presence for the spell count\n", spells);
                basecost += spells;
            }

            if (targets > 0)
            {
                b.AppendFormat("{0} presence for the targets\n", targets);
                basecost += targets;
            }

            if (range > 0)
            {
                b.AppendFormat("{0} presence for the range\n", range);
                basecost += range;
            }

            if (duration > 0)
            {
                b.AppendFormat("{0} presence for the duration\n", duration);
                basecost += duration;
            }

            if (force > 0)
            {
                b.AppendFormat("{0} presence for the force\n", force);
                basecost += force;
            }

            if (accuracy > 0)
            {
                b.AppendFormat("{0} presence for the accuracy\n", accuracy);
                basecost += accuracy;
            }

            int mana = basecost;
            int presence = basecost;
            int srs = basecost;

            if (ease * 2 > basecost)
            {
                b.AppendLine();
                b.AppendLine("Ease can only reduce the cost by 50%, this spell is not valid");
                ease = 0;
            }

            if (speed > basecost)
            {
                b.AppendLine();
                b.AppendLine("Speed cannot reduce the casting time below zero");
                speed = 0;
            }

            if (ease > 0)
            {
                b.AppendLine();
                b.AppendFormat("{0} presence for the ease for {0} mana reduced cost\n", ease);
                mana -= ease;
                srs += ease;
                presence += ease;
            }

            if (speed > 0)
            {
                b.AppendLine();
                b.AppendFormat("{0} presence for the speed for {0}sr faster casting\n", speed);
                srs -= speed;
                mana += speed;
                presence += speed;              
            }

            if (hold)
            {
                b.AppendFormat("{0} presence for hold (matching all other costs)\n", presence);

                mana += presence;
                srs += presence;
                presence += presence;
                b.AppendLine();
                b.AppendLine("1 presence will be required to keep the spell held.");
            }
            else if (perm)
            {
                b.AppendFormat("{0} presence for permanence (matching all other costs)\n", presence);
                
                mana += presence;
                srs += presence;
                presence += presence;
                b.AppendLine();
                b.AppendFormat("1 POW will be required to cast the spell\n", presence);
                b.AppendLine("1 presence will be required to maintain the spell.");
            }

            b.AppendLine();
            b.AppendFormat("{0} presence required to cast the spell\n", presence);
            b.AppendFormat("{0} mana required to cast the spell\n", mana);
            b.AppendFormat("{0} srs required to cast the spell\n", srs);
            b.AppendLine();
            b.AppendFormat("{0} minimum skill required in all manipulations used\n", presence * 10 - 9);
            b.AppendFormat("{0} minimum casting chance & equal ceremony or a better combination\n", (presence * 10 - 8)/2);

            m_description.Text = b.ToString();
        }
    }
}
