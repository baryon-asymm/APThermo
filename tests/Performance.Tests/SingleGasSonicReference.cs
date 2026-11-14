using APThermo.Data;

namespace APThermo.Performance.Tests;

/// <summary>Independent single-species sonic balance from numerical integrals of the raw NASA Cp fits.</summary>
internal sealed class SingleGasSonicReference(Species species, double chamberTemperature, int panels)
{
    public double Temperature { get; } = FindTemperature(species, chamberTemperature, panels);

    public double PressureRatio => Math.Exp(-Integral(species, Temperature, chamberTemperature, panels, entropy: true));

    public static double CpOverR(Species species, double temperature)
    {
        var interval = species.Intervals.First(fit => fit.TLow <= temperature && temperature <= fit.THigh);
        return interval.Coefficients.Select((coefficient, index) => coefficient * Math.Pow(temperature, interval.Exponents[index])).Sum();
    }

    private static double FindTemperature(Species species, double chamberTemperature, int panels)
    {
        var low = chamberTemperature * 0.5;
        var high = chamberTemperature;
        Assert.True(Balance(species, low, chamberTemperature, panels) > 0.0);
        Assert.True(Balance(species, high, chamberTemperature, panels) < 0.0);
        // Fixed bounded bisection, independent of the production pressure/momentum iteration.
        for (var iteration = 0; iteration < 60; iteration++)
        {
            var middle = 0.5 * (low + high);
            if (Balance(species, middle, chamberTemperature, panels) > 0.0)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return 0.5 * (low + high);
    }

    private static double Balance(Species species, double temperature, double chamberTemperature, int panels)
    {
        var cp = CpOverR(species, temperature);
        // In units of R_specific: 2(h_c-h)/R_specific = gamma(T) T at Mach 1.
        return 2.0 * Integral(species, temperature, chamberTemperature, panels, entropy: false) - cp / (cp - 1.0) * temperature;
    }

    private static double Integral(Species species, double low, double high, int panels, bool entropy)
    {
        var width = (high - low) / panels;
        var sum = 0.0;
        for (var index = 0; index <= panels; index++)
        {
            var temperature = low + index * width;
            var value = CpOverR(species, temperature) / (entropy ? temperature : 1.0);
            var weight = index == 0 || index == panels ? 1 : index % 2 == 0 ? 2 : 4;
            sum += weight * value;
        }

        return sum * width / 3.0;
    }
}
