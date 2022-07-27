using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Random = UnityEngine.Random;

namespace NEAT
{
    public class Species
    {
        public int Id { get; private set; }
        public static int ID_COUNTER { get; set; }

        public List<Genome> SpeciesPopulation { get; private set; }

        public List<Genome> NewPopulation { get; private set; }

        public Genome Representative { get; private set; }

        public float AverageFitness { get; private set; }

        public float MaxAverageFitness { get; private set; }

        public int AllowedOffspring { get; set; }

        public int LastBestAverageFitness { get; private set; }

        public Species(Genome representative) {
            SpeciesPopulation = new List<Genome>();
            NewPopulation = new List<Genome>();

            Id = ID_COUNTER++;

            Representative = representative;
            AddGenome(representative);
        }

        public void Reset() {
            int randomIndex = Random.Range(0, SpeciesPopulation.Count - 1);
            Representative = SpeciesPopulation[randomIndex];

            foreach (Genome genome in SpeciesPopulation.ToList()) {
                if (genome != Representative)
                    RemoveGenome(genome);
            }

            AverageFitness = 0;
        }

        public void AddGenome(Genome genome) {
            SpeciesPopulation.Add(genome);
            genome.BelongingSpecies = this;
        }

        public void RemoveGenome(Genome genome) {
            if (Representative == genome)
                Representative = null;

            SpeciesPopulation.Remove(genome);
            genome.BelongingSpecies = null;
        }

        public void ShareFitness() {
            foreach (Genome genome in SpeciesPopulation)
                genome.AdjustedFitness = NEAT.Instance.FitnessSharing(genome);

            AverageFitness = SpeciesPopulation.Sum(g => g.AdjustedFitness) / SpeciesPopulation.Count;

            if (MaxAverageFitness < AverageFitness) {
                MaxAverageFitness = AverageFitness;
                LastBestAverageFitness = 0;
            } else {
                LastBestAverageFitness++;
            }
        }

        public void KillWorstOfPopulation() {
            SpeciesPopulation.Sort((a, b) => b.AdjustedFitness.CompareTo(a.AdjustedFitness));

            int survivor = Mathf.RoundToInt(Mathf.Ceil(SpeciesPopulation.Count * ConfigNEAT.TOP_PERCENTAGE_REPRODUCTION));

            int index = 0;
            foreach (Genome genome in SpeciesPopulation.ToList()) {
                if (index >= survivor) {
                    RemoveGenome(genome);
                    NEAT.Instance.Population.Remove(genome);
                }
                index++;
            }
        }

        public void KillLastPopulation() {
            foreach (Genome genome in SpeciesPopulation.ToList()) {
                if (!genome.IsElite) {
                    RemoveGenome(genome);
                }
            }

            SpeciesPopulation.AddRange(NewPopulation);
            NewPopulation.Clear();
        }

        public bool IsStagnating() {
            if (LastBestAverageFitness >= ConfigNEAT.MAX_STAGNATION_GENERATION_COUNT) {
                AllowedOffspring = 0;

                return true;
            }

            return false;
        }


        public bool IsGoingExtinct() {
            if (AllowedOffspring == 0) {
                foreach (Genome genome in SpeciesPopulation.ToList()) {
                    if (genome.IsElite)
                        genome.BelongingSpecies = null;
                    else
                        RemoveGenome(genome);
                }

                Representative = null;

                return true;
            }

            return false;
        }

        public Genome Repopulate() {
            Genome child = null;

            int lastGenerationGenomeCount = SpeciesPopulation.Count(g => g.AdjustedFitness > 0);

            ///<summary>
            /// Sexual Reproduction
            /// </summary>
            if (lastGenerationGenomeCount > 1) {

                ///<summary>
                /// Fitmess proportionate selection
                /// </summary>
                SpeciesPopulation.Sort((a, b) => a.AdjustedFitness.CompareTo(b.AdjustedFitness));

                List<(float, Genome)> probabilities = GetParentProbabilities();

                Genome parentA = Functions.RouletteWheelSelection(probabilities);

                probabilities = GetParentProbabilities(parentA);

                Genome parentB = Functions.RouletteWheelSelection(probabilities);

                child = NEAT.Instance.Crossover(parentA, parentB);
                child.Mutate();

                #region Debug Logging
                //Debug.Log("<b>SP</b> New Child " + child.Id + " from: " + parentA.Id + " + " + parentB.Id + " SID: " + Id);

                //parentA.DebugNodeGenesLog("Parent A");
                //parentA.DebugConnectionsGenesLog("Parent A");

                //parentB.DebugNodeGenesLog("Parent B");
                //parentB.DebugConnectionsGenesLog("Parent B");

                //child.DebugNodeGenesLog("Child");
                //child.DebugConnectionsGenesLog("Child");
                #endregion
            }

        ///<summary>
        /// Asexual Reproduction
        /// </summary>
            else {
                    Genome parent = SpeciesPopulation[0];
                    child = new Genome(parent);
                    child.ForceMutate();

                    #region Debug Logging
                    //Debug.Log("<b>AP:</b> New Child " + child.Id + " from: " + parent.Id + " SID: " + Id);
                    //parent.DebugConnectionsGenesLog();
                    //parent.DebugNodeGenesLog();

                    //child.DebugConnectionsGenesLog();
                    //child.DebugNodeGenesLog();
                    #endregion
                }

                child.BelongingSpecies = this;
                NewPopulation.Add(child);

                return child;
            }

        public List<(float, Genome)> GetParentProbabilities(Genome exceptionGenome = null) {
            List<(float, Genome)> probabilities = new List<(float, Genome)>();
            float previous_probability = 0;
            float maxFitness = SpeciesPopulation.Sum(g => g.AdjustedFitness) - (exceptionGenome == null ? 0 : exceptionGenome.AdjustedFitness);

            for (int i = 0; i < SpeciesPopulation.Count; i++) {

                ///<summary>
                /// Needed for Sexual Reproduction -> If one Parent is already chosen, it is within exceptionsGenomes to not be the second Parent.
                /// Exception Genomes are not included in the Probabilities.
                /// </summary>
                if (exceptionGenome != null) {
                    if (SpeciesPopulation[i] == exceptionGenome)
                        continue;
                }

                ///<summary>
                /// Do not include new Offsprings
                /// </summary>
                if (SpeciesPopulation[i].AdjustedFitness == 0) {
                    continue;
                }

                float probability = SpeciesPopulation[i].AdjustedFitness / maxFitness;
                float totalProbability = probability + previous_probability;

                probabilities.Add((totalProbability, SpeciesPopulation[i]));

                previous_probability = totalProbability;
            }

            return probabilities;
        }
    }
}