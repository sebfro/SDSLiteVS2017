using Microsoft.Research.Science.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using MathNet.Numerics.Statistics;
using System.Globalization;

namespace SimuleringsApplikasjonen
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string tagId = (args[0]);
                double dayInc = double.Parse(args[1].Replace(".", ","));
                int releasedFish = int.Parse(args[2]);
                double tempDelta = double.Parse(args[3].Replace(".", ","));
                int DepthDelta = int.Parse(args[4]);
                double Increment = double.Parse(args[5].Replace(".", ","));
                double Probability = double.Parse(args[6].Replace(".", ","));
                int iterations = int.Parse(args[7]);
                int algorithm = int.Parse(args[8]);

                
                Console.WriteLine("Settings:");
                Console.WriteLine("Day increment: {0}\nSimulated trajectories: {1}\nTemperature Delta: {2}\nDepth delta: {3}\nFish length: {4}\nPropability: {5}\nIterations: {6}\nAlgorithm: {7}\nTag id: {8}",
                    dayInc, releasedFish, tempDelta, DepthDelta, Increment, Probability, iterations, algorithm == 0 ? "General" : "Merge", tagId);
                

                //Skriv til setup fil
                string[] setup = { "ID:\t Days:\t Numb of fish\t Temp delta:\t Depth delta:\t Increment 1: \t Increment 2:\t", args[0] + "\t" + args[1] + "\t" + args[2] + "\t"
                                                                                                                           + args[3] + "\t" + args[4] + "\t" + args[5] + "\t" + args[6] + "\t" + args[7]};

                File.WriteAllLines(@"C:\NCdata\fishData\setup.txt", setup);

                Console.WriteLine("Starting simulation...");
                var watch = Stopwatch.StartNew();
                bool failed = false;
                //controller = new Controller(dayInc, releasedFish, tempDelta, DepthDelta, Increment, Probability, iterations);
                if(algorithm == 0)
                {
                    Controller controller = new Controller(dayInc, releasedFish, tempDelta, DepthDelta, Increment, Probability, iterations, tagId);
                    controller.RunAlgorithm();
                } else if (algorithm == 1)
                {
                    ControllerReleaseFwAndBw controller = new ControllerReleaseFwAndBw(dayInc, releasedFish, tempDelta, DepthDelta, Increment, Probability, iterations, tagId);

                    if (controller.RunAlgorithmFW())
                    {
                        if (controller.RunAlgorithmBW())
                        {
                            Merge merge = new Merge(tagId);
                            merge.MergeFwAndBwFiles(Increment, dayInc);
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                    else
                    {
                        failed = true;
                    }

                    if (failed)
                    {
                        Console.WriteLine("Could not start simulation");
                    }
                } else
                {
                    //Console.WriteLine("Kj�rer nr 3");
                    //ControllerReleaseSteadily controller = new ControllerReleaseSteadily(dayInc, releasedFish, tempDelta, DepthDelta, Increment, Probability, iterations);
                    //controller.RunAlgorithm();
                }
                 

                watch.Stop();
                double elapsedMs = watch.ElapsedMilliseconds;
                Console.WriteLine("Run time of the program: {0} seconds.", elapsedMs / 1000);
                //controller.RunAlgorithm();
            } catch(Exception e) {

                Console.WriteLine(e);
                Console.WriteLine("Error, bruker standard");
                string tagId = "742";
                float tempDelta = 1.2f;
                int releasedFish = 10000;
                float dayInc = 1f;
                int depthDelta = 30;
                float fishlength = 0.82f;
                float probability = 0.75f;
                int iterations = 30;
                int algorithm = 1;
                GlobalVariables.select_random_location = true;
                Console.WriteLine("Day increment: {0} \nSimulated Trajectories: {1} \nTemperature delta: {2} \nDepth delta: {3} \nIterations: {4} \nPropability: {5} \nFish Lenght: {6} \nTag id: {7}",
                    dayInc, releasedFish, tempDelta, depthDelta, iterations, probability, fishlength, tagId);
                if (GlobalVariables.select_random_location)
                {
                    Console.WriteLine("Chooses valid location at random.");
                } else
                {
                    Console.WriteLine("Chooses valid location with temperature closest to DST temperature");
                }
                
                
                if (algorithm == 0)
                {
                    Console.WriteLine("Algorithm: General");
                    Controller controller = new Controller(dayInc, releasedFish, tempDelta, depthDelta, fishlength, probability, iterations, tagId);
                    controller.RunAlgorithm();
                } else if(algorithm == 1)
                {
                    Console.WriteLine("Algorithm: Merge");
                    ControllerReleaseFwAndBw controller = new ControllerReleaseFwAndBw(dayInc, releasedFish, tempDelta, depthDelta, fishlength, probability, iterations, tagId);
                    Console.WriteLine("Running Forward");
                    if (controller.RunAlgorithmFW())
                    {
                        Console.WriteLine("Running Backward");
                        if (controller.RunAlgorithmBW())
                        {
                            Merge merge = new Merge(tagId);
                            merge.MergeFwAndBwFiles(fishlength, dayInc);
                        }
                        else
                        {
                            Console.WriteLine("Backawards simulation could not be started.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Forwards simulation could not be started.");
                    }
                    
                } else if(algorithm == 2)
                {
                    ControllerReleaseSteadily controller = new ControllerReleaseSteadily(dayInc, releasedFish, tempDelta, depthDelta, fishlength, probability, iterations, tagId);
                    controller.RunAlgorithm();
                }
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Console.ReadKey();
        }

        public void CreateNewNetCDF()
        {
            int norkyst_eta_rho = 902;
            int norkyst_xi_rho = 2602;
            int barents_eta_rho = 580;
            int barents_xi_rho = 1202;

            DataSet norkystDs = DataSet.Open(@"D:\NCdata\Nye norkyst\norkyst_800m_avg.nc");
            Array norkystLatArray = norkystDs["lat_rho"].GetData();
            double[,] norkystLatTable = new double[norkyst_eta_rho, norkyst_xi_rho];
            Array norkystLonArray = norkystDs["lon_rho"].GetData();
            double[,] norkystLonTable = new double[norkyst_eta_rho, norkyst_xi_rho];
            for (int etaInc = 0; etaInc < norkyst_eta_rho; etaInc++)
            {
                for (int xiInc = 0; xiInc < norkyst_xi_rho; xiInc++)
                {
                    norkystLatTable[etaInc, xiInc] = (double)norkystLatArray.GetValue(etaInc, xiInc);
                    norkystLonTable[etaInc, xiInc] = (double)norkystLonArray.GetValue(etaInc, xiInc);
                }
            }

            DataSet barentsDs = DataSet.Open(@"D:\NCdata\ocean_avg_LatAndLonRho.nc");
            Array barentsLatArray = barentsDs["lat_rho"].GetData();
            double[,] barentsLatTable = new double[barents_eta_rho, barents_xi_rho];
            Array barentsLonArray = barentsDs["lon_rho"].GetData();
            double[,] barentsLonTable = new double[barents_eta_rho, barents_xi_rho];
            for (int etaInc = 0; etaInc < barents_eta_rho; etaInc++)
            {
                for (int xiInc = 0; xiInc < barents_xi_rho; xiInc++)
                {
                    barentsLatTable[etaInc, xiInc] = (double)barentsLatArray.GetValue(etaInc, xiInc);
                    barentsLonTable[etaInc, xiInc] = (double)barentsLonArray.GetValue(etaInc, xiInc);
                }
            }

            BlockingCollection<int[]> norkystEtaXi = new BlockingCollection<int[]>();
            BlockingCollection<int[]> barentsEtaXi = new BlockingCollection<int[]>();
            barentsEtaXi.Add(new int[] { 0, 0 });

            int[,] norKystGrid = new int[norkyst_eta_rho, norkyst_xi_rho];
            for (int i = 0; i < norkyst_eta_rho; i++)
            {
                for (int j = 0; j < norkyst_xi_rho; j++)
                {
                    norKystGrid[i, j] = j;
                }
            }
            int[,] barentsGrid = new int[barents_eta_rho, barents_xi_rho];
            for (int i = 0; i < barents_eta_rho; i++)
            {
                for (int j = 0; j < barents_xi_rho; j++)
                {
                    barentsGrid[i, j] = 10;
                }
            }

            
            DataSet norkystMappedToBarentsSea = DataSet.Open(@"D:\NCdata\TestData\norkystMappedToBarentsSea.nc");
            //norkystMappedToBarentsSea.AddVariable<int>("eta_rho", norKystGrid, new string[2] { "eta_rho", "xi_rho" });
            //norkystMappedToBarentsSea.AddVariable<int>("xi_rho", norKystGrid, new string[2] { "eta_rho", "xi_rho" });
            DataSet barentsSeaMappedToNorkyst = DataSet.Open(@"D:\NCdata\TestData\barentsSeaMappedToNorkyst.nc");
            //barentsSeaMappedToNorkyst.AddVariable<int>("eta_rho", barentsGrid, new string[2] { "eta_rho", "xi_rho" });
            //barentsSeaMappedToNorkyst.AddVariable<int>("xi_rho", barentsGrid, new string[2] { "eta_rho", "xi_rho" });


            Console.WriteLine("NetCDF value: " + barentsSeaMappedToNorkyst["eta_rho"].GetData().GetValue(10,10));
            Console.WriteLine("Array value: " + barentsGrid[100,100]);

            Console.WriteLine("DIMS:" + barentsSeaMappedToNorkyst["eta_rho"].Dimensions);

            Console.WriteLine("NetCDF created");
            Stopwatch stopWatch1 = new Stopwatch();
            stopWatch1.Start();
            //Disse skal inn i netcdfen som har barents i starten av navnet
            int[,] barentsEta = new int[barents_eta_rho, barents_xi_rho];
            int[,] barentsXi = new int[barents_eta_rho, barents_xi_rho];
            //Disse skal inn i netcdfen som har norkyst i starten av navnet
            int[,] norkystEta = new int[norkyst_eta_rho, norkyst_xi_rho];
            int[,] norkystXi = new int[norkyst_eta_rho, norkyst_xi_rho];

            for (int eta = 0; eta < norkyst_eta_rho; eta++)
            {
                Console.WriteLine("Eta: {0}, av {1}", eta, norkyst_eta_rho);
                int etaStart = 0;
                int etaEnd = barents_eta_rho;
                int xiStart = 0;
                int xiEnd = barents_xi_rho;
                for (int xi = 0; xi < norkyst_xi_rho; xi++)
                {
                    int[] etaXi = StdTest(eta, xi, norkystLatTable, norkystLonTable, barentsLatTable, barentsLonTable, etaStart, xiStart, etaEnd, xiEnd);
                    etaStart = etaXi[0];
                    xiStart = etaXi[1];

                    barentsEta[etaStart, xiStart] = eta;
                    barentsXi[etaStart, xiStart] = xi;

                    norkystEta[eta, xi] = etaStart;
                    norkystXi[eta, xi] = xiStart;

                    if (etaStart >= 10)
                    {
                        etaStart -= 10;
                    }
                    if (xiStart >= 10)
                    {
                        xiStart -= 10;
                    }
                    if (etaEnd <= barents_eta_rho - 10)
                    {
                        etaEnd += 10;
                    }
                    if (xiEnd <= barents_xi_rho - 10)
                    {
                        xiEnd += 10;
                    }
                    //Console.WriteLine("Eta: {0}, Xi: {1}", etaXi[0], etaXi[1]);
                    //Console.WriteLine("Eta: {0}, Xi: {1}", eta, xi);
                }
            }

            barentsSeaMappedToNorkyst["Eta"].PutData(barentsEta);
            barentsSeaMappedToNorkyst["Xi"].PutData(barentsXi);
            barentsSeaMappedToNorkyst.Commit();

            norkystMappedToBarentsSea["Eta"].PutData(norkystEta);
            norkystMappedToBarentsSea["Xi"].PutData(norkystXi);
            norkystMappedToBarentsSea.Commit();

            stopWatch1.Stop();
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch1.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine("CPU RunTime " + elapsedTime);
            Console.ReadKey();
        }

        

        public static int[] StdTest(int eta, int xi, double[,] norkystLat, double[,] norkystLon, double[,] barentsLat, double[,] barentsLon, int etaStart, int xiStart, int etaEnd, int xiEnd)
        {
            int norkyst_eta_rho = 902;
            int norkyst_xi_rho = 2602;
            int barents_eta_rho = 580;
            int barents_xi_rho = 1202;

            int currEta = eta;
            int currXi = xi;
            int tmpEta = 0;
            int tmpXi = 0;
            double mindelta = 1000;

            double lat = norkystLat[currEta, currXi];
            double lon = norkystLon[currEta, currXi];

            //for (int etaInc = 0; etaInc < barents_eta_rho; etaInc++)
            Parallel.For(etaStart, etaEnd, etaInc =>
            {
                for (int xiInc = xiStart; xiInc < xiEnd; xiInc++)
                {
                    double latDiff = (barentsLat[etaInc, xiInc] - lat);
                    double lonDiff = (barentsLon[etaInc, xiInc] - lon);

                    if (latDiff < 0)
                    {
                        latDiff = latDiff * (-1);
                    }

                    if (lonDiff < 0)
                    {
                        lonDiff = lonDiff * (-1);
                    }

                    double newDelta = latDiff + lonDiff;
                    if (newDelta < mindelta)
                    {
                        mindelta = newDelta;
                        tmpEta = etaInc;
                        tmpXi = xiInc;
                    }
                }
            });
            return new int[2] { tmpEta, tmpXi };
        }
    }
}  
   
   
   