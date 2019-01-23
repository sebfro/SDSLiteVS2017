﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpagettiMetoden
{
    class ConsoleUI
    {

       public void RunUI()
        {
            bool run = true;

            int tagId = 742;
            double dayInc = 1;
            int releasedFish = 10000;
            double tempDelta = 10;
            int DepthDelta = 30;
            double Increment = 65;
            double Increment2 = 45;
            double probability = 0.5;
            int possiblePaths = 30;

            string answer = "";

            Controller controller;

            controller = new Controller(dayInc, releasedFish, tempDelta, DepthDelta, Increment, probability, possiblePaths, "742");
            controller.RunAlgorithm();
            Console.ReadKey();
            while (run)
            {
                Console.Write("Enter day increment per iterasion: ");
                dayInc = double.Parse(Console.ReadLine());
                Console.WriteLine("The day increment per iterasjon will be {0}.", dayInc);


                Console.Write("Enter how many fish to release:");
                releasedFish = int.Parse(Console.ReadLine());
                Console.WriteLine("{0} fish will be released per iterasjon.", releasedFish);


                /*
                try
                {
                    Console.Write("Enter the tempdelta:");
                    tempDelta = double.Parse(Console.ReadLine());
                    Console.WriteLine("The tempDelta will be {0}.", tempDelta);
                }
                catch (FormatException ex)
                {
                    Console.WriteLine("Use a comma, not a dot! This expcetion was thrown: {0}", ex);
                }
                */

                Console.Write("Enter Increment:");
                Increment = double.Parse(Console.ReadLine());
                Console.WriteLine("Increment will be {0}.", Increment);

                Console.Write("Enter the Increment2:");
                Increment2 = double.Parse(Console.ReadLine());
                Console.WriteLine("Increment2 will be {0}.", Increment2);

                /*
                Console.Write("Enter the depth delta:");
                DepthDelta = int.Parse(Console.ReadLine());
                Console.WriteLine("Depth delta will be {0}.", DepthDelta);
                */


                Console.WriteLine("Loading files...");
            Console.WriteLine("Running algorithm...");
                controller = new Controller(dayInc, releasedFish, 1.2, DepthDelta, Increment, 0.85, 30, "742");
                bool runCurrentConfig = true;
                /*
                if(controller.RunAlgorithmFW())
                {
                    if (controller.RunAlgorithmBW())
                    {
                        //Merge merge = new Merge();
                        Merge.MergeFwAndBwFiles(Increment, dayInc);
                    }
                }
                 */
                //Merge.MergeFwAndBwFiles(Increment, dayInc);
                controller.RunAlgorithm();
                Console.ReadLine();

                while (runCurrentConfig)
                {
                    Console.WriteLine("Do you want to rerun the Algorithm with current configuration? (Y/N)");
                    answer = Console.ReadLine();
                    runCurrentConfig = (answer != "N");

                    if (runCurrentConfig)
                    {
                        Console.Write("Enter how many fish to release:");
                        releasedFish = int.Parse(Console.ReadLine());
                        Console.WriteLine("{0} fish will be released per iterasjon.", releasedFish);

                        /*
                        try
                        {
                            Console.Write("Enter the tempdelta:");
                            tempDelta = double.Parse(Console.ReadLine());
                            Console.WriteLine("The tempDelta will be {0}.", tempDelta);
                        }
                        catch (FormatException ex)
                        {
                            Console.WriteLine("Use a comma, not a dot! Thi expcetion was thrown: {0}", ex);
                        }
                        */

                        Console.Write("Enter Increment:");
                        Increment = double.Parse(Console.ReadLine());
                        Console.WriteLine("Increment will be {0}.", Increment);

                        Console.Write("Enter the Increment2:");
                        Increment2 = double.Parse(Console.ReadLine());
                        Console.WriteLine("Increment2 will be {0}.", Increment2);

                        /*
                        Console.Write("Enter the depth delta:");
                        DepthDelta = int.Parse(Console.ReadLine());
                        Console.WriteLine("Depth delta will be {0}.", DepthDelta);
                        */

                        controller.TempDelta = tempDelta;
                        //controller.ReleasedFish = releasedFish;
                        controller.SetDepthDelta(DepthDelta);

                        controller.RunAlgorithm();
                    }
                }

                Console.WriteLine("Do you want to change the configuration and rerun the Algorithm? (Y/N)");
                answer = Console.ReadLine();
                run = (answer != "N");

            }
        }
    }
}
