﻿using Microsoft.Research.Science.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SpagettiMetoden
{
    class CalcDistance_BetweenTwoLonLatCoordinates
    {
        public BlockingCollection<PositionData> PositionDataList { get; set;}
        public ExtractDataFromEtaAndXi ExtractDataFromEtaAndXi { get; set; }

        public double Increment { get; set; }
        public double Increment2 { get; set; }

        public int DayInc { get; set; }

        public object syncObject = new object();

        public double getLatOrLon(int eta, int xi, Array LatOrLonArray)
        {
            return ExtractDataFromEtaAndXi.GetLatOrLon(eta, xi, LatOrLonArray);
        }

        public CalcDistance_BetweenTwoLonLatCoordinates(double inc, int depthDelta, int dayInc)
        {
            DataSet ds = DataSet.Open(GlobalVariables.pathToNcHeatMaps);
            ExtractDataFromEtaAndXi = new ExtractDataFromEtaAndXi(
                ds["h"].GetData(), 
                DataSet.Open(GlobalVariables.pathToNcHeatMapFolder + "NK800_Z.nc")["Z"].GetData(),
                ds["mask_rho"].GetData(),
                depthDelta
                );
            //Increment = (int) (inc * dayInc * 24);
            //Increment2 = (int) (inc2 * dayInc * 24);

            /*
            Increment = (int) ((inc * 0.4 * 3.6)*(dayInc * 24));
            Random rand = new Random();
            double randDouble = rand.NextDouble() * (1 - 0.4) + 0.4;
            Increment2 = (int) ((inc2 * randDouble * 3.6)*(dayInc * 24));
             */

            DayInc = dayInc;

            Increment = inc;

            Console.WriteLine("Increment: {0}", Increment);
        }

        public void SetDepthDelta(int DepthDelta)
        {
            ExtractDataFromEtaAndXi.DepthDelta = DepthDelta;
        }

        //Gir i km
        public static double GetDistanceFromLatLonInKm(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = deg2rad(lat2 - lat1);  // deg2rad below
            var dLon = deg2rad(lon2 - lon1);
            var a =
                    Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(deg2rad(lat1)) * Math.Cos(deg2rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
                ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in km
            return d;
        }

        public static double deg2rad(double deg)
        {
            return deg * (Math.PI / 180);
        }

        

        public EtaXi[] CalculatePossibleEtaXi(int eta, int xi)
        {
            
            int increment = (int)((Increment * ThreadSafeRandom.RandomSpeed() * 3.6) * (DayInc * 24));
            int increment2 = (int)((Increment * ThreadSafeRandom.RandomSpeed() * 3.6) * (DayInc * 24));

            EtaXi[] etaXis = new EtaXi[17] {
                GenerateEtaXi(eta+increment, xi-increment, eta, xi),
                GenerateEtaXi(eta+increment, xi, eta, xi),
                GenerateEtaXi(eta+increment, xi+increment, eta, xi),
                GenerateEtaXi(eta, xi-increment, eta, xi),
                GenerateEtaXi(eta-increment, xi-increment, eta, xi),
                GenerateEtaXi(eta-increment, xi, eta, xi),
                GenerateEtaXi(eta-increment, xi+increment, eta, xi),
                GenerateEtaXi(eta, xi+increment, eta, xi),
                new EtaXi(eta, xi, true),
                GenerateEtaXi(eta+increment2, xi-increment2, eta, xi),
                GenerateEtaXi(eta+increment2, xi, eta, xi),
                GenerateEtaXi(eta+increment2, xi+increment2, eta, xi),
                GenerateEtaXi(eta, xi-increment2, eta, xi),
                GenerateEtaXi(eta-increment2, xi-increment2, eta, xi),
                GenerateEtaXi(eta-increment2, xi, eta, xi),
                GenerateEtaXi(eta-increment2, xi+increment2, eta, xi),
                GenerateEtaXi(eta, xi+increment2, eta, xi)};

            return etaXis.Where(etaXi => etaXi.Valid).ToArray();;

        }

        public BlockingCollection<PositionData> FindValidPositions(EtaXi[] etaXis, Array latDataArray, Array lonDataArray, TagData tagData, CallPython callPython, double tempDelta)
        {
            //CalculateXiAndEta calculateXiAndEta = new CalculateXiAndEta();
            PositionDataList = new BlockingCollection<PositionData>();
            //extractDataFromEtaAndXi = new ExtractDataFromEtaAndXi();
            //PositionData positionData = new PositionData();
            double depth = 0.0;
            double temp = 0.0;
            double lat = 0.0;
            double lon = 0.0;
            DepthData depthData;


            for (int i = 0; i < etaXis.Length; i++)
            {
                lock (syncObject)
                {
                    depth = ExtractDataFromEtaAndXi.GetDepth(etaXis[i].Eta_rho, etaXis[i].Xi_rho);
                    depthData = ExtractDataFromEtaAndXi.GetS_rhoValues(etaXis[i].Eta_rho, etaXis[i].Xi_rho, tagData.depth);
                }
                
                if(depthData.Valid && (depth - (-tagData.depth)) > 0)
                {
                    lock (syncObject)
                    {
                        temp = callPython.GetTemp(depthData.Z_rho, etaXis[i].Eta_rho, etaXis[i].Xi_rho);
                        //callPython.getTempFromNorKyst(day, depthData.z_rho, etaXis[i].eta_rho, etaXis[i].xi_rho);
                    }


                    if (Math.Abs(temp - tagData.temp) < tempDelta)
                    {

                        lock (syncObject)
                        {
                            lat = ExtractDataFromEtaAndXi.GetLatOrLon(etaXis[i].Eta_rho, etaXis[i].Xi_rho, latDataArray);
                            lon = ExtractDataFromEtaAndXi.GetLatOrLon(etaXis[i].Eta_rho, etaXis[i].Xi_rho, lonDataArray);
                        }
                        
                        PositionDataList.Add(new PositionData(lat, lon, depth, temp, tagData.depth, tagData.temp, etaXis[i].Eta_rho, etaXis[i].Xi_rho));
                    }
                }
            }

            return PositionDataList;
        }

        public EtaXi GenerateEtaXi(int eta, int xi, int org_eta, int org_xi)
        {
            bool valid = eta <= GlobalVariables.eta_rho_size && eta >= 0 && xi <= GlobalVariables.xi_rho_size && xi >= 0;
            
            if (valid)
            {
                int etaInc = 0;
                int xiInc = 0;
                if(eta > org_eta)
                {
                    etaInc = 1;
                }
                else if(eta < org_eta)
                {
                    etaInc = -1;
                }
                if (xi > org_xi)
                {
                    xiInc = 1;
                }
                else if(xi < org_xi)
                {
                    xiInc = -1;
                }
                int etaDiff = Math.Abs(eta - org_eta);
                int iterasions = etaDiff > 0 ? etaDiff : Math.Abs(xi - org_xi);
                for(int i = 0; i < iterasions && valid; i++)
                {
                    if(ExtractDataFromEtaAndXi.IsOnLand(org_eta + (etaInc*i), org_xi + (xiInc * i)))
                    {
                        valid = false;
                    }
                }
            }
            return new EtaXi(eta, xi, valid);
            /*
            
            if (valid)
            {
                int etaDiff = org_eta - eta;
                int xiDiff = org_xi - xi;
                lock (syncObject)
                {
                    if (etaDiff > 0 && xiDiff == 0)
                    {
                        for (int i = 1; i < etaDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta + i, xi))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff == 0 && xiDiff > 0)
                    {
                        for (int i = 1; i < xiDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta, xi + i))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff < 0 && xiDiff == 0)
                    {
                        for (int i = 1; i < etaDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta - i, xi))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff == 0 && xiDiff < 0)
                    {
                        for (int i = 1; i < xiDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta, xi - i))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff > 0 && xiDiff > 0)
                    {
                        for (int i = 1; i < etaDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta + i, xi + i))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff < 0 && xiDiff < 0)
                    {
                        for (int i = 1; i < etaDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta - i, xi - i))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff < 0 && xiDiff > 0)
                    {
                        for (int i = 1; i < etaDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta - i, xi + i))
                            {
                                valid = false;
                            }
                        }
                    }
                    else if (etaDiff > 0 && xiDiff < 0)
                    {
                        for (int i = 1; i < etaDiff; i++)
                        {
                            if (ExtractDataFromEtaAndXi.IsOnLand(eta + i, xi - i))
                            {
                                valid = false;
                            }
                        }
                    }
                }
            }
            return new EtaXi(eta, xi, valid);
            */
        }
        
    }

    class EtaXi
    {
        public int Eta_rho { get; set; }
        public int Xi_rho { get; set; }
        public bool Valid { get; set; }

        public EtaXi(int eta, int xi, bool valid)
        {
            
            Eta_rho = eta;
            Xi_rho = xi;
            Valid = valid;
        }
    }
}
