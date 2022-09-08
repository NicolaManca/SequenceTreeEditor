using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ECARules4All.RuleEngine;

// Base class for all windows that display planet information.
public class NodesWindow : EditorWindow
{
    public static Dictionary<string, string> InternalNodeNames = new(){ 
        { "parallel", "Parallel Execution" }, 
        { "sequence", "Sequence Execution" }
    };
    
    [SerializeField]
    protected VisualTreeAsset uxml;

    // Nested interface that can be either a single planet or a group of planets.
    protected interface INode
    {
        public int Id
        {
            get;
            set;
        }

        public string Prefix
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }
    }

    // Nested class that represents a planet.
    protected class Leaf : INode
    {
        public int Id
        {
            get;
            set;
        }
        public string Prefix
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }

        public Rule Rule
        {
            get;
            set;
        }

        public Leaf(int id, string name, string prefix = "", Rule rule = null)
        {
            this.Id = id;
            this.Name = name;
            this.Prefix = prefix;
            this.Rule = new Rule(new Action(), new List<Action>());
        }
    }

    // Nested class that represents a group of planets.
    protected class Internal : INode
    {
        public int Id
        {
            get;
            set;
        }
        public string Prefix
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        public Internal(int id, string name, string prefix = "")
        {
            this.Id = id;
            this.Name = name;
            this.Prefix = prefix;
        }
    }

    // Expresses planet data as a list of TreeViewItemData objects. Needed for TreeView and MultiColumnTreeView.
    protected static List<TreeViewItemData<INode>> TreeRoot
    {
        get
        {
            var root = new Internal(1, InternalNodeNames["parallel"], "R.");
            var roots = new List<TreeViewItemData<INode>> { new TreeViewItemData<INode>(1, root) };
            return roots;
        }
    }
}