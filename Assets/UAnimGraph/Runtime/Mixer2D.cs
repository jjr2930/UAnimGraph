/*
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    /// <summary>
    /// TODO: Inherit from MixerBase
    /// </summary>
    [Serializable]
    public class Mixer2D : PlayableElement
    {
        [Serializable]
        public class Mixer2DElement
        {
            [SerializeField] ClipPlayer clipPlayer;
            [SerializeField] Vector2 threshold;
            public ClipPlayer ClipPlayer => clipPlayer;
            public Vector2 Threshold => threshold;
        }


        [SerializeField] List<Mixer2DElement> inputs;
        // Retained for compatibility with existing serialized assets. Thresholds
        // define the blend space; values outside it are handled by interpolation.
        [SerializeField] MinMaxFloat horizontalMinMax;
        [SerializeField] MinMaxFloat verticalMinMax;
        [SerializeField] float localTimeScale = 1f;

        AnimationMixerPlayable mixer;
        float[] weights;

        ~Mixer2D()
        {
            inputs = new List<Mixer2DElement>();
            horizontalMinMax = new MinMaxFloat(-1f, 1f);
            verticalMinMax = new MinMaxFloat(-1f, 1f);

            mixer.Destroy();
        }

        public AnimationMixerPlayable BuildMixer2D(PlayableGraph playableGraph)
        {
            if (!playableGraph.IsValid())
                throw new ArgumentException("A valid PlayableGraph is required.", nameof(playableGraph));

            int count = inputs == null ? 0 : inputs.Count;
            for (int i = 0; i < count; i++)
            {
                if (inputs[i] == null || inputs[i].ClipPlayer == null || inputs[i].ClipPlayer.GetClip() == null)
                    throw new InvalidOperationException($"Mixer2D input {i} requires an animation clip.");
                if (!IsFinite(inputs[i].Threshold))
                    throw new InvalidOperationException($"Mixer2D input {i} requires a finite threshold.");
            }

            mixer = AnimationMixerPlayable.Create(playableGraph, count);
            weights = new float[count];
            for (int i = 0; i < count; i++)
            {
                AnimationClipPlayable clipPlayable
                    = inputs[i].ClipPlayer.BuildClipPlayable(playableGraph);

                playableGraph.Connect(clipPlayable, 0, mixer, i);
            }

            SetWeights(Vector2.zero);
            return mixer;
        }

        public void SetWeights(Vector2 value)
        {
            if (!mixer.IsValid() || inputs == null)
                return;
            if (!IsFinite(value))
                throw new ArgumentException("Mixer2D value must be finite.", nameof(value));

            Array.Clear(weights, 0, weights.Length);
            if (weights.Length == 0)
                return;

            int nearest = 0;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < inputs.Count; i++)
            {
                float distance = (value - inputs[i].Threshold).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
                if (distance == 0f)
                {
                    weights[i] = 1f;
                    ApplyWeights();
                    return;
                }
            }

            float sum = 0f;
            for (int i = 0; i < inputs.Count; i++)
            {
                Vector2 sampleI = inputs[i].Threshold;
                // Duplicate thresholds use the first input, consistently with
                // the exact-sample case, without dividing by a zero band length.
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                    if ((sampleI - inputs[j].Threshold).sqrMagnitude == 0f)
                    {
                        duplicate = true;
                        break;
                    }
                if (duplicate)
                    continue;

                float weight = 1f;
                for (int j = 0; j < inputs.Count; j++)
                {
                    if (i == j)
                        continue;
                    Vector2 sampleJ = inputs[j].Threshold;
                    if ((sampleI - sampleJ).sqrMagnitude == 0f)
                        continue;
                    weight = Mathf.Min(weight, CalculateBandWeight(sampleI, sampleJ, value));
                    if (weight <= 0f)
                    {
                        weight = 0f;
                        break;
                    }
                }
                weights[i] = weight;
                sum += weight;
            }

            if (sum > 0f && !float.IsNaN(sum) && !float.IsInfinity(sum))
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] /= sum;
            }
            else
            {
                Array.Clear(weights, 0, weights.Length);
                weights[nearest] = 1f;
            }
            ApplyWeights();
        }

        // Adapted from Locomotion System's PolarGradientBandInterpolator.
        // Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS.
        // See the original project's "TERMS OF USE" for licensing details.
        static float CalculateBandWeight(Vector2 sampleI, Vector2 sampleJ, Vector2 value)
        {
            float angleIJ;
            float angleIO;
            float angleMultiplier = 2f;
            if (sampleI.sqrMagnitude == 0f)
            {
                angleIJ = Vector2.Angle(value, sampleJ) * Mathf.Deg2Rad;
                angleIO = 0f;
                angleMultiplier = 1f;
            }
            else if (sampleJ.sqrMagnitude == 0f)
            {
                angleIJ = Vector2.Angle(value, sampleI) * Mathf.Deg2Rad;
                angleIO = angleIJ;
                angleMultiplier = 1f;
            }
            else
            {
                angleIJ = Vector2.Angle(sampleI, sampleJ) * Mathf.Deg2Rad;
                angleIO = 0f;
                if (angleIJ > 0f)
                {
                    if (value.sqrMagnitude == 0f)
                        angleIO = angleIJ;
                    else
                    {
                        // All points lie in XY, so projection onto the samples'
                        // plane leaves the value unchanged.
                        angleIO = Vector2.Angle(sampleI, value) * Mathf.Deg2Rad;
                        float crossIJ = sampleI.x * sampleJ.y - sampleI.y * sampleJ.x;
                        float crossIO = sampleI.x * value.y - sampleI.y * value.x;
                        if (angleIJ < Mathf.PI * 0.99f && crossIJ * crossIO < 0f)
                            angleIO = -angleIO;
                    }
                }
            }

            float magnitudeI = sampleI.magnitude;
            float magnitudeJ = sampleJ.magnitude;
            float averageMagnitude = (magnitudeI + magnitudeJ) * 0.5f;
            Vector2 band = new Vector2(angleIJ * angleMultiplier, (magnitudeJ - magnitudeI) / averageMagnitude);
            Vector2 offset = new Vector2(angleIO * angleMultiplier, (value.magnitude - magnitudeI) / averageMagnitude);
            if (band.sqrMagnitude == 0f)
                return 1f;
            return Mathf.Clamp01(1f - Vector2.Dot(band, offset) / band.sqrMagnitude);
        }

        void ApplyWeights()
        {
            for (int i = 0; i < weights.Length; i++)
                mixer.SetInputWeight(i, weights[i]);
        }

        static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        public void SetTime(int portNumber, float time)
        {
            if (inputs == null || portNumber < 0 || portNumber >= inputs.Count)
                throw new ArgumentOutOfRangeException(nameof(portNumber), "Invalid input index.");
            if (!inputs[portNumber].ClipPlayer.BuildClipPlayable(mixer.GetGraph()).IsValid())
                throw new InvalidOperationException($"Input {portNumber} clip playable is not valid.");

            inputs[portNumber].ClipPlayer.SetTime(time * localTimeScale);
        }

        public void SetAllTime(float time)
        {
            if (inputs == null)
                return;

            for (int i = 0; i < inputs.Count; i++)
            {
                inputs[i].ClipPlayer.SetTime(time * localTimeScale);
            }
        }

        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            throw new NotImplementedException();
        }

        public override void Evaluate(float time)
        {
            throw new NotImplementedException();
        }
    }
}
*/