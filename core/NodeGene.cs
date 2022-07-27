using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NEAT
{
    public enum Layer
    {
        Input,
        Hidden,
        Output,
    }

    public class NodeGene
    {
        public int Id { get; }

        public Layer Layer { get; }

        public int Order { get; set; }

        public float Output { get; set; }

        public NodeGene(int id, Layer layer, int order = default, float output = default) {
            Id = id;
            Layer = layer;
            Order = order;
            Output = output;
        }

        public NodeGene(NodeGene copy) {
            Id = copy.Id;
            Layer = copy.Layer;
            Order = copy.Order;
            Output = 0;
        }

        public void Activate(float x) {
            if (Layer.Equals(Layer.Input)) {
                return;
            }

            if (Layer.Equals(Layer.Output) && ConfigNEAT.DISTRIBUTE_PROBABILITY)
                Output = Functions.Exponential(x);
            else
                Output = ConfigNEAT.ACTIVATION(x);
        }

        public override bool Equals(object ob) {
            if (ob is NodeGene) {
                NodeGene ng = (NodeGene)ob;
                return Id == ng.Id;
            } else
                return false;
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }

        public override string ToString() {
            return "<b>" +
                   "ID: " + Id + " \t" +
                   "Layer: " + Layer.ToString() + " \t" +
                   "Order: " + Order + " \t" +
                   "Output: " + Output +
                   "</b>\n";
        }
    }
}