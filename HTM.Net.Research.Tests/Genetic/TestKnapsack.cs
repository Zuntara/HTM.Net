using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Genetic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Genetic
{
    [TestClass]
    public class TestKnapsack
    {
        /// <summary>
        /// This method verifies that the genetic algorithm is working on its own with a known problem.
        /// </summary>
        //[TestMethod]
        public void TestKnapsackProblem()
        {
            KnapsackSolver solver = new KnapsackSolver(100);
            solver.Initialize(100);// Generate 100 random knapsack items
            double bestFitnessSoFar = 0;

            int runs = 0;
            do
            {
                bestFitnessSoFar = solver.Epoch();
                runs++;
                Debug.WriteLine(bestFitnessSoFar);
                if (runs > 1000) break;
            } while (bestFitnessSoFar < 100);

            Assert.IsTrue(runs < 1000);
            Assert.AreEqual(100, bestFitnessSoFar);
        }
    }

    #region Knapsack implementation

    public class KnapsackSolver : GeneticAlgorithmSolver<KnapsackGenome, SackItem>
    {
        public KnapsackSolver(double desiredFitness)
            : base(desiredFitness)
        {
        }

        public override void Initialize(params int[] arguments)
        {
            Init(arguments[0]);
        }

        private void Init(int numItems)
        {
            Started = false;
            Generation = 0;

            // Create the sack items
            ExternalItems = new List<SackItem>();
            do
            {
                SackItem item = new SackItem(GAUtils.RandInt(0, 100), GAUtils.RandInt(0, 10));
                if (!ExternalItems.Any(i => i.Weight == item.Weight))
                {
                    ExternalItems.Add(item);
                }
            } while (ExternalItems.Count < numItems);

            int geneCombinations = numItems;
            int chromosoneLength = geneCombinations * 1; // 1 genes per chromosone (item in or out)

            // initialize the GA
            Algorithm = new GeneticAlgorithm<KnapsackGenome>(
                GeneticAlgorithm<KnapsackGenome>.DefaultCrossoverRate,
                GeneticAlgorithm<KnapsackGenome>.DefaultMutationRate,
                GeneticAlgorithm<KnapsackGenome>.DefaultPopulationSize,
                chromosoneLength,
                geneCombinations);

            // grab the genomes
            GenomePopulation = Algorithm.GrabGenomes();
        }

        // specific methods or props

        public override double Epoch()
        {
            double bestFitnessSoFar = 0;

            // iterate through the population
            for (int p = 0; p < GenomePopulation.Count; ++p)
            {
                KnapsackGenome genome = GenomePopulation[p];

                double fitness = CalculateFitness(genome);

                //assign radius to fitness
                genome.Fitness = fitness;

                //keep a record of the best
                if (fitness > bestFitnessSoFar)
                {
                    bestFitnessSoFar = fitness;

                    BestGenomeIndex = p;
                }

            }//next genome

            if (bestFitnessSoFar >= DesiredFitness)
            {
                ToggleStarted();
                return bestFitnessSoFar;
            }

            //now perform an epoch of the GA. First replace the genomes
            Algorithm.PutGenomes(GenomePopulation);

            //let the GA do its stuff
            Algorithm.Epoch();

            //grab the new genome
            GenomePopulation = Algorithm.GrabGenomes();

            Generation++;

            return bestFitnessSoFar;
        }

        protected override double CalculateFitness(KnapsackGenome genome)
        {
            int maxWeight = 300;
            int maxVolume = 50;

            // decode this genome
            List<int> includedItems;
            Decode(genome, out includedItems);

            int totalWeight = ExternalItems.Where((si, i) => includedItems.Contains(i)).Sum(i => i.Weight);
            int totalVolume = ExternalItems.Where((si, i) => includedItems.Contains(i)).Sum(i => i.Volume);

            // Volume and weight are very important (50% each)
            double fitnessWeight = (50.0 / maxWeight) * totalWeight;
            double fitnessVolume = (50.0 / maxVolume) * totalVolume;

            if (fitnessWeight > 50.0) fitnessWeight = 0;
            if (fitnessVolume > 50.0) fitnessVolume = 0;

            return fitnessWeight + fitnessVolume;
        }

        private void Decode(KnapsackGenome genome, out List<int> includedItems)
        {
            List<int> decodedGenome = genome.Decode();
            includedItems = decodedGenome;
        }
    }

    public class KnapsackGenome : BaseGenome
    {
        public override List<int> Decode()
        {
            List<int> decoded = new List<int>();

            // step through the chromosome a gene at a time (one for weight and one for volume?)
            for (int i = 0; i < Genes.Count; i++)
            {
                // get the gene at this position
                Gene gene = Genes[i];
                //convert to decimal and add to list of decoded
                decoded.AddRange(GetIncludedItems(gene));
            }

            return decoded;
        }

        private IEnumerable<int> GetIncludedItems(Gene gene)
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < gene.Bits.Count; i++)
            {
                if (gene.Bits[i] == 1)
                    indices.Add(i);
            }
            return indices;
        }
    }

    public struct SackItem
    {
        public SackItem(int weight, int volume)
        {
            Weight = weight;
            Volume = volume;
        }

        public int Weight;
        public int Volume;
    }

    #endregion
}