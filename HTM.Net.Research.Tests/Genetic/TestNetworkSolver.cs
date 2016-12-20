using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Text;
using HTM.Net.Algorithms;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Genetic;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Research.Tests.Examples.Random;
using HTM.Net.Research.Tests.Swarming.Experiments;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Genetic
{
    [TestClass]
    public class TestNetworkSolver
    {
        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void SolveSineNetwork()
        {
            NetworkSolver solver = new NetworkSolver(100, new RandomPermutationParameters(), new RandomDescriptionParameters());
            solver.Initialize(5);

            double bestFitnessSoFar;

            int runs = 0;
            do
            {
                bestFitnessSoFar = solver.Epoch();
                runs++;
                Console.WriteLine(bestFitnessSoFar);
                if (runs > 5) break;
            } while (bestFitnessSoFar < 100);
        }
    }

    /// <summary>
    /// We have multiple genomes in the chromosone, each genome has genes and each gene has gene parts
    /// - Each genome represents a possible solution
    /// - Each gene represents a permutable parameter/setting of the network
    /// </summary>
    public class NetworkSolver : GeneticAlgorithmSolver<NetworkGenome, NetworkGene, Network.Network>
    {
        private List<int[]> _selectedData = new List<int[]>();
        private Map<int, double[]> _predictedValues; // step, values
        private List<RandomGuess> _predictions;

        public NetworkSolver(double desiredFitness, ExperimentPermutationParameters parameters, ExperimentParameters descriptionParameters)
            : base(desiredFitness)
        {
            Random = new XorshiftRandom(42);
            PermutationParameters = parameters;
            DescriptionParameters = descriptionParameters;
        }

        #region Overrides of GeneticAlgorithmSolver<NetworkGenome,object>

        public override void Initialize(params object[] arguments)
        {
            Initialize((int)arguments[0]);
        }

        public override double Epoch()
        {
            double bestFitnessSoFar = 0;

            // iterate through the population
            for (int p = 0; p < GenomePopulation.Count; ++p)
            {
                NetworkGenome genome = GenomePopulation[p];

                double fitness = CalculateFitness(genome);

                //assign radius to fitness
                genome.Fitness = fitness;

                //keep a record of the best
                if (fitness > bestFitnessSoFar && fitness <= 100)
                {
                    bestFitnessSoFar = fitness;

                    BestGenomeIndex = p;
                }

            } // next genome

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

        protected override double CalculateFitness(NetworkGenome genome)
        {
            // decode this genome
            var network = Decode(genome);

            // run this network and check the fitness
            _predictions = new List<RandomGuess>();
            _predictedValues = null;
            
            network.Observe().Subscribe(GetNetworkSubscriber());
            network.Start();
            network.GetTail().GetTail().GetLayerThread().Wait();

            var lastGuesses = _predictions.AsQueryable().Reverse().Take(10).Reverse();

            //int winningRounds = lastGuesses.Count(g=> g.GetProfit()>0);

            double dRatio = 100.0 / lastGuesses.Sum(g => g.Count);
            int winningGuesses = lastGuesses.Sum(g => g.Count(x => x.Revenue - x.Cost > 0));

            return winningGuesses * dRatio;
        }

        #endregion

        private void Initialize(int numberOfGenomes)
        {
            Started = false;
            Generation = 0;

            // Create the parameters for the network that we are going to use to test the fitness. (?)
            ExternalItems = new List<Network.Network>();
            //do
            //{
            //    SackItem item = new SackItem(GAUtils.RandInt(0, 100), GAUtils.RandInt(0, 10));
            //    if (!ExternalItems.Any(i => i.Weight == item.Weight))
            //    {
            //        ExternalItems.Add(item);
            //    }
            //} while (ExternalItems.Count < numberOfGenomes);

            //int geneCombinations = numberOfGenomes;
            //int chromosoneLength = geneCombinations * 1; // 1 genes per chromosone (item in or out)

            // initialize the GA
            Algorithm = new GeneticAlgorithm<NetworkGenome, NetworkGene>(
                0.0,    // no cross over
                0.45,    // mutate enough
                numberOfGenomes,
                BuildGenome);

            // grab the genomes
            GenomePopulation = Algorithm.GrabGenomes();
        }

        /// <summary>
        /// Make sure the genome's genes are populated correctly.
        /// </summary>
        /// <param name="genome"></param>
        private void BuildGenome(NetworkGenome genome)
        {
            Parameters baseParameters = DescriptionParameters.Copy();

            var variables = PermutationParameters.GetPermutationVars();
            Parameters movedParameters = MoveParameters(variables);

            Parameters networkParameters = baseParameters.Union(movedParameters);

            // Assign the parameters to the genome
            genome.AssignGenes(networkParameters, variables.ToArray());
            // Add network to external items for reference in fitness function

        }

        private Parameters MoveParameters(List<Tuple<Parameters.KEY, PermuteVariable>> variables)
        {
            Parameters p = Parameters.Empty();

            foreach (Tuple<Parameters.KEY, PermuteVariable> variable in variables)
            {
                object currentPos = variable.Item2.GetPosition();
                // Set our variable
                p.SetParameterByKey(variable.Item1, TypeConverter.Convert(currentPos, variable.Item1.GetFieldType()));
                // Move to new position
                object globalPos = null;
                if (variable.Item2 is PermuteFloat)
                {
                    globalPos = ((PermuteFloat)variable.Item2).max;
                }
                variable.Item2.NewPosition(globalPos, Random);
            }
            //Debug.WriteLine("--> " + p.ToString());
            return p;
        }

        private Network.Network Decode(NetworkGenome genome)
        {
            var sensor = PrepareRandomDataAndGetSensor();

            Parameters p = genome.GetEffectiveParameters();

            Network.Network n = Network.Network.Create("RandomData Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new Algorithms.SpatialPooler())
                        .Add(sensor)));

            return n;
        }

        private Sensor<ObservableSensor<string[]>> PrepareRandomDataAndGetSensor()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("Date,Number 1,Number 2,Number 3,Number 4,Number 5,Number 6,Bonus")
                .AddHeader("datetime,int,int,int,int,int,int,int")
                .AddHeader("T,,,,,,,")
                .Build();

            List<string> fileLines = YieldingFileReader.ReadAllLines("RandomData.csv", Encoding.UTF8).ToList();

            // Cleanup and reverse
            List<string> fileLinesReversed = fileLines.Skip(3).Reverse().ToList();
            List<string> fileLinesFinal = new List<string>();

            // Define number of records to train on
            int takeTrainCount = 150;//_numberOfRecordsToTrain;
            int takeTotalCount = takeTrainCount + 10;//_nrOfRecordsToEvaluateAfterTraining;
            // Define offset from start of file
            int skipCount = fileLinesReversed.Count - takeTotalCount - 0;//_offsetFromEnd;

            // Repeat the training set if needed
            for (int i = 0; i < 0/*_iterationsToRepeat*/; i++)
            {
                fileLinesFinal.AddRange(fileLinesReversed.Skip(skipCount).Take(takeTrainCount));
            }
            fileLinesFinal.AddRange(fileLinesReversed.Skip(skipCount).Take(takeTotalCount)); // last x record given, not trained before

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", manual }));

            //StreamWriter sw = new StreamWriter("RandomData_rev.csv");
            foreach (string line in fileLinesFinal)
            {
                //sw.WriteLine(line);
                manual.OnNext(line);
                _selectedData.Add(line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(int.Parse).ToArray());
            }
            manual.OnComplete();

            //sw.Flush();
            //sw.Close();
            return sensor;
        }

        private IObserver<IInference> GetNetworkSubscriber()
        {
            return Observer.Create<IInference>(output =>
            {
                string[] classifierFields = { "Number 1", "Number 2", "Number 3", "Number 4", "Number 5", "Number 6", "Bonus" };
                Map<int, double[]> newPredictions = new Map<int, double[]>();
                // Step 1 is certainly there
                if (classifierFields.Any(cf => null != output.GetClassification(cf).GetMostProbableValue(1)))
                {
                    foreach (int step in output.GetClassification(classifierFields[0]).StepSet())
                    {
                        newPredictions.Add(step, classifierFields.Take(7)
                            .Select(cf => ((double?)output.GetClassification(cf).GetMostProbableValue(step)).GetValueOrDefault(-1)).ToArray());
                    }
                }
                else
                {
                    newPredictions = _predictedValues;
                }

                RandomGuess gd = RandomGuess.From(_predictedValues, newPredictions, output, classifierFields);

                if (gd.RecordNumber > 0) // first prediction is bogus, ignore it
                    _predictions.Add(gd);

                // Statistical good numbers for chances
                List<int[]> goodChances = RandomGuessNetworkApi.GetBestChances(/*_offsetFromEnd*/0 + 1);
                foreach (int[] chance in goodChances)
                {
                    gd.AddPrediction(chance.Select(c => (double)c).ToArray(), false);
                    gd.NextPredictions.Add(chance);
                }
                gd.NextPredictions = gd.NextPredictions.Take(10).ToList();

                _predictedValues = newPredictions;

                //if (_writeToFile)
                //{
                //    WriteToFile(gd);
                //}

            }, Console.WriteLine, () =>
            {
                //if (_writeToFile)
                //{
                //    Console.WriteLine("Stream completed. see output: " + _outputFile.FullName);
                //    try
                //    {
                //        _pw.Flush();
                //        _pw.Close();
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e);
                //    }
                //}
            });
        }

        private ExperimentPermutationParameters PermutationParameters { get; set; }
        private ExperimentParameters DescriptionParameters { get; set; }
        private IRandom Random { get; }
    }

    /// <summary>
    /// A genome has genes, a gene represents a permutable setting of the network
    /// </summary>
    public class NetworkGenome : BaseGenome<NetworkGene>
    {
        public void AssignGenes(Parameters parameters, Tuple<Parameters.KEY, PermuteVariable>[] permuteVars)
        {
            foreach (var key in parameters.Keys())
            {
                NetworkGene gene = null;
                if (!permuteVars.Any(t => t.Item1.Equals(key)))
                {
                    gene = new NetworkGene(key, parameters.GetParameterByKey(key));
                    gene.IsFrozen = true;
                }
                else
                {
                    gene = new NetworkGene(key, parameters.GetParameterByKey(key));
                    gene.PermuteVar = permuteVars.Single(t => t.Item1.Equals(key)).Item2;
                }
                Genes.Add(gene);
            }
        }

        public Parameters GetEffectiveParameters()
        {
            Parameters p = Parameters.Empty();
            // loop through all the genes and fillup the parameter collection again
            foreach (NetworkGene networkGene in Genes)
            {
                if (networkGene.Value is IConvertible)
                {
                    p.SetParameterByKey(networkGene.Key,
                        TypeConverter.Convert(networkGene.Value, networkGene.Key.GetFieldType()));
                }
                else
                {
                    p.SetParameterByKey(networkGene.Key, networkGene.Value);
                }
            }
            return p;
        }
    }

    public class NetworkGene : GeneBase<NetworkGene>
    {
        public NetworkGene()
        {

        }

        public NetworkGene(Parameters.KEY key, object value)
        {
            Key = key;
            Value = value;
        }

        #region Overrides of GeneBase<NetworkGene>

        /// <summary>
        /// Exchange parameters between two genes (swap)
        /// </summary>
        /// <param name="dad">other gene to cross with</param>
        /// <param name="invert">true when from this to dad, false otherwise</param>
        /// <returns></returns>
        public override List<NetworkGene> Cross(NetworkGene dad, bool invert)
        {
            throw new System.NotImplementedException();
        }

        public override void Mutate(double mutationRate)
        {
            if (GAUtils.RandDouble() < mutationRate)
            {
                // Do a mutation
                if (PermuteVar is PermuteFloat)
                {
                    ((PermuteFloat)PermuteVar).Agitate();
                    Value = PermuteVar.NewPosition(((PermuteFloat) PermuteVar).max, GAUtils.Random);
                }
            }
        }

        public override int Length
        {
            get { return 1; }
        }

        #endregion

        public object Value { get; set; }

        public Parameters.KEY Key { get; set; }

        public PermuteVariable PermuteVar { get; set; }
    }
}