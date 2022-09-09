using System.Collections.Generic;
using System.Drawing.Design;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;


public class SequenceTreeView : NodesWindow
{
    public static readonly string ussNodeContainer = "node-container";
    public static readonly string ussNodeLabel = "node-label";
    public static readonly string ussNodeTextField = "node-text-field";

    private int m_Id = 1;
    private TreeView m_TreeView;
    private VisualElement m_RuleInspectorContainer;

    [MenuItem("Sequence/Sequence Tree")]
    static void Summon()
    {
        GetWindow<SequenceTreeView>("Sequence Tree Editor");
    }

    void CreateGUI()
    {
        uxml.CloneTree(rootVisualElement);


        var treeTbMenu = rootVisualElement.Q<ToolbarMenu>("AddNodeMenu");
        treeTbMenu.menu.AppendAction("Add Sequence Node", a => { AddNodeToSelection("sequence"); }, UpdateActionMenuStatus);
        treeTbMenu.menu.AppendAction("Add Parallel Node", a => { AddNodeToSelection("parallel"); }, UpdateActionMenuStatus);
        treeTbMenu.menu.AppendAction("Add Leaf Node", a => { AddNodeToSelection("leaf"); }, UpdateActionMenuStatus);

        var ruleInspTbMenu = rootVisualElement.Q<ToolbarMenu>("AddMenu");
        var ruleInspDiscardB = rootVisualElement.Q<ToolbarButton>("DiscardButton");
        var ruleInspSaveB = rootVisualElement.Q<ToolbarButton>("SaveButton");
        ruleInspTbMenu.menu.AppendAction("Add Action", a => { Debug.Log("Add Action Clicked"); }, a => DropdownMenuAction.Status.Normal);
        ruleInspTbMenu.menu.AppendAction("Add Condition", a => { Debug.Log("Add Condition Clicked"); }, action => DropdownMenuAction.Status.Normal);


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

            TextField textField = new()
            {
                name = "textfield",
                isDelayed = true,
                focusable = true,
                delegatesFocus = true,
            };
            textField.AddToClassList(ussNodeTextField);
            textField.style.display = DisplayStyle.None;

            textField.RegisterValueChangedCallback(e => UpdateNodeName(e, container));


            container.Add(prefix);
            container.Add(nodeName);
            container.Add(textField);
            return container;
        };

        // Set TreeView.bindItem to bind an initialized node to a data item.
        m_TreeView.bindItem = (VisualElement element, int index) =>
        {
            //Debug.Log($"Label: {(element as NodeName).id}, Node Name: {m_TreeView.GetItemDataForIndex<INode>(index).Id}");
            element.userData = index;
            var node = m_TreeView.GetItemDataForIndex<INode>(index);
            element.Q<Label>("prefix").text = node.Prefix;
            element.Q<Label>("nodeName").text = node.Name;
        };

        m_TreeView.destroyItem = (VisualElement element) =>
        {
            Debug.Log($"Destroyed: {element.userData}");
        };



        m_TreeView.onItemsChosen += EditNodeNode;
        m_TreeView.onSelectionChange += UpdateRuleEditorStatus;
        m_TreeView.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Delete) DeleteItemNode(); } );

        m_RuleInspectorContainer = rootVisualElement.Q<VisualElement>("RuleInspectorContainer");
        m_RuleInspectorContainer.SetEnabled(false);
        var ruleButton = m_RuleInspectorContainer.Q<VisualElement>("RuleInspector").Q<UnityEngine.UIElements.Button>();
        ruleButton.clickable = new Clickable(OnButtonClicked);


    }

    private void DeleteItemNode()
    {
        var selection = m_TreeView.selectedItem as INode;
        if (selection.Id == 1) return;
        m_TreeView.TryRemoveItem(selection.Id);
    }

    private void UpdateNodeName(ChangeEvent<string> evt, VisualElement container)
    {
        var node = m_TreeView.GetItemDataForIndex<INode>((int)container.userData);
        node.Name = evt.newValue;
        container.Q<TextField>("textfield").style.display = DisplayStyle.None;
        m_TreeView.RefreshItems();
    }
    private void UpdateNodeName(VisualElement container)
    {
        var textField = container.Q<TextField>("textfield");
        var node = m_TreeView.GetItemDataForIndex<INode>((int)container.userData);
        node.Name = textField.value;
        textField.style.display = DisplayStyle.None;
        m_TreeView.RefreshItems();
    }

    private void EditNodeNode(IEnumerable<object> obj)
    {
        var selection = obj.First() as INode;
        if (selection.GetType() == typeof(Leaf)) return;
        int selectionIndex = m_TreeView.selectedIndex;
        var itemNode = m_TreeView.Q<VisualElement>("unity-content-container").ElementAt(selectionIndex).Q<VisualElement>("container");
        var textField = itemNode.Q<TextField>("textfield");
        textField.style.display = DisplayStyle.Flex;
        textField.SetValueWithoutNotify(selection.Name);

        rootVisualElement.schedule.Execute(() => { 
            textField.Focus();
        });
    }

    private void UpdateRuleEditorStatus(IEnumerable<object> obj)
    {
        var selection = obj.First() as INode;
        if (selection.GetType() != typeof(Leaf))
        {
            m_RuleInspectorContainer.SetEnabled(false);
            return;
        }
        m_RuleInspectorContainer.SetEnabled(true);
    }

    public void OnButtonClicked()
    {
        var selectionCollection = m_TreeView.GetSelectedItems<INode>();
        if (selectionCollection.Count() > 0)
        {
            var ruleLabel = m_RuleInspectorContainer.Q<VisualElement>("RuleInspector").Q<Label>();
            var selection = selectionCollection.First();
            var leafNode = selection.data as Leaf;
            var rule = leafNode.Rule;
            ruleLabel.text = $"Got the Rule from the Leaf Node with id: {selection.id}\nrule is now expty by defualt: {rule}";
        }
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
            string prefix = selection.data.Prefix == "R." ? "" : selection.data.Prefix;
            prefix += $"{selection.children.Count() + 1}.";

            if (nodeType == "leaf") node = new Leaf(id, "No Rule", prefix);
            else node = new Internal(id, InternalNodeNames[nodeType], prefix);
            var newItem = new TreeViewItemData<INode>(id, node);
            m_TreeView.AddItem(newItem, parentId: selection.id, rebuildTree: true);
            m_TreeView.ExpandItem(selection.id);
        }
    }

}






#region Context Menu
/*
//Add contextual menu only to the Internal Nodes.
if (node.GetType() != typeof(Leaf))
{
    container.AddManipulator(new ContextualMenuManipulator(evt => { BuildContextualMenu(evt, container.label, node); }));
}
*/
#endregion


#region Button for each node
/*
public class TestElement : VisualElement
{
    public static readonly string ussNodeContainer = "node-container";
    public static readonly string ussNodeLabel = "node-label";

    public TestElement()
    {
        AddToClassList(ussNodeContainer);
        var label = new Label();
        label.AddToClassList(ussNodeLabel);
        var button = new UnityEngine.UIElements.Button();
        button.text = "Test";
        Add(label);
        Add(button);
    }
}





m_TreeView.makeItem = () => new TestElement();

        // Set TreeView.bindItem to bind an initialized node to a data item.
        m_TreeView.bindItem = (VisualElement element, int index) =>
        {

            (element as TestElement).Q<Label>().text = m_TreeView.GetItemDataForIndex<INode>(index).Name;
            var button = (element as TestElement).Q<UnityEngine.UIElements.Button>();
            if(m_TreeView.GetItemDataForIndex<INode>(index).GetType() == typeof(Leaf))
            {
                button.visible = false;
            }
            else
            {
                button.clickable = new Clickable(TestButtonClick);
            }
        };
*/
#endregion