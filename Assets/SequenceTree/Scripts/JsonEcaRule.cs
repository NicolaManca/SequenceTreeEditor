using ECARules4All.RuleEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EcaRules.Json
{
    
    // Enumeration to differentiate each node
    public enum NodeType {
        Leaf,
        Seq,
        OrdInd
    }


    // Class that contains the complete tree
    [Serializable]
    public class JsonEcaTree
    {
        public JsonEcaNode Tree;

        #region Override
        
        protected bool Equals(JsonEcaTree other)
        {
            return Equals(Tree, other.Tree);
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((JsonEcaTree)obj);
        }
        public override int GetHashCode()
        {
            return (Tree != null ? Tree.GetHashCode() : 0);
        }
        
        #endregion
    }
    
    // Class that represent a single node
    [Serializable]
    public class JsonEcaNode
    {
        public NodeType nodeType;
        public List<JsonEcaNode> Children = new();
        public JsonEcaRule[] Rules = Array.Empty<JsonEcaRule>();

        public JsonEcaNode(NodeType nodeType)
        {
            this.nodeType = nodeType;
        }
        public static NodeType StringToNodeType(string type)
        {
            return type switch
            {
                "Sequence Execution" => NodeType.Seq,
                "Parallel Execution" => NodeType.OrdInd,
                _ => NodeType.Leaf,
            };
        }

        #region Override

        protected bool Equals(JsonEcaNode other)
        {
            return Equals(Children, other.Children) && nodeType == other.nodeType;
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((JsonEcaNode)obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Children != null ? Children.GetHashCode() : 0) * 397);
            }
        }
        
        #endregion

    }






    //  not now ---

    [Serializable]
    public class JsonEcaRules
    {
        public JsonEcaRule[] Rules = Array.Empty<JsonEcaRule>();

        #region OverRide
        protected bool Equals(JsonEcaRules other)
        {
            return Equals(Rules, other.Rules);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((JsonEcaRules) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Rules != null ? Rules.GetHashCode() : 0) * 397;
            }
        }
        #endregion

    }

    [Serializable]
    public class JsonEcaRule
    {
        public JsonEcaAction Event = new JsonEcaAction();
        public JsonEcaCondition Condition = new JsonEcaCondition();
        public JsonEcaAction[] Actions = Array.Empty<JsonEcaAction>();

        public JsonEcaRule(JsonEcaAction eventAction, JsonEcaCondition condition, JsonEcaAction[] actions)
        {
            this.Event = eventAction;
            this.Condition = condition;
            this.Actions = actions;
        }
        public JsonEcaRule()
        {
            this.Event = new JsonEcaAction();
            this.Condition = new JsonEcaCondition();
            this.Actions = new JsonEcaAction[] { new JsonEcaAction() };
        }


        public static JsonEcaRule ParseRuleToJsonRule(Rule rule)
        {
            var ruleEvent = rule.GetEvent();
            JsonEcaAction.ParseActionToJsonAction(ruleEvent, out string subj, out string verb, out string dirObj, out string spec, out string specVal);
            JsonEcaAction eventAction = new(subj, verb, dirObj, spec, specVal);

            var ruleCondition = rule.GetCondition();
            //TODO: understand how to convert RuleEngine.Condition into JsonEcaCondition. What is Lambda and Ids
            JsonEcaCondition condition = new();

            var ruleActions = rule.GetActions();
            List<JsonEcaAction> actionslist = new();
            foreach (var action in ruleActions)
            {
                JsonEcaAction.ParseActionToJsonAction(action, out string actionSubj, out string actionVerb, out string actionDirObj, out string actionSpec, out string actionSpecVal);
                actionslist.Add(new(actionSubj, actionVerb, actionDirObj, actionSpec, actionSpecVal));
            }
            JsonEcaAction[] actions = actionslist.ToArray();

            return new JsonEcaRule(eventAction, condition, actions);
        }


        public override string ToString()
        {
            String actionString = "";
            foreach (var action in Actions)
            {
                actionString += $"<b>Then</b> {action.Subj} {action.Verb} {action.DirObj}\n";
            }

            return $"<b>When</b> {Event.Subj} {Event.Verb} {Event.DirObj}\n{actionString}";
        }

        public override bool Equals(object o)
        {
            if (o is JsonEcaRule rule)
            {
                return this.Equals(rule);
            }
            else
            {
                return false;
            }
        }

        protected bool Equals(JsonEcaRule other)
        {
            return this.Event.Equals(other.Event) &&
                   this.Condition.Equals(other.Condition) &&
                   this.Actions.SequenceEqual(other.Actions);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Event != null ? Event.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Condition != null ? Condition.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Actions != null ? Actions.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    [Serializable]
    public class JsonEcaAction
    {
        // init to empty string for compatibility with JsonUtility.ToJson()
        public string Subj = "";
        public string Verb = "";
        public string DirObj = "";
        public string Spec = "";
        public string SpecVal = "";

        public JsonEcaAction()
        {

        }

        public JsonEcaAction(string subj, string verb, string dirObj = "", string spec = "", string specVal = "")
        {
            this.Subj = subj;
            this.Verb = verb;
            this.DirObj = dirObj;
            this.Spec = spec;
            this.SpecVal = specVal;
        }

        public static void ParseActionToJsonAction(ECARules4All.RuleEngine.Action ruleAction, out string subj, out string verb, out string dirObj, out string spec, out string specVal)
        {
            subj = ruleAction.GetSubject().name;
            verb = ruleAction.GetActionMethod();
            dirObj = (ruleAction.GetObject() != null) ? ruleAction.GetObject().ToString() : "";
            spec = ruleAction.GetModifier();
            specVal = (ruleAction.GetModifierValue() != null) ? ruleAction.GetModifierValue().ToString() : "";
        }


        public override bool Equals(object o)
        {
            if (o is JsonEcaAction act)
            {
                return this.Equals(act);
            }
            else
            {
                return false;
            }
        }

        protected bool Equals(JsonEcaAction other)
        {
            return this.Subj.Equals(other.Subj) &&
                   this.Verb.Equals(other.Verb) &&
                   this.DirObj.Equals(other.DirObj) &&
                   this.Spec.Equals(other.Spec) &&
                   this.SpecVal.Equals(other.Spec);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Subj != null ? Subj.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Verb != null ? Verb.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DirObj != null ? DirObj.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Spec != null ? Spec.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SpecVal != null ? SpecVal.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    [Serializable]
    public class JsonEcaCondition
    {
        public string[] Ids = Array.Empty<string>();
        public string LambdaExpr = "";

        public override bool Equals(object o)
        {
            if (o is JsonEcaCondition cnd)
            {
                return this.Equals(cnd);
            }
            else
            {
                return false;
            }
        }

        protected bool Equals(JsonEcaCondition other)
        {
            return this.Ids.SequenceEqual(other.Ids)
                   && this.LambdaExpr.Equals(other.LambdaExpr);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Ids != null ? Ids.GetHashCode() : 0) * 397) ^ (LambdaExpr != null ? LambdaExpr.GetHashCode() : 0);
            }
        }
    }

}