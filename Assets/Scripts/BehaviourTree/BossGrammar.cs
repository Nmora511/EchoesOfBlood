using System;
using System.Collections.Generic;

// Grammar + genotype -> phenotype mapping for Grammatical Evolution.
// Pure C#, no Unity.
//
// Genotype : int[] (codons). This is what the GA crosses over and mutates. The tree is NEVER kept between
//            bosses: each boss receives the genome and builds its own tree (each node has its own state).
// Phenotype: a tree of BehaviourTree.Node.
//
// BNF (each "choice" consumes one codon: option = codon mod number of options; at the end of the genome it wraps around):
//
//   <tree>      ::= Selector( <branch> {, <branch>} )                 1 to MaxBranches branches
//   <branch>    ::= Sequence( <cond> {, <cond>} , <action> {, <action>} )   1 to MaxConds conditions, 1 to MaxCombo actions
//   <cond>      ::= <condName> | Not(<condName>)
//   <condName>  ::= InMelee | InArea | InRushRange | CanMelee | CanArea | CanRush | HealthLow
//   <action>    ::= Chase | Wait | MeleeAttack | Rush | AreaAttack
//
// Why it is built this way:
//   - Every derivation TERMINATES and is valid (no open-ended recursion): there are no invalid individuals to discard.
//   - Conditions only appear before actions and are never Running.
//   - The fallback Chase is added OUTSIDE the genome (BuildBossTree), so the boss never stands still
//     just because the evolved tree failed on every branch.
namespace BehaviourTree
{
    // Whoever implements the leaves (BossEnemy). Each CreateAction call returns a NEW INSTANCE.
    public interface IBossLeaves
    {
        Node CreateAction(string name);
        Func<bool> GetCondition(string name);
    }

    public static class BossGrammar
    {
        public const int MaxBranches = 4;
        public const int MaxConds = 3;
        public const int MaxCombo = 3;

        // Worst case: 1 + MaxBranches * (1 + MaxConds*2 + 1 + MaxCombo) = 45 codons. Use genomes of 48 or more.
        public const int RecommendedGenomeLength = 48;

        public static readonly string[] Conditions =
            { "InMelee", "InArea", "InRushRange", "CanMelee", "CanArea", "CanRush", "HealthLow" };

        public static readonly string[] Actions =
            { "Chase", "Wait", "MeleeAttack", "Rush", "AreaAttack" };

        public static int[] RandomGenome(Random rng, int length = RecommendedGenomeLength)
        {
            var genome = new int[length];
            for (var i = 0; i < length; i++) genome[i] = rng.Next(0, 256);
            return genome;
        }

        // Full boss tree: evolved branches + fallback Chase.
        public static Node BuildBossTree(int[] genome, IBossLeaves leaves)
        {
            var evolved = Build(genome, leaves);
            return new Selector(new List<Node> { evolved, leaves.CreateAction("Chase") });
        }

        // Evolved part only (<tree>).
        public static Node Build(int[] genome, IBossLeaves leaves)
        {
            if (genome == null || genome.Length == 0) throw new ArgumentException("Genoma vazio.");
            var reader = new CodonReader(genome);

            var branches = new List<Node>();
            var branchCount = 1 + reader.Pick(MaxBranches);
            for (var b = 0; b < branchCount; b++)
            {
                var children = new List<Node>();

                var condCount = 1 + reader.Pick(MaxConds);
                for (var c = 0; c < condCount; c++)
                {
                    var name = Conditions[reader.Pick(Conditions.Length)];
                    Node cond = new ConditionNode(name, leaves.GetCondition(name));
                    if (reader.Pick(4) == 0) cond = new Inverter(cond); // ~25% negated
                    children.Add(cond);
                }

                var actionCount = 1 + reader.Pick(MaxCombo);
                for (var a = 0; a < actionCount; a++)
                    children.Add(leaves.CreateAction(Actions[reader.Pick(Actions.Length)]));

                branches.Add(new Sequence(children));
            }

            return new Selector(branches);
        }

        private sealed class CodonReader
        {
            private readonly int[] _genome;
            private int _pos;

            public CodonReader(int[] genome) { _genome = genome; }

            // Mod rule protected against negative codons; wraps around at the end of the genome (wrapping).
            public int Pick(int optionCount)
            {
                var codon = _genome[_pos++ % _genome.Length];
                return ((codon % optionCount) + optionCount) % optionCount;
            }
        }
    }
}