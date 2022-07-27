using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public enum Activation
{
    Sigmoid,
    Tanh,
    Heaveside
}

public abstract class Config : MonoBehaviour
{
    private static Config _instance;

    [Header("General")]
    [SerializeField] private int _inputNodeCount = 3;
    public static int INPUT_NODE_COUNT { get => _instance._inputNodeCount; }

    [SerializeField] private int _outputNodeCount = 2;
    public static int OUTPUT_NODE_COUNT { get => _instance._outputNodeCount; }

    [SerializeField] private int _populationCount = 20;
    public static int POPULATION_COUNT { get => _instance._populationCount; }

    [SerializeField] private int _maxGenerationCount = -1;
    public static int MAX_GENERATION_COUNT { get => _instance._maxGenerationCount; }

    [Header("Calculation")]
    [SerializeField] private Activation _activation = Activation.Sigmoid;
    public static Func<float, float> ACTIVATION {
        get {
            switch (_instance._activation) {
                case Activation.Sigmoid:
                    return Functions.Sigmoid;
                case Activation.Tanh:
                    return Functions.Tanh;
                case Activation.Heaveside:
                    return Functions.Heaveside;
                default:
                    Debug.LogError("Could not match Activation to its Function");
                    return null;
            }
        }
    }
    [SerializeField] private bool _distributeProbability = false;
    public static bool DISTRIBUTE_PROBABILITY { get => _instance._distributeProbability; }

    public virtual void Awake() {
        _instance = this;
    }
}
