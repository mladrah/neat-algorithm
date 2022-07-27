using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace NEAT
{
    public class NEAT : MonoBehaviour
    {
        public Agent agent;

        public static NEAT Instance { get; private set; }

        #region Genotype
        private Dictionary<int, NodeGene> _globalNodeGenes;

        private Dictionary<int, ConnectionGene> _globalConnectionGenes;
        private ConnectionGene[,] _globalConnectionGenesCombination;

        private int _globalInnovNr;
        public int GlobalInnovNr { get => _globalInnovNr++; }
        #endregion

        #region Speciation
        private List<Species> _species;
        #endregion

        public List<Genome> Population { get; private set; }
        private int _populationCounter;

        #region Generation
        private int _generationCounter;
        public int GenerationCounter { get => _generationCounter; }

        public Genome BestGenomeLastGen { get; private set; }
        #endregion

        #region Events
        public delegate void NEATEvent();
        public event NEATEvent OnNextGeneration;
        #endregion

        #region Debug
        [Header("Debug")]
        [SerializeField] private bool debugTestCode;
        [SerializeField] private bool debugNextGenomeLogging;
        [SerializeField] private bool debugGlobalLogging;
        [SerializeField] private bool debugGenomeCalculation;
        [SerializeField] private bool debugGenomesComparison;
        [SerializeField] private bool debugSerialisationLogging;
        #endregion

        private void Awake() {
            Instance = this;
            _globalNodeGenes = new Dictionary<int, NodeGene>();
            _globalConnectionGenes = new Dictionary<int, ConnectionGene>();
            _globalConnectionGenesCombination = new ConnectionGene[1000, 1000];
            Population = new List<Genome>();
            _species = new List<Species>();
            Species.ID_COUNTER = 0;
        }

        private void Start() {
            if (debugTestCode)
                return;

            Population = InitalizePopulation();

            Debug.Log("------------------ Initialization ------------------");

            DebugPopulationLog("Start ->");

            RunGenome();

            #region Debug
            if (debugGenomeCalculation) {
                Population[0].Evaluate(new List<float> { 0.25f, 0.5f, 0.75f });
                Population[0].DebugNodeGenesLog();
                Population[0].DebugConnectionsGenesLog();
            }

            if (debugGlobalLogging) {
                DebugGlobalNodeGenesLog();
                DebugGlobalConnectionGenesLog();
            }
            #endregion
        }

        public List<Genome> InitalizePopulation() {
            List<Genome> population = new List<Genome>();

            for (int i = 0; i < ConfigNEAT.POPULATION_COUNT; i++) {
                population.Add(new Genome());
                population[i].Initialize();
            }

            return population;
        }

        #region Loop
        public void RunGenome() {
            agent.ResetAgent(Population[_populationCounter]);

            if (debugNextGenomeLogging)
                Population[_populationCounter].DebugConnectionsGenesLog();

            UIManagerNEAT.Instance.UpdateCurrentGenome(_populationCounter);
            UIManagerNEAT.Instance.UpdateCurrentSpecies(Population[_populationCounter].BelongingSpecies != null ? Population[_populationCounter].BelongingSpecies.Id : -1);
            UIManagerNEAT.Instance.UpdateSpeciesCount(_species.Count);
            UIManagerNEAT.Instance.UpdateCurrentGeneration(_generationCounter);
        }

        public void Lost(float fitness) {
            Population[_populationCounter].Fitness = fitness;
            _populationCounter++;

            if (_populationCounter <= Population.Count - 1)
                RunGenome();
            else {
                NewGeneration();
            }
        }

        ///<summary>
        /// Steps:
        /// 1. Evaluate
        /// 2. Speciate
        /// 2. Evalute Species Score; Calculate adjusting Fitness
        /// 3. Take Elites over
        /// 4. Determine Offspring; Penalize stagnated Species
        /// 5. Remove extinct Species
        /// 6. Kill worst genomes of remaining Species
        /// 7. Repopulate the missing Population
        /// </summary>
        public void NewGeneration() {

            #region UI Log
            BestGenomeLastGen = Population.Aggregate((g1, g2) => g1.Fitness > g2.Fitness ? g1 : g2);
            //bestGenome.DebugConnectionsGenesLog();
            UIManagerNEAT.Instance.UpdateMaxFitnessUI(BestGenomeLastGen);
            #endregion

            List<Genome> newPopulation = new List<Genome>();

            #region Speciation & Evaluation
            Speciation();

            foreach (Species species in _species) {
                species.ShareFitness();
            }

            DebugPopulationLog("After Speciation & Evaluation");
            #endregion

            Debug.Log("------------------ New Generation ------------------");

            #region Elitism
            if (ConfigNEAT.IS_ELITISM_ENABLED) {
                List<Genome> elites = new List<Genome>();

                List<Genome> tmpPopulation = new List<Genome>();
                tmpPopulation.AddRange(Population);
                tmpPopulation = tmpPopulation.OrderByDescending(g => g.Fitness).ToList();

                for (int i = 0; i < ConfigNEAT.NUMBER_OF_ELITES; i++) {
                    Genome elite = tmpPopulation[i];
                    elite.IsElite = true;
                    elites.Add(elite);
                }

                newPopulation.AddRange(elites);
            }
            #endregion

            #region Determine Offspring
            float totalAverageSpeciesFitness = _species.Sum(s => s.AverageFitness);
            int totalOffspring = 0;
            int missingPopulationCount = Population.Count - newPopulation.Count;

            foreach (Species species in _species) {
                int allowedOffspring;

                if (!species.IsStagnating()) {
                    species.AllowedOffspring = Mathf.RoundToInt((species.AverageFitness / totalAverageSpeciesFitness) * missingPopulationCount);
                }

                allowedOffspring = species.AllowedOffspring;
                totalOffspring += species.AllowedOffspring;

            }

            int populationDifference = totalOffspring + newPopulation.Count - ConfigNEAT.POPULATION_COUNT;

            if (populationDifference > 0) {
                Species highestOffspringSpecies = _species.Aggregate((s1, s2) => s1.AllowedOffspring > s2.AllowedOffspring ? s1 : s2);
                highestOffspringSpecies.AllowedOffspring -= Mathf.Abs(populationDifference);
            } else if (populationDifference < 0) {
                Species highestOffspringSpecies = _species.Aggregate((s1, s2) => s1.AllowedOffspring > s2.AllowedOffspring ? s1 : s2);
                highestOffspringSpecies.AllowedOffspring += Mathf.Abs(populationDifference);
            }
            #endregion

            #region Reproduction
            foreach (Species species in _species.ToList()) {
                if (species.IsGoingExtinct())
                    _species.Remove(species);
                else {
                    species.KillWorstOfPopulation();
                    for (int i = 0; i < species.AllowedOffspring; i++) {
                        Genome child = species.Repopulate();
                        newPopulation.Add(child);
                    }
                    species.KillLastPopulation();
                }
            }
            #endregion

            Population.Clear();
            Population.AddRange(newPopulation);
            newPopulation.Clear();

            DebugPopulationLog("Start -> Population");

            #region Reset Variables
            _populationCounter = 0;
            _generationCounter++;

            foreach (Genome genome in Population) {
                if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Feedforwad))
                    genome.HardResetNodes();
                genome.IsElite = false;
            }
            #endregion

            #region Events
            OnNextGeneration?.Invoke();
            #endregion

            RunGenome();
        }
        #endregion

        #region Related to Speciation
        public void Speciation() {
            foreach (Species species in _species)
                species.Reset();

            if (_generationCounter > 0) {
                if (_species.Count < ConfigNEAT.SPECIES_TARGET) {
                    ConfigNEAT.DISTANCE_THRESHOLD -= ConfigNEAT.DISTANCE_CHANGE;
                } else if (_species.Count > ConfigNEAT.SPECIES_TARGET) {
                    ConfigNEAT.DISTANCE_THRESHOLD += ConfigNEAT.DISTANCE_CHANGE;
                }
            }

            if (ConfigNEAT.DISTANCE_THRESHOLD < ConfigNEAT.DISTANCE_CHANGE)
                ConfigNEAT.DISTANCE_THRESHOLD = ConfigNEAT.DISTANCE_CHANGE;

            for (int i = 0; i < Population.Count; i++) {
                bool speciesFound = false;

                if (Population[i].BelongingSpecies != null) {
                    continue;
                }

                ///<summary>
                /// Calculate for each Species Representative the Compatiblity Distance
                /// </summary>
                foreach (Species species in _species) {
                    float compatibilityDistance = CalculateCompatibilityDistance(Population[i], species.Representative);

                    ///<summary>
                    /// Found a Species where Genome and the Representative is below the Distance Threshold
                    /// </summary>
                    if (compatibilityDistance <= ConfigNEAT.DISTANCE_THRESHOLD) {
                        species.AddGenome(Population[i]);
                        speciesFound = true;
                        break;
                    }
                }

                ///<summary>
                /// If so Species is found, create a new Species with the Genome as Representative
                /// </summary>
                if (!speciesFound) {
                    Species newSpecies = new Species(Population[i]);
                    _species.Add(newSpecies);
                }
            }
        }

        public float FitnessSharing(Genome genome) {
            float adjustedFitness;
            int numberOfGenomesInSameSpecies;

            numberOfGenomesInSameSpecies = genome.BelongingSpecies.SpeciesPopulation.Count;

            adjustedFitness = genome.Fitness / numberOfGenomesInSameSpecies;

            return adjustedFitness;
        }
        #endregion

        #region Crossover
        public Genome Crossover(Genome a, Genome b) {
            Genome child = new Genome();

            ///<summary>
            /// Assign same Input and Output Nodes (They are always there) -> For FS-NEAT
            /// </summary>>
            List<NodeGene> inputNodes = a.NodeGenes.Where(ng => ng.Layer.Equals(Layer.Input)).ToList();
            List<NodeGene> outputNodes = a.NodeGenes.Where(ng => ng.Layer.Equals(Layer.Output)).ToList();
            foreach (NodeGene ng in inputNodes)
                child.AddNodeGene(ng);
            foreach (NodeGene ng in outputNodes)
                child.AddNodeGene(ng);

            ///<summary>
            /// Sort List by InnovNr so that 'i' is InnovNr in for-loop
            /// </summary>
            a.ConnectionGenes.Sort((x, y) => x.InnovNr.CompareTo(y.InnovNr));
            b.ConnectionGenes.Sort((x, y) => x.InnovNr.CompareTo(y.InnovNr));

            ///<summary>
            /// Variables to determine if Connection Gene is 'Excess' or 'Disjoint' 
            ///</summary>
            int highestInnovNrA = a.ConnectionGenes[a.ConnectionGenes.Count - 1].InnovNr;
            int highestInnovNrB = b.ConnectionGenes[b.ConnectionGenes.Count - 1].InnovNr;

            ///<summary>
            /// Assigned by the Genome with the highest Connection Gene Count 
            ///</summary>
            int numberOfComparisions = highestInnovNrA >= highestInnovNrB ? highestInnovNrA : highestInnovNrB;

            for (int i = 0; i <= numberOfComparisions; i++) {

                ///<summary>
                /// Both Genomes have the same Connection Gene
                ///</summary>
                if (a.ConnectionGenes.Exists(cg => cg.InnovNr == i) && b.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {

                    ///<summary>
                    /// Matching Genes are inherited randomly (p.12)
                    /// </summary>
                    ConnectionGene cgA = a.ConnectionGenes.Find(cg => cg.InnovNr == i);
                    ConnectionGene cgB = b.ConnectionGenes.Find(cg => cg.InnovNr == i);
                    ConnectionGene cg;

                    if (Random.Range(0f, 1f) >= 0.5f) {
                        cg = child.AddConnectionGene(cgA);
                    } else {
                        cg = child.AddConnectionGene(cgB);
                    }

                    ///<summary>
                    /// If one Connection Gene is disabled it could remain disabled
                    /// </summary>
                    if (!cgA.IsEnabled || !cgB.IsEnabled)
                        cg.IsEnabled = ConfigNEAT.INHERITED_GENE_REMAINS_DISABLED_RATE >= Random.Range(0f, 1f) ? false : true;

                    ///<summary>
                    /// Genome 'A' has only the Connection Gene
                    /// </summary>
                } else if (a.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {

                    ///<summary>
                    /// 'Disjoint' or 'Excess' Case
                    /// </summary>
                    if (i < highestInnovNrB || i > highestInnovNrB) {
                        ConnectionGene cg;

                        ///<summary>
                        /// 'Disjoint' and 'Excess' Genes are inherited from the more fit parent (p.12)
                        /// </summary>
                        if (a.AdjustedFitness > b.AdjustedFitness) {
                            cg = a.ConnectionGenes.Find(cg => cg.InnovNr == i);
                            child.AddConnectionGene(cg).IsEnabled = cg.IsEnabled ? true : ConfigNEAT.INHERITED_GENE_REMAINS_DISABLED_RATE >= Random.Range(0f, 1f) ? false : true;

                            ///<summary>
                            /// If both have equal fitness, 'Disjoint' and 'Excess' Genes are inherited randomly (p.12)
                            /// </summary>
                        } else if (a.AdjustedFitness == b.AdjustedFitness) {
                            if (Random.Range(0f, 1f) >= 0.5f) {
                                cg = a.ConnectionGenes.Find(cg => cg.InnovNr == i);
                                child.AddConnectionGene(cg).IsEnabled = cg.IsEnabled ? true : ConfigNEAT.INHERITED_GENE_REMAINS_DISABLED_RATE >= Random.Range(0f, 1f) ? false : true;
                            }
                        }
                    } else
                        Debug.LogError("Disjoint/Excess Error (A)");

                    ///<summary>
                    /// Genome 'B' has only the Connection Gene
                    /// </summary>
                } else if (b.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {

                    ///<summary>
                    /// Same as above but vice versa
                    /// </summary>
                    if (i < highestInnovNrA || i > highestInnovNrA) {
                        ConnectionGene cg;

                        if (b.AdjustedFitness > a.AdjustedFitness) {
                            cg = b.ConnectionGenes.Find(cg => cg.InnovNr == i);
                            child.AddConnectionGene(cg);
                        } else if (b.AdjustedFitness == a.AdjustedFitness) {
                            if (Random.Range(0f, 1f) >= 0.5f) {
                                cg = b.ConnectionGenes.Find(cg => cg.InnovNr == i);
                                child.AddConnectionGene(cg);
                            }
                        }
                    } else
                        Debug.LogError("Disjoint/Excess Error (B)");

                }
            }

            return child;
        }
        #endregion

        #region Compatibility Distance
        public float CalculateCompatibilityDistance(Genome a, Genome b) {
            if (!ConfigNEAT.IS_SPECIATION_ENABLED)
                return 0;

            ///<summary>
            /// Parameters for Compatibility Distance Function
            /// </summary>
            int excessCount = 0;
            int disjointCount = 0;
            int numberOfGenes = 1;
            float averageWeightDifference = 0;
            List<float> weightDifferences = new List<float>();

            if (a.ConnectionGenes.Count >= 20 || b.ConnectionGenes.Count >= 20)
                numberOfGenes = a.ConnectionGenes.Count >= b.ConnectionGenes.Count ? a.ConnectionGenes.Count : b.ConnectionGenes.Count;

            ///<summary>
            /// Sort List by InnovNr so that 'i' is InnovNr in for-loop
            /// </summary>
            a.ConnectionGenes.Sort((x, y) => x.InnovNr.CompareTo(y.InnovNr));
            b.ConnectionGenes.Sort((x, y) => x.InnovNr.CompareTo(y.InnovNr));

            ///<summary>
            /// Variables to determine if Connection Gene is 'Excess' or 'Disjoint' 
            ///</summary>
            int highestInnovNrA = a.ConnectionGenes[a.ConnectionGenes.Count - 1].InnovNr;
            int highestInnovNrB = b.ConnectionGenes[b.ConnectionGenes.Count - 1].InnovNr;

            ///<summary>
            /// Assigned by the Genome with the highest Connection Gene Count 
            ///</summary>
            int numberOfComparisions = highestInnovNrA >= highestInnovNrB ? highestInnovNrA : highestInnovNrB;

            for (int i = 0; i <= numberOfComparisions; i++) {

                ///<summary>
                /// Both Genomes have the same Connection Gene
                ///</summary>
                if (a.ConnectionGenes.Exists(cg => cg.InnovNr == i) && b.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {

                    ///<summary>
                    /// Adding the Weight Difference to a List
                    /// </summary>
                    weightDifferences.Add(Mathf.Abs(a.ConnectionGenes.Find(cg => cg.InnovNr == i).Weight - b.ConnectionGenes.Find(cg => cg.InnovNr == i).Weight));

                    ///<summary>
                    /// Genome 'A' has only the Connection Gene
                    /// </summary>
                } else if (a.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {

                    ///<summary>
                    /// Connection Gene is 'Disjoint' if Genome 'B' has still more Connection Genes with higher Innovation Numbers;
                    /// Connection Geen is 'Excess' if its Innovation Number is higher than the highest Innovation Number of Genome 'B'
                    /// </summary>
                    if (i < highestInnovNrB)
                        disjointCount++;
                    else if (i > highestInnovNrB)
                        excessCount++;
                    else
                        Debug.LogError("Disjoint/Excess Error (A)");

                    ///<summary>
                    /// Genome 'B' has only the Connection Gene
                    /// </summary>
                } else if (b.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {

                    ///<summary>
                    /// Same as above but vice versa
                    /// </summary>
                    if (i < highestInnovNrA)
                        disjointCount++;
                    else if (i > highestInnovNrA)
                        excessCount++;
                    else
                        Debug.LogError("Disjoint/Excess Error (B)");

                }
            }

            averageWeightDifference = weightDifferences.Count > 0 ? weightDifferences.Sum() / weightDifferences.Count : 0;

            float compatibilityDistance = CompatibilityDistanceFunction(excessCount, disjointCount, numberOfGenes, averageWeightDifference);

            if (debugGenomesComparison) {
                a.DebugConnectionsGenesLog();
                b.DebugConnectionsGenesLog();
                Debug.LogFormat("<b>| Disjoint: " + disjointCount + " | Excess: " + excessCount + " | Avg. Weight Diff: " + averageWeightDifference + " |</b> => <b>Distance: " + compatibilityDistance + "</b>\n");
            }

            return compatibilityDistance;
        }

        private float CompatibilityDistanceFunction(int excessCount, int disjointCount, int numberOfGenes, float averageWeightDifference) {
            return (ConfigNEAT.DISTANCE_EXCESS_COEFFICIENT * excessCount) / numberOfGenes +
                   (ConfigNEAT.DISTANCE_DISJOINT_COEFFICIENT * disjointCount) / numberOfGenes +
                    ConfigNEAT.DISTANCE_WEIGHT_COEFFICIENT * averageWeightDifference;
        }
        #endregion

        #region Field Methods
        public void AddGlobalNodeGene(NodeGene nodeGene) {
            if (ContainsGlobalNodeGene(nodeGene)) {
                Debug.LogError("Node is already within Global Node Pool");
                return;
            }

            _globalNodeGenes.Add(nodeGene.GetHashCode(), nodeGene);
        }

        public bool ContainsGlobalNodeGene(NodeGene nodeGene) {
            if (_globalNodeGenes.ContainsKey(nodeGene.GetHashCode()))
                return true;

            return false;
        }

        public int GetGlobalNodeGenesSize() {
            return _globalNodeGenes.Count;
        }

        public void AddGlobalConnectionGene(ConnectionGene connectionGene) {
            if (ContainsGlobalConnectionGene(connectionGene)) {
                Debug.LogError("Connection is already within Global Connection Pool");
                return;
            }

            _globalConnectionGenesCombination[connectionGene.InNode.Id, connectionGene.OutNode.Id] = connectionGene;
        }

        public bool ContainsGlobalConnectionGene(ConnectionGene connectionGene) {
            if (_globalConnectionGenesCombination[connectionGene.InNode.Id, connectionGene.OutNode.Id] != null)
                return true;
            return false;
        }

        public ConnectionGene GetGlobalConnectionGene(ConnectionGene connectionGene) {
            ConnectionGene cg = _globalConnectionGenesCombination[connectionGene.InNode.Id, connectionGene.OutNode.Id];
            if (cg == null)
                Debug.LogError("Could not find Connection Gene within Global Connection Pool");

            return cg;
        }
        #endregion

        #region Serialisation
        public void SaveModel(string directory = "") {
            string PATH = Application.dataPath + "/Resources/Neuroevolution/models/" + directory;

            GenomeData genome = new GenomeData(agent.brainNEAT);

            //Reading the Network model into a string.
            string jsonRep = JsonUtility.ToJson(genome);
            string filename = "NEAT" + "_" + agent.GetFitness() + ".txt";
            if (!Directory.Exists(PATH)) {
                Directory.CreateDirectory(PATH);
            }
            //Saving the file.
            File.WriteAllText(PATH + filename, jsonRep);

            #region Logging
            if (debugSerialisationLogging) {
                Debug.LogFormat("--------------");
                Debug.LogFormat("Logging: <b>Sucessfully Saved! Genome Genometype below</b>");
                agent.brainNEAT.DebugConnectionsGenesLog();
                agent.brainNEAT.DebugNodeGenesLog();
                Debug.LogFormat("--------------");
            }
            #endregion
        }

        public void AutoSaveModel(Genome genome, string directory = "") {
            string PATH = Application.dataPath + "/Resources/Neuroevolution/models/" + directory;

            GenomeData genomeData = new GenomeData(genome);

            //Reading the Network model into a string.
            string jsonRep = JsonUtility.ToJson(genomeData);
            string filename = "NEAT" + "_" + genome.Fitness + ".txt";
            if (!Directory.Exists(PATH)) {
                Directory.CreateDirectory(PATH);
            }
            //Saving the file.
            File.WriteAllText(PATH + filename, jsonRep);

            #region Logging
            if (debugSerialisationLogging) {
                Debug.LogFormat("--------------");
                Debug.LogFormat("Logging: <b>Sucessfully Auto Saved! Genome Genometype below</b>");
                genome.DebugConnectionsGenesLog();
                genome.DebugNodeGenesLog();
                Debug.LogFormat("--------------");
            }
            #endregion
        }

        public void LoadModel(string filename, string directory = "") {
            string PATH = Application.dataPath + "/Resources/Neuroevolution/models/" + directory;

            //Reading the json file from the input file path.
            //string jsonRep = File.ReadAllText(PATH + filename + ".txt");
            string jsonRep = Resources.Load<TextAsset>("Neuroevolution/models/" + directory + filename).text;

            Debug.Log("Forced Save State Number change");
            GameManager.Instance.saveStateNumber = 54;

            GenomeData model = JsonUtility.FromJson<GenomeData>(jsonRep);

            Genome genome = new Genome(model);
            agent.ResetAgent(genome);

            #region Logging
            if (debugSerialisationLogging) {
                Debug.LogFormat("--------------");
                Debug.LogFormat("Logging: <b>Sucessfully Loaded! Genome Genometype below</b>");
                genome.DebugConnectionsGenesLog();
                genome.DebugNodeGenesLog();
                Debug.LogFormat("--------------");
            }
            #endregion
        }

        [System.Serializable]
        public struct GenomeData
        {
            public List<NodeGeneData> nodeGenes;
            public List<ConnectionGeneData> connectionGenes;

            public GenomeData(Genome genome) {
                this.nodeGenes = genome.NodeGenes.Select(ng => new NodeGeneData(ng)).ToList();
                this.connectionGenes = genome.ConnectionGenes.Select(cg => new ConnectionGeneData(cg)).ToList();
            }
        }

        [System.Serializable]
        public struct NodeGeneData
        {
            public int id;
            public Layer layer;
            public int order;

            public NodeGeneData(NodeGene nodeGene) {
                this.id = nodeGene.Id;
                this.layer = nodeGene.Layer;
                this.order = nodeGene.Order;
            }
        }

        [System.Serializable]
        public struct ConnectionGeneData
        {
            public NodeGeneData inNode;
            public NodeGeneData outNode;
            public float weight;
            public int innovNr;
            public bool isEnabled;

            public ConnectionGeneData(ConnectionGene connectionGene) {
                this.inNode = new NodeGeneData(connectionGene.InNode);
                this.outNode = new NodeGeneData(connectionGene.OutNode);
                this.weight = connectionGene.Weight;
                this.innovNr = connectionGene.InnovNr;
                this.isEnabled = connectionGene.IsEnabled;
            }
        }
        #endregion

        #region Debug
        public void DebugPopulationLog(string before = "") {
            string formattedString = "";
            int totalGenomeCounter = 0;

            foreach (Species species in _species) {
                string speciesString = "<b><color=cyan>" +
                                       "ID " + species.Id + " \t" +
                                       "Genome Count: " + species.SpeciesPopulation.Count + " \t" +
                                       "Average Fit: " + species.AverageFitness +
                                       "</color></b>\n";

                foreach (Genome genome in species.SpeciesPopulation) {
                    string genomeString = "<b>" +
                                          "ID " + genome.Id + "\t" +
                                          "Normal Fit: " + genome.Fitness + "\t\t" +
                                          "Adjusted Fit: " + genome.AdjustedFitness + "</b>\n";
                    totalGenomeCounter++;
                    if (genome == species.Representative)
                        genomeString = "<color=green>" + genomeString + "</color>";
                    else if (genome.IsElite)
                        genomeString = "<color=orange>" + genomeString + "</color>";

                    speciesString += genomeString;
                }

                formattedString += speciesString + "\n";
            }

            bool begin = true;
            foreach (Genome genome in Population) {
                if (genome.BelongingSpecies == null) {
                    if (begin) {
                        string header = "<b><color=cyan>" +
                       "N/A \t" +
                       "Genome Count: " + Population.Count(g => g.BelongingSpecies == null) + " \t" +
                       "</color></b>\n";
                        begin = false;
                        formattedString += header;
                    }
                    totalGenomeCounter++;
                    string genomeString = "<b>" +
                      "ID " + genome.Id + "\t" +
                      "Normal Fit: " + genome.Fitness + "\t\t" +
                      "Adjusted Fit: " + genome.AdjustedFitness + "</b>\n";

                    formattedString += genomeString;
                }
            }

            Debug.LogFormat("Logging : <b>{0}</b> <color=cyan>Generation <b>({1})</b> | Species Pool <b>({2})</b></color> Average Fitness: <b>{3}</b> | [Debug] Total Genome Counter <b>({4})</b>\n\n{5}",
                before, (_generationCounter + 1), _species.Count, _species.Sum(s => s.AverageFitness), totalGenomeCounter, formattedString);
        }

        public void DebugGenomeComparisonLog(Genome a, Genome b) {
            string formattedString = "";
            string genomeAConnectionsString = "";
            string genomeBConnectionsString = "";

            ///<summary>
            /// Variables to determine if Connection Gene is 'Excess' or 'Disjoint' 
            ///</summary>
            int highestInnovNrA = a.ConnectionGenes[a.ConnectionGenes.Count - 1].InnovNr;
            int highestInnovNrB = b.ConnectionGenes[b.ConnectionGenes.Count - 1].InnovNr;

            ///<summary>
            /// Assigned by the Genome with the highest Connection Gene Count 
            ///</summary>
            int numberOfComparisions = highestInnovNrA >= highestInnovNrB ? highestInnovNrA : highestInnovNrB;

            for (int i = 0; i <= numberOfComparisions; i++) {

                ConnectionGene connectionGene;

                if (a.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {
                    connectionGene = a.ConnectionGenes.Find(cg => cg.InnovNr == i);
                    genomeAConnectionsString += "<b>| " +
                                                connectionGene.InnovNr + " " +
                                                connectionGene.InNode.Id + " -> " + connectionGene.OutNode.Id + " " +
                                                (connectionGene.IsEnabled ? "<color=green>E</color>" : "<color=red>D</color>") +
                                                " |</b>\t";
                } else
                    genomeAConnectionsString += "<b>| X X -> X X |</b>\t";

                if (b.ConnectionGenes.Exists(cg => cg.InnovNr == i)) {
                    connectionGene = b.ConnectionGenes.Find(cg => cg.InnovNr == i);
                    genomeBConnectionsString += "<b>| " +
                                                connectionGene.InnovNr + " " +
                                                connectionGene.InNode.Id + " -> " + connectionGene.OutNode.Id + " " +
                                                (connectionGene.IsEnabled ? "<color=green>E</color>" : "<color=red>D</color>") +
                                                " |</b>\t";
                } else
                    genomeBConnectionsString += "<b>| X X -> X X |</b>\t";
            }

            formattedString += genomeAConnectionsString + "\n";
            formattedString += genomeBConnectionsString + "\n";

            Debug.LogFormat(formattedString);
        }

        public void DebugGlobalNodeGenesLog() {
            string formattedString = "";

            foreach (KeyValuePair<int, NodeGene> element in _globalNodeGenes) {
                string nodeGene = "<b>" +
                                  "ID: " + element.Value.Id + " \t" +
                                  "Layer: " + element.Value.Layer.ToString() + " \t" +
                                  "</b>\n";

                formattedString += nodeGene;
            }

            formattedString += "\n";

            Debug.LogFormat("Logging : <color=red>{0}</color>\n\n{1}", "Global Node Gene Pool", formattedString);
        }

        public void DebugGlobalConnectionGenesLog() {
            string formattedString = "";

            foreach (KeyValuePair<int, ConnectionGene> element in _globalConnectionGenes) {
                string connectionGene = "<b>" +
                                        "Innov: " + element.Value.InnovNr + " \t\t" +
                                        "In: " + element.Value.InNode.Id + "\t" +
                                        "Out: " + element.Value.OutNode.Id + "\t" +
                                        "</b>\n";

                formattedString += connectionGene;
            }

            Debug.LogFormat("Logging : <color=red>{0}</color>\n\n{1}", "Global Connection Gene Pool", formattedString);
        }
        #endregion
    }
}