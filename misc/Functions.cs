using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using NEAT;

using Random = UnityEngine.Random;

public class Functions : MonoBehaviour
{
    public static Matrix<float> Tanh(Matrix<float> m) {
        return ActivateMatrix(m, x => Tanh(x));
    }

    public static float Tanh(float x) {
        double xDouble = x;
        return (float) Math.Tanh(xDouble);
    }

    public static Matrix<float> Sigmoid(Matrix<float> m) {
        return ActivateMatrix(m, (x) => Sigmoid(x));
    }

    public static float Sigmoid(float x) {
        return 1 / (1 + Mathf.Exp(-x));
    }

    public static Matrix<float> Heaveside(Matrix<float> m) {
        return ActivateMatrix(m, (x) => Heaveside(x));
    }

    public static float Heaveside(float x) {
        return x >= 0 ? 1 : 0;
    }

    private static Matrix<float> ActivateMatrix(Matrix<float> m, Func<float, float> ActivationFunction) {
        Matrix<float> newMatrix = m;

        for (int i = 0; i < m.RowCount; i++) {
            for (int j = 0; j < m.ColumnCount; j++) {
                newMatrix[i, j] = ActivationFunction(m[i, j]);
            }
        }

        return newMatrix;
    }

    public static Matrix<float> SoftmaxFunction(Matrix<float> m) {
        Matrix<float> newMatrix = m;
        float sum = SumSoftmaxFunction(m);

        for (int i = 0; i < m.RowCount; i++) {
            for (int j = 0; j < m.ColumnCount; j++) {
                newMatrix[i, j] = Exponential(m[i, j]) / sum;
            }
        }

        return newMatrix;
    }

    private static float SumSoftmaxFunction(Matrix<float> m) {
        float sum = 0;

        for (int i = 0; i < m.RowCount; i++) {
            for (int j = 0; j < m.ColumnCount; j++) {
                sum += Exponential(m[i, j]);
            }
        }

        return sum;
    }

    public static float Exponential(float x) {
        return Mathf.Exp(x);
    }

    public static T RouletteWheelSelection<T>(List<(float, T)> probabilities) {
        float randomNum = Random.Range(0f, 1f);

        ///<summary>
        /// Sort Probabilities Ascending
        /// </summary>
        probabilities.Sort();

        for (int i = 0; i < probabilities.Count; i++) {
            if (randomNum < probabilities[i].Item1) {
                return probabilities[i].Item2;
            }
        }

        Debug.LogError("Probability does not match" +
                      (probabilities[0].Item2 is Genome ? " -> Single Parent Crossover not allowed?" : ""));

        return (T)Convert.ChangeType(probabilities, typeof(T));
    }
}
