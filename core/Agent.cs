using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NEAT;

public abstract class Agent : MonoBehaviour
{
    public Genome brainNEAT;

    public List<float> input;
    public List<float> output;
    public float fitness;

    public virtual void Awake() {
        input = new List<float>();
        output = new List<float>();
    }

    public abstract List<float> UpdateInput();
    public abstract float CalculateFitness();
    public abstract void ExecuteAction();
    public abstract void OnResetAgent();

    public void Think() {
        input = UpdateInput();
        CalculateOutput();
        ExecuteAction();
    }

    public void UpdateFitness() {
        this.fitness = CalculateFitness();
    }
    public void Next() {
        if (NEAT.NEAT.Instance.GenerationCounter == ConfigNEAT.MAX_GENERATION_COUNT) {
            OnResetAgent();
            UIManagerNEAT.Instance.StopTime();
            Time.timeScale = 0;
        } else {
            if (this.brainNEAT != null)
                NEAT.NEAT.Instance.Lost(GetFitness());
        }
    }

    public virtual void CalculateOutput() {
        output = brainNEAT.Evaluate(input);
    }

    public void ResetAgent(Genome genome) {
        this.fitness = 0;
        this.brainNEAT = genome;
        UIManagerNEAT.Instance.BuildNN(genome);

        OnResetAgent();
    }

    public float GetFitness() {
        return this.fitness;
    }
}