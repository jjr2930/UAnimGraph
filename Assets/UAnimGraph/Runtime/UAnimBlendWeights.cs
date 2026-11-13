using System;
using UnityEngine;

namespace UAnimGraph
{
    public static class UAnimBlendWeights
    {
        public static void One(float value, float[] thresholds, float[] weights)
        {
            Array.Clear(weights, 0, weights.Length);
            if(weights.Length == 0)
            {
                return;
            }

            if(weights.Length == 1)
            {
                weights[0] = 1;
                return;
            }

            int lower = -1, upper = -1;
            for(int i = 0; i < thresholds.Length; i++)
            {
                if(thresholds[i] <= value && (lower < 0 || thresholds[i] > thresholds[lower]))
                {
                    lower = i;
                }

                if(thresholds[i] >= value && (upper < 0 || thresholds[i] < thresholds[upper]))
                {
                    upper = i;
                }
            }

            if(lower < 0)
            {
                weights[upper] = 1;
            }
            else if(upper < 0 || lower == upper)
            {
                weights[lower] = 1;
            }
            else
            {
                float a = (float)Math.Clamp(((double)value - thresholds[lower]) / ((double)thresholds[upper] - thresholds[lower]), 0, 1);
                weights[lower] = 1 - a;
                weights[upper] = a;
            }
        }

        // Polar gradient-band weights support an origin and different speeds in
        // each direction. All intermediate math uses double to avoid overflow.
        public static void Two(Vector2 value, Vector2[] samples, float[] weights, string[] ids = null)
        {
            Array.Clear(weights, 0, weights.Length);
            if(weights.Length == 0)
            {
                return;
            }

            int nearest = 0;
            double best = double.PositiveInfinity, sum = 0;
            for(int i = 0; i < samples.Length; i++)
            {
                double dx = (double)value.x - samples[i].x, dy = (double)value.y - samples[i].y, d = dx * dx + dy * dy;
                if(d < best || (d == best && ids != null && ids.Length == samples.Length && string.CompareOrdinal(ids[i], ids[nearest]) < 0))
                {
                    best = d;
                    nearest = i;
                }

                if(d == 0)
                {
                    weights[i] = 1;
                    return;
                }
            }

            for(int i = 0; i < samples.Length; i++)
            {
                double w = 1;
                for(int j = 0; j < samples.Length && w > 0; j++)
                {
                    if(j != i)
                    {
                        w = Math.Min(w, Band(samples[i], samples[j], value));
                    }
                }

                weights[i] = (float)w;
                sum += w;
            }

            if(sum > 0 && !double.IsNaN(sum) && !double.IsInfinity(sum))
            {
                for(int i = 0; i < weights.Length; i++)
                {
                    weights[i] = (float)(weights[i] / sum);
                }
            }
            else
            {
                Array.Clear(weights, 0, weights.Length);
                weights[nearest] = 1;
            }
        }

        static double Length(Vector2 v)
        {
            return Math.Sqrt((double)v.x * v.x + (double)v.y * v.y);
        }

        static double Angle(Vector2 a, Vector2 b)
        {
            double m = Length(a) * Length(b);
            return m == 0 ? 0 : Math.Acos(Math.Clamp(((double)a.x * b.x + (double)a.y * b.y) / m, -1, 1));
        }

        static double Band(Vector2 a, Vector2 b, Vector2 value)
        {
            double ma = Length(a), mb = Length(b), mv = Length(value), ij, io, mult = 2;
            if(ma == 0)
            {
                ij = Angle(value, b);
                io = 0;
                mult = 1;
            }
            else if(mb == 0)
            {
                ij = Angle(value, a);
                io = ij;
                mult = 1;
            }
            else
            {
                ij = Angle(a, b);
                io = 0;
                if(ij > 0)
                {
                    io = mv == 0 ? ij : Angle(a, value);
                    double crossAB = (double)a.x * b.y - (double)a.y * b.x, crossAV = (double)a.x * value.y - (double)a.y * value.x;
                    if(mv != 0 && ij < Math.PI * .99 && crossAB * crossAV < 0)
                    {
                        io = -io;
                    }
                }
            }

            double average = (ma + mb) * .5;
            if(average == 0)
            {
                return 1;
            }

            double x = ij * mult, y = (mb - ma) / average, ox = io * mult, oy = (mv - ma) / average, d = x * x + y * y;
            return d == 0 ? 1 : Math.Clamp(1 - (ox * x + oy * y) / d, 0, 1);
        }
    }
}
