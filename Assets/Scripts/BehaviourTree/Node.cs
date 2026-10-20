namespace BehaviourTree
{
    public enum NodeState
    {
        Running,
        Success,
        Failure
    }

    public abstract class Node
    {
        public NodeState State { get; protected set; } = NodeState.Failure;
 
        public abstract NodeState Evaluate();
 
        public virtual void Abort() { }
 
        public virtual bool IsInterruptible => true;
 
        // Human-readable tree text (useful for telemetry logs and for the thesis text).
        public abstract string Describe();
    }

}
