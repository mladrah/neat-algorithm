using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace NEAT
{
    public class ConnectionGene
    {
        public NodeGene InNode { get; }

        public NodeGene OutNode { get; }

        public float Weight { get; set; }

        public int InnovNr { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsRecurrent { get; set; }


        public ConnectionGene(NodeGene inNode, NodeGene outNode, float weight, int innovNr = default, bool isEnabled = true, bool isRecurrent = false) {
            InNode = inNode;
            OutNode = outNode;
            Weight = weight;
            InnovNr = innovNr;
            IsEnabled = isEnabled;
            IsRecurrent = isRecurrent;
        }

        public override bool Equals(object ob) {
            if (ob is ConnectionGene) {
                ConnectionGene cg = (ConnectionGene)ob;
                return InNode.Equals(cg.InNode) && OutNode.Equals(cg.OutNode);
            } else
                return false;
        }

        public override int GetHashCode() {
            return 100 * InNode.GetHashCode() + OutNode.GetHashCode();
        }

        public override string ToString() {
            return "<b>" +
                    "In: " + InNode.Id + "\t" +
                    "Out: " + OutNode.Id + "\t" +
                    "Weight: " + Weight + "\t\t" +
                    (IsEnabled ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>") + "\t" +
                    "Innov: " + InnovNr + " \t" +
                    (IsRecurrent ? "<color=magenta>Recurrent</color>" : "") +
                    "</b>\n";
        }
    }
}

