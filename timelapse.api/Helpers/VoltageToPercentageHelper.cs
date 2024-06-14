namespace timelapse.api.Helpers
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
                new VoltagePercentage { Voltage = 4.2, Percentage = 100 },
                new VoltagePercentage { Voltage = 4.1, Percentage = 90 },
                new VoltagePercentage { Voltage = 4.0, Percentage = 80 },
                new VoltagePercentage { Voltage = 3.9, Percentage = 70 },
                new VoltagePercentage { Voltage = 3.8, Percentage = 60 },
                new VoltagePercentage { Voltage = 3.7, Percentage = 50 },
                new VoltagePercentage { Voltage = 3.6, Percentage = 40 },
                new VoltagePercentage { Voltage = 3.5, Percentage = 30 },
                new VoltagePercentage { Voltage = 3.4, Percentage = 20 },
                new VoltagePercentage { Voltage = 3.3, Percentage = 10 },
                new VoltagePercentage { Voltage = 3.2, Percentage = 0 }
            }
        };

        public static int VoltageToPercentage(double voltage)
        {
            if (voltage >= 4.2)
            {
                return 100;
            }
            else if (voltage <= 3.2)
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