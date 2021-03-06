﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimuleringsApplikasjonen
{
    class ControllerReleaseFwAndBw
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
        public string[] DateTable { get; set; }

        private EtaXiConverter EtaXiConverter { get; set; }

        //All these varaibles are used in the Algorithm functions
        public string FishTag { get; set; }
        private bool Use_Norkyst { get; set; }

        static readonly object syncObject = new object();

        public void SetDayIncrement(double dayInc)
        {
            DayIncrement = dayInc;
            //144 er incrementet for å hoppe 24 timer/1 dag i merkedage
            //Ganger det med antall dager som skal inkrementeres.
            TagStep = (int)(144 * dayInc);
        }

        public ControllerReleaseFwAndBw(double dayInc, int releasedFish, double tempDelta, int depthDelta, double Increment, double Probability, int iterations, string fishTag)
        {
            FishTag = fishTag;
            TempDelta = tempDelta;
            ReleasedFish = releasedFish;
            SetDayIncrement(dayInc);

            File = new ReadFromFile();

            FishList = new Dictionary<string, Fish>();
            KeyList = new List<string>();

            File.ReadReleaseAndCapture(FishList, KeyList);
            File.ReadTagData(FishList, KeyList);

            GlobalVariables.Probability = Probability;

            EtaXiConverter = new EtaXiConverter();

            HeatMap = new HeatMap();
            EtaXis = new EtaXi[0];
            TempContainer = new TempContainer(FishList[FishTag].TagDataList, TagStep);
            CalculateCoordinates = new CalculateCoordinates(Increment, depthDelta, dayInc, iterations);
            DateTable = new string[(FishList[FishTag].TagDataList.Count / TagStep) + 1];
        }

        public void SetDepthDelta(int DepthDelta)
        {
            CalculateCoordinates.SetDepthDelta(DepthDelta);
        }

        public bool RunAlgorithmFW()
        {
            int counter = 1;
            int deadFishCounter = 0;
            Stopwatch Stopwatch = Stopwatch.StartNew();
            int halfTagDataCount = (FishList[FishTag].TagDataList.Count / 2);
            FishList[FishTag].FishRouteList = new BlockingCollection<FishRoute>(boundedCapacity: ReleasedFish);
            Use_Norkyst = true;
            bool fishStillAlive = true;
            
            for (int i = 0; i < halfTagDataCount && fishStillAlive; i += TagStep)
            {
                TempContainer.UpdateTempArray(FishList[FishTag].TagDataList[i].Date);
                DateTable[i / TagStep] = FishList[FishTag].TagDataList[i].Date;
                ConsoleUI.DrawTextProgressBar(i / TagStep, FishList[FishTag].TagDataList.Count / TagStep);
                /*
                int month = short.Parse(FishList[FishTag].TagDataList[i].Date.Substring(4, 2));
                if (month == 5 || month == 11)
                {
                    GlobalVariables.Probability = Math.Abs(GlobalVariables.Probability - 1);
                }
                */
                if (i == 0)
                {
                    deadFishCounter = RunFirstIterationOfAligorithm(deadFishCounter, i, FishList[FishTag].ReleaseLat, FishList[FishTag].ReleaseLon, FishList[FishTag].CaptureLat, FishList[FishTag].CaptureLon);
                }
                else
                {
                    if (deadFishCounter < ReleasedFish)
                    {

                        deadFishCounter = RunAllOtherIterationsOfAlgorithm(deadFishCounter, i, counter, FishList[FishTag].CaptureLat, FishList[FishTag].CaptureLon);
                    }
                    else
                    {
                        fishStillAlive = false;
                    }

                    counter++;
                }
                TempContainer.UpdateTempArray(FishList[FishTag].TagDataList[i].Date);
            }

            Stopwatch.Stop();
            HelperFunctions.DisplayStatisticsOfSimulation(Stopwatch.ElapsedMilliseconds, deadFishCounter, ReleasedFish);
            if (deadFishCounter == ReleasedFish)
            {
                Console.WriteLine("All fish are dead");
            }
            else
            {
                SaveRoutesToFile("FW");
            }
            
            return !(deadFishCounter == ReleasedFish);
        }

        public bool RunAlgorithmBW()
        {
            int counter = 1;
            int deadFishCounter = 0;
            Stopwatch Stopwatch = Stopwatch.StartNew();
            int halfTagDataCount = (FishList[FishTag].TagDataList.Count / 2);
            int tagDataCount = FishList[FishTag].TagDataList.Count -1;
            FishList[FishTag].FishRouteList = new BlockingCollection<FishRoute>(boundedCapacity: ReleasedFish);
            Use_Norkyst = true;
            bool fishStillAlive = true;

            for (int i = tagDataCount; i > halfTagDataCount && fishStillAlive; i -= TagStep)
            {
                TempContainer.UpdateTempArray(FishList[FishTag].TagDataList[i].Date);
                DateTable[i / TagStep] = FishList[FishTag].TagDataList[i].Date;
                ConsoleUI.DrawTextProgressBar(i / TagStep, FishList[FishTag].TagDataList.Count / TagStep);
                /* Dynamic weigthing
                int month = short.Parse(FishList[FishTag].TagDataList[i].Date.Substring(4, 2));
                if (month == 5 || month == 11)
                {
                    GlobalVariables.Probability = Math.Abs(GlobalVariables.Probability - 1);
                    //GlobalVariables.use_Recapture_Weigthing = !GlobalVariables.use_Recapture_Weigthing;
                }
                */
                if (i == tagDataCount)
                {
                    deadFishCounter = RunFirstIterationOfAligorithm(deadFishCounter, i, FishList[FishTag].CaptureLat, FishList[FishTag].CaptureLon, FishList[FishTag].ReleaseLat, FishList[FishTag].ReleaseLon);
                }
                else
                {
                    if (deadFishCounter < ReleasedFish)
                    {
                        deadFishCounter = RunAllOtherIterationsOfAlgorithm(deadFishCounter, i, counter, FishList[FishTag].ReleaseLat, FishList[FishTag].ReleaseLon);
                    }
                    else
                    {
                        fishStillAlive = false;
                    }

                    counter++;
                }
                TempContainer.UpdateTempArray(FishList[FishTag].TagDataList[i].Date);
            }

            Stopwatch.Stop();
            HelperFunctions.DisplayStatisticsOfSimulation(Stopwatch.ElapsedMilliseconds, deadFishCounter, ReleasedFish);
            if (deadFishCounter == ReleasedFish)
            {
                Console.WriteLine("All fish are dead");
            }
            else
            {
                SaveRoutesToFile("BW");
            }
            return !(deadFishCounter == ReleasedFish);
        }
        
        public void SaveRoutesToFile(string folder)
        {
            string path = GlobalVariables.pathToSaveFishData + @"\" + FishTag + @"\" + folder + @"\";
            HelperFunctions.DeleteFolderContent(path);

            int count = 1;

            foreach (var fishRoute in FishList[FishTag].FishRouteList)
            {
                if (fishRoute.Alive)
                {
                    string[] fishData = fishRoute.FromListToString(DateTable);

                    System.IO.File.WriteAllLines(path + fishRoute.Id + "_" + count + ".txt", fishData);
                    count++;
                }
            }
        }

        public int RunFirstIterationOfAligorithm(int deadFishCounter, int indeks, double startLat, double startLon, double goalLat, double goalLon)
        {
            int localDeadFishCounter = deadFishCounter;
            int randInt = 0;
            bool chosenPosition;
            PositionData positionData = CalculateXiAndEta.GeneratePositionDataArrayList(HeatMap.NorKystLatArray, HeatMap.NorKystLonArray,
                HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, startLat, startLon, Use_Norkyst);
            EtaXi[] EtaXis = CalculateCoordinates.CalculatePossibleEtaXi(positionData.Eta_rho, positionData.Xi_rho, false, FishList[FishTag].TagDataList[indeks].Depth, Use_Norkyst);

            BlockingCollection<PositionData> validPositionsDataList =
                CalculateCoordinates.FindValidPositions(EtaXis, HeatMap.NorKystLatArray, HeatMap.NorKystLonArray, HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, FishList[FishTag].TagDataList[indeks], TempContainer, TempDelta, Use_Norkyst);

            float releaseLat = (float)FishList[FishTag].ReleaseLat;
            float releaseLon = (float)FishList[FishTag].ReleaseLon;

            Parallel.For(0, ReleasedFish, (j) =>
            {

                chosenPosition = false;
                bool addedToPosDataList = false;
                bool addedToFishRoutList = false;

                if (validPositionsDataList.Count > 0)
                {
                    FishRoute fishRoute = new FishRoute(FishTag, Use_Norkyst);
                    fishRoute.PositionDataList.Add((new PositionData(startLat, startLon)));

                    RouteChooser routeChooser = new RouteChooser(goalLat, goalLon, startLat, startLon);

                    while (!chosenPosition)
                    {
                        randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                        chosenPosition = routeChooser.ChosenRoute(validPositionsDataList, randInt);
                    }

                    while (!addedToPosDataList)
                    {
                        addedToPosDataList = fishRoute.PositionDataList.TryAdd((new PositionData(
                            validPositionsDataList.ElementAt(randInt).Lat, validPositionsDataList.ElementAt(randInt).Lon,
                            validPositionsDataList.ElementAt(randInt).Depth, validPositionsDataList.ElementAt(randInt).Temp, FishList[FishTag].TagDataList[indeks].Depth,
                            FishList[FishTag].TagDataList[indeks].Temp, validPositionsDataList.ElementAt(randInt).Eta_rho, validPositionsDataList.ElementAt(randInt).Xi_rho)));
                    }

                    while (!addedToFishRoutList)
                    {
                        addedToFishRoutList = FishList[FishTag].FishRouteList.TryAdd(fishRoute);
                    }
                }
                else
                {
                    Interlocked.Increment(ref localDeadFishCounter);
                }
            });
            return localDeadFishCounter;
        }
        public int RunAllOtherIterationsOfAlgorithm(int deadFishCounter, int indeks, int counter, double goalLat, double goalLon)
        {
            TagData tagData = FishList[FishTag].TagDataList[indeks];
            int localDeadFishCounter = deadFishCounter;
            bool chosenPosition;
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
                        possiblePositionsArray = CalculateCoordinates.CalculatePossibleEtaXi(pData.Eta_rho, pData.Xi_rho, Math.Abs(pData.Depth + tagData.Depth) < 30, tagData.Depth, fishRoute.Use_Norkyst);
                        validPositionsDataList =
                            CalculateCoordinates.FindValidPositions(
                                possiblePositionsArray,
                                HeatMap.NorKystLatArray, HeatMap.NorKystLonArray, HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, tagData, TempContainer, TempDelta, fishRoute.Use_Norkyst);

                        //if (((validPositionsDataList.Count == 0) || ((pData.Lat >= 71 && (pData.Lon >= 25) && fishRoute.Use_Norkyst)) || (pData.Lat < 71 && pData.Lon < 25 && !fishRoute.Use_Norkyst)) && GlobalVariables.allow_switching)
                        if (validPositionsDataList.Count == 0 && GlobalVariables.allow_switching)
                        {
                            fishRoute.Use_Norkyst = !fishRoute.Use_Norkyst;

                            EtaXi etaXi = EtaXiConverter.ConvertNorkystOrBarents(pData.Eta_rho, pData.Xi_rho, fishRoute.Use_Norkyst);

                            fishRoute.PositionDataList.ElementAt(counter).Eta_rho = etaXi.Eta_rho;
                            fishRoute.PositionDataList.ElementAt(counter).Xi_rho = etaXi.Xi_rho;

                            pData = fishRoute.PositionDataList.ElementAt(counter);

                            possiblePositionsArray = CalculateCoordinates.CalculatePossibleEtaXi(pData.Eta_rho, pData.Xi_rho, Math.Abs(pData.Depth + tagData.Depth) < 30, tagData.Depth, fishRoute.Use_Norkyst);
                            validPositionsDataList =
                                CalculateCoordinates.FindValidPositions(
                                    possiblePositionsArray,
                                    HeatMap.NorKystLatArray, HeatMap.NorKystLonArray, HeatMap.BarentsSeaLatArray, HeatMap.BarentsSeaLonArray, tagData, TempContainer, TempDelta, fishRoute.Use_Norkyst);
                        }

                    }

                    if (validPositionsDataList.Count > 0)
                    {
                        RouteChooser routeChooser =
                                new RouteChooser(goalLat, goalLon, pData.Lat, pData.Lon);
                        while (!chosenPosition)
                        {
                            if (GlobalVariables.select_random_location)
                            {
                                randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                                chosenPosition = routeChooser.ChosenRoute(validPositionsDataList, randInt);
                            } else
                            {
                                randInt = routeChooser.ChoosePosWithClosestTemp(validPositionsDataList, tagData.Temp);
                                chosenPosition = true;
                            }
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
            return localDeadFishCounter;
        }
        
    }
}