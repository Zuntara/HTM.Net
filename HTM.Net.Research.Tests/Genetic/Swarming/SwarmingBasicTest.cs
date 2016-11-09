using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HTM.Net.Research.Genetic;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Genetic.Swarming
{
    [TestClass]
    public class SwarmingBasicTest
    {
        // A genome has genes and a chromosone has genomes.
        //      chromosone -> genomes -> genes
        // mutation occurs between genomes in a chromosone
        // crossover occurs between genomes in a chromosone
        // a genome is a possible solution in a collection of possible solutions within the chromosone

        // a gene has a list of bits that include or exclude certain items from the collection to scan
        // the genome has a fitness indicator to tell the algo that we are on the right track or not

        // we need an indication that when a bit has been changed the result went worse or better (fitness)
        // we will introduce a 'GenePart' that will keep track of it's value in relation with the fitness function

        //[TestMethod]
        //public void TestBuildOfSwarmingDefinition_KnapSack()
        //{
        //    SwarmEngine engine = SwarmEngine.GetBuilder<KnapSack, KnapSackItem>()
        //        .ForModel(() => new KnapSack(50, 100))                      // Define genome
        //        .WithCombinationOf(sack => sack.Items)                      // Define gene items
        //        .FitnessFunction(sack => sack.GetPercentualFitness())
        //        .WithInitialCollection(BuildKnapSackItems)
        //        .MaximumGenerations(20)
        //        .Build();

        //    engine.Run();
        //}

        //[TestMethod]
        //public void TestSwarm()
        //{
        //    ParticleProgram.Test();
        //}

        private IList<KnapSackItem> BuildKnapSackItems()
        {
            return new List<KnapSackItem>
            {
                new KnapSackItem(10, 20),
                new KnapSackItem(20, 50),
                new KnapSackItem(30, 20),
                new KnapSackItem(40, 10),
                new KnapSackItem(5, 10),
                new KnapSackItem(3, 20),
                new KnapSackItem(2, 50),
                new KnapSackItem(21, 25),
                new KnapSackItem(29, 15),
                new KnapSackItem(32, 25),
                new KnapSackItem(52, 5),
            };
        }
    }



    #region SwarmTest

    class ParticleProgram
    {
        public static void Test()
        {
            Console.WriteLine("\nBegin Particle Swarm Optimization demo\n");
            Console.WriteLine("Goal is to minimize f(x0,x1) = x0 * exp( -(x0^2 + x1^2) )");
            Console.WriteLine("Known solution is at x0 = -0.707107, x1 = 0.000000");

            int dim = 2; // problem dimensions
            int numParticles = 5;
            int maxEpochs = 1000;
            double exitError = 0.0; // exit early if reach this error
            double minX = -10.0; // problem-dependent
            double maxX = 10.0;

            Console.WriteLine("\nSetting problem dimension to " + dim);
            Console.WriteLine("Setting numParticles = " + numParticles);
            Console.WriteLine("Setting maxEpochs = " + maxEpochs);
            Console.WriteLine("Setting early exit error = " + exitError.ToString("F4"));
            Console.WriteLine("Setting minX, maxX = " + minX.ToString("F1") + " " + maxX.ToString("F1"));
            Console.WriteLine("\nStarting PSO");

            double[] bestPosition = Solve(dim, numParticles, minX, maxX, maxEpochs, exitError);
            double bestError = Error(bestPosition);

            Console.WriteLine("Best position/solution found:");
            for (int i = 0; i < bestPosition.Length; ++i)
            {
                Console.Write("x" + i + " = ");
                Console.WriteLine(bestPosition[i].ToString("F6") + " ");
            }
            Console.WriteLine("");
            Console.Write("Final best error = ");
            Console.WriteLine(bestError.ToString("F5"));

            Console.WriteLine("\nEnd PSO demo\n");
            
        } // Main

        static double Error(double[] x)
        {
            // 0.42888194248035300000 when x0 = -sqrt(2), x1 = 0
            double trueMin = -0.42888194; // true min for z = x * exp(-(x^2 + y^2))
            double z = x[0] * Math.Exp(-((x[0] * x[0]) + (x[1] * x[1])));
            return (z - trueMin) * (z - trueMin); // squared diff
        }

        static double[] Solve(int dim, int numParticles, double minX, double maxX, int maxEpochs, double exitError)
        {
            // assumes existence of an accessible Error function and a Particle class
            Random rnd = new Random(0);

            Particle[] swarm = new Particle[numParticles];
            double[] bestGlobalPosition = new double[dim]; // best solution found by any particle in the swarm
            double bestGlobalError = double.MaxValue; // smaller values better

            // swarm initialization
            for (int i = 0; i < swarm.Length; ++i)
            {
                double[] randomPosition = new double[dim];
                for (int j = 0; j < randomPosition.Length; ++j)
                    randomPosition[j] = (maxX - minX) * rnd.NextDouble() + minX; // 

                double error = Error(randomPosition);
                double[] randomVelocity = new double[dim];

                for (int j = 0; j < randomVelocity.Length; ++j)
                {
                    double lo = minX * 0.1;
                    double hi = maxX * 0.1;
                    randomVelocity[j] = (hi - lo) * rnd.NextDouble() + lo;
                }
                swarm[i] = new Particle(randomPosition, error, randomVelocity, randomPosition, error);

                // does current Particle have global best position/solution?
                if (swarm[i].error < bestGlobalError)
                {
                    bestGlobalError = swarm[i].error;
                    swarm[i].position.CopyTo(bestGlobalPosition, 0);
                }
            } // initialization

            // prepare
            double w = 0.729; // inertia weight. see http://ieeexplore.ieee.org/stamp/stamp.jsp?arnumber=00870279
            double c1 = 1.49445; // cognitive/local weight
            double c2 = 1.49445; // social/global weight
            double r1, r2; // cognitive and social randomizations
            double probDeath = 0.01;
            int epoch = 0;

            double[] newVelocity = new double[dim];
            double[] newPosition = new double[dim];
            double newError;

            // main loop
            while (epoch < maxEpochs)
            {
                for (int i = 0; i < swarm.Length; ++i) // each Particle
                {
                    Particle currP = swarm[i]; // for clarity

                    // new velocity
                    for (int j = 0; j < currP.velocity.Length; ++j) // each component of the velocity
                    {
                        r1 = rnd.NextDouble();
                        r2 = rnd.NextDouble();

                        newVelocity[j] = (w * currP.velocity[j]) +
                          (c1 * r1 * (currP.bestPosition[j] - currP.position[j])) +
                          (c2 * r2 * (bestGlobalPosition[j] - currP.position[j]));
                    }
                    newVelocity.CopyTo(currP.velocity, 0);

                    // new position
                    for (int j = 0; j < currP.position.Length; ++j)
                    {
                        newPosition[j] = currP.position[j] + newVelocity[j];
                        if (newPosition[j] < minX)
                            newPosition[j] = minX;
                        else if (newPosition[j] > maxX)
                            newPosition[j] = maxX;
                    }
                    newPosition.CopyTo(currP.position, 0);

                    newError = Error(newPosition);
                    currP.error = newError;

                    if (newError < currP.bestError)
                    {
                        newPosition.CopyTo(currP.bestPosition, 0);
                        currP.bestError = newError;
                    }

                    if (newError < bestGlobalError)
                    {
                        newPosition.CopyTo(bestGlobalPosition, 0);
                        bestGlobalError = newError;
                    }

                    // death?
                    double die = rnd.NextDouble();
                    if (die < probDeath)
                    {
                        // new position, leave velocity, update error
                        for (int j = 0; j < currP.position.Length; ++j)
                            currP.position[j] = (maxX - minX) * rnd.NextDouble() + minX;
                        currP.error = Error(currP.position);
                        currP.position.CopyTo(currP.bestPosition, 0);
                        currP.bestError = currP.error;

                        if (currP.error < bestGlobalError) // global best by chance?
                        {
                            bestGlobalError = currP.error;
                            currP.position.CopyTo(bestGlobalPosition, 0);
                        }
                    }

                } // each Particle
                ++epoch;
            } // while

            // show final swarm
            Console.WriteLine("\nProcessing complete");
            Console.WriteLine("\nFinal swarm:\n");
            for (int i = 0; i < swarm.Length; ++i)
                Console.WriteLine(swarm[i].ToString());

            double[] result = new double[dim];
            bestGlobalPosition.CopyTo(result, 0);
            return result;
        } // Solve

    } // program class

    public class Particle
    {
        public double[] position;
        public double error;
        public double[] velocity;
        public double[] bestPosition;
        public double bestError;

        public Particle(double[] pos, double err, double[] vel, double[] bestPos, double bestErr)
        {
            this.position = new double[pos.Length];
            pos.CopyTo(this.position, 0);
            this.error = err;
            this.velocity = new double[vel.Length];
            vel.CopyTo(this.velocity, 0);
            this.bestPosition = new double[bestPos.Length];
            bestPos.CopyTo(this.bestPosition, 0);
            this.bestError = bestErr;
        }

        public override string ToString()
        {
            string s = "";
            s += "==========================\n";
            s += "Position: ";
            for (int i = 0; i < this.position.Length; ++i)
                s += this.position[i].ToString("F4") + " ";
            s += "\n";
            s += "Error = " + this.error.ToString("F4") + "\n";
            s += "Velocity: ";
            for (int i = 0; i < this.velocity.Length; ++i)
                s += this.velocity[i].ToString("F4") + " ";
            s += "\n";
            s += "Best Position: ";
            for (int i = 0; i < this.bestPosition.Length; ++i)
                s += this.bestPosition[i].ToString("F4") + " ";
            s += "\n";
            s += "Best Error = " + this.bestError.ToString("F4") + "\n";
            s += "==========================\n";
            return s;
        }

    } // Particle

    #endregion

    #region Test Implementation

    public class GenePart<TCombinationModel>
    {
        public TCombinationModel Model { get; set; }

        public bool Active { get; set; }
    }

    public class Gene<TCombinationModel>
    {
        private ISwarmEngineSettings<TCombinationModel> _settings;

        public Gene(ISwarmEngineSettings<TCombinationModel> settings)
        {
            Parts = new List<GenePart<TCombinationModel>>();
            _settings = settings;

            // TODO: initialize this gene with gene parts and link somehow to the original models

        }

        public void Initialize()
        {
            int geneSize = _settings.InitialPopulation.Count; // number of all items to be combined
            Parts.Clear(); // for re-initialisation when there is already a gene that's the same
            for (int i = 0; i < geneSize; i++)
            {
                Parts.Add(new GenePart<TCombinationModel>
                {
                    Model = _settings.InitialPopulation[i],
                    Active = GAUtils.RandDouble() > 0.5
                });
            }
        }

        public List<Gene<TCombinationModel>> Cross(Gene<TCombinationModel> other, bool invert)
        {
            List<Gene<TCombinationModel>> result = new List<Gene<TCombinationModel>>();
            Gene<TCombinationModel> baby1 = new Gene<TCombinationModel>(_settings);
            Gene<TCombinationModel> baby2 = new Gene<TCombinationModel>(_settings);

            if (invert)
            {
                // first dad, then mom
                for (int i = 0; i < this.Parts.Count; i++)
                {
                    baby1.Parts.Add(other.Parts[i]);
                    baby2.Parts.Add(this.Parts[i]);
                }
            }
            else
            {
                // first mom, then dad
                for (int i = 0; i < this.Parts.Count; i++)
                {
                    baby1.Parts.Add(this.Parts[i]);
                    baby2.Parts.Add(other.Parts[i]);
                }
            }
            result.Add(baby1);
            result.Add(baby2);

            return result;
        }

        public List<GenePart<TCombinationModel>> Parts { get; set; }

        public string GetActivePartsString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("[");
            for (int i = 0; i < Parts.Count; i++)
            {
                b.Append($"{(Parts[i].Active ? "1" : "0")},");
            }
            b.Remove(b.Length - 1, 1);
            b.Append("]");
            return b.ToString();
        }
    }

    public class Genome<TMainModel, TCombinationModel>
    {
        private static int Counter = 0;

        private SwarmEngineSettings<TMainModel, TCombinationModel> _settings;
        private TMainModel _model;

        internal Genome(SwarmEngineSettings<TMainModel, TCombinationModel> settings)
        {
            Genes = new List<Gene<TCombinationModel>>();
            _settings = settings;
            _model = settings.CreateModelFunc();
            Id = Counter++;
        }

        public void Initialize()
        {
            Genes.Clear();

            Fitness = 0;

            int genes = _settings.GenomeLength;
            // create a random bit string (random genes)
            for (int i = 0; i < genes; ++i)
            {
                var gene = new Gene<TCombinationModel>(_settings);
                int initPasses = 0;
                // Make sure all genes are unique
                do
                {
                    gene.Initialize();
                    initPasses++;
                } while (Genes.Any(g=>g.GetActivePartsString().Equals(gene.GetActivePartsString())));

                Genes.Add(gene);
                Debug.WriteLineIf(initPasses>1, $"# Re-initialized because of duplicate gene ({initPasses})");
            }
            RebuildModelFollowingGenes();
            UpdateFitnessScore();
        }

        /// <summary>
        /// Iterates through each gene flipping the bits acording to the mutation rate
        /// </summary>
        public void Mutate()
        {
            // Go through each gene
            for (int i = 0; i < Genes.Count; i++)
            {
                Gene<TCombinationModel> gene = Genes[i];
                int mutationCounter = 0;
                for (int j = 0; j < gene.Parts.Count; j++)
                {
                    //do we flip this bit?
                    if (GAUtils.RandDouble() < _settings.MutationRate)
                    {
                        //flip the bit
                        gene.Parts[j].Active = gene.Parts[j].Active == false;
                        mutationCounter++;
                    }
                }
                //Debug.WriteLineIf(mutationCounter > 0, $"> Mutated {mutationCounter}/{gene.Parts.Count} items");
            }
        }

        /// <summary>
        /// Takes 2 parent gene vectors, selects a midpoint and then swaps the ends
        /// of each genome creating 2 new genomes which are stored in baby1 and baby2
        /// </summary>
        /// <param name="other"></param>
        /// <param name="baby1"></param>
        /// <param name="baby2"></param>
        public void CrossOver(Genome<TMainModel, TCombinationModel> other, Genome<TMainModel, TCombinationModel> baby1, Genome<TMainModel, TCombinationModel> baby2)
        {
            // just return parents as offspring dependent on the rate
            // or if parents are the same
            if ((GAUtils.RandDouble() > _settings.CrossoverRate) || (this == other))
            {
                baby1.Genes = this.Genes;
                baby2.Genes = other.Genes;
                return;
            }

            if (baby1.Genes.Count <= 1 || baby2.Genes.Count <= 1)
            {
                baby1.Genes = this.Genes;
                baby2.Genes = other.Genes;
                return;
            }

            
            // determine a crossover point
            int crossOverPoint = GAUtils.RandInt(0, this.Genes.Count - 1); // = 29

            for (int i = 0; i < this.Genes.Count; i++)
            {
                var crossed = Genes[i].Cross(other.Genes[i], i < crossOverPoint);
                baby1.Genes.Add(crossed[0]);
                baby2.Genes.Add(crossed[1]);
            }
        }

        public void UpdateFitnessScore()
        {
            Fitness = _settings.FitnessFunction(_model);
        }

        public void RebuildModelFollowingGenes()
        {
            var activeModels = Genes.SelectMany(g => g.Parts.Where(p => p.Active)).Select(gp => gp.Model);

            var collectionToAdd = _settings.CombinationCollection(_model);
            collectionToAdd.Clear();
            foreach (var activeModel in activeModels)
            {
                collectionToAdd.Add(activeModel);
            }
        }

        public TMainModel GetModel()
        {
            return _model;
        }

        public string GetUniqueGenesString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("[");
            for (int i = 0; i < Genes.Count; i++)
            {
                b.Append($"[{Genes[i].GetActivePartsString()}]");
            }
            b.Append("]");
            return b.ToString();
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"Id: {Id}";
        }

        #endregion

        public List<Gene<TCombinationModel>> Genes { get; set; }

        public double Fitness { get; set; }

        public int Id { get; }
    }

    public interface IChromosone
    {
        void CreateStartPopulation();

        void Epoch();

        bool IsFitEnough();

        int Generation { get; }
        double TotalFitnessScore { get; }
        TModel GetWinningModel<TModel>();
    }

    public class Chromosone<TMainModel, TCombinationModel> : IChromosone
    {
        private SwarmEngineSettings<TMainModel, TCombinationModel> _settings;

        internal Chromosone(SwarmEngineSettings<TMainModel, TCombinationModel> settings)
        {
            Genomes = new List<Genome<TMainModel, TCombinationModel>>();
            _settings = settings;
        }

        public void CreateStartPopulation()
        {
            // clear existing population
            Genomes.Clear();

            //Parallel.For(0, _settings.PopulationSize, (i) =>
            for(int i = 0; i < _settings.PopulationSize; i++)
            {
                var genome = new Genome<TMainModel, TCombinationModel>(_settings);

                // Make sure all genomes are initially different
                int initPasses = 0;
                do
                {
                    genome.Initialize();
                    initPasses++;
                    lock (Genomes)
                    {
                        if (!Genomes.AsParallel().Any(g => g.GetUniqueGenesString().Equals(genome.GetUniqueGenesString())))
                        {
                            break; // we are all good
                        }
                        if(initPasses>10) throw new InvalidOperationException("No more combinations possible, reduce population size!");
                    }
                } while (true);
                

                lock (Genomes)
                {
                    Genomes.Add(genome);
                }
                //Debug.WriteLineIf(initPasses>1, $"# Re-initialized because of duplicate genome ({initPasses})");
            };
            
            //reset all variables
            Generation = 0;
            TotalFitnessScore = 0;
        }

        /// <summary>
        /// Main work function the the algorithm
        /// </summary>
        public void Epoch()
        {
            //CalculateTotalFitness();

            // Create storage for our new changed genomes
            List<Genome<TMainModel, TCombinationModel>> babyGenomes = new List<Genome<TMainModel, TCombinationModel>>();

            //make sure we add an EVEN number or the roulette wheel
            //sampling will crash
            int DefaultNumCopiesElite = 1;
            int DefaultNumElite = 4;

            // Keep the NBest in the selection
            if (((DefaultNumCopiesElite * DefaultNumElite) % 2) == 0)
            {
                GrabNBest(DefaultNumElite, DefaultNumCopiesElite, babyGenomes);
            }

            //babyGenomes.ForEach(g=>g.Mutate());

            while (babyGenomes.Count < Genomes.Count)
            {
                //select 2 parents
                Genome<TMainModel, TCombinationModel> mum = RouletteWheelSelection();
                Genome<TMainModel, TCombinationModel> dad = RouletteWheelSelection();

                //operator - crossover
                Genome<TMainModel, TCombinationModel> baby1 = new Genome<TMainModel, TCombinationModel>(_settings);
                Genome<TMainModel, TCombinationModel> baby2 = new Genome<TMainModel, TCombinationModel>(_settings);
                //    List<Gene> baby1Genes = baby1.Genes;
                //    List<Gene> baby2Genes = baby2.Genes;
                //    Crossover(mum.Genes, dad.Genes, ref baby1Genes, ref baby2Genes);
                mum.CrossOver(dad, baby1, baby2);
                //    baby1.Genes = baby1Genes;
                //    baby2.Genes = baby2Genes;

                //operator - mutate
                baby1.Mutate();
                baby2.Mutate();

                //add to new population
                babyGenomes.Add(baby1);
                babyGenomes.Add(baby2);
            }

            // Replace starter population with babies
            Genomes = babyGenomes;

            // Increment the generation counter
            Generation++;

            CalculateTotalFitness();
        }

        private void CalculateTotalFitness()
        {
            Genomes.ForEach(g =>
            {
                g.RebuildModelFollowingGenes();
                g.UpdateFitnessScore();
            });
            
            TotalFitnessScore = Genomes.Max(g => g.Fitness);
        }

        public TModel GetWinningModel<TModel>()
        {
            var winningGenome = Genomes.OrderByDescending(g => g.Fitness).First();
            return (TModel)Convert.ChangeType(winningGenome.GetModel(), typeof(TModel));
        }

        private void GrabNBest(int nBest, int numCopies, List<Genome<TMainModel, TCombinationModel>> newPopulation)
        {
            //sort(m_vecGenomes.begin(), m_vecGenomes.end());
            var genomes = Genomes.OrderByDescending(g => g.Fitness).Take(nBest).ToList();

            //now add the required amount of copies of the n most fittest 
            //to the supplied vector
            while (nBest-- > 0)
            {
                for (int i = 0; i < numCopies; ++i)
                {
                    newPopulation.Add(genomes[nBest]);
                }
            }
        }

        private Genome<TMainModel, TCombinationModel> RouletteWheelSelection()
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

        public bool IsFitEnough()
        {
            return TotalFitnessScore >= 99;
        }

        public List<Genome<TMainModel, TCombinationModel>> Genomes { get; set; }

        public int Generation { get; set; }


        public double TotalFitnessScore { get; set; }
    }

    public interface ISwarmEngineSettings
    {
        double CrossoverRate { get; }
        double MutationRate { get; }
        int MaxGenerations { get; }
    }

    public interface ISwarmEngineSettings<TCombinationModel> : ISwarmEngineSettings
    {
        IList<TCombinationModel> InitialPopulation { get; }
    }

    internal class SwarmEngineSettings<TModel, TCombinationModel> : ISwarmEngineSettings<TCombinationModel>
    {
        //private Func<IList<TCombinationModel>> _createInitialPopulation;
        public Func<TModel> CreateModelFunc { get; set; }
        public Func<TModel, double> FitnessFunction { get; set; }
        public Func<TModel, IList<TCombinationModel>> CombinationCollection { get; set; }
        public int MaxGenerations { get; set; }

        public Func<IList<TCombinationModel>> CreateInitialPopulation
        {
            set
            {
                //_createInitialPopulation = value;
                InitialPopulation = value();
            }
        }

        public IList<TCombinationModel> InitialPopulation { get; private set; }

        public int PopulationSize { get; set; }
        public int GenomeLength { get; set; }
        public double CrossoverRate { get; set; }
        public double MutationRate { get; set; }
    }

    public class SwarmEngine
    {
        private SwarmEngine()
        {
            Chromosones = new List<IChromosone>();
        }

        /// <summary>
        /// Returns an instance of an enginebuilder for creating an algorithm to solve
        /// the combinatoric problem
        /// </summary>
        /// <typeparam name="TModel">Model of the main model to make combinations in</typeparam>
        /// <typeparam name="TCombinationModel">Model of the items in the main model to actually combine.</typeparam>
        /// <returns></returns>
        public static EngineBuilder<TModel, TCombinationModel> GetBuilder<TModel, TCombinationModel>()
        {
            return new EngineBuilder<TModel, TCombinationModel>();
        }

        public void Run()
        {
            do
            {
                // Run parallel for each chromosone
                foreach (IChromosone chromosone in Chromosones)
                {
                    chromosone.Epoch();
                }

                Debug.WriteLine($"End of iteration {Chromosones.Max(c => c.Generation)} => Fitness : {Chromosones.Max(c => c.TotalFitnessScore)}");

            } while (Chromosones.All(c => c.Generation < Settings.MaxGenerations)
                    && !Chromosones.Any(c => c.IsFitEnough()));

            var winningChromo = Chromosones.First(cr => cr.TotalFitnessScore == Chromosones.Max(c => c.TotalFitnessScore));
            var winningModel = winningChromo.GetWinningModel<KnapSack>();
            Debug.WriteLine(winningModel);
        }

        public class EngineBuilder<TModel, TCombinationModel>
        {
            // Default values
            private int _defaultChromosoneCount = 200;
            private int _defaultPopulationSize = 200;
            private double _defaultCrossoverRate = 0.8;
            private double _defaultMutationRate = 0.05;

            private SwarmEngineSettings<TModel, TCombinationModel> _settings;


            internal EngineBuilder()
            {
                _settings = new SwarmEngineSettings<TModel, TCombinationModel>();
                _settings.PopulationSize = _defaultPopulationSize;
                _settings.CrossoverRate = 0;//_defaultCrossoverRate;
                _settings.MutationRate = 0.8;//_defaultMutationRate;
                _settings.GenomeLength = 1; // 1 gene per genome, a gene contains enabled combinations
            }

            public EngineBuilder<TModel, TCombinationModel> ForModel(Func<TModel> createModelFunc)
            {
                _settings.CreateModelFunc = createModelFunc;
                return this;
            }

            public EngineBuilder<TModel, TCombinationModel> WithCombinationOf(Func<TModel, IList<TCombinationModel>> func)
            {
                _settings.CombinationCollection = func;
                return this;
            }

            public EngineBuilder<TModel, TCombinationModel> WithInitialCollection(Func<IList<TCombinationModel>> func)
            {
                _settings.CreateInitialPopulation = func;
                return this;
            }

            public EngineBuilder<TModel, TCombinationModel> FitnessFunction(Func<TModel, double> fitnessFunc)
            {
                _settings.FitnessFunction = fitnessFunc;
                return this;
            }

            public EngineBuilder<TModel, TCombinationModel> MaximumGenerations(int maxIterations)
            {
                _settings.MaxGenerations = maxIterations;
                return this;
            }

            public SwarmEngine Build()
            {
                var engine = new SwarmEngine();

                // Build chromosones
                for (int i = 0; i < _defaultChromosoneCount; i++)
                {
                    IChromosone c = new Chromosone<TModel, TCombinationModel>(_settings);
                    c.CreateStartPopulation();
                    engine.Chromosones.Add(c);
                }
                engine.Settings = _settings;
                return engine;
            }
        }

        public List<IChromosone> Chromosones { get; }

        public ISwarmEngineSettings Settings { get; private set; }
    }


    // Basic Model test

    public class KnapSackItem
    {
        public KnapSackItem(int weight, int volume)
        {
            Weight = weight;
            Volume = volume;
        }

        public int Weight { get; }
        public int Volume { get; }
    }

    public class KnapSack
    {
        public KnapSack(int maxWeight, int maxVolume)
        {
            MaxWeight = maxWeight;
            MaxVolume = maxVolume;
            Items = new List<KnapSackItem>();
        }

        public double GetPercentualFitness()
        {
            int totalWeight = Items.Sum(si => si.Weight);
            int totalVolume = Items.Sum(si => si.Volume);

            // Volume and weight are very important (50% each)
            double fitnessWeight = (50.0 / MaxWeight) * totalWeight;
            double fitnessVolume = (50.0 / MaxVolume) * totalVolume;

            if (fitnessWeight > 50.0) fitnessWeight = 0;
            if (fitnessVolume > 50.0) fitnessVolume = 0;

            return fitnessWeight + fitnessVolume;
        }

        public List<KnapSackItem> Items { get; set; }

        public int MaxWeight { get; }
        public int MaxVolume { get; }

        #region Overrides of Object

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (KnapSackItem item in Items)
            {
                sb.AppendLine($"W: {item.Weight}\tV:{item.Volume}");
            }

            sb.AppendLine();
            sb.AppendLine($"MaxW: {MaxWeight}\tMaxVolume: {MaxVolume}");
            sb.AppendLine($"TotalW:{Items.Sum(i => i.Weight)}\tTotalVolume:{Items.Sum(i => i.Volume)}");


            return sb.ToString();

        }

        #endregion
    }

    #endregion
}