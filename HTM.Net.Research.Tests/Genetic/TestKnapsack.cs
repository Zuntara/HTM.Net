using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Genetic;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Genetic
{
    [TestClass]
    public class TestKnapsack
    {
        /// <summary>
        /// This method verifies that the genetic algorithm is working on its own with a known problem.
        /// </summary>
        [TestMethod]
        public void TestKnapsackProblem()
        {
            KnapsackSolver solver = new KnapsackSolver(100);
            solver.Initialize(25); // Generate 100 random knapsack items
            double bestFitnessSoFar;

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
            var genome = solver.GenomePopulation[solver.BestGenomeIndex];
            var items = genome.Decode();
            Assert.IsNotNull(items);

            foreach (int itemIndex in items)
            {
                var item = solver.ExternalItems[itemIndex];

                Debug.WriteLine($"V: {item.Volume} - W: {item.Weight}");
            }
        }
    }

    #region Knapsack implementation

    public class KnapsackSolver : GeneticAlgorithmSolver<KnapsackGenome, BitGene, SackItem>
    {
        public KnapsackSolver(double desiredFitness)
            : base(desiredFitness)
        {
        }

        public override void Initialize(params object[] arguments)
        {
            Init((int)arguments[0]);
        }

        private void Init(int numItems)
        {
            Started = false;
            Generation = 0;

            // Create the sack items
            ExternalItems = new List<SackItem>();
            do
            {
                SackItem item = new SackItem(GAUtils.RandInt(0, 100), GAUtils.RandInt(1, 10));
                if (!ExternalItems.Any(i => i.Weight == item.Weight))
                {
                    ExternalItems.Add(item);
                }
            } while (ExternalItems.Count < numItems);

            int geneCombinations = numItems;
            int chromosoneLength = geneCombinations * 1; // 1 genes per chromosone (item in or out)

            // initialize the GA
            Algorithm = new GeneticAlgorithm<KnapsackGenome, BitGene>(
                GeneticAlgorithm<KnapsackGenome, BitGene>.DefaultCrossoverRate,
                GeneticAlgorithm<KnapsackGenome, BitGene>.DefaultMutationRate,
                GeneticAlgorithm<KnapsackGenome, BitGene>.DefaultPopulationSize,
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
                if (fitness > bestFitnessSoFar && fitness <= 100)
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
            List<SackItem> includedItems;
            Decode(genome, out includedItems);

            int totalWeight = includedItems.Sum(i => i.Weight);
            int totalVolume = includedItems.Sum(i => i.Volume);

            // Volume and weight are very important (50% each)
            double dRatioWeight = 100.0 / maxWeight;
            double dRatioVolume = 100.0 / maxVolume;

            double fitnessWeight = (dRatioWeight * totalWeight) / 2.0;
            double fitnessVolume = (dRatioVolume * totalVolume) / 2.0;

            if (fitnessWeight > 200.0) fitnessWeight = 0;
            if (fitnessWeight > 50.0) fitnessWeight = 40 + ((8.0 / 150.0) * (50 - fitnessWeight));

            if (fitnessVolume > 200.0) fitnessVolume = 0;
            if (fitnessVolume > 50.0) fitnessVolume = 40 + ((8.0 / 150.0) * (50 - fitnessVolume));

            return fitnessWeight + fitnessVolume;
        }

        private void Decode(KnapsackGenome genome, out List<SackItem> includedItems)
        {
            List<int> decodedGenome = genome.Decode();
            includedItems = decodedGenome.Select(i => ExternalItems[i]).ToList();
        }
    }

    public class KnapsackGenome : BitGenome
    {
        public override List<int> Decode()
        {
            List<int> decoded = new List<int>();

            // step through the chromosome a gene at a time (one for weight and one for volume?)
            for (int i = 0; i < Genes.Count; i++)
            {
                // get the gene at this position
                BitGene gene = Genes[i];
                //convert to decimal and add to list of decoded
                decoded.AddRange(GetIncludedItems(gene));
            }

            return decoded;
        }

        private IEnumerable<int> GetIncludedItems(BitGene gene)
        {
            return ArrayUtils.Where(gene.Bits, i => i == 1);
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