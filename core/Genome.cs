using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using MathNet.Numerics.Distributions;

using Random = UnityEngine.Random;

namespace NEAT
{
    public class Genome
    {
        public int Id { get; private set; }
        private static int _idCounter;

        public List<NodeGene> NodeGenes { get; }

        public List<ConnectionGene> ConnectionGenes { get; }

        public float Fitness { get; set; }

        public float AdjustedFitness { get; set; }

        public Species BelongingSpecies { get; set; }

        public bool IsElite { get; set; }

        public Genome() {
            Id = _idCounter++;
            NodeGenes = new List<NodeGene>();
            ConnectionGenes = new List<ConnectionGene>();
        }

        public Genome(Genome copy) : this() {
            foreach (NodeGene nodeGeneCopy in copy.NodeGenes)
                AddNodeGene(nodeGeneCopy);

            foreach (ConnectionGene connectionGeneCopy in copy.ConnectionGenes)
                AddConnectionGene(connectionGeneCopy);
        }

        public Genome(NEAT.GenomeData genomeData) : this() {
            foreach (NEAT.NodeGeneData nodeGeneData in genomeData.nodeGenes) {
                AddNodeGene(new NodeGene(nodeGeneData.id, nodeGeneData.layer, nodeGeneData.order));
            }

            foreach (NEAT.ConnectionGeneData cgd in genomeData.connectionGenes) {
                AddConnectionGene(new ConnectionGene(new NodeGene(cgd.inNode.id, cgd.inNode.layer),
                                                    new NodeGene(cgd.outNode.id, cgd.outNode.layer),
                                                    cgd.weight, cgd.innovNr, cgd.isEnabled));
            }
        }

        #region Initialization
        public void Initialize() {
            if (ConfigNEAT.NEAT_TYPE.Equals(NeatType.FS) && ConfigNEAT.START_WITH_HIDDEN_NODE) {
                Debug.LogError("Can't initialize FS-NEAT with Hidden Nodes");
                return;
            }

            ///<summary>
            /// Initialize Input Node Genes
            /// </summary>
            NodeGene biasUnit = new NodeGene(NodeGenes.Count, Layer.Input, 0, 1);
            AddNodeGene(biasUnit);

            for (int i = 0; i < ConfigNEAT.INPUT_NODE_COUNT; i++) {
                NodeGene inputNodeGene = new NodeGene(NodeGenes.Count, Layer.Input, 0);
                AddNodeGene(inputNodeGene);
            }

            switch (ConfigNEAT.NEAT_TYPE) {
                case NeatType.Regular:
                    InitializeRegularNEAT();
                    break;
                case NeatType.FS:
                    InitializeFSNEAT();
                    break;
                default:
                    Debug.LogError("Neat Type Error");
                    break;
            }

        }

        private void InitializeRegularNEAT() {

            ///<summary>
            /// Initialize Output and (if configured) Hidden Nodes
            /// </summary>
            for (int i = 0; i < ConfigNEAT.OUTPUT_NODE_COUNT; i++) {
                int outputOrder = ConfigNEAT.START_WITH_HIDDEN_NODE ? 2 : 1;
                NodeGene outputNodeGene = new NodeGene(NodeGenes.Count, Layer.Output, outputOrder);
                AddNodeGene(outputNodeGene);
            }

            if (ConfigNEAT.START_WITH_HIDDEN_NODE) {
                NodeGene hiddenNodeGene = new NodeGene(NodeGenes.Count, Layer.Hidden, 1);
                AddNodeGene(hiddenNodeGene);
            }

            ///<summary>
            /// Initalize Connection Genes
            /// </summary>
            if (ConfigNEAT.START_WITH_HIDDEN_NODE) {
                ConnectNodeGeneLayers(Layer.Input, Layer.Hidden);
                ConnectNodeGeneLayers(Layer.Hidden, Layer.Output);
            } else {
                ConnectNodeGeneLayers(Layer.Input, Layer.Output);
            }
        }

        private void InitializeFSNEAT() {

            ///<summary>
            /// Initialize Output Nodes
            /// </summary>
            for (int i = 0; i < ConfigNEAT.OUTPUT_NODE_COUNT; i++) {
                NodeGene outputNodeGene = new NodeGene(NodeGenes.Count, Layer.Output, 1);
                AddNodeGene(outputNodeGene);
            }

            ///<summary>
            /// Initalize Random Connection Gene
            /// </summary>
            List<NodeGene> inputNodes = NodeGenes.Where(ng => ng.Layer.Equals(Layer.Input)).ToList();
            List<NodeGene> outputNodes = NodeGenes.Where(ng => ng.Layer.Equals(Layer.Output)).ToList();

            NodeGene randomInputNode = inputNodes[Random.Range(0, inputNodes.Count - 1)];
            NodeGene randomOutputNode = outputNodes[Random.Range(0, outputNodes.Count - 1)];

            ConnectionGene connectionGene = new ConnectionGene(randomInputNode, randomOutputNode, Random.Range(-1f, 1f));
            AddConnectionGene(connectionGene);
        }

        private void ConnectNodeGeneLayers(Layer fromLayer, Layer toLayer) {
            foreach (NodeGene fromNode in NodeGenes) {

                if (fromNode.Layer.Equals(fromLayer)) {
                    foreach (NodeGene toNode in NodeGenes) {

                        if (toNode.Layer.Equals(toLayer)) {
                            ConnectionGene connectionGene = new ConnectionGene(fromNode, toNode, Random.Range(-1f, 1f));
                            AddConnectionGene(connectionGene);
                        }
                    }
                }
            }
        }
        #endregion

        #region Add to Structure
        private void ReOrder(NodeGene nodeGene) {
            foreach (ConnectionGene cg in ConnectionGenes) {
                ///<summary>
                /// Iteriere all Connection Genes of nodeGene
                /// </summary>
                if (cg.InNode.Id == nodeGene.Id) {
                    ///<summary>
                    /// If Outnode has same Order as nodeGene, increase it by 1
                    /// </summary>
                    if (cg.OutNode.Order == nodeGene.Order) {
                        cg.OutNode.Order = nodeGene.Order + 1;
                        ReOrder(cg.OutNode);
                    }
                }
                if (cg.OutNode.Id == nodeGene.Id) {
                    if (cg.InNode.Order == nodeGene.Order) {
                        cg.OutNode.Order = nodeGene.Order - 1;
                        ReOrder(cg.OutNode);
                    }
                }
            }
        }

        private void CheckOrder() {
            int skippedOrder = 0;
            int highestOrder = NodeGenes.Max(ng => ng.Order);

            List<NodeGene> tmpNodeGenes = new List<NodeGene>();
            tmpNodeGenes.AddRange(NodeGenes);
            tmpNodeGenes.Sort((a, b) => a.Order.CompareTo(b.Order));

            foreach (NodeGene ng in tmpNodeGenes) {
                if (ng.Order == skippedOrder) {
                    skippedOrder++;
                }
            }

            if (skippedOrder > highestOrder)
                skippedOrder = highestOrder;

            if (skippedOrder != highestOrder) {
                int lastOrderAfterSkip = 0;
                int difference = highestOrder;

                foreach (NodeGene ng in NodeGenes) {
                    if (ng.Order > skippedOrder) {
                        int tmpTdif = ng.Order - skippedOrder;
                        if (tmpTdif < difference) {
                            lastOrderAfterSkip = ng.Order;
                            difference = tmpTdif;
                        }
                    }
                }

                foreach (NodeGene ng in NodeGenes) {
                    if (ng.Order >= lastOrderAfterSkip)
                        ng.Order -= difference;
                }
            }
        }

        public bool LoopExists(NodeGene from, NodeGene to, int n) {
            if (n >= NodeGenes.Max(ng => ng.Order))
                return false;

            foreach (ConnectionGene cg in ConnectionGenes) {
                if (cg.InNode.Id == to.Id) {
                    if (cg.OutNode.Id == from.Id) {
                        Debug.Log("Loop " + n);
                        return true;
                    }
                }
            }

            bool exists = false;

            foreach (ConnectionGene cg in ConnectionGenes) {
                if (cg.OutNode.Id == to.Id && cg.InNode.Id != from.Id)
                    exists = LoopExists(cg.InNode, to, n + 1);
                if (exists)
                    return true;
            }

            return false;
        }

        public ConnectionGene AddConnectionGene(ConnectionGene connectionGene) {
            NodeGene inNode;
            NodeGene outNode;

            if (!NodeGenes.Exists(ng => ng.Id == connectionGene.InNode.Id))
                inNode = AddNodeGene(connectionGene.InNode);
            else
                inNode = NodeGenes.Find(ng => ng.Id == connectionGene.InNode.Id);

            if (!NodeGenes.Exists(ng => ng.Id == connectionGene.OutNode.Id))
                outNode = AddNodeGene(connectionGene.OutNode);
            else
                outNode = NodeGenes.Find(ng => ng.Id == connectionGene.OutNode.Id);

            ///<summary>
            /// Hidden Nodes without In Nodes have Order 1
            /// </summary>
            inNode.Order = inNode.Layer.Equals(Layer.Hidden) ? (inNode.Order == 0 ? 1 : inNode.Order) : inNode.Order;

            ///<summary>
            /// Update Order of Out Node in regard to its In Node
            /// </summary>
            bool isRecurrent = false;
            if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Feedforwad)) {
                if (outNode.Order <= inNode.Order) {
                    outNode.Order = inNode.Order + 1;
                    ReOrder(outNode);
                    CheckOrder();
                }
            } else if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Recurrent)) {
                if (outNode.Order <= inNode.Order) {
                    if (!outNode.Layer.Equals(Layer.Input) && !inNode.Layer.Equals(Layer.Output) && outNode.Id != inNode.Id) {
                        if (!LoopExists(inNode, outNode, 0)) {
                            outNode.Order = inNode.Order + 1;
                            ReOrder(outNode);
                            CheckOrder();
                        } else {
                            isRecurrent = true;
                        }
                    }
                }

                if (outNode.Order <= inNode.Order)
                    isRecurrent = true;
            }

            ///<summary>
            /// Update all Orders of Output Nodes if Out Node Layer is 'Hidden' or 'Input'
            /// </summary>
            int highestOrder = NodeGenes.Where(ng => !ng.Layer.Equals(Layer.Output)).Max(ng => ng.Order);

            if (!outNode.Layer.Equals(Layer.Output)) {
                foreach (NodeGene nodeGene in NodeGenes) {
                    if (nodeGene.Layer.Equals(Layer.Output)) {
                        nodeGene.Order = highestOrder + 1;
                    }
                }
            }

            ConnectionGene newConnectionGene = null;

            if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Feedforwad)) {
                newConnectionGene = new ConnectionGene(inNode, outNode, connectionGene.Weight, connectionGene.InnovNr, connectionGene.IsEnabled);
            } else if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Recurrent)) {
                newConnectionGene = new ConnectionGene(inNode, outNode, connectionGene.Weight, connectionGene.InnovNr, connectionGene.IsEnabled, isRecurrent);
            }

            if (!NEAT.Instance.ContainsGlobalConnectionGene(newConnectionGene)) {
                newConnectionGene.InnovNr = NEAT.Instance.GlobalInnovNr;
                NEAT.Instance.AddGlobalConnectionGene(newConnectionGene);
            } else {
                newConnectionGene.InnovNr = NEAT.Instance.GetGlobalConnectionGene(newConnectionGene).InnovNr;
            }

            if (ConnectionGenes.Exists(cg => cg.InnovNr == newConnectionGene.InnovNr)) {
                Debug.LogError("Connection Gene: <b>" + newConnectionGene.InnovNr + " (Innov) | " + newConnectionGene.InNode.Id + " -> " + newConnectionGene.OutNode.Id + "</b> is already existing within Internal Connection Gene Pool");
                DebugNodeGenesLog();
                DebugConnectionsGenesLog();
                ConnectionGene existingCG = ConnectionGenes.Find(cg => cg.InnovNr == newConnectionGene.InnovNr);
                Debug.LogError("INNOV 4 EXISTING (" + existingCG.InNode.Id + " -> " + existingCG.OutNode.Id + ") HASCODE: " + existingCG.GetHashCode() + " | INNO 4 WANT TO ADD (" + newConnectionGene.InNode.Id + " -> " + newConnectionGene.OutNode.Id + ") HASHCODE: " + newConnectionGene.GetHashCode());
                return null;
            }

            ConnectionGenes.Add(newConnectionGene);

            return newConnectionGene;
        }

        public NodeGene AddNodeGene(NodeGene nodeGene) {
            if (NodeGenes.Exists(ng => ng.Id == nodeGene.Id)) {
                Debug.LogError("Node Gene: <b>" + nodeGene.Id + " (Id)</b> is already existing within Internal Node Gene Pool");
                return null;
            }

            NodeGene newNodeGene = new NodeGene(nodeGene.Id, nodeGene.Layer);

            if (nodeGene.Id == 0) {
                newNodeGene.Order = 0;
                newNodeGene.Output = 1;
            }

            if (nodeGene.Layer.Equals(Layer.Output))
                newNodeGene.Order = nodeGene.Order;

            if (!NEAT.Instance.ContainsGlobalNodeGene(newNodeGene))
                NEAT.Instance.AddGlobalNodeGene(newNodeGene);

            NodeGenes.Add(newNodeGene);

            return newNodeGene;
        }
        #endregion

        #region Evaluation
        public List<float> Evaluate(List<float> inputValues) {
            if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Feedforwad))
                return EvaluateFFNN(inputValues);
            else if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Recurrent))
                return EvaluteRNN(inputValues);

            return null;
        }

        private List<float> EvaluteRNN(List<float> inputValues) {
            if (inputValues.Count != ConfigNEAT.INPUT_NODE_COUNT) {
                Debug.LogError("Amount of Input Values <b>(" + inputValues.Count + ")</b> does not match Input Node Count <b>(" + ConfigNEAT.INPUT_NODE_COUNT + ")</b>");
                return null;
            }

            List<NodeGene> outputNodes = new List<NodeGene>();

            ///<summary>
            /// Sort NodeGenes by 'Order'
            /// </summary>
            NodeGenes.Sort((a, b) => a.Order.CompareTo(b.Order));

            ///<summary>
            /// Calculate the Output of the Nodes; 
            /// Input Nodes are initialized with Parameter 'inputValues'
            /// </summary>
            int index = 0;
            foreach (NodeGene nodeGene in NodeGenes) {

                ///<summary>
                /// Node Gene with Id = 0 is reserverd for Bias Unit
                /// </summary>
                if (nodeGene.Id == 0)
                    nodeGene.Output = 1;

                if (nodeGene.Layer.Equals(Layer.Input) && nodeGene.Id != 0) {
                    nodeGene.Output = inputValues[index];
                    index++;
                }
            }

            for (int i = 0; i < NodeGenes.Count; i++) {
                if (NodeGenes[i].Layer.Equals(Layer.Output))
                    outputNodes.Add(NodeGenes[i]);

                CalculateOutput(NodeGenes[i]);
            }

            return outputNodes.Select(o => o.Output).ToList();
        }

        private List<float> EvaluateFFNN(List<float> inputValues) {
            if (inputValues.Count != ConfigNEAT.INPUT_NODE_COUNT) {
                Debug.LogError("Amount of Input Values <b>(" + inputValues.Count + ")</b> does not match Input Node Count <b>(" + ConfigNEAT.INPUT_NODE_COUNT + ")</b>");
                return null;
            }

            List<NodeGene> outputNodes = new List<NodeGene>();

            ///<summary>
            /// Sort NodeGenes by 'Order'
            /// </summary>
            NodeGenes.Sort((a, b) => a.Order.CompareTo(b.Order));

            ///<summary>
            /// Calculate the Output of the Nodes; 
            /// Input Nodes are initialized with Parameter 'inputValues'
            /// </summary>
            int index = 0;
            List<NodeGene> inputNodeGenes = NodeGenes.Where(ng => ng.Layer.Equals(Layer.Input)).ToList();
            inputNodeGenes.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (NodeGene nodeGene in inputNodeGenes) {

                ///<summary>
                /// Node Gene with Id = 0 is reserved for Bias Unit
                /// </summary>
                if (nodeGene.Id != 0) {
                    //Debug.Log(nodeGene.Id + " = " + index);
                    nodeGene.Output = inputValues[index];
                    index++;
                }
            }

            for (int i = 0; i < NodeGenes.Count; i++) {
                if (NodeGenes[i].Layer.Equals(Layer.Input))
                    continue;

                if (NodeGenes[i].Layer.Equals(Layer.Output))
                    outputNodes.Add(NodeGenes[i]);

                CalculateOutput(NodeGenes[i]);
            }

            ///<summary>
            /// Softmax Function
            /// </summary>
            if (ConfigNEAT.DISTRIBUTE_PROBABILITY) {
                float sum = outputNodes.Sum(o => o.Output);
                for (int i = 0; i < outputNodes.Count; i++) {
                    outputNodes[i].Output = outputNodes[i].Output / sum;
                }
            }

            outputNodes.Sort((a, b) => a.Id.CompareTo(b.Id));

            return outputNodes.Select(o => o.Output).ToList();
        }

        private void CalculateOutput(NodeGene nodeGene) {
            float outputValue = default;
            string calculation = "";

            foreach (ConnectionGene connectionGene in ConnectionGenes) {
                if (connectionGene.OutNode.Id == nodeGene.Id && connectionGene.IsEnabled) {
                    outputValue += connectionGene.InNode.Output * connectionGene.Weight;
                    string cal = outputValue + " = " + connectionGene.InNode.Output + " (" + connectionGene.InNode.Id + ") " + " * " + connectionGene.Weight;
                    calculation += cal + "\n";
                }
            }

            if (nodeGene.Layer.Equals(Layer.Input)) {
                nodeGene.Output += outputValue;
            } else {
                nodeGene.Activate(outputValue);
            }

        }

        /// <summary>
        /// Reset Output off all Nodes to default (0), except for Bias Unit
        /// </summary>
        public void HardResetNodes() {
            foreach (NodeGene nodeGene in NodeGenes) {
                if (nodeGene.Id == 0)
                    continue;

                nodeGene.Output = default;
            }
        }
        #endregion

        #region Mutation
        public void Mutate() {

            if (ConfigNEAT.MUTATE_NEW_CONNECTION_RATE >= Random.Range(0f, 1f))
                MutateConnection();

            if (ConfigNEAT.MUTATE_NEW_NODE_RATE >= Random.Range(0f, 1f))
                MutateNode();

            if (ConfigNEAT.MUTATE_WEIGHT_RATE >= Random.Range(0f, 1f))
                MutateWeight();

            if (ConfigNEAT.MUTATE_CONNECTION_VALIDITY_RATE >= Random.Range(0f, 1f))
                MutateConnectionValidity();
        }

        public void ForceMutate() {
            List<(float, Action)> mutationProbabilities = GetMutationProbabilities();
            Action mutationAction = Functions.RouletteWheelSelection(mutationProbabilities);

            mutationAction.Invoke();
        }

        public List<(float, Action)> GetMutationProbabilities() {
            List<(float, Action)> probabilities = new List<(float, Action)>();
            List<(float, Action)> mutationRates = new List<(float, Action)>();

            if (ConfigNEAT.MUTATE_NEW_CONNECTION_RATE > 0)
                mutationRates.Add((ConfigNEAT.MUTATE_NEW_CONNECTION_RATE, MutateConnection));
            if (ConfigNEAT.MUTATE_NEW_NODE_RATE > 0)
                mutationRates.Add((ConfigNEAT.MUTATE_NEW_NODE_RATE, MutateNode));
            if (ConfigNEAT.MUTATE_WEIGHT_RATE > 0)
                mutationRates.Add((ConfigNEAT.MUTATE_WEIGHT_RATE, MutateWeight));
            if (ConfigNEAT.MUTATE_CONNECTION_VALIDITY_RATE > 0)
                mutationRates.Add((ConfigNEAT.MUTATE_CONNECTION_VALIDITY_RATE, MutateConnectionValidity));

            if (mutationRates.Count == 0) {
                Debug.LogError("All Mutation Rates are Zero %. Please have one parameter above 0.");
                return null;
            }

            float previous_probability = 0;

            float mutationRateSum = ConfigNEAT.MUTATE_NEW_CONNECTION_RATE + ConfigNEAT.MUTATE_NEW_NODE_RATE +
                                   ConfigNEAT.MUTATE_WEIGHT_RATE + ConfigNEAT.MUTATE_CONNECTION_VALIDITY_RATE;

            for (int i = 0; i < mutationRates.Count; i++) {
                float probability = mutationRates[i].Item1 / mutationRateSum;
                float totalProbability = probability + previous_probability;

                probabilities.Add((totalProbability, mutationRates[i].Item2));

                previous_probability = totalProbability;
            }

            return probabilities;
        }

        public void MutateNode() {
            int randomIndex = Random.Range(0, ConnectionGenes.Count - 1);

            ConnectionGene connectionToSplit = ConnectionGenes[randomIndex];
            connectionToSplit.IsEnabled = false;

            NodeGene newNodeGene = new NodeGene(NEAT.Instance.GetGlobalNodeGenesSize(), Layer.Hidden);

            ConnectionGene newConnection = new ConnectionGene(connectionToSplit.InNode, newNodeGene, 1);
            ConnectionGene oldConnection = new ConnectionGene(newNodeGene, connectionToSplit.OutNode, connectionToSplit.Weight);

            AddNodeGene(newNodeGene);
            AddConnectionGene(newConnection);
            AddConnectionGene(oldConnection);
        }

        public void MutateConnection() {
            List<ConnectionGene> possibleConnections = new List<ConnectionGene>();

            if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Feedforwad)) {

                foreach (NodeGene inNode in NodeGenes) {

                    if (!inNode.Layer.Equals(Layer.Output)) {

                        foreach (NodeGene outNode in NodeGenes) {

                            if (!outNode.Layer.Equals(Layer.Input)) {

                                if (outNode.Id != inNode.Id) {

                                    bool connectionExists = false;

                                    foreach (ConnectionGene connectionGene in ConnectionGenes) {

                                        if ((connectionGene.InNode.Id == inNode.Id && connectionGene.OutNode.Id == outNode.Id) ||
                                            (connectionGene.OutNode.Id == inNode.Id && connectionGene.InNode.Id == outNode.Id)) {
                                            connectionExists = true;
                                            break;
                                        }
                                    }

                                    if (!connectionExists) {
                                        possibleConnections.Add(new ConnectionGene(inNode, outNode, Random.Range(-1f, 1f)));
                                    }
                                }
                            }
                        }
                    }
                }
            } else if (ConfigNEAT.NETWORK_TYPE.Equals(NetworkType.Recurrent)) {

                foreach (NodeGene inNode in NodeGenes) {

                    foreach (NodeGene outNode in NodeGenes) {

                        if (inNode.Layer.Equals(Layer.Input) && outNode.Layer.Equals(Layer.Input))
                            continue;

                        bool connectionExists = false;

                        foreach (ConnectionGene connectionGene in ConnectionGenes) {

                            if ((connectionGene.InNode.Id == inNode.Id && connectionGene.OutNode.Id == outNode.Id)) {
                                connectionExists = true;
                                break;
                            }
                        }

                        if (!connectionExists) {
                            possibleConnections.Add(new ConnectionGene(inNode, outNode, Random.Range(-1f, 1f)));
                        }
                    }
                }
            }

            if (possibleConnections.Count > 0) {
                int randomIndex = Random.Range(0, possibleConnections.Count - 1);
                AddConnectionGene(possibleConnections[randomIndex]);
            }
        }

        public void MutateWeight() {
            foreach (ConnectionGene connectionGene in ConnectionGenes) {
                if (!connectionGene.IsEnabled)
                    continue;

                if (ConfigNEAT.MUTATE_WEIGHT_PERTURBE_RATE >= Random.Range(0f, 1f)) {
                    double mean = 0;
                    double stdDev = 0.5;
                    MathNet.Numerics.Distributions.Normal normalDist = new Normal(mean, stdDev);
                    double randomGaussianValue = normalDist.Sample();

                    connectionGene.Weight += (float)randomGaussianValue;
                } else {
                    connectionGene.Weight = Random.Range(-1f, 1f);
                }
            }
        }

        public void MutateConnectionValidity() {
            int randomIndex = Random.Range(0, ConnectionGenes.Count - 1);
            ConnectionGene connectionGeneToMutate = ConnectionGenes[randomIndex];

            connectionGeneToMutate.IsEnabled = !connectionGeneToMutate.IsEnabled;
        }
        #endregion

        #region Debug
        public void DebugNodeGenesLog(string before = "") {
            string formattedString = "";

            foreach (NodeGene nodeGene in NodeGenes) {
                formattedString += nodeGene.ToString();
            }

            Debug.LogFormat("Logging : <b>{0}</b> <color=orange><b>ID {1}</b> Internal Node Gene Pool <b>({2})</b></color>\n\n{3}", before, Id, NodeGenes.Count, formattedString);
            ;
        }

        public void DebugConnectionsGenesLog(string before = "") {
            string formattedString = "";

            foreach (ConnectionGene connectionGene in ConnectionGenes) {
                formattedString += connectionGene.ToString();
            }

            Debug.LogFormat("Logging : <b>{0}</b> <color=orange><b>ID {1}</b> Internal Connection Gene Pool <b>({2})</b></color>\n\n{3}", before, Id, ConnectionGenes.Count, formattedString);
        }
        #endregion
    }
}