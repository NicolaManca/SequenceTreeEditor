using Antlr4.Runtime.Tree;
using Codice.Client.BaseCommands;
using EcaRules.Json;
using ECARules4All.RuleEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Action = ECARules4All.RuleEngine.Action;

public class SequenceTreeView : NodesWindow
{
    public static readonly string savedJsonFilePath = $"{Application.streamingAssetsPath}/Sequence Tree.txt";
    public static readonly string ussNodeContainer = "node-container";
    public static readonly string ussNodeLabel = "node-label";

    private readonly int m_RootId = 0;
    private int m_Id;
    private TreeView m_TreeView;
    private VisualElement m_RuleEditorContainer;
    private int m_PrevSelectedNode = -1;


    [MenuItem("Sequence/Sequence Tree")]
    static void Summon()
    {
        GetWindow<SequenceTreeView>("Sequence Tree Editor");
    }

    void CreateGUI()
    {
        m_Id = 0;
        uxml.CloneTree(rootVisualElement);

        //Setup TreeView Toolbar Menu
        var treeViewToolbar = rootVisualElement.Q<Toolbar>("TreeViewToolbar"); 
        var treeTbMenu = treeViewToolbar.Q<ToolbarMenu>("AddNodeMenu");
        treeTbMenu.menu.AppendAction("Add Sequence Node", a => { AddNodeToSelection("sequence"); }, UpdateActionMenuStatus);
        treeTbMenu.menu.AppendAction("Add Parallel Node", a => { AddNodeToSelection("parallel"); }, UpdateActionMenuStatus);
        treeTbMenu.menu.AppendAction("Add Leaf Node", a => { AddNodeToSelection("leaf"); }, UpdateActionMenuStatus);
        var saveTreeBtn = treeViewToolbar.Q<ToolbarButton>("SaveTreeButton");
        saveTreeBtn.clickable = new Clickable(() => SaveSequenceTreeAsJson());

        m_TreeView = rootVisualElement.Q<TreeView>();

        // Call TreeView.SetRootItems() to populate the data in the tree.
        m_TreeView.SetRootItems(TreeRoot);

        // Set TreeView.makeItem to initialize each node in the tree.
        m_TreeView.makeItem = () =>
        {
            VisualElement container = new() { name = "container", focusable = true };
            container.AddToClassList(ussNodeContainer);
            

            Label prefix = new() { name = "prefix" };
            prefix.AddToClassList(ussNodeLabel);

            Label nodeName = new() { name = "nodeName" };
            nodeName.AddToClassList(ussNodeLabel);

            container.AddManipulator(new ContextualMenuManipulator(evt => { BuildContextualMenu(evt, container); }));

            container.Add(prefix);
            container.Add(nodeName);
            return container;
        };

        // Set TreeView.bindItem to bind an initialized node to a data item.
        m_TreeView.bindItem = (VisualElement element, int index) =>
        {
            element.userData = index;
            var node = m_TreeView.GetItemDataForIndex<INode>(index);
            string prefix = node.Prefix == 0 ? "R." : GetNodePrefix(node);
            element.Q<Label>("prefix").text = prefix;
            element.Q<Label>("nodeName").text = node.Name;
        };


        m_TreeView.onSelectionChange += UpdateRuleEditorStatus;
        m_TreeView.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Delete) DeleteItemNode(evt); } );

        m_RuleEditorContainer = rootVisualElement.Q<VisualElement>("RuleEditorContainer");
        m_RuleEditorContainer.SetEnabled(false);
        RuleEditorManager.SetupManager(m_RuleEditorContainer);
    }



    private string GetNodePrefix(INode node)
    {
        if (node.Prefix == 0) return "";
        var parent = m_TreeView.GetItemDataForId<Internal>(node.ParentId);
        return GetNodePrefix(parent) + $"{node.Prefix}.";
    }

    private void BuildContextualMenu(ContextualMenuPopulateEvent evt, VisualElement container)
    {
        var node = m_TreeView.GetItemDataForIndex<INode>((int)container.userData);
        if (node.GetType() == typeof(Leaf)) return;
        evt.menu.AppendAction(InternalNodeNames["sequence"], a => { UpdateNodeName(InternalNodeNames["sequence"], container); }, DropdownMenuAction.AlwaysEnabled);
        evt.menu.AppendAction(InternalNodeNames["parallel"], a => { UpdateNodeName(InternalNodeNames["parallel"], container); }, DropdownMenuAction.AlwaysEnabled);
    }

    private void DeleteItemNode(KeyDownEvent evt)
    {
        if (m_TreeView.selectedItem is not INode selection || selection.Id == 0) return;
        var parent = m_TreeView.GetItemDataForId<Internal>(selection.ParentId);
        //Consider only the siblings with higher Id since they are the only ones that need to be changed.
        var siblings = parent.childrenIds.FindAll(x => x > selection.Id);
        siblings.ForEach(x => { m_TreeView.GetItemDataForId<INode>(x).Prefix--; });
        m_TreeView.TryRemoveItem(selection.Id);
        parent.childrenIds.Remove(selection.Id);

    }


    private void UpdateNodeName(string newName, VisualElement container)
    {
        var node = m_TreeView.GetItemDataForIndex<INode>((int)container.userData);
        node.Name = newName;
        m_TreeView.RefreshItems();
    }

    private void UpdateRuleEditorStatus(IEnumerable<object> obj)
    {
        if (m_TreeView.selectedIndex == m_PrevSelectedNode) return;
        var previousNode = m_TreeView.GetItemDataForIndex<INode>(m_PrevSelectedNode);
        m_PrevSelectedNode = m_TreeView.selectedIndex;
        var selection = obj.First() as INode;
        if (selection.GetType() != typeof(Leaf))
        {
            m_RuleEditorContainer.SetEnabled(false);
            return;
        }
        

        m_RuleEditorContainer.SetEnabled(true);


        var rule = (selection as Leaf).Rule;
        if (rule != null) rule = rule.Clone() as Rule;
        RuleEditorManager ruleEditorManager = new(rule);

        //Setup RuleEditor Toolbar Menu
        var addActionBtn = m_RuleEditorContainer.Q<ToolbarButton>("AddActionButton");
        var addConditionBtn = m_RuleEditorContainer.Q<ToolbarButton>("AddConditionButton");
        var discardBtn = m_RuleEditorContainer.Q<ToolbarButton>("DiscardButton");
        var saveBtn = m_RuleEditorContainer.Q<ToolbarButton>("SaveButton");
        addActionBtn.clickable = new Clickable(() => { ruleEditorManager.AddAction(new Action(), true); });
        addConditionBtn.clickable = new Clickable(() => { ruleEditorManager.AddCondition(new CustomCondition(), true); });
        discardBtn.clickable = new Clickable(() => { ruleEditorManager.DiscardRule(); });
        saveBtn.clickable = new Clickable(() => { SaveRuleIntoNode(selection as Leaf, ruleEditorManager); });

    }

    private void SaveRuleIntoNode(Leaf node, RuleEditorManager ruleEditorManager)
    {
        Debug.Log($"Saving Rule...");
        var ruleG = ruleEditorManager.GetRule();
        if(ruleG == null)
        {
            RuleEditorManager.SetErrorMessageVisibility(DisplayStyle.Flex);
            return;
        }
        Debug.Log($"Rule Saved!\nRule: {ruleG}");

        node.Rule = ruleG;
        node.Name = ruleG.ToString();
        m_TreeView.RefreshItems();
    }

    private DropdownMenuAction.Status UpdateActionMenuStatus(DropdownMenuAction arg)
    {
        if (m_TreeView.selectedItem == null) return DropdownMenuAction.Status.Disabled;
        if (m_TreeView.GetSelectedItems<INode>().First().data.GetType() == typeof(Leaf)) return DropdownMenuAction.Status.Disabled;
        return DropdownMenuAction.Status.Normal;
    }

    public void AddNodeToSelection(string nodeType)
    {
        INode node;
        var selectionCollection = m_TreeView.GetSelectedItems<INode>();
        if (selectionCollection.Count() > 0)
        {
            int id = ++m_Id;
            var selection = selectionCollection.First();
            int prefix = selection.children.Count() + 1;

            if (nodeType == "leaf") node = new Leaf(id, "No Rule", prefix);
            else node = new Internal(id, InternalNodeNames[nodeType], prefix);
            node.ParentId = selection.id;
            (selection.data as Internal).childrenIds.Add(id);
            var newItem = new TreeViewItemData<INode>(id, node);
            m_TreeView.AddItem(newItem, parentId: selection.id, rebuildTree: true);
            m_TreeView.ExpandItem(selection.id);
        }
    }

    private void SaveSequenceTreeAsJson()
    {
        Debug.Log("Saving Sequence Tree...");
        var root = m_TreeView.GetItemDataForId<INode>(m_RootId);

        JsonEcaTree jsonSequenceTree = ParseJsonTree(root);

        string jsonTree = JsonUtility.ToJson(jsonSequenceTree, true);

        var uniqueFileName = AssetDatabase.GenerateUniqueAssetPath(savedJsonFilePath);


        File.WriteAllText(uniqueFileName, jsonTree);
        string fileName = uniqueFileName.Split("/").Last();
        Debug.Log($"{fileName} Generated!");

    }
    private JsonEcaTree ParseJsonTree(INode root)
    {
        var jsonTree = new JsonEcaTree();
        
        jsonTree.Tree = new JsonEcaNode(JsonEcaNode.StringToNodeType(root.Name));
        DFS(root, jsonTree.Tree);

        return jsonTree;
    }
    private void DFS(INode node, JsonEcaNode tree)
    {
        if (node.IsLeaf())
        {
            tree.Rules = new JsonEcaRule[] { JsonEcaRule.ParseRuleToJsonRule((node as Leaf).Rule) };
            return;
        }
        foreach(int id in (node as Internal).childrenIds)
        {
            var child = m_TreeView.GetItemDataForId<INode>(id);
            
            if (child.IsLeaf() && (child as Leaf).Rule == null) 
                continue;
            //TODO: Instead of checking if it has children, check if there is at least one Rule inside the subtree having child as root.
            if (!child.IsLeaf() && (child as Internal).childrenIds.Count == 0) 
                continue;

            var newNode = new JsonEcaNode(JsonEcaNode.StringToNodeType(child.Name));
            DFS(child, newNode);
            tree.Children.Add(newNode);
        }
    }

}