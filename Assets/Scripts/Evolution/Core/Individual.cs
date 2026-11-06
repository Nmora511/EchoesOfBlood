using System;

namespace Evolution.Core
{
    // A genome plus its fitness (higher is better).
    [Serializable]
    public class Individual<T>
    {
        public T Genome;
        public double Fitness;

        public Individual(T genome, double fitness)
        {
            Genome = genome;
            Fitness = fitness;
        }
    }
}
