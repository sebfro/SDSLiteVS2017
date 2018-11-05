﻿using Microsoft.Research.Science.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpagettiMetoden
{
    class Controller
    {
        public ReadFromFile file { get; set; }
        Dictionary<string, Fish> FishList { get; set; }
        List<string> KeyList { get; set; }

        HeatMap HeatMap { get; set; }
        EtaXi[] EtaXis { get; set; }
        public CallPython callPython { get; set; }
        public CalcDistance_BetweenTwoLonLatCoordinates calcDistance_BetweenTwoLonLatCoordinates { get; set; }

        public int TagStep { get; set; }
        public int DayIncrement { get; set; }
        public int ReleasedFish { get; set; }
        public double TempDelta { get; set; }
        

        static object syncObject = new object();

        public void SetDayIncrement(int dayInc)
        {
            DayIncrement = dayInc;
            //144 er incrementet for å hoppe 24 timer/1 dag i merkedage
            //Ganger det med antall dager som skal inkrementeres.
            TagStep = 144 * dayInc;
        }

        public Controller(int dayInc, int releasedFish, double tempDelta, int depthDelta, double Increment, double Increment2)
        {
            TempDelta = tempDelta;
            ReleasedFish = releasedFish;
            SetDayIncrement(dayInc);

            file = new ReadFromFile();

            FishList = new Dictionary<string, Fish>();
            KeyList = new List<string>();

            file.readReleaseAndCapture(FishList, KeyList);
            file.readTagData(FishList, KeyList);



            HeatMap = new HeatMap();
            EtaXis = new EtaXi[0];
            callPython = new CallPython(dayInc);
            calcDistance_BetweenTwoLonLatCoordinates = new CalcDistance_BetweenTwoLonLatCoordinates(Increment, Increment2, depthDelta, dayInc);
            
        }

        public void SetIncrements(double Increment, double Increment2)
        {
            calcDistance_BetweenTwoLonLatCoordinates.Increment = Increment;
            calcDistance_BetweenTwoLonLatCoordinates.Increment2 = Increment2;
        }

        public void SetDepthDelta(int DepthDelta)
        {
            calcDistance_BetweenTwoLonLatCoordinates.SetDepthDelta(DepthDelta);
        }

        public void RunAlgorithm()
        {
            int day = GlobalVariables.day;
            int counter = 1;
            int deadFishCounter = 0;
            var watch = Stopwatch.StartNew();
            FishList["742"].FishRouteList = new BlockingCollection<FishRoute>(boundedCapacity: ReleasedFish);
            Console.WriteLine("Released Fish: {0}", ReleasedFish);
            Console.WriteLine("Tagstep: {0}", TagStep);

            for (int i = 0; i < FishList["742"].TagDataList.Count; i += TagStep)
            {
                
                Console.WriteLine("I iterasjon: " + i / TagStep);
                bool chosenPosition;

                if (i == 0)
                {
                    var watch2 = Stopwatch.StartNew();

                    int randInt = 0;
                    PositionData positionData = CalculateXiAndEta.GeneratePositionDataArrayList(HeatMap.LatArray, HeatMap.LonArray, FishList["742"].ReleaseLat, FishList["742"].ReleaseLon);
                    BlockingCollection<PositionData> validPositionsDataList =
                        calcDistance_BetweenTwoLonLatCoordinates.FindValidPositions(
                            calcDistance_BetweenTwoLonLatCoordinates.CalculatePossibleEtaXi(positionData.eta_rho, positionData.xi_rho),
                        HeatMap.LatArray, HeatMap.LonArray, FishList["742"].TagDataList[i], callPython, TempDelta
                            );

                    float releaseLat = (float)FishList["742"].ReleaseLat;
                    float releaseLon = (float)FishList["742"].ReleaseLon;

                    Parallel.For(0, ReleasedFish, (j) =>
                    {
                        
                        chosenPosition = false;
                        bool addedToPosDataList = false;
                        bool addedToFishRoutList = false;

                        if (validPositionsDataList.Count > 0)
                        {
                            FishRoute fishRoute = new FishRoute("742");
                            fishRoute.PositionDataList.Add((new PositionData(releaseLat,
                                releaseLon)));

                            RouteChooser routeChooser = new RouteChooser(releaseLat, releaseLon, FishList["742"]);

                            while (!chosenPosition)
                            {
                                randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                                chosenPosition = routeChooser.chosenRoute(validPositionsDataList, randInt);
                            }

                            while (!addedToPosDataList)
                            {
                                addedToPosDataList = fishRoute.PositionDataList.TryAdd((new PositionData(
                                    validPositionsDataList.ElementAt(randInt).lat, validPositionsDataList.ElementAt(randInt).lon,
                                    validPositionsDataList.ElementAt(randInt).depth, validPositionsDataList.ElementAt(randInt).temp, FishList["742"].TagDataList[i].depth,
                                    FishList["742"].TagDataList[i].temp, validPositionsDataList.ElementAt(randInt).eta_rho, validPositionsDataList.ElementAt(randInt).xi_rho)));
                            }

                            while (!addedToFishRoutList)
                            {
                                addedToFishRoutList = FishList["742"].FishRouteList.TryAdd(fishRoute);
                            }
                        } else
                        {
                            Interlocked.Increment(ref deadFishCounter);
                        }
                    });
                }
                else
                {
                    callPython.UpdateTempArray(day);
                    BlockingCollection<FishRoute> fishRoutes = FishList["742"].FishRouteList;
                    TagData tagData = FishList["742"].TagDataList[i];
                    if (deadFishCounter < ReleasedFish)
                    {
                        
                        Parallel.ForEach(fishRoutes, (fishRoute) =>
                        {
                            int randInt = 0;
                            chosenPosition = false;
                            EtaXi[] possiblePositionsArray;
                            BlockingCollection<PositionData> validPositionsDataList;
                            if (fishRoute.alive)
                            {
                                PositionData pData = fishRoute.PositionDataList.ElementAt(counter);

                                lock (syncObject)
                                {
                                    possiblePositionsArray = calcDistance_BetweenTwoLonLatCoordinates.CalculatePossibleEtaXi(pData.eta_rho, pData.xi_rho);
                                    validPositionsDataList =
                                        calcDistance_BetweenTwoLonLatCoordinates.FindValidPositions(
                                            possiblePositionsArray,
                                            HeatMap.LatArray, HeatMap.LonArray, tagData, callPython, TempDelta);
                                }

                                

                                if (validPositionsDataList.Count > 0)
                                {

                                        RouteChooser routeChooser =
                                            new RouteChooser(pData.lat, pData.lon, FishList["742"]);
                                        while (!chosenPosition)
                                        {
                                            randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                                            chosenPosition = routeChooser.chosenRoute(validPositionsDataList, randInt);
                                        }

                                        fishRoute.PositionDataList.Add((new PositionData(
                                            validPositionsDataList.ElementAt(randInt).lat,
                                            validPositionsDataList.ElementAt(randInt).lon,
                                            validPositionsDataList.ElementAt(randInt).depth,
                                            validPositionsDataList.ElementAt(randInt).temp,
                                            tagData.depth, tagData.temp,
                                            validPositionsDataList.ElementAt(randInt).eta_rho,
                                            validPositionsDataList.ElementAt(randInt).xi_rho)));
                                    
                                }

                                else
                                {
                                    Interlocked.Increment(ref deadFishCounter);
                                    fishRoute.commitNotAlive();
                                    /*Console.WriteLine("I iterasjon: " + i / GlobalVariables.tagStep + " ELIMINERT");
                                    Console.WriteLine("eta: " + pData.eta_rho + ", xi: " + pData.xi_rho);
                                    Console.WriteLine("dybde: " + tagData.depth + ", temp: " + tagData.temp);
                                    Console.WriteLine("dybde: " + pData.depth + ", temp: " + pData.temp);
                                    */
                                }
                            }
                        });
                    }
                    else
                    {
                        i = FishList["742"].TagDataList.Count;
                    }

                    counter++;
                }
                
                day += DayIncrement;
            }

            watch.Stop();
            double elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine("Hvor lang tid tok programmet: " + elapsedMs);
            var count = 1;
            string folderName = "Uakseptabel";
            var FishData = FishList["742"];
            double captureLat = FishData.CaptureLat;
            double captureLon = FishData.CaptureLon;


            Console.WriteLine("Hvor lang tid tok programmet: {0} minutter.", elapsedMs / 60000);
            Console.WriteLine("Hvor lang tid tok programmet: {0} sekunder.", elapsedMs / 1000);
            Console.WriteLine("Fishlist count: {0}", FishList["742"].FishRouteList.Count);
            Console.WriteLine("Dead fish counter: {0}", deadFishCounter);
            Console.WriteLine("Alive fish counter: {0}", ReleasedFish - deadFishCounter);
            if (deadFishCounter == ReleasedFish)
            {
                Console.WriteLine("All fish are dead");

            }
            else
            {
                //SLETTER ALLE FILER I FOLDER AKSEPTABEL OG UAKSEPTABEL !!!!!!!!!!!!!!!!!! Lag backup folder om tester hjemme eller HI
                DirectoryInfo di = new DirectoryInfo(@"C:\NCdata\fishData\Akseptabel\");
                DirectoryInfo di2 = new DirectoryInfo(@"C:\NCdata\fishData\Uakseptabel\");

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (FileInfo file in di2.GetFiles())
                {
                    file.Delete();
                }
            }
            Console.WriteLine("Day is: {0}", day);

            foreach (var fishRoute in FishList["742"].FishRouteList)
            {
                //Console.WriteLine("Is fish alive?: " + fishRoute.alive);
                if (fishRoute.alive)
                {
                    var posData = fishRoute.PositionDataList.ElementAt(fishRoute.PositionDataList.Count - 1);
                    if (CalcDistance_BetweenTwoLonLatCoordinates.GetDistanceFromLatLonInKm(posData.lat, posData.lon, captureLat, captureLon) < calcDistance_BetweenTwoLonLatCoordinates.Increment)
                    {
                        folderName = "Akseptabel";
                    } else
                    {
                        folderName = "Uakseptabel";
                    }
                    string[] fishData = fishRoute.fromListToString();

                    File.WriteAllLines(GlobalVariables.pathToSaveFishData + @"\\" + folderName + "\\" + fishRoute.id + "_" + count + ".txt", fishData);
                    count++;
                }
            }
            
        }
    }
}
