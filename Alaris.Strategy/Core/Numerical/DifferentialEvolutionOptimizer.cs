namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Production-grade Differential Evolution optimizer for global optimization.
/// Particularly effective for non-convex, multi-modal objective functions.
///
/// DE is a population-based stochastic optimization algorithm that:
/// - Doesn't require gradient information
/// - Handles non-smooth, non-convex objectives
/// - Good at escaping local minima
/// - Robust for calibrating jump-diffusion models
///
/// References:
/// Storn and Price (1997) "Differential Evolution - A Simple and Efficient Heuristic
/// for Global Optimization over Continuous Spaces"
/// </summary>
public sealed class DifferentialEvolutionOptimizer
{
    /// <summary>
    /// Population size (typically 10 * dimension). 0 means auto-select.
    /// </summary>
    public int PopulationSize { get; set; }

    /// <summary>
    /// Maximum number of generations.
    /// </summary>
    public int MaxGenerations { get; set; } = 500;

    /// <summary>
    /// Differential weight F ∈ [0, 2], typically 0.8.
    /// Controls the amplification of differential variation.
    /// </summary>
    public double DifferentialWeight { get; set; } = 0.8;

    /// <summary>
    /// Crossover probability CR ∈ [0, 1], typically 0.9.
    /// Controls the fraction of parameter values copied from the mutant.
    /// </summary>
    public double CrossoverProbability { get; set; } = 0.9;

    /// <summary>
    /// Tolerance for convergence (standard deviation of population objective values).
    /// </summary>
    public double Tolerance { get; set; } = 1e-6;

    /// <summary>
    /// Random number generator seed (null for random).
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Minimizes an objective function using Differential Evolution.
    /// </summary>
    /// <param name="objective">Objective function to minimize.</param>
    /// <param name="lowerBounds">Lower bounds for parameters (required).</param>
    /// <param name="upperBounds">Upper bounds for parameters (required).</param>
    /// <returns>Optimization result.</returns>
    public OptimizationResult Minimize(
        Func<double[], double> objective,
        double[] lowerBounds,
        double[] upperBounds)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(lowerBounds);
        ArgumentNullException.ThrowIfNull(upperBounds);

        int dimension = lowerBounds.Length;

        if (upperBounds.Length != dimension)
        {
            throw new ArgumentException("Bounds must have same dimension.");
        }

        // Auto-select population size if not specified
        int popSize = PopulationSize > 0 ? PopulationSize : 10 * dimension;

        // CA5394: Random is acceptable for optimization algorithms (no security context)
#pragma warning disable CA5394
        Random rng = RandomSeed.HasValue ? new Random(RandomSeed.Value) : new Random();
#pragma warning restore CA5394

        // Initialize population randomly
        double[][] population = new double[popSize][];
        double[] fitness = new double[popSize];

        for (int i = 0; i < popSize; i++)
        {
            population[i] = new double[dimension];
            for (int j = 0; j < dimension; j++)
            {
#pragma warning disable CA5394 // Random is acceptable for optimization algorithms (no security context)
                population[i][j] = lowerBounds[j] +
                    (rng.NextDouble() * (upperBounds[j] - lowerBounds[j]));
#pragma warning restore CA5394
            }

            fitness[i] = EvaluateIndividual(population, i, objective);
        }

        // Track best solution
        int bestIndex = FindMinIndex(fitness);
        double[] bestSolution = GetIndividual(population, bestIndex);
        double bestFitness = fitness[bestIndex];

        int generation = 0;
        int stagnationCount = 0;
        const int maxStagnation = 50;

        while (generation < MaxGenerations)
        {
            double[][] newPopulation = new double[popSize][];
            double[] newFitness = new double[popSize];

            for (int i = 0; i < popSize; i++)
            {
                newPopulation[i] = new double[dimension];

                // Select three random distinct individuals (different from i)
                int a = SelectRandomIndex(rng, popSize, i);
                int b = SelectRandomIndex(rng, popSize, i, a);
                int c = SelectRandomIndex(rng, popSize, i, a, b);

                // Create mutant vector: v = x_a + F * (x_b - x_c)
                double[] mutant = new double[dimension];
                for (int j = 0; j < dimension; j++)
                {
                    mutant[j] = population[a][j] +
                        (DifferentialWeight * (population[b][j] - population[c][j]));

                    // Ensure bounds
                    mutant[j] = Math.Clamp(mutant[j], lowerBounds[j], upperBounds[j]);
                }

                // Crossover: create trial vector
                double[] trial = new double[dimension];
#pragma warning disable CA5394 // Random is acceptable for optimization algorithms (no security context)
                int forcedIndex = rng.Next(dimension); // Ensure at least one parameter from mutant
#pragma warning restore CA5394

                for (int j = 0; j < dimension; j++)
                {
#pragma warning disable CA5394 // Random is acceptable for optimization algorithms (no security context)
                    if (rng.NextDouble() < CrossoverProbability || j == forcedIndex)
#pragma warning restore CA5394
                    {
                        trial[j] = mutant[j];
                    }
                    else
                    {
                        trial[j] = population[i][j];
                    }
                }

                // Selection: keep better of trial and current
                double trialFitness = objective(trial);

                if (trialFitness < fitness[i])
                {
                    // Trial is better - accept it
                    newPopulation[i] = trial;
                    newFitness[i] = trialFitness;

                    // Update global best
                    if (trialFitness < bestFitness)
                    {
                        bestSolution = (double[])trial.Clone();
                        bestFitness = trialFitness;
                        stagnationCount = 0;
                    }
                }
                else
                {
                    // Keep current individual
                    newPopulation[i] = (double[])population[i].Clone();
                    newFitness[i] = fitness[i];
                }
            }

            population = newPopulation;
            fitness = newFitness;
            generation++;
            stagnationCount++;

            // Check convergence: population diversity
            double fitnessStd = ComputeStandardDeviation(fitness);
            if (fitnessStd < Tolerance)
            {
                return CreateResult(bestSolution, bestFitness, generation,
                    OptimizationStatus.ObjectiveConvergence);
            }

            // Early stopping if stagnant
            if (stagnationCount > maxStagnation)
            {
                return CreateResult(bestSolution, bestFitness, generation,
                    OptimizationStatus.ParameterConvergence);
            }
        }

        return CreateResult(bestSolution, bestFitness, generation,
            OptimizationStatus.MaxIterationsReached);
    }

    private static double EvaluateIndividual(double[][] population, int index, Func<double[], double> objective)
    {
        return objective(population[index]);
    }

    private static double[] GetIndividual(double[][] population, int index)
    {
        return (double[])population[index].Clone();
    }

    private static int FindMinIndex(double[] values)
    {
        int minIndex = 0;
        double minValue = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < minValue)
            {
                minValue = values[i];
                minIndex = i;
            }
        }
        return minIndex;
    }

    private static int SelectRandomIndex(Random rng, int popSize, params int[] exclude)
    {
        while (true)
        {
#pragma warning disable CA5394 // Random is acceptable for optimization algorithms (no security context)
            int index = rng.Next(popSize);
#pragma warning restore CA5394
            if (!exclude.Contains(index))
            {
                return index;
            }
        }
    }

    private static double ComputeStandardDeviation(double[] values)
    {
        double mean = values.Average();
        double sumSquaredDiff = values.Sum(x => (x - mean) * (x - mean));
        return Math.Sqrt(sumSquaredDiff / values.Length);
    }

    private static OptimizationResult CreateResult(
        double[] x,
        double objectiveValue,
        int iterations,
        OptimizationStatus status)
    {
        return new OptimizationResult
        {
            OptimalParameters = x,
            OptimalValue = objectiveValue,
            RMSE = Math.Sqrt(objectiveValue),
            Iterations = iterations,
            Status = status
        };
    }
}
