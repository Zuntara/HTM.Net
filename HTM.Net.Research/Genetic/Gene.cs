using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;

namespace HTM.Net.Research.Genetic
{
    public static class GAUtils
    {
        private static readonly IRandom Random = new XorshiftRandom(42);

        public static int RandInt(int from, int to)
        {
            to += 1;    // make excl incl boundary
            int max = to - from;
            return Random.NextInt(max) + from;
        }

        public static double RandDouble()
        {
            return Random.NextDouble();
        }
    }

    public class Gene
    {
        public Gene()
        {
            Bits = new List<int>();
        }

        public Gene(List<int> bits)
        {
            Bits = bits;
        }

        public Gene(int numberOfBits)
        {
            Bits = new List<int>();
            // create a random bit string (random genes)
            for (int i = 0; i < numberOfBits; ++i)
            {
                Bits.Add(GAUtils.RandInt(0, 1));
            }
        }

        public static List<TGene> Cross<TGene>(TGene mom, TGene dad, bool invert)
            where TGene : Gene, new()
        {
            List<TGene> result = new List<TGene>();
            TGene baby1 = new TGene();
            TGene baby2 = new TGene();

            if (invert)
            {
                // first dad, then mom
                for (int i = 0; i < mom.Bits.Count; i++)
                {
                    baby1.Bits.Add(dad.Bits[i]);
                    baby2.Bits.Add(mom.Bits[i]);
                }
            }
            else
            {
                // first mom, then dad
                for (int i = 0; i < mom.Bits.Count; i++)
                {
                    baby1.Bits.Add(mom.Bits[i]);
                    baby2.Bits.Add(dad.Bits[i]);
                }
            }
            result.Add(baby1);
            result.Add(baby2);
            return result;
        }

        public List<int> Bits { get; set; }

        public int Length
        {
            get { return Bits.Count; }
        }
    }

    public abstract class BaseGenome
    {
        public const int DefaultGeneLength = 10;
        public const int DefaultChromosoneLength = 3 * DefaultGeneLength;

        protected BaseGenome()
        {
            Genes = new List<Gene>();
            Fitness = 0;
        }

        protected BaseGenome(int numBits, int geneSize)
        {
            Genes = new List<Gene>();

            Fitness = 0;

            int genes = numBits / geneSize;
            // create a random bit string (random genes)
            for (int i = 0; i < genes; ++i)
            {
                Genes.Add(new Gene(geneSize));
            }
        }

        public void Initialize(int numBits, int geneSize)
        {
            Genes = new List<Gene>();

            Fitness = 0;

            int genes = numBits / geneSize;
            // create a random bit string (random genes)
            for (int i = 0; i < genes; ++i)
            {
                Genes.Add(new Gene(geneSize));
            }
        }

        public List<Gene> Genes { get; set; }

        public double Fitness { get; set; }

        //decodes each gene into number
        public virtual List<int> Decode()
        {
            List<int> decoded = new List<int>();

            // step through the chromosome a gene at a time
            for (int i = 0; i < Genes.Count; i++)
            {
                // get the gene at this position
                Gene gene = Genes[i];
                //convert to decimal and add to list of decoded
                decoded.Add(BinaryToInteger(gene.Bits));
            }

            return decoded;
        }

        public int BinaryToInteger(List<int> vec)
        {
            int val = 0;
            int multiplier = 1;

            for (int cBit = vec.Count; cBit > 0; cBit--)
            {
                val += vec[cBit - 1] * multiplier;

                multiplier *= 2;
            }

            return val;
        }
    }

    public class GeneticAlgorithm<TGenome>
        where TGenome : BaseGenome, new()
    {
        public const double DefaultCrossoverRate = 0.8;
        public const double DefaultMutationRate = 0.05;

        public const int DefaultNumElite = 4;
        public const int DefaultNumCopiesElite = 1;

        public const int DefaultPopulationSize = 150;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="crossRat"></param>
        /// <param name="mutRat"></param>
        /// <param name="popSize"></param>
        /// <param name="numBits">chromozone length</param>
        /// <param name="geneLen"></param>
        public GeneticAlgorithm(double crossRat, double mutRat, int popSize, int numBits, int geneLen)
        {
            CrossoverRate = crossRat;
            MutationRate = mutRat;
            ChromosoneLength = numBits;
            TotalFitnessScore = 0;
            Generation = 0;
            GeneLength = geneLen;

            Genomes = new List<TGenome>();
            if (popSize > 0 && numBits > 0 && geneLen > 0)
                CreateStartPopulation(popSize, numBits, geneLen);
        }

        public GeneticAlgorithm(double crossRat, double mutRat, int popSize, Action<TGenome> genomeCreationMethod)
        {
            CrossoverRate = crossRat;
            MutationRate = mutRat;
            TotalFitnessScore = 0;
            Generation = 0;

            Genomes = new List<TGenome>();

            CreateStartPopulation(popSize, genomeCreationMethod);
        }

        /// <summary>
        ///	This is the workhorse of the GA. It first updates the fitness
        ///	scores of the population then creates a new population of
        ///	genomes using the Selection, Croosover and Mutation operators
        ///	we have discussed
        /// </summary>
        public void Epoch()
        {
            //Now to create a new population

            CalculateTotalFitness();

            //create some storage for the baby genomes 
            List<TGenome> babyGenomes = new List<TGenome>();

            //Now to add a little elitism we shall add in some copies of the
            //fittest genomes

            //make sure we add an EVEN number or the roulette wheel
            //sampling will crash
            if (((DefaultNumCopiesElite * DefaultNumElite) % 2) == 0)
            {
                GrabNBest(DefaultNumElite, DefaultNumCopiesElite, babyGenomes);
            }

            while (babyGenomes.Count < Genomes.Count)
            {
                //select 2 parents
                TGenome mum = RouletteWheelSelection();
                TGenome dad = RouletteWheelSelection();

                //operator - crossover
                TGenome baby1 = new TGenome();
                TGenome baby2 = new TGenome();
                List<Gene> baby1Genes = baby1.Genes;
                List<Gene> baby2Genes = baby2.Genes;
                Crossover(mum.Genes, dad.Genes, ref baby1Genes, ref baby2Genes);
                baby1.Genes = baby1Genes;
                baby2.Genes = baby2Genes;

                //operator - mutate
                Mutate(baby1.Genes);
                Mutate(baby2.Genes);

                //add to new population
                babyGenomes.Add(baby1);
                babyGenomes.Add(baby2);
            }

            //copy babies back into starter population
            Genomes = babyGenomes;

            //increment the generation counter
            Generation++;
        }

        //accessor methods
        public List<TGenome> GrabGenomes() { return Genomes; }

        public void PutGenomes(List<TGenome> gen) { Genomes = gen; }

        /// <summary>
        ///	iterates through each genome flipping the bits acording to the
        ///	mutation rate
        /// </summary>
        /// <param name="genes"></param>
        private void Mutate(List<Gene> genes)
        {
            // Go through each gene
            for (int i = 0; i < genes.Count; i++)
            {
                Gene gene = genes[i];
                for (int j = 0; j < gene.Bits.Count; j++)
                {
                    //do we flip this bit?
                    if (GAUtils.RandDouble() < MutationRate)
                    {
                        //flip the bit
                        gene.Bits[j] = gene.Bits[j] == 0 ? 1 : 0;
                    }
                }
            }
        }

        /// <summary>
        ///	Takes 2 parent gene vectors, selects a midpoint and then swaps the ends
        ///	of each genome creating 2 new genomes which are stored in baby1 and
        ///	baby2.
        /// </summary>
        /// <param name="mum"></param>
        /// <param name="dad"></param>
        /// <param name="baby1"></param>
        /// <param name="baby2"></param>
        private void Crossover(List<Gene> mum, List<Gene> dad, ref List<Gene> baby1, ref List<Gene> baby2)
        {
            //just return parents as offspring dependent on the rate
            //or if parents are the same
            if ((GAUtils.RandDouble() > CrossoverRate) || (mum == dad))
            {
                baby1 = mum;
                baby2 = dad;
                return;
            }

            //determine a crossover point
            int crossOverPoint = GAUtils.RandInt(0, mum.Count - 1); // = 29

            for (int i = 0; i < mum.Count; i++)
            {
                var crossed = Gene.Cross(mum[i], dad[i], i < crossOverPoint);
                baby1.Add(crossed[0]);
                baby2.Add(crossed[1]);
            }

            // swap the bits
            // for (int i = 0; i < crossOverPoint; ++i)
            // {
            //     baby1.Add(mum[i]);
            //     baby2.Add(dad[i]);
            // }

            // for (int i = crossOverPoint; i < mum.Count; ++i)
            // {
            //     baby1.Add(dad[i]);
            //     baby2.Add(mum[i]);
            // }
        }

        /// <summary>
        ///	selects a member of the population by using roulette wheel 
        ///	selection as described in the text.
        /// </summary>
        /// <returns></returns>
        private TGenome RouletteWheelSelection()
        {
            double fSlice = GAUtils.RandDouble() * TotalFitnessScore;

            double cfTotal = 0.0;

            int selectedGenome = 0;

            for (int i = 0; i < Genomes.Count; ++i)
            {
                cfTotal += Genomes[i].Fitness;

                if (cfTotal > fSlice)
                {
                    selectedGenome = i;
                    break;
                }
            }

            return Genomes[selectedGenome];
        }

        private void GrabNBest(int nBest, int numCopies, List<TGenome> newPopulation)
        {
            //sort(m_vecGenomes.begin(), m_vecGenomes.end());
            Genomes = Genomes.OrderByDescending(g => g.Fitness).ToList();

            //now add the required amount of copies of the n most fittest 
            //to the supplied vector
            while (nBest-- > 0)
            {
                for (int i = 0; i < numCopies; ++i)
                {
                    newPopulation.Add(Genomes[nBest]);
                }
            }
        }

        private void CreateStartPopulation(int genomes, int numChromosoneBits, int numGeneBits)
        {
            //clear existing population
            Genomes.Clear();

            for (int i = 0; i < genomes; i++)
            {
                var genome = new TGenome();
                genome.Initialize(numChromosoneBits, numGeneBits);
                Genomes.Add(genome);
            }

            //reset all variables
            Generation = 0;
            TotalFitnessScore = 0;
        }

        private void CreateStartPopulation(int genomes, Action<TGenome> genomeCreationMethod)
        {
            //clear existing population
            Genomes.Clear();

            for (int i = 0; i < genomes; i++)
            {
                var genome = new TGenome();
                genomeCreationMethod(genome);
                Genomes.Add(genome);
            }

            //reset all variables
            Generation = 0;
            TotalFitnessScore = 0;

            GeneLength = -1;
            ChromosoneLength = Genomes.First().Genes.Sum(g => g.Length);
        }

        private void CalculateTotalFitness()
        {
            TotalFitnessScore = Genomes.Sum(g => g.Fitness);
        }

        //our population of genomes
        private List<TGenome> Genomes { get; set; }

        private double CrossoverRate { get; set; }

        private double MutationRate { get; set; }

        //how many bits per chromosome
        private int ChromosoneLength { get; set; }

        //how many bits per gene
        private int GeneLength { get; set; }

        private double TotalFitnessScore { get; set; }

        private int Generation { get; set; }
    }

    public abstract class GeneticAlgorithmSolver<TGenome, TModel>
        where TGenome : BaseGenome, new()
    {
        protected GeneticAlgorithmSolver(double desiredFitness)
        {
            Algorithm = null;
            GenomePopulation = new List<TGenome>();
            DesiredFitness = desiredFitness;
        }

        ~GeneticAlgorithmSolver()
        {
            Algorithm = null;
        }

        public double DesiredFitness { get; protected set; }

        protected GeneticAlgorithm<TGenome> Algorithm { get; set; }

        //local copy of the population of genomes
        public List<TGenome> GenomePopulation { get; protected set; }

        //index into the fittest genome in the population
        public int BestGenomeIndex { get; protected set; }
        public int Generation { get; protected set; }
        protected bool Started { get; set; }

        public List<TModel> ExternalItems { get; protected set; }

        public abstract void Initialize(params int[] arguments);

        public abstract double Epoch();

        protected abstract double CalculateFitness(TGenome genome);

        public void ToggleStarted()
        {
            Started = !Started;
        }

        public bool IsStarted()
        {
            return Started;
        }
    }


}