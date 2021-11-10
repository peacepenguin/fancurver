using System;
using System.Threading;
using LibreHardwareMonitor.Hardware;

//

namespace fancurver
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Launching Fancurver");
            Computer computer = new Computer
            {
                // limit to just the cpu and gpu to be checked:
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = true,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };

            computer.Open();

            //manually set these for now TODO: make a UI to select the cpu/gpu sources.

            //gpu cpu identifiers:
            // AMD Radeon RX 6800 XT, GPU Core, 32, Temperature
            // AMD Radeon RX 6800 XT, GPU Hot Spot, 37, Temperature

            string gpuhardwarename = "AMD Radeon RX 6800 XT";
            string gpusensorname = "GPU Core";

            string cpuhardwarename = "AMD Ryzen 9 5900X";
            string cpusensorname = "Core (Tctl/Tdie)";

            //string cpuhardwarename = "Intel Core i7-4810MQ";
            //string cputempname = "CPU Package";

            // Fan identifiers:
            //ASUS TUF GAMING X570-PLUS, Nuvoton NCT6798D, Fan Control #3, 32.941177, Control //front intake
            //ASUS TUF GAMING X570-PLUS, Nuvoton NCT6798D, Fan Control #4, 32.941177, Control //rear exhaust

            string fanAhardwarename = "ASUS TUF GAMING X570-PLUS";
            string fanBhardwarename = "ASUS TUF GAMING X570-PLUS";
            string fanAsubhardwarename = "Nuvoton NCT6798D";
            string fanBsubhardwarename = "Nuvoton NCT6798D";
            string fanAsensorname = "Fan #3";
            string fanBsensorname = "Fan #4";
            string fanAcontrolname = "Fan Control #3";
            string fanBcontrolname = "Fan Control #4";



            //fan curve:

            // we're using the CPU and GPU as an aggregate input,
            // take the SUM of the temperatures, and adjust the curve to that temp.
            // this makes more sense from an engineering standpoint. The total heat of the system = ~ CPU + GPU temp
            // the high end of the curve could be set to 100c, so either device can trigger the higher curve. 30c+70c=100c == set to 100%
            // or more ideally 

            // initial point on the curve, defines the lowest possible fan speed:
            float curveAtemp1 = 80;
            float curveAspeed1 = 20;

            // light load point
            float curveAtemp2 = 100;
            float curveAspeed2 = 40;

            // medium load point
            float curveAtemp3 = 120;
            float curveAspeed3 = 60;

            // max load point
            float curveAtemp4 = 140;
            float curveAspeed4 = 100;  //final point should always be 100, or the "max" value for the curve.

            // hysterysis value (don't change the fan speed unless the new speed is 5% more or less than the previously set value) // might not use this, little janky, use temp for hysterysis.
            int temphysterysis = 5;

            bool speedchange = false;


            // end config 

            // get the slope of the points:    each point of curve is just an (x, y) coordinate. x is temp, y is speed on our fan curve.
            float slope1to2 = (curveAspeed2 - curveAspeed1) / (curveAtemp2 - curveAtemp1);
            float slope2to3 = (curveAspeed3 - curveAspeed2) / (curveAtemp3 - curveAtemp2);
            float slope3to4 = (curveAspeed4 - curveAspeed3) / (curveAtemp4 - curveAtemp3);

            //calculate the lines 'b' value (from the Direct method/equation to a line)
            float slope1to2bvalue = curveAspeed1 - (slope1to2 * curveAtemp1);
            float slope2to3bvalue = curveAspeed2 - (slope2to3 * curveAtemp2);
            float slope3to4bvalue = curveAspeed3 - (slope3to4 * curveAtemp3);


            // doing bvalue calcs in advance now instead of in the loop for efficiency 
            //float bvalue;


            float gpucurrenttemp = 0;
            float cpucurrenttemp = 0;

            float sumoftemps = 0;
            float previoussumoftemps = 0;
            float sumoftempslastused = 0;


            int speedtoset = 40;  //set an initial value to ensure the fans are set to something besides null or 0 if an exception occurs.


            //set integer i to 0
            int i = 0;

            // run this many times: (or forever)
            while (i < 30)
            {
                Console.WriteLine("...starting data collection phase...");
                //get the current sensor data using a new "UpdateVisitor" object, defined below as a public class.
                computer.Accept(new UpdateVisitor());

                foreach (IHardware hardware in computer.Hardware)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (string.Equals(hardware.Name, gpuhardwarename) || string.Equals(hardware.Name, cpuhardwarename))
                        {
                            if (string.Equals(sensor.Name, gpusensorname))
                            {
                                if (string.Equals(sensor.SensorType.ToString(), "Temperature"))
                                {
                                    //Print the raw item that matches our exact criteria:
                                    Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}", hardware.Name, sensor.Name, sensor.Value, sensor.SensorType, sensor.Index, sensor.Identifier);

                                    //now that we've recorded the previous results, update gpucurrenttemp from the sensor object:
                                    gpucurrenttemp = (int)Math.Round((float)sensor.Value, 0);


                                }
                            }

                            if (string.Equals(sensor.Name, cpusensorname))
                            {
                                if (string.Equals(sensor.SensorType.ToString(), "Temperature"))
                                {
                                    //Print the raw item that matches our exact criteria:
                                    Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}", hardware.Name, sensor.Name, sensor.Value, sensor.SensorType, sensor.Index, sensor.Identifier);

                                    //now that we've recorded the previous results, update cpucurrenttemp from the sensor object:
                                    cpucurrenttemp = (int)Math.Round((float)sensor.Value, 0);

                                    
                                }
                            }

                        }
                    }
                }

                /////////////////
                // this code block needs to be after the last temp sensor collection, but before the fan sensors
                // also there's so many foreach loops to deal with, we only want this to run once, so positioning is critical

                Console.WriteLine("...processing data...");

                // now that we have all the current temps, add them together for the fan curves usage:
                previoussumoftemps = sumoftemps;
                sumoftemps = cpucurrenttemp + gpucurrenttemp;

                Console.WriteLine("gpucurrenttemp:      {0}", gpucurrenttemp);
                Console.WriteLine("cpucurrenttemp:      {0}", cpucurrenttemp);

                Console.WriteLine("sumoftemps =         {0}", sumoftemps);
                Console.WriteLine("previoussumoftemps = {0}", previoussumoftemps);

                //calculate the temp from the slope value:
                // first find what slope to use, what points is the current value between:
                // see if its smaller than the first point, and just set to that points value:
                if (sumoftemps <= curveAtemp1)
                {
                    //convert float to int, with rounding to nearest (normal rounding), 0 decimal points.
                    speedtoset = (int)Math.Round(curveAspeed1, 0);
                }

                else
                {
                    if (sumoftemps < curveAtemp2)
                    {
                        // use the slope for this position to get the speed to set:
                        // take the x, y coord of point 1, and the slope value, to calc the current temps x, y coord
                        // the y coord, speed, is what we need.
                        // direct method to solve line/slope problems:
                        // y = ax + b
                        // a is the slope
                        // x is the known x coord
                        // y is the known y coord
                        // aquire the equation of the line to use:
                        // get the b value of the line first using the known coords and slope: b = y - a * x

                        // bvalue = curveAspeed1 - (slope1to2 * curveAtemp1); moved bvalue calcs to global section, one less math to do each loop.

                        speedtoset = (int)Math.Round((slope1to2 * sumoftemps + slope1to2bvalue), 0);

                    }
                    else
                    {
                        if (sumoftemps < curveAtemp3)
                        {
                            //bvalue = curveAspeed2 - (slope2to3 * curveAtemp2);
                            speedtoset = (int)Math.Round((slope2to3 * sumoftemps + slope2to3bvalue), 0);
                        }
                        else
                        {
                            if (sumoftemps < curveAtemp4)
                            {
                                //bvalue = curveAspeed3 - (slope3to4 * curveAtemp3);
                                speedtoset = (int)Math.Round((slope3to4 * sumoftemps + slope3to4bvalue), 0);
                            }
                            else
                            {
                                //only way to be here is equal to or greater than curveAtemp4 by definition, so just set the value:
                                speedtoset = (int)Math.Round(curveAspeed4, 0);
                            }
                        }

                    }
                }

                // check the hysterysis value to ensure we should actually set a new speed value:
                if ((Math.Abs(sumoftemps - sumoftempslastused)) >= temphysterysis)
                {
                    sumoftempslastused = sumoftemps;
                    speedchange = true;
                    //sensor.Control.SetSoftware(speedtoset); // dont set the sensors yet, still testing.
                    Console.WriteLine("speedchange: {0}", speedchange);
                    Console.WriteLine("sumoftempslastused: {0}", sumoftempslastused);
                }
                else
                {
                    speedchange = false;
                    Console.WriteLine("speedchange: {0}", speedchange);
                    Console.WriteLine("sumoftempslastused: {0}", sumoftempslastused);
                }

                Console.WriteLine("speedtoset = {0}", speedtoset);

                /////////////////

                Console.WriteLine("...starting control phase...");
                foreach (IHardware hardware in computer.Hardware)
                {

                    if (string.Equals(hardware.Name, fanAhardwarename) || string.Equals(hardware.Name, fanBhardwarename))
                    {
                        foreach (IHardware subhardware in hardware.SubHardware)
                        {
                            if (string.Equals(subhardware.Name, fanAsubhardwarename) || string.Equals(subhardware.Name, fanBsubhardwarename))
                            {
                                foreach (ISensor sensor in subhardware.Sensors)
                                {
                                    if (string.Equals(sensor.Name, fanAsensorname))
                                    {
                                        Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}", hardware.Name, subhardware.Name, sensor.Name, sensor.Value, sensor.SensorType, sensor.Index, sensor.Identifier);
                                    }

                                    if (string.Equals(sensor.Name, fanBsensorname))
                                    {
                                        Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}", hardware.Name, subhardware.Name, sensor.Name, sensor.Value, sensor.SensorType, sensor.Index, sensor.Identifier);
                                    }
                                    
                                    if (string.Equals(sensor.Name, fanAcontrolname))
                                    {
                                        Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}", hardware.Name, subhardware.Name, sensor.Name, sensor.Value, sensor.SensorType, sensor.Index, sensor.Identifier);

                                        if (speedchange)
                                        {
                                            sensor.Control.SetSoftware(speedtoset);
                                        }

                                    }

                                    if (string.Equals(sensor.Name, fanBcontrolname))
                                    {
                                        Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}", hardware.Name, subhardware.Name, sensor.Name, sensor.Value, sensor.SensorType, sensor.Index, sensor.Identifier);

                                        if (speedchange)
                                        {
                                            sensor.Control.SetSoftware(speedtoset);
                                        }

                                    }
                                }
                            }
                        }
                    }

                }
                //clear old values fromt he computer object to keep memory usage low:
                //computer.Reset(); // this makes mem and cpu usage worse.. don't do it.

                //increment i variable by 1 (or comment out to run forever)
                i++;

                // sleep a few seconds in between loop runs
                Thread.Sleep(2000);
            }

            // now we're outside the main loop, must have hit the max run point:
            // set controlled fans back to default on exit:
            Console.WriteLine("exiting, setting all controlled fans to bios control mode ie. Default");

            foreach (IHardware hardware in computer.Hardware)
            {

                if (string.Equals(hardware.Name, fanAhardwarename) || string.Equals(hardware.Name, fanBhardwarename))
                {
                    foreach (IHardware subhardware in hardware.SubHardware)
                    {
                        if (string.Equals(subhardware.Name, fanAsubhardwarename) || string.Equals(subhardware.Name, fanBsubhardwarename))
                        {
                            foreach (ISensor sensor in subhardware.Sensors)
                            {

                                if (string.Equals(sensor.Name, fanAcontrolname))
                                {

                                    if (speedchange)
                                    {
                                        sensor.Control.SetDefault();
                                    }

                                }

                                if (string.Equals(sensor.Name, fanBcontrolname))
                                {

                                    if (speedchange)
                                    {
                                        sensor.Control.SetDefault();
                                    }

                                }
                            }
                        }
                    }
                }

            }



            computer.Close();

        }
    }

    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
