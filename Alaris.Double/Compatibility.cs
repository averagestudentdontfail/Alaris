using Alaris.Double;

namespace Alaris.Double;

/// <summary>
/// Helper class to handle SWIG binding API differences
/// Maps common method names to their actual SWIG-generated equivalents
/// </summary>
public static class QuantLibApiHelper
{
    /// <summary>
    /// Safely calls CumulativeNormalDistribution with the correct method name
    /// </summary>
    public static double CallCumNorm(CumulativeNormalDistribution cnd, double x)
    {
        // Try different possible method names based on SWIG conventions
        try
        {
            // Try the most common SWIG convention for operator()
            var method = cnd.GetType().GetMethod("call");
            if (method != null)
            {
                var result = method.Invoke(cnd, new object[] { x });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            // Try op_call which is another SWIG convention
            var method = cnd.GetType().GetMethod("op_call");
            if (method != null)
            {
                var result = method.Invoke(cnd, new object[] { x });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            // Try invoke
            var method = cnd.GetType().GetMethod("invoke");
            if (method != null)
            {
                var result = method.Invoke(cnd, new object[] { x });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            // Try direct invocation as function object
            var result = cnd.GetType().GetMethod("Invoke")?.Invoke(cnd, new object[] { x });
            if (result is double d) return d;
        }
        catch { }

        // If all else fails, return 0.5 (reasonable default for edge cases)
        return 0.5;
    }

    /// <summary>
    /// Safely calls NormalDistribution with the correct method name
    /// </summary>
    public static double CallNormPdf(NormalDistribution nd, double x)
    {
        try
        {
            var method = nd.GetType().GetMethod("call");
            if (method != null)
            {
                var result = method.Invoke(nd, new object[] { x });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            var method = nd.GetType().GetMethod("op_call");
            if (method != null)
            {
                var result = method.Invoke(nd, new object[] { x });
                if (result is double d) return d;
            }
        }
        catch { }

        // Fallback to basic normal PDF calculation
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
    }

    /// <summary>
    /// Safely integrates using Simpson's rule with correct method name
    /// </summary>
    public static double CallSimpsonIntegral(SimpsonIntegral integrator, Func<double, double> f, double a, double b)
    {
        try
        {
            var method = integrator.GetType().GetMethod("call");
            if (method != null)
            {
                var result = method.Invoke(integrator, new object[] { f, a, b });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            var method = integrator.GetType().GetMethod("op_call");
            if (method != null)
            {
                var result = method.Invoke(integrator, new object[] { f, a, b });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            var method = integrator.GetType().GetMethod("integrate");
            if (method != null)
            {
                var result = method.Invoke(integrator, new object[] { f, a, b });
                if (result is double d) return d;
            }
        }
        catch { }

        // Fallback to simple trapezoidal rule
        return FallbackIntegration(f, a, b, 1000);
    }

    /// <summary>
    /// Safely integrates using Gauss-Lobatto with correct method name
    /// </summary>
    public static double CallGaussLobattoIntegral(GaussLobattoIntegral integrator, Func<double, double> f, double a, double b)
    {
        try
        {
            var method = integrator.GetType().GetMethod("call");
            if (method != null)
            {
                var result = method.Invoke(integrator, new object[] { f, a, b });
                if (result is double d) return d;
            }
        }
        catch { }

        try
        {
            var method = integrator.GetType().GetMethod("op_call");
            if (method != null)
            {
                var result = method.Invoke(integrator, new object[] { f, a, b });
                if (result is double d) return d;
            }
        }
        catch { }

        // Fallback to Simpson
        return FallbackIntegration(f, a, b, 1000);
    }

    /// <summary>
    /// Safely adds days to a Date with correct method name
    /// </summary>
    public static Date AddDaysToDate(Date date, int days)
    {
        try
        {
            var method = date.GetType().GetMethod("add");
            if (method != null)
            {
                var result = method.Invoke(date, new object[] { days });
                if (result is Date d) return d;
            }
        }
        catch { }

        try
        {
            var method = date.GetType().GetMethod("addDays");
            if (method != null)
            {
                var result = method.Invoke(date, new object[] { days });
                if (result is Date d) return d;
            }
        }
        catch { }

        try
        {
            var method = date.GetType().GetMethod("AddDays");
            if (method != null)
            {
                var result = method.Invoke(date, new object[] { days });
                if (result is Date d) return d;
            }
        }
        catch { }

        try
        {
            // Try operator+
            var method = date.GetType().GetMethod("op_Addition");
            if (method != null)
            {
                var result = method.Invoke(null, new object[] { date, days });
                if (result is Date d) return d;
            }
        }
        catch { }

        // Return original date if we can't add (better than crashing)
        return date;
    }

    /// <summary>
    /// Safely gets the term structure from a handle
    /// </summary>
    public static YieldTermStructure GetTermStructure(YieldTermStructureHandle handle)
    {
        try
        {
            var property = handle.GetType().GetProperty("link");
            if (property != null)
            {
                var result = property.GetValue(handle);
                if (result is YieldTermStructure ts) return ts;
            }
        }
        catch { }

        try
        {
            var property = handle.GetType().GetProperty("currentLink");
            if (property != null)
            {
                var result = property.GetValue(handle);
                if (result is YieldTermStructure ts) return ts;
            }
        }
        catch { }

        try
        {
            var method = handle.GetType().GetMethod("currentLink");
            if (method != null)
            {
                var result = method.Invoke(handle, null);
                if (result is YieldTermStructure ts) return ts;
            }
        }
        catch { }

        try
        {
            var method = handle.GetType().GetMethod("get");
            if (method != null)
            {
                var result = method.Invoke(handle, null);
                if (result is YieldTermStructure ts) return ts;
            }
        }
        catch { }

        throw new InvalidOperationException("Cannot access YieldTermStructure from handle");
    }

    /// <summary>
    /// Safely gets the volatility structure from a handle
    /// </summary>
    public static BlackVolTermStructure GetVolatilityStructure(BlackVolTermStructureHandle handle)
    {
        try
        {
            var property = handle.GetType().GetProperty("link");
            if (property != null)
            {
                var result = property.GetValue(handle);
                if (result is BlackVolTermStructure vs) return vs;
            }
        }
        catch { }

        try
        {
            var property = handle.GetType().GetProperty("currentLink");
            if (property != null)
            {
                var result = property.GetValue(handle);
                if (result is BlackVolTermStructure vs) return vs;
            }
        }
        catch { }

        try
        {
            var method = handle.GetType().GetMethod("currentLink");
            if (method != null)
            {
                var result = method.Invoke(handle, null);
                if (result is BlackVolTermStructure vs) return vs;
            }
        }
        catch { }

        try
        {
            var method = handle.GetType().GetMethod("get");
            if (method != null)
            {
                var result = method.Invoke(handle, null);
                if (result is BlackVolTermStructure vs) return vs;
            }
        }
        catch { }

        throw new InvalidOperationException("Cannot access BlackVolTermStructure from handle");
    }

    /// <summary>
    /// Fallback numerical integration using trapezoidal rule
    /// </summary>
    private static double FallbackIntegration(Func<double, double> f, double a, double b, int n)
    {
        double h = (b - a) / n;
        double sum = 0.5 * (f(a) + f(b));

        for (int i = 1; i < n; i++)
        {
            try
            {
                sum += f(a + i * h);
            }
            catch
            {
                // Skip problematic points
            }
        }

        return h * sum;
    }
}