using System;
using System.Collections.Generic;

namespace BehaviourTree
{
    // SEQUENCE with memory: remembers which child it stopped at and resumes from it on the next frame.
    // Fails if a child fails; succeeds only when all children succeed.
    public class Sequence : Node
    {
        private readonly List<Node> _children;
        private int _index;
        private bool _running;
 
        public Sequence(List<Node> children) { _children = children; }
 
        public override bool IsInterruptible => !_running || _children[_index].IsInterruptible;
 
        public override NodeState Evaluate()
        {
            while (_index < _children.Count)
            {
                var s = _children[_index].Evaluate();
                switch (s)
                {
                    case NodeState.Running:
                        _running = true;
                        return State = NodeState.Running;
                    case NodeState.Failure:
                        _index = 0;
                        _running = false;
                        return State = NodeState.Failure;
                    default:
                        _index++;
                        break;
                }
            }
 
            _index = 0;
            _running = false;
            return State = NodeState.Success;
        }
 
        public override void Abort()
        {
            if (_running && _index < _children.Count) _children[_index].Abort();
            _index = 0;
            _running = false;
        }
 
        public override string Describe() => "Sequence(" + string.Join(", ", _children.ConvertAll(c => c.Describe())) + ")";
    }


    // Reactive SELECTOR: every frame it re-evaluates from the first child to the last
    // (priority from top to bottom). If a higher-priority child takes over, the child that
    // was Running is aborted.
    public class Selector : Node
    {
        private readonly List<Node> _children;
        private int _runningNodeIndex = -1;
 
        public Selector(List<Node> children) { _children = children; }
 
        public override bool IsInterruptible => _runningNodeIndex == -1 || _children[_runningNodeIndex].IsInterruptible;
 
        public override NodeState Evaluate()
        {
            // Committed child (e.g. attack in progress): keep running only it, without looking at higher-priority ones.
            if (_runningNodeIndex != -1 && !_children[_runningNodeIndex].IsInterruptible)
            {
                var committed = _children[_runningNodeIndex].Evaluate();
                if (committed != NodeState.Running) _runningNodeIndex = -1;
                if (committed != NodeState.Failure) return State = committed;
                // Failed midway: fall through to the normal loop below.
            }
 
            for (var i = 0; i < _children.Count; i++)
            {
                var s = _children[i].Evaluate();
                if (i == _runningNodeIndex && s != NodeState.Running) _runningNodeIndex = -1; // finished on its own
                if (s == NodeState.Failure) continue;
 
                // Success or Running: this child takes over. Abort whatever was running in another branch.
                if (_runningNodeIndex != -1 && _runningNodeIndex != i)
                {
                    _children[_runningNodeIndex].Abort();
                    _runningNodeIndex = -1;
                }
                if (s == NodeState.Running) _runningNodeIndex = i;
                return State = s;
            }
 
            return State = NodeState.Failure;
        }
 
        public override void Abort()
        {
            if (_runningNodeIndex == -1) return;
            _children[_runningNodeIndex].Abort();
            _runningNodeIndex = -1;
        }
 
        public override string Describe() => "Selector(" + string.Join(", ", _children.ConvertAll(c => c.Describe())) + ")";
    }
    
    // INVERTER: Success <-> Failure. Running passes through.
    public class Inverter : Node
    {
        private readonly Node _child;
        public Inverter(Node child) { _child = child; }
 
        public override bool IsInterruptible => _child.IsInterruptible;
 
        public override NodeState Evaluate()
        {
            var state = _child.Evaluate();
            return state switch
            {
                NodeState.Success => State = NodeState.Failure,
                NodeState.Failure => State = NodeState.Success,
                _ => State = NodeState.Running
            };
        }
 
        public override void Abort() => _child.Abort();
        public override string Describe() => "Not(" + _child.Describe() + ")";
    }

}