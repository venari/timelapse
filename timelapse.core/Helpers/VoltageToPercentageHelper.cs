namespace timelapse.core.Helpers
{
    class VoltagePercentage
    {
        public double Voltage { get; set; }
        public double Percentage { get; set; }
    }

    class VoltagePercentageList
    {
        public List<VoltagePercentage> VoltagePercentages { get; set; }
    }

    public static class VoltageToPercentageHelper
    {
        static VoltagePercentageList voltagePercentageList18650 = new VoltagePercentageList
        {
            VoltagePercentages = new List<VoltagePercentage>
            {
                // new VoltagePercentage { Voltage = 4.2, Percentage = 100 },
                // new VoltagePercentage { Voltage = 4.15, Percentage = 95 },
                // new VoltagePercentage { Voltage = 4.11, Percentage = 90 },
                // new VoltagePercentage { Voltage = 4.08, Percentage = 85 },
                // new VoltagePercentage { Voltage = 4.02, Percentage = 80 },
                // new VoltagePercentage { Voltage = 3.98, Percentage = 75 },
                // new VoltagePercentage { Voltage = 3.95, Percentage = 70 },
                // new VoltagePercentage { Voltage = 3.91, Percentage = 65 },
                // new VoltagePercentage { Voltage = 3.87, Percentage = 60 },
                // new VoltagePercentage { Voltage = 3.85, Percentage = 55 },
                // new VoltagePercentage { Voltage = 3.84, Percentage = 50 },
                // new VoltagePercentage { Voltage = 3.82, Percentage = 45 },
                // new VoltagePercentage { Voltage = 3.80, Percentage = 40 },
                // new VoltagePercentage { Voltage = 3.79, Percentage = 35 },
                // new VoltagePercentage { Voltage = 3.77, Percentage = 30 },
                // new VoltagePercentage { Voltage = 3.75, Percentage = 25 },
                // new VoltagePercentage { Voltage = 3.73, Percentage = 20 },
                // new VoltagePercentage { Voltage = 3.71, Percentage = 15 },
                // new VoltagePercentage { Voltage = 3.69, Percentage = 10 },
                // new VoltagePercentage { Voltage = 3.27, Percentage = 5 },
                // new VoltagePercentage { Voltage = 3.2, Percentage = 0 }

                new VoltagePercentage { Voltage = 4.2, Percentage = 100 },
                new VoltagePercentage { Voltage = 3.88, Percentage = 90 },
                new VoltagePercentage { Voltage = 3.75, Percentage = 80 },
                new VoltagePercentage { Voltage = 3.65, Percentage = 70 },
                new VoltagePercentage { Voltage = 3.535, Percentage = 60 },
                new VoltagePercentage { Voltage = 3.475, Percentage = 50 },
                new VoltagePercentage { Voltage = 3.435, Percentage = 40 },
                new VoltagePercentage { Voltage = 3.385, Percentage = 30 },
                new VoltagePercentage { Voltage = 3.280, Percentage = 20 },
                new VoltagePercentage { Voltage = 3.0, Percentage = 10 },
                new VoltagePercentage { Voltage = 2.8, Percentage = 0 }
            }
        };

        public static int VoltageToPercentage(double voltage)
        {
            if (voltage >= 4.2)
            {
                return 100;
            }
            else if (voltage <= 2.8)
            {
                return 0;
            }
            else
            {
                for (int i = 0; i < voltagePercentageList18650.VoltagePercentages.Count - 1; i++)
                {
                    if (voltage <= voltagePercentageList18650.VoltagePercentages[i].Voltage && voltage > voltagePercentageList18650.VoltagePercentages[i + 1].Voltage)
                    {
                        double percentage = voltagePercentageList18650.VoltagePercentages[i].Percentage + (voltage - voltagePercentageList18650.VoltagePercentages[i].Voltage) / (voltagePercentageList18650.VoltagePercentages[i + 1].Voltage - voltagePercentageList18650.VoltagePercentages[i].Voltage) * (voltagePercentageList18650.VoltagePercentages[i + 1].Percentage - voltagePercentageList18650.VoltagePercentages[i].Percentage);
                        return (int)percentage;
                    }
                }
            }
            return 0;
        }
    }
}