using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ECARules4All.RuleEngine;
using System.Linq;
using System.Diagnostics.CodeAnalysis;



// Base class for all windows that display node information.
public class NodesWindow : EditorWindow
{
    public static Dictionary<string, string> InternalNodeNames = new(){ 
        { "parallel", "Parallel Execution" }, 
        { "sequence", "Sequence Execution" }
    };
    
    [SerializeField]
    protected VisualTreeAsset uxml;

    protected interface INode
    {
        public int ParentId
        {
            get;
            set;
        }
        public int Id
        {
            get;
            set;
        }

        public int Prefix
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

    protected class Leaf : INode
    {
        public int ParentId
        {
            get;
            set;
        }
        public int Id
        {
            get;
            set;
        }
        public int Prefix
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

        public Leaf(int id, string name, int prefix = -1)
        {
            this.Id = id;
            this.Name = name;
            this.Prefix = prefix;
            this.Rule = null;
        }
    }

    protected class Internal : INode
    {
        public int ParentId
        {
            get;
            set;
        }
        public int Id
        {
            get;
            set;
        }
        public int Prefix
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        public List<int> childrenIds = new List<int>();

        public Internal(int id, string name, int prefix = -1)
        {
            this.Id = id;
            this.Name = name;
            this.Prefix = prefix;
        }
    }

    // Expresses data as a list of TreeViewItemData objects.
    protected static List<TreeViewItemData<INode>> TreeRoot
    {
        get
        {
            var root = new Internal(0, InternalNodeNames["parallel"], 0) { ParentId = -1 };
            var roots = new List<TreeViewItemData<INode>> { new TreeViewItemData<INode>(0, root) };
            return roots;
        }
    }
}