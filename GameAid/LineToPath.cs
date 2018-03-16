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
using System.Windows.Shapes;
using System.Windows;
using System.Diagnostics;
using System.Windows.Media;

namespace GameAid
{
    public enum ConnectionType
    {
        LoopBack,
        Connect,
        NewSegment
    };

    public struct LineDisposition
    {
        public bool forward;
        public ConnectionType connect;
        public double x1, x2, y1, y2;
        public short next;
        public short prev;

        public Point Start
        {
            get { return forward ? new Point(x1, y1) : new Point(x2, y2); }
        }

        public Point End
        {
            get { return forward ? new Point(x2, y2) : new Point(x1, y1); }
        }
    }

    public class LineToPath
    {
        static Random random = new Random();

        static public Path FindOptimalPath(List<Line> lines)
        {
            var matrix = LineToPath.FindOptimalPath(lines.ToArray());

            var disps = matrix.disps;
            int start = matrix.start;

            // Create a path to draw a geometry with.
            Path path = new Path();
            PathGeometry pg = new PathGeometry();
            PathFigure pf = new PathFigure();
            pf.StartPoint = disps[start].Start;
            Point p0 = disps[start].Start;
            int i0 = 0;
            int here = start;
            int segments = 0;

            for (int i = 0; i < disps.Length; i++)
            {
                int next = disps[here].next;

                if (i == disps.Length - 1)
                {
                    switch (disps[here].connect)
                    {
                        case ConnectionType.Connect:
                        case ConnectionType.NewSegment:
                            AddSeg(pf, disps[here].End, true, ref segments);
                            break;

                        case ConnectionType.LoopBack:
                            // full cycle, special case
                            if (i0 == 0)
                            {
                                pf.IsClosed = true;
                                pf.IsFilled = true;
                            }
                            else
                            {
                                AddSeg(pf, p0, true, ref segments);
                            }
                            break;
                    }
                }
                else
                {
                    switch (disps[here].connect)
                    {
                        case ConnectionType.Connect:
                            AddSeg(pf, disps[here].End, true, ref segments);
                            break;

                        case ConnectionType.NewSegment:
                            AddSeg(pf, disps[here].End, true, ref segments);
                            AddSeg(pf, disps[next].Start, false, ref segments);
                            p0 = disps[next].Start;
                            i0 = i;
                            break;

                        case ConnectionType.LoopBack:
                            AddSeg(pf, p0, true, ref segments);
                            AddSeg(pf, disps[next].Start, false, ref segments);
                            p0 = disps[next].Start;
                            i0 = i;
                            break;
                    }
                }

                here = next;
            }

            pg.Figures.Add(pf);
            pg.FillRule = FillRule.EvenOdd;
            path.Data = pg;

            return path;
        }

        static void AddSeg(PathFigure pf, Point pt, bool connected, ref int segments)
        {
            if (connected == false) segments++;
            Point ptNew = new Point(pt.X + segments * 10, pt.Y + segments * 10);
            pf.Segments.Add(new LineSegment(ptNew, connected));
        }
        
        static Matrix FindOptimalPath(Line[] lines)
        {
            Matrix.BeginOptimization(lines);
            var result = BeeSim.Simulate();
            Matrix.EndOptimization();
            return result;
        }

        public static StringBuilder msg;

        public class BeeSim
        {
            public static Matrix Simulate()
            {
                int totalNumberBees = 100;
                int numberInactive = (int)(.20 * totalNumberBees);
                int numberActive = (int)(.50 * totalNumberBees);
                int numberScout = (int)(.30 * totalNumberBees);

                int maxNumberVisits = 100;
                int maxNumberCycles = 30000;

                msg = new StringBuilder();

                Hive hive = new Hive(numberInactive, numberActive, numberScout, maxNumberVisits, maxNumberCycles);
                hive.Solve();
                MessageBox.Show(msg.ToString());

                double score = hive._bestMatrix.Score();

                Matrix.EndOptimization();

                return hive._bestMatrix;
            }
        }

        public class Matrix
        {
            static Matrix tempMatrix = null;
            static Line[] linesInput;

            public LineDisposition[] disps;
            public short start;

            short endCount;
            short[] endIndices;
            short[] endIndicesUndo;

            Matrix.UndoRecord _undo;

            public Matrix()
            {
                disps = new LineDisposition[linesInput.Length];
                start = 0;

                endIndices = new short[linesInput.Length];
                endIndicesUndo = new short[linesInput.Length];
                endCount = 0;
               
                ClearDispositions();
            }

            public struct UndoRecord
            {
                public short mutation;
                public short i;
                public short j;
                public short endCount;
            }

            static public void BeginOptimization(Line[] lines)
            {
                linesInput = lines;
                tempMatrix = new Matrix();
            }

            static public void EndOptimization()
            {
                tempMatrix = null;
                linesInput = null;
            }

            public void CopyMatrixTo(Matrix dest)
            {
                Array.Copy(disps, dest.disps, disps.Length);
                Array.Copy(endIndices, dest.endIndices, endIndices.Length);
                dest.start = start;
                dest.endIndices = endIndices;
            }

            public static Matrix GetTemp()
            {
                return tempMatrix;
            }

            public static Matrix GenerateEmptyTempMemoryMatrix()
            {
                Matrix result = GetTemp();

                result.ClearDispositions();

                return result;
            }

            public static Matrix GenerateRandomTempMemoryMatrix()
            {
                Matrix result = GenerateEmptyTempMemoryMatrix();

                for (int i = 0; i < result.disps.Length * 3; i++)
                {
                    result.PerturbMemoryMatrix();
                }

                return result;
            }

            public void PerturbMemoryMatrix()
            {
                short[] temp = endIndices;
                endIndices = endIndicesUndo;
                endIndicesUndo = temp;
                _undo.endCount = endCount;

                _undo.mutation = (short)random.Next(22);

                if (_undo.mutation < 20)
                {
                    if (_undo.mutation < 12 && endCount > 1)
                    {
                        int i1 = random.Next(endCount);
                        int i2 = (i1 + 1 + random.Next(endCount - 1)) % endCount;

                        _undo.i = endIndices[i1];
                        _undo.j = endIndices[i2];

                        if (_undo.mutation < 6)
                        {
                            _undo.j = disps[_undo.j].prev;
                        }

                        if (_undo.i == _undo.j)
                        {
                            _undo.j = (short)((_undo.i + 1 + random.Next(disps.Length - 1)) % disps.Length);
                        }
                    }
                    else
                    {
                        _undo.i = (short)(random.Next(disps.Length));
                        _undo.j = (short)((_undo.i + 1 + random.Next(disps.Length - 1)) % disps.Length);
                    }

                    if (_undo.mutation % 2 == 0)
                    {
                        disps[_undo.i].forward = !disps[_undo.i].forward;
                    }

                    var di = disps[_undo.i];

                    // unlink i
                    disps[di.prev].next = di.next;
                    disps[di.next].prev = di.prev;

                    var dj = disps[_undo.j];

                    // insert i between j and jprev
                    disps[dj.prev].next = _undo.i;
                    disps[_undo.j].prev = _undo.i;

                    // link i to its neighbours
                    disps[_undo.i].next = _undo.j;
                    disps[_undo.i].prev = dj.prev;

                    // now remember to undo this we put 'i' back after 'j'
                    _undo.j = di.prev;
                }
                else
                {
                    _undo.j = (short)(random.Next(disps.Length - 1));
                    start = (short)((start + _undo.j) % disps.Length);
                }
            }

            public void UndoPerturbMemoryMatrix()
            {
                short[] temp = endIndices;
                endIndices = endIndicesUndo;
                endIndicesUndo = temp;
                endCount = _undo.endCount;

                if (_undo.mutation < 20)
                {
                    if (_undo.mutation % 2 == 0)
                    {
                        disps[_undo.i].forward = !disps[_undo.i].forward;
                    }

                    var di = disps[_undo.i];

                    // unlink i
                    disps[di.prev].next = di.next;
                    disps[di.next].prev = di.prev;

                    var dj = disps[_undo.j];

                    // insert i between j and j next
                    disps[_undo.j].next = _undo.i;
                    disps[dj.next].prev = _undo.i;

                    // link i to its neighbours
                    disps[_undo.i].prev = _undo.j;
                    disps[_undo.i].next = dj.next;
                }
                else
                {
                    start = (short)((start + disps.Length - _undo.j) % disps.Length);
                }

                _undo.i = -1;
            }

            void ClearDispositions()
            {
                for (short i = 0; i < disps.Length; i++)
                {
                    var disp = new LineDisposition();
                    disp.x1 = linesInput[i].X1;
                    disp.x2 = linesInput[i].X2;
                    disp.y1 = linesInput[i].Y1;
                    disp.y2 = linesInput[i].Y2;
                    disp.forward = true;
                    disp.connect = 0;
                    disp.next = (short)((i + 1) % disps.Length);
                    disp.prev = (short)((i + disps.Length - 1) % disps.Length);
                    disps[i] = disp;
                }

                endCount = 0;
            }

            public double Score()
            {
                double score = 0;
                Point p0 = disps[start].Start;
                int i0 = 0;
                short here = start;
                int cycle = 0;

                // the start point is the first 'end'
                endCount = 1;
                endIndices[0] = start;

                double discount = 1.01;

                for (int i = 0; i < disps.Length; i++)
                {
                    cycle++;
                    short next = disps[here].next;

                    var d1 = disps[here];
                    var d2 = disps[next];

                    Point p1 = d1.End;
                    Point p2 = d2.Start;

                    var distsq = (p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y);
                    var distsq0 = (p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y);

                    // option 1 cost of the merge is the skipped distance from my end to his start
                    double m1 = distsq;
                    // penalty for a bad merge
                    if (distsq > 225) m1 += 10000;

                    // option 2, new segment penalty for starting a new segment
                    double m2 = 225;

                    // a break at the end has a wee discount
                    if (i == disps.Length - 1 && i0 == 0)
                        m2 -= 100;

                    // option 3, loop back, make a cycle back to the last known start
                    double m3 = distsq0 / 2;
                    // penalty for a bad merge
                    if (distsq0 > 225) m3 += 10000; 
                    // this isn't a cycle, it's got to be a least a triangle
                    if (cycle < 3) m3 += 10000;

                    if (m3 <= m1 && m3 <= m2)
                    {
                        disps[here].connect = ConnectionType.LoopBack;
                        score += m3;
                        // new start
                        p0 = p2;
                        i0 = i;
                        RecordEnd(here);
                        RecordEnd(next);
                    }
                    else if (m1 < m2)
                    {
                        disps[here].connect = ConnectionType.Connect;
                        score += m1;
                        discount *= 1.01;
                    }
                    else
                    {
                        disps[here].connect = ConnectionType.NewSegment;
                        score += m2;
                        p0 = p2;
                        i0 = i;
                        cycle = 0;
                        RecordEnd(here);
                        RecordEnd(next);
                        score -= discount;
                        discount = 1.01;
                    }

                    here = next;
                }

                return score;
            }

            void RecordEnd(short here)
            {
                if (endCount == 0 || endIndices[endCount - 1] != here) 
                    if (endCount < endIndices.Length)
                        endIndices[endCount++] = here;
            }
        }       
        
        class Hive
        {
            public int _beesTotal; // mostly for readability in the object constructor call
            public int _beesInactive;
            public int _beesActive;
            public int _beesScouting;

            public int _iterationsMax; // one cycle represents an action by all bees in the hive

            public int _visitsMax; // max number of times bee will visit a given food source without finding a better neighbor
            public double _probPersuasion = 0.90; // probability inactive bee is persuaded by better waggle solution
            public double _probMistake = 0.01; // probability an active bee will reject a better neighbor food source OR accept worse neighbor food source

            public Bee[] _bees;
            public Matrix _bestMatrix; // problem-specific
            public double _bestScore;
            public int[] _indicesInactive; // contains indexes into the bees array

            int _wagglesCount;
            int _superscoutsCount;

            public Hive(int beesInactive, int beesActive, int beesScouting, int visitsMax, int iterationsMax)
            {
                _visitsMax = visitsMax;
                _iterationsMax = iterationsMax;

                _beesInactive = beesInactive;
                _beesActive = beesActive;
                _beesScouting = beesScouting;
                _beesTotal = beesInactive + beesScouting + beesActive;

                _bees = new Bee[_beesTotal];
                _bestMatrix = new Matrix();
                _bestScore = Bee.SuperScout(_bestMatrix, double.MaxValue, 10000);

                // these bees are not active
                _indicesInactive = new int[beesInactive];

                int next = 0;

                for (int i = 0; i < beesInactive; i++)
                {
                    _bees[next++] = NewBee(BeeState.Inactive);
                    _indicesInactive[i] = i; 
                }

                for (int i = 0; i < beesActive; i++)
                {
                    _bees[next++] = NewBee(BeeState.Active);
                }

                for (int i = 0; i < beesScouting; i++)
                {
                    _bees[next++] = NewBee(BeeState.Scouting);
                }
            }

            Bee NewBee(BeeState state)
            {
                var bee = new Bee(state);

                TestNewBest(bee._memoryMatrix, bee._memoryScore);

                return bee;
            }

            // solve it
            public void Solve()
            {
                int progressIterations = 1000;
                int iterations = 0;

                while (iterations < _iterationsMax)
                {
                    for (int i = 0; i < _beesTotal; i++) 
                    {
                        var bee = _bees[i];

                        if (bee._state == BeeState.Active)
                            bee.DoActive(this, i);
                        else if (bee._state == BeeState.Scouting)
                            bee.DoScout(this, i);
                    }
                    iterations++;

                    // emit progress
                    if (iterations % progressIterations == 0)
                    {
                        _bestScore = Bee.SuperScout(_bestMatrix, _bestScore, 1000);

                        msg.AppendFormat("Iterations:{0} Score: {1}  ss:{2} wag:{3}\n", iterations, _bestScore, _superscoutsCount, _wagglesCount);

                        _wagglesCount = 0;
                        _superscoutsCount = 0;
                    }
                }
            }

            void TestNewBest(Matrix newBest, double score)
            {
                if (score < _bestScore)
                {
                    newBest.CopyMatrixTo(_bestMatrix);
                    _bestScore = score;
                }
            }

            void Waggle(Bee dancer)
            {
                for (int i = 0; i < _beesInactive; i++)
                {
                    var watcher = _bees[_indicesInactive[i]];

                    if (watcher._newlyInactive || dancer._memoryScore < watcher._memoryScore) 
                    {
                        watcher._newlyInactive = false;

                        if (random.NextDouble() < _probPersuasion)
                        {
                            dancer._memoryMatrix.CopyMatrixTo(watcher._memoryMatrix);
                            watcher._memoryScore = dancer._memoryScore;
                        } 
                    } 
                }
                _wagglesCount++;
            }

            public enum BeeState
            {
                Active,
                Scouting,
                Inactive
            };

            public class Bee
            {
                public BeeState _state;
                public Matrix _memoryMatrix; // problem-specific. a path of cities.
                public double _memoryScore; // smaller values are better. total distance of path.
                public int _visits;
                public bool _newlyInactive;

                public Bee(BeeState state)
                {
                    _memoryMatrix = new Matrix();
                    Matrix.GenerateRandomTempMemoryMatrix().CopyMatrixTo(_memoryMatrix);
                    _memoryScore = _memoryMatrix.Score();
                    _state = state;
                    _visits = 0;
                }

                public void DoActive(Hive hive, int index)
                {
                    _memoryMatrix.PerturbMemoryMatrix(); // find a neighbor solution
                    double trialScore = _memoryMatrix.Score(); // get its quality

                    if (trialScore < _memoryScore)
                    {
                        // active bee found better neighbor (< because smaller values are better)
                        if (random.NextDouble() < hive._probMistake)
                        {
                            // bee makes mistake: rejects a better neighbor food source
                            _memoryMatrix.UndoPerturbMemoryMatrix();
                            _visits++;
                        }
                        else
                        {
                            // bee does not make a mistake: accepts a better neighbor
                            _memoryScore = trialScore; // update the quality
                            _visits = 0;
                        }
                    }
                    else
                    {
                        // active bee did not find a better neighbor

                        if (random.NextDouble() < hive._probMistake)
                        {
                            // bee makes mistake: accepts a worse neighbor food source
                            _memoryScore = trialScore; // update the quality
                            _visits = 0;
                        }
                        else
                        {
                            // no mistake: bee rejects worse food source
                            _memoryMatrix.UndoPerturbMemoryMatrix();
                            _visits++;
                        }
                    }

                    if (_visits == 0)
                    {
                        // we found something better, visits was reset

                        // if we have the new best score, save it
                        hive.TestNewBest(_memoryMatrix, _memoryScore);

                        // tell everyone the great news
                        hive.Waggle(this);
                    }
                    else if (_visits > hive._visitsMax)
                    {
                        // we're tired, too many visits with no progress, this be goes inactive
                        _state = BeeState.Inactive;
                        _newlyInactive = true;
                        _visits = 0;

                        // wake up a currently inactive bee
                        int wakeup = random.Next(hive._beesInactive);
                        hive._bees[hive._indicesInactive[wakeup]]._state = BeeState.Active;
                        hive._indicesInactive[wakeup] = index;
                    }
                }

                static public double SuperScout(Matrix matrix, double score, int count)
                {
                    int visits = 0;
                    while (visits < count)
                    {
                        // try something nearby
                        matrix.PerturbMemoryMatrix();
                        double trialScore = matrix.Score();

                        // take only better scores
                        if (trialScore < score)
                        {
                            score = trialScore;
                            visits = 0;
                        }
                        else
                        {
                            matrix.UndoPerturbMemoryMatrix();
                            visits++;
                            trialScore = matrix.Score();
                            Debug.Assert(trialScore == score);
                        }
                    }

                    return score;
                }

                public void DoScout(Hive hive, int index)
                {
                    Matrix trial;
                    double trialScore;
                    GenerateTempScoutingMatrix(hive, out trial, out trialScore);

                    // let's see if we have an announcement to make
                    if (random.NextDouble() < hive._probMistake * 5 || trialScore < _memoryScore)
                    {
                        // keep the trial
                        trial.CopyMatrixTo(_memoryMatrix);
                        _memoryScore = trialScore;

                        // if we have a new best score save it
                        hive.TestNewBest(_memoryMatrix, _memoryScore);

                        // tell everyone the good news
                        hive.Waggle(this);
                    }
                }

                void GenerateTempScoutingMatrix(Hive hive, out Matrix temp, out double trialScore)
                {
                    bool fSuper = false;

                    int r = random.Next(10000);
                    if (r == 0)
                    {
                        temp = Matrix.GenerateEmptyTempMemoryMatrix(); // scout bee starts at home base
                        fSuper = true;
                    }
                    else if (r == 1)
                    {
                        temp = Matrix.GetTemp();
                        hive._bestMatrix.CopyMatrixTo(temp);

                        var k = random.Next(50) + 5;

                        for (int j = 0; j < k; j++)
                            temp.PerturbMemoryMatrix();

                        fSuper = true;
                    }
                    else if (r == 2)
                    {
                        temp = Matrix.GetTemp();
                        _memoryMatrix.CopyMatrixTo(temp);

                        var k = random.Next(50) + 5;

                        for (int j = 0; j < k; j++)
                            temp.PerturbMemoryMatrix();

                        fSuper = true;
                    }
                    else
                    {
                        if (r == 3)
                            fSuper = true;

                        temp = Matrix.GenerateRandomTempMemoryMatrix(); // scout bee finds a random food source. . . 
                    }

                    trialScore = temp.Score();

                    if (fSuper)
                    {
                        hive._superscoutsCount++;
                        trialScore = SuperScout(temp, trialScore, 100);
                    }
                }
            }
        } 
    }
}
