using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NEAT
{   
    public enum NetworkType
    {
        Feedforwad,
        Recurrent
    }

    public enum NeatType
    {
        Regular,
        FS
    }

    public class ConfigNEAT : Config
    {
        private static ConfigNEAT _instance;

        [Header("Genotype")]
        [SerializeField] private bool _startWithHiddenNode = false;
        public static bool START_WITH_HIDDEN_NODE { get => _instance._startWithHiddenNode; }

        [SerializeField] private NeatType _neatType = NeatType.Regular;
        public static NeatType NEAT_TYPE { get => _instance._neatType; }

        [SerializeField] private NetworkType _networkType = NetworkType.Feedforwad;
        public static NetworkType NETWORK_TYPE { get => _instance._networkType; }

        [SerializeField] private int _globalConnectionGeneSize = 1000;
        public static int GLOBA_CONNECTION_GENE_SIZE { get => _instance._globalConnectionGeneSize; }

        [Header("New Generation")]
        [SerializeField] private bool _isElitismEnabled = true;
        public static bool IS_ELITISM_ENABLED { get => _instance._isElitismEnabled; }

        [SerializeField] private int _numberOfElites = 1;
        public static int NUMBER_OF_ELITES { get => _instance._numberOfElites; }

        [SerializeField] private float _topPercentageReproduction = 0.2f;
        public static float TOP_PERCENTAGE_REPRODUCTION { get => _instance._topPercentageReproduction; }

        [Header("Crossover")]
        [SerializeField] private float _inheritedGeneRemainsDisabledRate = 0.75f;
        public static float INHERITED_GENE_REMAINS_DISABLED_RATE { get => _instance._inheritedGeneRemainsDisabledRate; }

        [Header("Mutation")]
        [SerializeField] private float _mutateNewNodeRate = 0.03f;
        public static float MUTATE_NEW_NODE_RATE { get => _instance._mutateNewNodeRate; }

        [SerializeField] private float _mutateNewConnectionRate = 0.05f;
        public static float MUTATE_NEW_CONNECTION_RATE { get => _instance._mutateNewConnectionRate; }

        [SerializeField] private float _mutateWeightRate = 0.8f;
        public static float MUTATE_WEIGHT_RATE { get => _instance._mutateWeightRate; }

        [SerializeField] private float _mutateWeightPerturbeRate = 0.9f;
        public static float MUTATE_WEIGHT_PERTURBE_RATE { get => _instance._mutateWeightPerturbeRate; }

        [SerializeField] private float _mutateConnectionValidityRate = 0.05f;
        public static float MUTATE_CONNECTION_VALIDITY_RATE { get => _instance._mutateConnectionValidityRate; }

        [Header("Speciation")]
        [SerializeField] private bool _IsSpeciationEnabled = true;
        public static bool IS_SPECIATION_ENABLED { get => _instance._IsSpeciationEnabled; }

        [SerializeField] private int _maxStagnationGenerationCount = 10;
        public static int MAX_STAGNATION_GENERATION_COUNT { get => _instance._maxStagnationGenerationCount; }

        [SerializeField] private int _speciesTarget = 5;
        public static int SPECIES_TARGET { get => _instance._speciesTarget; }

        [SerializeField] private float _distanceChange = 0.5f;
        public static float DISTANCE_CHANGE { get => _instance._distanceChange; }

        [SerializeField] private float _distanceThreshold = 3;
        public static float DISTANCE_THRESHOLD { get => _instance._distanceThreshold; set => _instance._distanceThreshold = value; }

        [SerializeField] private float _distanceExcessCoefficient = 1f;
        public static float DISTANCE_EXCESS_COEFFICIENT { get => _instance._distanceExcessCoefficient; }

        [SerializeField] private float _distanceDisjointCoefficient = 1f;
        public static float DISTANCE_DISJOINT_COEFFICIENT { get => _instance._distanceDisjointCoefficient; }

        [SerializeField] private float _distanceWeightCoefficient = 1f;
        public static float DISTANCE_WEIGHT_COEFFICIENT { get => _instance._distanceWeightCoefficient; }

        public override void Awake() {
            base.Awake();
            _instance = this;
        }
    }
}
