using System;

namespace Evolution.Core
{
    // Summary of one generation, used for the logs.
    [Serializable]
    public class GenerationStats
    {
        public int Generation;      // 0 = the population before any evolution
        public double Best;
        public double Mean;
        public double Worst;
        public double Diversity;    // mean distance between individuals (0 when no Distance function is given)
        public double ElapsedMs;    // time spent creating and evaluating this generation

        public override string ToString() =>
            $"gen {Generation}: best {Best:0.0000} mean {Mean:0.0000} worst {Worst:0.0000} " +
            $"diversity {Diversity:0.000} ({ElapsedMs:0.0} ms)";
    }
}
