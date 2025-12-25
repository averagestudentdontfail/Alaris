// STPR004A.cs - levenberg-Marquardt optimizer implementation using MathNet.Numerics.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Levenberg-Marquardt optimizer implementation using MathNet.Numerics.
/// </summary>
public sealed class STPR004A
{
    private const int DefaultMaxIterations = 1000;
    private const double DefaultTolerance = 1e-8;

    /// <summary>
    /// Maximum number of iterations.
    /// </summary>
    public int MaxIterations { get; init; } = DefaultMaxIterations;

    /// <summary>
    /// Convergence tolerance.
    /// </summary>
    public double Tolerance { get; init; } = DefaultTolerance;

    /// <summary>
    /// Parameter convergence tolerance.
    /// </summary>
    public double ParameterTolerance { get; init; } = DefaultTolerance;

    /// <summary>
    /// Objective function convergence tolerance.
    /// </summary>
    public double ObjectiveTolerance { get; init; } = DefaultTolerance;

    /// <summary>
    /// Minimizes the objective function (sum of squared residuals) using MathNet.Numerics LevenbergMarquardtMinimizer.
    /// </summary>
    public OptimizationResult Minimize(
        Func<double[], double[]> residuals,
        double[] initialGuess,
        double[]? lowerBounds = null,
        double[]? upperBounds = null)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentNullException.ThrowIfNull(initialGuess);

        // Use the strictest tolerance provided
        double effectiveTolerance = Math.Min(Tolerance, Math.Min(ParameterTolerance, ObjectiveTolerance));

        IObjectiveModel obj = new LeastSquaresObjective(residuals);

        LevenbergMarquardtMinimizer solver = new LevenbergMarquardtMinimizer(
            gradientTolerance: effectiveTolerance,
            stepTolerance: effectiveTolerance,
            functionTolerance: effectiveTolerance,
            maximumIterations: MaxIterations);
            
        NonlinearMinimizationResult result = solver.FindMinimum(obj, Vector<double>.Build.DenseOfArray(initialGuess));

        return CreateResult(result, residuals);
    }

    private OptimizationResult CreateResult(
        NonlinearMinimizationResult result, 
        Func<double[], double[]> residuals)
    {
        double[] finalParams = result.MinimizingPoint.ToArray();
        double[] finalResiduals = residuals(finalParams);
        double sumSq = finalResiduals.Sum(r => r * r);
        
        return new OptimizationResult
        {
            OptimalParameters = finalParams,
            OptimalValue = 0.5 * sumSq,
            RMSE = Math.Sqrt(sumSq / finalResiduals.Length),
            Iterations = result.Iterations,
            Status = MapStatus(result.ReasonForExit)
        };
    }

    private static OptimizationStatus MapStatus(MathNet.Numerics.Optimization.ExitCondition reason)
    {
        return reason switch
        {
            ExitCondition.Converged => OptimizationStatus.ParameterConvergence,
            ExitCondition.ExceedIterations => OptimizationStatus.MaxIterationsReached,
            _ => OptimizationStatus.Failed
        };
    }

    private class LeastSquaresObjective : IObjectiveModel
    {
        private readonly Func<double[], double[]> _residuals;
        private Vector<double> _point;
        private Vector<double> _residualsVector = null!;
        private Matrix<double> _jacobian = null!;
        private bool _evaluated;

        public LeastSquaresObjective(Func<double[], double[]> residuals)
        {
            _residuals = residuals;
            _point = Vector<double>.Build.Dense(0); // Dummy
        }

        public void EvaluateAt(Vector<double> point)
        {
            _point = point;
            double[] r = _residuals(point.ToArray());
            _residualsVector = Vector<double>.Build.DenseOfArray(r);
            
            // Custom Numerical Jacobian (Central Difference)
            _jacobian = ComputeJacobian(point);
                
            _evaluated = true;
            FunctionEvaluations++;
            JacobianEvaluations++;
        }

        private Matrix<double> ComputeJacobian(Vector<double> point)
        {
            int n = point.Count;
            int m = _residualsVector.Count;
            Matrix<double> J = Matrix<double>.Build.Dense(m, n);
            double h = 1e-8;

            // We need to mutate point, so copy it or restore it.
            // Vector<double> is mutable.
            
            for (int j = 0; j < n; j++)
            {
                double original = point[j];
                
                point[j] = original + h;
                Vector<double> rPlus = Vector<double>.Build.DenseOfArray(_residuals(point.ToArray()));
                
                point[j] = original - h;
                Vector<double> rMinus = Vector<double>.Build.DenseOfArray(_residuals(point.ToArray()));
                
                point[j] = original; // Restore

                Vector<double> col = (rPlus - rMinus) / (2 * h);
                J.SetColumn(j, col);
            }
            return J;
        }

        public double Value 
        {
            get 
            {
                if (!_evaluated)
                {
                    throw new InvalidOperationException("EvaluateAt must be called first.");
                }
                return 0.5 * _residualsVector.DotProduct(_residualsVector);
            }
        }

        public Vector<double> Gradient
        {
            get
            {
                if (!_evaluated)
                {
                    throw new InvalidOperationException("EvaluateAt must be called first.");
                }
                return _jacobian.TransposeThisAndMultiply(_residualsVector);
            }
        }

        public Matrix<double> Hessian
        {
            get
            {
                if (!_evaluated)
                {
                    throw new InvalidOperationException("EvaluateAt must be called first.");
                }
                return _jacobian.TransposeThisAndMultiply(_jacobian);
            }
        }

        public bool IsGradientSupported => true;
        public bool IsHessianSupported => true;
        
        public int DegreeOfFreedom => Math.Max(0, (_residualsVector?.Count ?? 0) - (_point?.Count ?? 0));
        
        public int FunctionEvaluations { get; set; }
        public int JacobianEvaluations { get; set; }
        public int GradientEvaluations { get => JacobianEvaluations; set => JacobianEvaluations = value; }

        public Vector<double> Point => _point;
        
        public Vector<double> ObservedY => Vector<double>.Build.Dense(_residualsVector?.Count ?? 0);
        public Vector<double> ModelValues => _residualsVector;
        public Matrix<double> Weights => null!;

        public void SetParameters(Vector<double> parameters, List<bool> isFixed) { }
        public IObjectiveModel Fork() => new LeastSquaresObjective(_residuals);
        public IObjectiveModel CreateNew() => new LeastSquaresObjective(_residuals);
        public IObjectiveFunction ToObjectiveFunction() 
        {
             return ObjectiveFunction.Gradient(
                 (x) => { EvaluateAt(x); return Value; },
                 (x) => { EvaluateAt(x); return Gradient; }
             );
        }
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
