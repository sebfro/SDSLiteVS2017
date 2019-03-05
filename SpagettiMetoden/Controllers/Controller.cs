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
        public ReadFromFile File { get; set; }
        Dictionary<string, Fish> FishList { get; set; }
        List<string> KeyList { get; set; }

        HeatMap HeatMap { get; set; }
        EtaXi[] EtaXis { get; set; }
        public TempContainer TempContainer { get; set; }
        public CalculateCoordinates CalculateCoordinates { get; set; }

        public int TagStep { get; set; }
        public double DayIncrement { get; set; }
        public int ReleasedFish { get; set; }
        public double TempDelta { get; set; }
        public string FishTag { get; set; }

        private EtaXiConverter EtaXiConverter { get; set; }

        static readonly object syncObject = new object();

        public void SetDayIncrement(double dayInc)
        {
            DayIncrement = dayInc;
            //144 er incrementet for å hoppe 24 timer/1 dag i merkedage
            //Ganger det med antall dager som skal inkrementeres.
            TagStep = (int) (144 * dayInc);
        }

        public Controller(double dayInc, int releasedFish, double tempDelta, int depthDelta, double Increment, double Probability, int iterations, string fishTag)
        {
            FishTag = fishTag;
            //Console.WriteLine("dayInc: {0}, releasedFish: {1}, tempdelta: {2}, depthDelta: {3}, increment: {4}, Probability: {5}, iterations: {6}", dayInc, releasedFish, tempDelta, depthDelta, Increment, Probability, iterations);
            TempDelta = tempDelta;
            ReleasedFish = releasedFish;
            SetDayIncrement(dayInc);

            File = new ReadFromFile();

            FishList = new Dictionary<string, Fish>();
            KeyList = new List<string>();

            File.readReleaseAndCapture(FishList, KeyList);
            File.readTagData(FishList, KeyList);

            GlobalVariables.Probability = Probability;

            EtaXiConverter = new EtaXiConverter();

            HeatMap = new HeatMap();
            EtaXis = new EtaXi[0];
            TempContainer = new TempContainer(FishList[FishTag].TagDataList, TagStep);
            CalculateCoordinates = new CalculateCoordinates(Increment, depthDelta, dayInc, iterations);
            
        }

        public void SetDepthDelta(int DepthDelta)
        {
            CalculateCoordinates.SetDepthDelta(DepthDelta);
        }

        public void RunAlgorithm()
        {
            int counter = 1;
            int deadFishCounter = 0;
            var watch = Stopwatch.StartNew();
            bool use_Norkyst = true;
            FishList[FishTag].FishRouteList = new BlockingCollection<FishRoute>(boundedCapacity: ReleasedFish);
            Console.WriteLine("Released Fish: {0}", ReleasedFish);
            Console.WriteLine("Tagstep: {0}", TagStep);
            bool fishStillAlive = true;
            for (int i = 0; i < FishList[FishTag].TagDataList.Count && fishStillAlive; i += TagStep)
            {
                TempContainer.UpdateTempArray(FishList[FishTag].TagDataList[i].Date);
                ConsoleUI.DrawTextProgressBar(i / TagStep, FishList[FishTag].TagDataList.Count / TagStep);
                bool chosenPosition;
                if (i == 0)
                {
                    int randInt = 0;
                    PositionData positionData = CalculateXiAndEta.GeneratePositionDataArrayList(HeatMap.NorKystLatArray, HeatMap.NorKystLonArray,
                        HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, FishList[FishTag].ReleaseLat, FishList[FishTag].ReleaseLon, use_Norkyst);
                    BlockingCollection<PositionData> validPositionsDataList =
                        CalculateCoordinates.FindValidPositions(
                            CalculateCoordinates.CalculatePossibleEtaXi(positionData.Eta_rho, positionData.Xi_rho, false, FishList[FishTag].TagDataList[i].Depth, use_Norkyst),
                        HeatMap.NorKystLatArray, HeatMap.NorKystLonArray, HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, FishList[FishTag].TagDataList[i], TempContainer, TempDelta, use_Norkyst
                            );

                    float releaseLat = (float)FishList[FishTag].ReleaseLat;
                    float releaseLon = (float)FishList[FishTag].ReleaseLon;

                    Parallel.For(0, ReleasedFish, (j) =>
                    {
                        
                        chosenPosition = false;
                        bool addedToPosDataList = false;
                        bool addedToFishRoutList = false;

                        if (validPositionsDataList.Count > 0)
                        {
                            FishRoute fishRoute = new FishRoute(FishTag, use_Norkyst);
                            fishRoute.PositionDataList.Add((new PositionData(releaseLat,
                                releaseLon)));

                            RouteChooser routeChooser = new RouteChooser(releaseLat, releaseLon, FishList[FishTag]);

                            while (!chosenPosition)
                            {
                                randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                                chosenPosition = routeChooser.ChosenRoute(validPositionsDataList, randInt);
                            }

                            while (!addedToPosDataList)
                            {
                                addedToPosDataList = fishRoute.PositionDataList.TryAdd((new PositionData(
                                    validPositionsDataList.ElementAt(randInt).Lat, validPositionsDataList.ElementAt(randInt).Lon,
                                    validPositionsDataList.ElementAt(randInt).Depth, validPositionsDataList.ElementAt(randInt).Temp, FishList[FishTag].TagDataList[i].Depth,
                                    FishList[FishTag].TagDataList[i].Temp, validPositionsDataList.ElementAt(randInt).Eta_rho, validPositionsDataList.ElementAt(randInt).Xi_rho)));
                            }

                            while (!addedToFishRoutList)
                            {
                                addedToFishRoutList = FishList[FishTag].FishRouteList.TryAdd(fishRoute);
                            }
                        } else
                        {
                            Interlocked.Increment(ref deadFishCounter);
                        }
                    });
                }
                else
                {
                    TagData tagData = FishList[FishTag].TagDataList[i];
                    if (deadFishCounter < ReleasedFish)
                    {
                        Parallel.ForEach(FishList[FishTag].FishRouteList, (fishRoute) =>
                        {
                            int randInt = 0;
                            chosenPosition = false;
                            EtaXi[] possiblePositionsArray;
                            BlockingCollection<PositionData> validPositionsDataList;
                            if (fishRoute.Alive)
                            {
                                PositionData pData = fishRoute.PositionDataList.ElementAt(counter);

                                lock (syncObject)
                                {
                                    possiblePositionsArray = CalculateCoordinates.CalculatePossibleEtaXi(pData.Eta_rho, pData.Xi_rho, Math.Abs(pData.Depth - tagData.Depth) < 30, tagData.Depth, fishRoute.Use_Norkyst);
                                    validPositionsDataList =
                                        CalculateCoordinates.FindValidPositions(
                                            possiblePositionsArray,
                                            HeatMap.NorKystLatArray, HeatMap.NorKystLonArray, HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, tagData, TempContainer, TempDelta, fishRoute.Use_Norkyst);
                                    
                                    if(validPositionsDataList.Count == 0 && GlobalVariables.allow_switching)
                                    {
                                        fishRoute.Use_Norkyst = !fishRoute.Use_Norkyst;

                                        EtaXi etaXi = EtaXiConverter.ConvertNorkystOrBarents(pData.Eta_rho, pData.Xi_rho, fishRoute.Use_Norkyst);

                                        fishRoute.PositionDataList.ElementAt(counter).Eta_rho = etaXi.Eta_rho;
                                        fishRoute.PositionDataList.ElementAt(counter).Xi_rho = etaXi.Xi_rho;

                                        pData = fishRoute.PositionDataList.ElementAt(counter);

                                        possiblePositionsArray = CalculateCoordinates.CalculatePossibleEtaXi(pData.Eta_rho, pData.Xi_rho, Math.Abs(pData.Depth - tagData.Depth) < 30, tagData.Depth, fishRoute.Use_Norkyst);
                                        validPositionsDataList =
                                            CalculateCoordinates.FindValidPositions(
                                                possiblePositionsArray,
                                                HeatMap.NorKystLatArray, HeatMap.NorKystLonArray, HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, tagData, TempContainer, TempDelta, fishRoute.Use_Norkyst);
                                    }
                                    
                                }

                                if (validPositionsDataList.Count > 0)
                                {
                                    RouteChooser routeChooser =
                                            new RouteChooser(pData.Lat, pData.Lon, FishList[FishTag]);
                                        while (!chosenPosition)
                                        {
                                            randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                                            chosenPosition = routeChooser.ChosenRoute(validPositionsDataList, randInt);
                                        }
                                        fishRoute.PositionDataList.Add((new PositionData(
                                            validPositionsDataList.ElementAt(randInt).Lat,
                                            validPositionsDataList.ElementAt(randInt).Lon,
                                            validPositionsDataList.ElementAt(randInt).Depth,
                                            validPositionsDataList.ElementAt(randInt).Temp,
                                            tagData.Depth, tagData.Temp,
                                            validPositionsDataList.ElementAt(randInt).Eta_rho,
                                            validPositionsDataList.ElementAt(randInt).Xi_rho)));
                                }
                                else
                                {
                                    Interlocked.Increment(ref deadFishCounter);
                                    fishRoute.CommitNotAlive();
                                }
                            }
                        });
                    }
                    else
                    {
                        fishStillAlive = false;
                    }
                    counter++;
                }
            }

            watch.Stop();
            double elapsedMs = watch.ElapsedMilliseconds;
            var count = 1;
            string folderName = "Uakseptabel";
            var FishData = FishList[FishTag];
            double captureLat = FishData.CaptureLat;
            double captureLon = FishData.CaptureLon;

            Console.WriteLine();
            Console.WriteLine("Program runtime: {0} minutes / {1} seconds.", elapsedMs / 60000, elapsedMs / 1000);
            Console.WriteLine("Number of failed routes:      {0}", deadFishCounter);
            Console.WriteLine("Number of successfull routes: {0}", ReleasedFish - deadFishCounter);
            if (deadFishCounter == ReleasedFish)
            {
                Console.WriteLine("All fish are dead");

            }
            else
            {
                if (Directory.Exists(@"C:\NCdata\fishData\" + FishTag))
                {
                    //SLETTER ALLE FILER I FOLDER AKSEPTABEL OG UAKSEPTABEL !!!!!!!!!!!!!!!!!! Lag backup folder om tester hjemme eller HI
                    DirectoryInfo di = new DirectoryInfo(@"C:\NCdata\fishData\" +FishTag+ @"\Akseptabel\");
                    DirectoryInfo di2 = new DirectoryInfo(@"C:\NCdata\fishData\" + FishTag + @"\Uakseptabel\");

                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                    foreach (FileInfo file in di2.GetFiles())
                    {
                        file.Delete();
                    }
                } else
                {
                    Directory.CreateDirectory(@"C:\NCdata\fishData\" + FishTag);
                    Directory.CreateDirectory(@"C:\NCdata\fishData\" + FishTag + @"\Akseptabel");
                    Directory.CreateDirectory(@"C:\NCdata\fishData\" + FishTag + @"\Uakseptabel");

                }
            }

            foreach (var fishRoute in FishList[FishTag].FishRouteList)
            {
                if (fishRoute.Alive)
                {
                    var posData = fishRoute.PositionDataList.ElementAt(fishRoute.PositionDataList.Count - 1);
                    if (CalculateCoordinates.GetDistanceFromLatLonInKm(posData.Lat, posData.Lon, captureLat, captureLon) <
                        ((CalculateCoordinates.Increment * 3.6) * (DayIncrement * 24)))
                    {
                        folderName = "Akseptabel";
                    } else
                    {
                        folderName = "Uakseptabel";
                    }
                    string[] fishData = fishRoute.FromListToString();

                    System.IO.File.WriteAllLines(GlobalVariables.pathToSaveFishData + @"\\" + FishTag + @"\\" + folderName + "\\" + fishRoute.Id + "_" + count + ".txt", fishData);
                    count++;
                }
            }
            
        }
    }
}