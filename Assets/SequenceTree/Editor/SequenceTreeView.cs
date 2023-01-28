using Antlr4.Runtime.Tree;
using Codice.Client.BaseCommands;
using EcaRules.Json;
using ECARules4All.RuleEngine;
using System;
using System.Collections;
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

    readonly int _rootId = 0;
    int _id;
    TreeView _treeView;
    VisualElement _ruleEditorContainer;
    int _prevSelectedNodeId = -1;
    Label _exportMessage;

    [MenuItem("Sequence Tree/Sequence Tree Editor")]
    static void Summon()
    {
        GetWindow<SequenceTreeView>("Sequence Tree Editor");
    }

    void CreateGUI()
    {
        _id = 0;
        uxml.CloneTree(rootVisualElement);

        //Setup TreeView Toolbar Menu
        var treeViewToolbar = rootVisualElement.Q<Toolbar>("TreeViewToolbar"); 
        var treeTbMenu = treeViewToolbar.Q<ToolbarMenu>("AddNodeMenu");
        treeTbMenu.menu.AppendAction("Add Order Ind. Node", a => { AddNodeToSelection("parallel"); }, UpdateActionMenuStatus);
        treeTbMenu.menu.AppendAction("Add Sequence Node", a => { AddNodeToSelection("sequence"); }, UpdateActionMenuStatus);
        treeTbMenu.menu.AppendAction("Add Leaf Node", a => { AddNodeToSelection("leaf"); }, UpdateActionMenuStatus);
        _exportMessage = treeViewToolbar.Q<Label>("OkMessage");
        var saveTreeBtn = treeViewToolbar.Q<ToolbarButton>("SaveTreeButton");
        saveTreeBtn.clickable = new Clickable(() => SaveSequenceTreeAsJson());

        _treeView = rootVisualElement.Q<TreeView>();

        // Call TreeView.SetRootItems() to populate the data in the tree.
        _treeView.SetRootItems(TreeRoot);

        // Set TreeView.makeItem to initialize each node in the tree.
        _treeView.makeItem = () =>
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
        _treeView.bindItem = (VisualElement element, int index) =>
        {
            element.userData = index;
            var node = _treeView.GetItemDataForIndex<INode>(index);
            string prefix = node.Prefix == 0 ? "R." : GetNodePrefix(node);
            element.Q<Label>("prefix").text = prefix;
            element.Q<Label>("nodeName").text = node.Name;
        };


        _treeView.onSelectionChange += UpdateRuleEditorStatus;
        _treeView.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Delete) DeleteItemNode(evt); } );

        _ruleEditorContainer = rootVisualElement.Q<VisualElement>("RuleEditorContainer");
        _ruleEditorContainer.SetEnabled(false);
        RuleEditorManager.SetupManager(_ruleEditorContainer);
    }



    private string GetNodePrefix(INode node)
    {
        if (node.Prefix == 0) return "";
        var parent = _treeView.GetItemDataForId<Internal>(node.ParentId);
        return GetNodePrefix(parent) + $"{node.Prefix}.";
    }

    private void BuildContextualMenu(ContextualMenuPopulateEvent evt, VisualElement container)
    {
        var node = _treeView.GetItemDataForIndex<INode>((int)container.userData);
        if (node.GetType() == typeof(Leaf)) return;
        evt.menu.AppendAction(InternalNodeNames["sequence"], a => { UpdateNodeName(InternalNodeNames["sequence"], container); }, DropdownMenuAction.AlwaysEnabled);
        evt.menu.AppendAction(InternalNodeNames["parallel"], a => { UpdateNodeName(InternalNodeNames["parallel"], container); }, DropdownMenuAction.AlwaysEnabled);
    }

    private void DeleteItemNode(KeyDownEvent evt)
    {
        if (_treeView.selectedItem is not INode selection || selection.Id == 0) return;
        var parent = _treeView.GetItemDataForId<Internal>(selection.ParentId);
        //Consider only the siblings with higher Id since they are the only ones that need to be changed.
        var siblings = parent.childrenIds.FindAll(x => x > selection.Id);
        siblings.ForEach(x => { _treeView.GetItemDataForId<INode>(x).Prefix--; });
        _treeView.TryRemoveItem(selection.Id);
        parent.childrenIds.Remove(selection.Id);
        _ruleEditorContainer.SetEnabled(false);
    }


    private void UpdateNodeName(string newName, VisualElement container)
    {
        var node = _treeView.GetItemDataForIndex<INode>((int)container.userData);
        node.Name = newName;
        _treeView.RefreshItems();
    }

    private void UpdateRuleEditorStatus(IEnumerable<object> obj)
    {
        var selection = obj.First() as INode;

        if (selection.Id == _prevSelectedNodeId) return;
        _prevSelectedNodeId = _treeView.selectedIndex;

        if (selection.GetType() != typeof(Leaf))
        {
            _ruleEditorContainer.SetEnabled(false);
            return;
        }


        _ruleEditorContainer.SetEnabled(true);

        var rule = (selection as Leaf).Rule;
        if (rule != null) rule = rule.Clone() as Rule;
        RuleEditorManager ruleEditorManager = new(rule);
        ruleEditorManager.SetConditionsSectionVisibility(DisplayStyle.None);


        //Setup RuleEditor Toolbar Menu
        var addActionBtn = _ruleEditorContainer.Q<ToolbarButton>("AddActionButton");
        var addConditionBtn = _ruleEditorContainer.Q<ToolbarButton>("AddConditionButton");
        var discardBtn = _ruleEditorContainer.Q<ToolbarButton>("DiscardButton");
        var saveBtn = _ruleEditorContainer.Q<ToolbarButton>("SaveButton");
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
        _treeView.RefreshItems();
    }

    private DropdownMenuAction.Status UpdateActionMenuStatus(DropdownMenuAction arg)
    {
        if (_treeView.selectedItem == null) return DropdownMenuAction.Status.Disabled;
        if (_treeView.GetSelectedItems<INode>().First().data.GetType() == typeof(Leaf)) return DropdownMenuAction.Status.Disabled;
        return DropdownMenuAction.Status.Normal;
    }

    public void AddNodeToSelection(string nodeType)
    {
        INode node;
        var selectionCollection = _treeView.GetSelectedItems<INode>();
        if (selectionCollection.Count() > 0)
        {
            int id = ++_id;
            var selection = selectionCollection.First();
            int prefix = selection.children.Count() + 1;

            if (nodeType == "leaf") node = new Leaf(id, "No Rule", prefix);
            else node = new Internal(id, InternalNodeNames[nodeType], prefix);
            node.ParentId = selection.id;
            (selection.data as Internal).childrenIds.Add(id);
            var newItem = new TreeViewItemData<INode>(id, node);
            _treeView.AddItem(newItem, parentId: selection.id, rebuildTree: true);
            _treeView.ExpandItem(selection.id);
        }
    }

    private void SaveSequenceTreeAsJson()
    {
        Debug.Log("Saving Sequence Tree...");
        var root = _treeView.GetItemDataForId<INode>(_rootId);

        JsonEcaTree jsonSequenceTree = ParseJsonTree(root);

        string jsonTree = JsonUtility.ToJson(jsonSequenceTree, true);

        var uniqueFileName = AssetDatabase.GenerateUniqueAssetPath(savedJsonFilePath);


        File.WriteAllText(uniqueFileName, jsonTree);
        string fileName = uniqueFileName.Split("/").Last();

        ShowOkMessage(fileName);

        Debug.Log($"{fileName} Generated!");

    }

    void ShowOkMessage(string fileName)
    {
        _exportMessage.text = $"{fileName} Generated!";
        _exportMessage.style.display = DisplayStyle.Flex;
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
            var child = _treeView.GetItemDataForId<INode>(id);
            
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