namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Production-grade Levenberg-Marquardt optimizer for nonlinear least squares problems.
/// Commonly used for calibrating option pricing models to market data.
///
/// The LM algorithm combines Gauss-Newton and gradient descent methods:
/// - Near the solution: behaves like Gauss-Newton (fast convergence)
/// - Far from solution: behaves like gradient descent (stable)
///
/// References:
/// - Levenberg (1944) "A Method for the Solution of Certain Non-Linear Problems in Least Squares"
/// - Marquardt (1963) "An Algorithm for Least-Squares Estimation of Nonlinear Parameters"
/// </summary>
public sealed class LevenbergMarquardtOptimizer
{
    /// <summary>
    /// Maximum number of iterations.
    /// </summary>
    public int MaxIterations { get; set; } = 200;

    /// <summary>
    /// Tolerance for convergence in parameter space.
    /// </summary>
    public double ParameterTolerance { get; set; } = 1e-8;

    /// <summary>
    /// Tolerance for convergence in objective value.
    /// </summary>
    public double ObjectiveTolerance { get; set; } = 1e-8;

    /// <summary>
    /// Tolerance for gradient norm.
    /// </summary>
    public double GradientTolerance { get; set; } = 1e-8;

    /// <summary>
    /// Initial damping parameter (lambda).
    /// </summary>
    public double InitialDamping { get; set; } = 1e-3;

    /// <summary>
    /// Factor to increase damping when step is rejected.
    /// </summary>
    public double DampingIncreaseFactor { get; set; } = 10.0;

    /// <summary>
    /// Factor to decrease damping when step is accepted.
    /// </summary>
    public double DampingDecreaseFactor { get; set; } = 0.1;

    /// <summary>
    /// Finite difference step for numerical Jacobian.
    /// </summary>
    public double FiniteDifferenceStep { get; set; } = 1e-6;

    /// <summary>
    /// Optimizes a nonlinear least squares problem.
    /// Minimizes sum of squared residuals: min_x sum_i (r_i(x))^2
    /// </summary>
    /// <param name="residuals">Function that computes residuals r(x).</param>
    /// <param name="initialGuess">Initial parameter values.</param>
    /// <param name="lowerBounds">Optional lower bounds for parameters.</param>
    /// <param name="upperBounds">Optional upper bounds for parameters.</param>
    /// <returns>Optimization result.</returns>
    public OptimizationResult Minimize(
        Func<double[], double[]> residuals,
        double[] initialGuess,
        double[]? lowerBounds = null,
        double[]? upperBounds = null)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentNullException.ThrowIfNull(initialGuess);

        int n = initialGuess.Length;
        double[] x = (double[])initialGuess.Clone();

        // Apply bounds if specified
        if (lowerBounds != null || upperBounds != null)
        {
            x = ProjectToBounds(x, lowerBounds, upperBounds);
        }

        double[] r = residuals(x);
        int m = r.Length;

        if (m < n)
        {
            throw new ArgumentException("Number of residuals must be >= number of parameters.");
        }

        double lambda = InitialDamping;
        double objectiveValue = ComputeObjective(r);
        int iterations = 0;

        while (iterations < MaxIterations)
        {
            // Compute Jacobian numerically
            double[][] jacobian = ComputeJacobian(residuals, x, r, lowerBounds, upperBounds);

            // Compute J^T * J (Hessian approximation) and J^T * r (gradient)
            double[][] jTj = MatrixMultiplyTranspose(jacobian);
            double[] jTr = MatrixVectorMultiplyTranspose(jacobian, r);

            // Compute gradient norm for convergence check
            double gradientNorm = VectorNorm(jTr);
            if (gradientNorm < GradientTolerance)
            {
                return CreateResult(x, r, iterations, OptimizationStatus.GradientConvergence);
            }

            // LM step: solve (J^T*J + lambda*diag(J^T*J)) * delta = -J^T*r
            double[][] lhs = new double[n][];
            for (int i = 0; i < n; i++)
            {
                lhs[i] = new double[n];
                for (int j = 0; j < n; j++)
                {
                    lhs[i][j] = jTj[i][j];
                }
                lhs[i][i] += lambda * Math.Max(jTj[i][i], 1e-10); // Regularization
            }

            double[]? delta = SolveLinearSystem(lhs, jTr);
            if (delta == null)
            {
                // Singular matrix - increase damping and retry
                lambda *= DampingIncreaseFactor;
                iterations++;
                continue;
            }

            // Negate for descent direction
            for (int i = 0; i < n; i++)
            {
                delta[i] = -delta[i];
            }

            // Try the step
            double[] xNew = new double[n];
            for (int i = 0; i < n; i++)
            {
                xNew[i] = x[i] + delta[i];
            }

            // Apply bounds
            xNew = ProjectToBounds(xNew, lowerBounds, upperBounds);

            double[] rNew = residuals(xNew);
            double objectiveNew = ComputeObjective(rNew);

            // Check if step improves objective
            if (objectiveNew < objectiveValue)
            {
                // Accept step
                double objectiveChange = Math.Abs(objectiveValue - objectiveNew);
                double parameterChange = VectorNorm(delta);

                x = xNew;
                r = rNew;
                objectiveValue = objectiveNew;
                lambda *= DampingDecreaseFactor;

                // Check convergence
                if (objectiveChange < ObjectiveTolerance * (1 + objectiveValue))
                {
                    return CreateResult(x, r, iterations, OptimizationStatus.ObjectiveConvergence);
                }

                if (parameterChange < ParameterTolerance * (1 + VectorNorm(x)))
                {
                    return CreateResult(x, r, iterations, OptimizationStatus.ParameterConvergence);
                }
            }
            else
            {
                // Reject step - increase damping
                lambda *= DampingIncreaseFactor;
            }

            iterations++;
        }

        return CreateResult(x, r, iterations, OptimizationStatus.MaxIterationsReached);
    }

    /// <summary>
    /// Computes the Jacobian matrix numerically using forward differences.
    /// </summary>
    private double[][] ComputeJacobian(
        Func<double[], double[]> residuals,
        double[] x,
        double[] r0,
        double[]? lowerBounds,
        double[]? upperBounds)
    {
        int m = r0.Length;
        int n = x.Length;
        double[][] jacobian = new double[m][];
        for (int i = 0; i < m; i++)
        {
            jacobian[i] = new double[n];
        }

        double[] xPerturbed = (double[])x.Clone();

        for (int j = 0; j < n; j++)
        {
            double h = FiniteDifferenceStep * Math.Max(Math.Abs(x[j]), 1.0);

            // Check bounds
            double originalValue = x[j];
            xPerturbed[j] = originalValue + h;

            // Respect bounds for perturbation
            if (upperBounds != null && xPerturbed[j] > upperBounds[j])
            {
                xPerturbed[j] = originalValue;
                h = -h;
                xPerturbed[j] = originalValue + h;
            }

            if (lowerBounds != null && xPerturbed[j] < lowerBounds[j])
            {
                xPerturbed[j] = originalValue;
                h = Math.Abs(h);
                xPerturbed[j] = originalValue + h;
            }

            double[] rPerturbed = residuals(xPerturbed);

            for (int i = 0; i < m; i++)
            {
                jacobian[i][j] = (rPerturbed[i] - r0[i]) / h;
            }

            xPerturbed[j] = originalValue;
        }

        return jacobian;
    }

    private static double ComputeObjective(double[] residuals)
    {
        double sum = 0;
        foreach (double r in residuals)
        {
            sum += r * r;
        }
        return 0.5 * sum;
    }

    private static double[][] MatrixMultiplyTranspose(double[][] a)
    {
        int m = a.Length;
        int n = a[0].Length;
        double[][] result = new double[n][];
        for (int i = 0; i < n; i++)
        {
            result[i] = new double[n];
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double sum = 0;
                for (int k = 0; k < m; k++)
                {
                    sum += a[k][i] * a[k][j];
                }
                result[i][j] = sum;
            }
        }

        return result;
    }

    private static double[] MatrixVectorMultiplyTranspose(double[][] a, double[] v)
    {
        int m = a.Length;
        int n = a[0].Length;
        double[] result = new double[n];

        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int j = 0; j < m; j++)
            {
                sum += a[j][i] * v[j];
            }
            result[i] = sum;
        }

        return result;
    }

    private static double VectorNorm(double[] v)
    {
        double sum = 0;
        foreach (double x in v)
        {
            sum += x * x;
        }
        return Math.Sqrt(sum);
    }

    /// <summary>
    /// Solves linear system Ax = b using Gaussian elimination with partial pivoting.
    /// </summary>
    private static double[]? SolveLinearSystem(double[][] a, double[] b)
    {
        int n = b.Length;
        double[][] augmented = new double[n][];
        for (int i = 0; i < n; i++)
        {
            augmented[i] = new double[n + 1];
        }

        // Create augmented matrix
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                augmented[i][j] = a[i][j];
            }
            augmented[i][n] = b[i];
        }

        // Forward elimination with partial pivoting
        for (int k = 0; k < n; k++)
        {
            // Find pivot
            int pivotRow = k;
            double maxPivot = Math.Abs(augmented[k][k]);
            for (int i = k + 1; i < n; i++)
            {
                if (Math.Abs(augmented[i][k]) > maxPivot)
                {
                    maxPivot = Math.Abs(augmented[i][k]);
                    pivotRow = i;
                }
            }

            if (maxPivot < 1e-12)
            {
                return null; // Singular matrix
            }

            // Swap rows
            if (pivotRow != k)
            {
                for (int j = 0; j <= n; j++)
                {
                    (augmented[k][j], augmented[pivotRow][j]) = (augmented[pivotRow][j], augmented[k][j]);
                }
            }

            // Eliminate
            for (int i = k + 1; i < n; i++)
            {
                double factor = augmented[i][k] / augmented[k][k];
                for (int j = k; j <= n; j++)
                {
                    augmented[i][j] -= factor * augmented[k][j];
                }
            }
        }

        // Back substitution
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = augmented[i][n];
            for (int j = i + 1; j < n; j++)
            {
                sum -= augmented[i][j] * x[j];
            }
            x[i] = sum / augmented[i][i];
        }

        return x;
    }

    private static double[] ProjectToBounds(double[] x, double[]? lowerBounds, double[]? upperBounds)
    {
        double[] result = (double[])x.Clone();
        int n = x.Length;

        for (int i = 0; i < n; i++)
        {
            if (lowerBounds != null && result[i] < lowerBounds[i])
            {
                result[i] = lowerBounds[i];
            }
            if (upperBounds != null && result[i] > upperBounds[i])
            {
                result[i] = upperBounds[i];
            }
        }

        return result;
    }

    private static OptimizationResult CreateResult(
        double[] x,
        double[] residuals,
        int iterations,
        OptimizationStatus status)
    {
        double objective = ComputeObjective(residuals);
        return new OptimizationResult
        {
            OptimalParameters = x,
            OptimalValue = objective,
            RMSE = Math.Sqrt(2 * objective / residuals.Length),
            Iterations = iterations,
            Status = status
        };
    }
}

/// <summary>
/// Result of optimization.
/// </summary>
public sealed class OptimizationResult
{
    /// <summary>
    /// Optimal parameter values.
    /// </summary>
    public required IReadOnlyList<double> OptimalParameters { get; init; }

    /// <summary>
    /// Optimal objective value (sum of squared residuals).
    /// </summary>
    public double OptimalValue { get; init; }

    /// <summary>
    /// Root mean squared error.
    /// </summary>
    public double RMSE { get; init; }

    /// <summary>
    /// Number of iterations performed.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Optimization status.
    /// </summary>
    public OptimizationStatus Status { get; init; }

    /// <summary>
    /// Whether optimization converged successfully.
    /// </summary>
    public bool Converged => Status == OptimizationStatus.GradientConvergence ||
                             Status == OptimizationStatus.ObjectiveConvergence ||
                             Status == OptimizationStatus.ParameterConvergence;
}

/// <summary>
/// Status of optimization algorithm.
/// </summary>
public enum OptimizationStatus
{
    /// <summary>
    /// Gradient norm below tolerance.
    /// </summary>
    GradientConvergence,

    /// <summary>
    /// Objective change below tolerance.
    /// </summary>
    ObjectiveConvergence,

    /// <summary>
    /// Parameter change below tolerance.
    /// </summary>
    ParameterConvergence,

    /// <summary>
    /// Maximum iterations reached.
    /// </summary>
    MaxIterationsReached,

    /// <summary>
    /// Optimization failed.
    /// </summary>
    Failed
}
