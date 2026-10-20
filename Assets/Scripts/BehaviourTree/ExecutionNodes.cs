using System;

namespace BehaviourTree
{
    // ACTION: leaf with its OWN state (each instance in the tree has its own).
    // OnEnter runs when the action starts, OnTick every frame, OnExit when it finishes OR is aborted.
    public abstract class ActionNode : Node
    {
        private readonly string _name;
        private bool _active;
 
        protected ActionNode(string name) { _name = name; }
 
        protected abstract NodeState OnTick();
        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
 
        public sealed override NodeState Evaluate()
        {
            if (!_active)
            {
                _active = true;
                OnEnter();
            }
 
            var state = OnTick();
            if (state == NodeState.Running) return State = state;
            _active = false;
            OnExit();
            return State = state;
        }
 
        public sealed override void Abort()
        {
            if (!_active) return;
            _active = false;
            OnExit();
        }
 
        public override string Describe() => _name;
    }

    
    // CONDITION: yes/no question, never Running and stateless.
    public class ConditionNode : Node
    {
        private readonly string _name;
        private readonly Func<bool> _condition;
 
        public ConditionNode(string name, Func<bool> condition)
        {
            _name = name;
            _condition = condition;
        }
 
        public override NodeState Evaluate() => State = _condition() ? NodeState.Success : NodeState.Failure;
        public override string Describe() => _name;
    }

}