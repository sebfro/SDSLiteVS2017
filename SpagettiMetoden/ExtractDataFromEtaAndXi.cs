﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.Science.Data;

namespace SpagettiMetoden
{
    class ExtractDataFromEtaAndXi
    {

        public double getDepth(int eta_rho, int xi_rho, Array depthArray)
        {
            Console.WriteLine("eta: " + (eta_rho - 1) + ", xi: " + (xi_rho - 1));
            return (double)depthArray.GetValue(eta_rho, xi_rho);
        }
        //Vet ikke helt hvorfor, men double fungerer ikke. Det er en single som blir returnert fra netcdf filen
        //antar det er en single precision og derfor kan kun bli castet til float. En double kan ta mot den senere.
        public float getTemp(int ocean_time, int s_rho, int eta_rho, int xi_rho, Array tempArray)
        {
            return (float)tempArray.GetValue(ocean_time, s_rho, eta_rho, xi_rho);
        }


        public DepthData getS_rhoValues(int eta_rho, int xi_rho, double depth, Array Z_Array)
        {
            double deltaDepth = 20;

            ArrayList potentialDepthArray = new ArrayList();

            for(int k = 0; k < GlobalVariables.Z_rho_size; k++)
            {
                for (int i = 0; i < GlobalVariables.eta_rho_size; i++)
                {
                    for (int j = 0; j < GlobalVariables.xi_rho_size; j++)
                    {
                        if (Math.Abs((double)Z_Array.GetValue(k, i, j) - (-depth)) < deltaDepth)
                        {
                            potentialDepthArray.Add(new DepthData(k, i, j, (double)Z_Array.GetValue(k,i,j)));
                        }
                    }
                }
            }

            //Sammenligne eta_rho og xi_rho fra de potensielle dybdene med den faktiske dybden og velge denn med minst differanse

            double minDelta = 0.0;
            bool deltaHasBeenSet = false;
            DepthData depthData = new DepthData(0, 0, 0, 0.0);

            foreach (DepthData dData in potentialDepthArray)
            {
                if (dData.eta_rho == eta_rho && dData.xi_rho == xi_rho)
                {
                    double newDelta = Math.Abs(dData.depth - (-depth));

                    if (!deltaHasBeenSet)
                    {
                        minDelta = dData.depth - (-depth);
                        depthData = dData;

                        deltaHasBeenSet = true;
                    }
                    
                    if( newDelta < minDelta)
                    {
                        minDelta =  newDelta;
                        depthData = dData;
                    }
                }
            }

            if(!deltaHasBeenSet)
            {
                Console.WriteLine("Did not find matching eta_rho and xi_rho");
            }

            return depthData;
            
        }
        
    }

    class DepthData
    {
        public int xi_rho { get; set; }
        public int eta_rho { get; set; }
        public double depth { get; set; }
        public int z_rho { get; set; }

        public DepthData(int z_rho, int eta_rho, int xi_rho, double depth)
        {
            this.xi_rho = xi_rho;
            this.eta_rho = eta_rho;
            this.depth = depth;
            this.z_rho = z_rho;
        }

    }
}
