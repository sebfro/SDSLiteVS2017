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

namespace SpagettiMetoden
{
    class Program
    {
        static void Main(string[] args)
        {

            int deadFishCounter = 0;
            
            ReadFromFile file = new ReadFromFile();

            Dictionary<string, Fish> FishList = new Dictionary<string, Fish>();
            List<string> KeyList = new List<string>();
            DataSet dsOfZ = DataSet.Open(GlobalVariables.pathToNcHeatMapFolder + "NK800_Z.nc");
            Array Z_Array = dsOfZ["Z"].GetData();

            file.readReleaseAndCapture(FishList, KeyList);
            file.readTagData(FishList, KeyList);
           
            int counter = 1;

            HeatMap heatMap = new HeatMap();
            EtaXi[] etaXis = new EtaXi[0];
            int day = GlobalVariables.day;
            CallPython callPython = new CallPython();

            

            var watch = Stopwatch.StartNew();


            //Har pr�vd � endre i fra 500 til GlobalVariables.tagStep
            for (int i = 0; i < FishList["742"].tagDataList.Count; i+=GlobalVariables.tagStep)
            {
                Console.WriteLine("I iterasjon: " + i / GlobalVariables.tagStep);
                bool chosenPosition;

                if (i == 0)
                {
                    var watch2 = Stopwatch.StartNew();

                    int randInt = 0;
                    PositionData positionData = CalculateXiAndEta.GeneratePositionDataArrayList(heatMap.latArray, heatMap.lonArray, FishList["742"].releaseLat, FishList["742"].releaseLon);
                    BlockingCollection<PositionData> validPositionsDataList =
                        CalcDistance_BetweenTwoLonLatCoordinates.FindValidPositions(CalcDistance_BetweenTwoLonLatCoordinates.calculatePossibleEtaXi(positionData.eta_rho, positionData.xi_rho, heatMap.mask_rhoArray), 
                        heatMap.latArray, heatMap.lonArray, FishList["742"].tagDataList[i], heatMap.depthArray, Z_Array, day, callPython);

                    float releaseLat = (float)FishList["742"].releaseLat;
                    float releaseLon = (float)FishList["742"].releaseLon;

                    Parallel.For(0, GlobalVariables.releasedFish, j =>
                    {
                        chosenPosition = false;

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

                            fishRoute.PositionDataList.Add((new PositionData(
                                    validPositionsDataList.ElementAt(randInt).lat, validPositionsDataList.ElementAt(randInt).lon,
                                    validPositionsDataList.ElementAt(randInt).depth, validPositionsDataList.ElementAt(randInt).temp, FishList["742"].tagDataList[i].depth,
                                    FishList["742"].tagDataList[i].temp, validPositionsDataList.ElementAt(randInt).eta_rho, validPositionsDataList.ElementAt(randInt).xi_rho)));
                            FishList["742"].FishRouteList.Add(fishRoute);
                        }
                    });
                }
                else
                {
                    callPython.updateTempArray(day);
                    BlockingCollection<FishRoute> fishRoutes = FishList["742"].FishRouteList;
                    TagData tagData = FishList["742"].tagDataList[i];
                    if(deadFishCounter < GlobalVariables.releasedFish)
                    {
                        Parallel.ForEach(fishRoutes, (fishRoute) =>
                        {
                            int randInt = 0;
                            chosenPosition = false;

                            if (fishRoute.alive)
                            {
                                PositionData pData = fishRoute.PositionDataList.ElementAt(counter);

                                BlockingCollection<PositionData> validPositionsDataList =
                                    CalcDistance_BetweenTwoLonLatCoordinates.FindValidPositions(CalcDistance_BetweenTwoLonLatCoordinates.calculatePossibleEtaXi(pData.eta_rho,
                                    pData.xi_rho, heatMap.mask_rhoArray), heatMap.latArray, heatMap.lonArray, tagData, heatMap.depthArray, Z_Array, day, callPython);

                                if (validPositionsDataList.Count > 0)
                                {
                                    RouteChooser routeChooser = new RouteChooser(pData.lat, pData.lon, FishList["742"]);
                                    while (!chosenPosition)
                                    {
                                        randInt = ThreadSafeRandom.Next(validPositionsDataList.Count);
                                        chosenPosition = routeChooser.chosenRoute(validPositionsDataList, randInt);
                                    }
                                    fishRoute.PositionDataList.Add((new PositionData(
                                        validPositionsDataList.ElementAt(randInt).lat, validPositionsDataList.ElementAt(randInt).lon,
                                        validPositionsDataList.ElementAt(randInt).depth, validPositionsDataList.ElementAt(randInt).temp,
                                        tagData.depth, tagData.temp,
                                        validPositionsDataList.ElementAt(randInt).eta_rho, validPositionsDataList.ElementAt(randInt).xi_rho)));
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
                    } else
                    {
                        i = FishList["742"].tagDataList.Count;
                    }
                    
                    counter++;
                }
                day += GlobalVariables.dayIncrement;
                

            }

            watch.Stop();
            double elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine("Hvor lang tid tok programmet: " + elapsedMs);
            var count = 1;
            string folderName = "Uakseptabel";
            var FishData = FishList["742"];
            double captureLat = FishData.captureLat;
            double captureLon = FishData.captureLon;
            foreach (var fishRoute in FishList["742"].FishRouteList)
            {
                //Console.WriteLine("Is fish alive?: " + fishRoute.alive);
                if (fishRoute.alive)
                {
                    var posData = fishRoute.PositionDataList.ElementAt(fishRoute.PositionDataList.Count-1);
                    if(CalcDistance_BetweenTwoLonLatCoordinates.getDistanceFromLatLonInKm(posData.lat, posData.lon, captureLat, captureLon) <  GlobalVariables.increment2*4)
                    {
                        folderName = "Akseptabel";
                    }
                    string[] fishData = fishRoute.fromListToString();

                    File.WriteAllLines(GlobalVariables.pathToSaveFishData + @"\\" + folderName + "\\" + fishRoute.id +"_" + count + ".txt", fishData);
                    count++;
                }
            }
            Console.WriteLine("Hvor lang tid tok programmet: " + elapsedMs/60000);
            Console.WriteLine("Dead fish counter: {0}", deadFishCounter);
            Console.WriteLine("Alive fish counter: {0}", GlobalVariables.releasedFish - deadFishCounter);
            if (deadFishCounter == GlobalVariables.releasedFish)
            {
                Console.WriteLine("All fish are dead");
            }
            Console.WriteLine("Day is: {0}", day);
            Console.ReadLine();
        }
    }
}  
   
   