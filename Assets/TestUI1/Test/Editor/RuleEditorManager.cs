using ECARules4All;
using ECARules4All.RuleEngine;
using PlasticGui.Help.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro.EditorUtilities;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Examples.UIRule.Prefabs;
using static ECARules4All.RuleEngine.CompositeCondition;
using Action = ECARules4All.RuleEngine.Action;
using FloatField = UnityEngine.UIElements.FloatField;


public class CustomCondition
{
    public ConditionType op;
    public SimpleCondition condition = new();


    public static List<CustomCondition> ParseConditionToCustom(Condition condition)
    {
        List<CustomCondition> conditionsList = new();

        conditionsList = DFS(condition, conditionsList);


        return conditionsList;
    }

    public static Condition ParseCustomToCondition(List<CustomCondition> customList)
    {
        Condition condition = customList.First().condition;

        customList.RemoveAt(0);
        foreach (var customC in customList)
        {
            //If there is only one node in the condition tree.
            if (condition.IsLeaf())
            {
                if(customC.op == ConditionType.NOT)
                {
                    CompositeCondition notCondition = new(ConditionType.NOT, new List<Condition>() { customC.condition });
                    condition = new CompositeCondition(ConditionType.OR, new List<Condition>() { condition, notCondition });
                }
                else
                    condition = new CompositeCondition(customC.op, new List<Condition>() { condition, customC.condition });
            }
            //If the new condition has the same op as the current tree root, add it as a child.
            else if((condition as CompositeCondition).Op == customC.op)
            {
                (condition as CompositeCondition).AddChild(customC.condition);
            }
            //If the new condition has op NOT, we first need to create a CompositeCondition with op NOT and the condition as only child.
            else if(customC.op == ConditionType.NOT)
            {
                CompositeCondition notCondition = new(ConditionType.NOT, new List<Condition>() { customC.condition });
                //The Not condition is added to tree under an Or type condition. If there is already one as root, add the Not conditon as its child.
                if ((condition as CompositeCondition).Op == ConditionType.OR)
                {
                    (condition as CompositeCondition).AddChild(notCondition);
                }
                //Otherwise create an Or condition set it as new root of the condition tree.
                else
                    condition = new CompositeCondition(ConditionType.OR, new List<Condition>() { condition, notCondition });
            }
            //If the new node cannot be directly added as child of the current root, create a new one.
            else
                condition = new CompositeCondition(customC.op, new List<Condition>() { condition, customC.condition });
        }

        return condition;
    }

    private static List<CustomCondition> DFS(Condition condition, List<CustomCondition> conditionsList)
    {
        if (condition.IsLeaf()) 
        {
            CustomCondition customCondition = new();
            customCondition.condition = condition as SimpleCondition;
            customCondition.op = (condition.GetParent() as CompositeCondition).Op;
            conditionsList.Add(customCondition);
            return conditionsList;
        }
        foreach (var child in (condition as CompositeCondition).Children())
        {
            conditionsList = DFS(child, conditionsList);
        }
        return conditionsList;

    }

    public static ConditionType ParseStringToConditionType(string cType)
    {
        return cType switch
        {
            "And" => ConditionType.AND,
            "Or" => ConditionType.OR,
            "Not" => ConditionType.NOT,
            _ => ConditionType.NONE,
        };
    }

    public override string ToString()
    {
        return $"{op} {condition}";
    }
}




public class RuleEditorManager
{
    public static VisualElement EventContainer { get; set; }
    public static VisualElement ConditionsContainer { get; set; }
    private static VisualElement conditionHeader;  
    public static ScrollView ActionsSV { get; set; }

    private static readonly VisualTreeAsset m_ActionPrefabUxml = Resources.Load<VisualTreeAsset>("ActionPrefab");
    private static readonly VisualTreeAsset m_ConditionPrefabUxml = Resources.Load<VisualTreeAsset>("ConditionPrefab");
    
    
    private ActionDropdownsManager eventManager = new();
    //private List<ConditionDropdownsManager> conditionManagers = new();
    //private List<ActionDropdownsManager> actionManagers = new();

    private Action eventAction = new();
    private List<Action> actions = new() { new Action() };
    private List<CustomCondition> conditions = new();


    public static void SetupManager(VisualElement ruleEditor)
    {
        EventContainer = ruleEditor.Q<VisualElement>("RuleParts").Q<VisualElement>("Event");

        ConditionsContainer = ruleEditor.Q<VisualElement>("RuleParts").Q<VisualElement>("Conditions");
        conditionHeader = ruleEditor.Q<VisualElement>("Headers").Q<VisualElement>("Conditions");

        ActionsSV = ruleEditor.Q<VisualElement>("RuleParts").Q<VisualElement>("Actions").Q<ScrollView>("ActionsSV");
    }


    public RuleEditorManager(Rule rule = null)
    {
        //Get the event if there is already a Rule inside the Leaf node.
        if (rule != null) eventAction = rule.GetEvent();
        //Setup the event.
        //eventAction can either be the event in the Rule or a new blank Action.
        eventManager.SetUpDropdownMenus(EventContainer, eventAction);

        //Get the actions if there is already a Rule inside the Leaf node.
        if (rule != null) actions = rule.GetActions();

        //Add the actions (Only one if there is no Rule).
        foreach (var action in actions)
        {
            AddAction(action);
        }

        //Get the conditions if there is already a Rule inside the Leaf node.
        if (rule != null) conditions = CustomCondition.ParseConditionToCustom(rule.GetCondition());

        //Add the conditions (None if there is no Rule).
        foreach (var condition in conditions)
        {
            AddCondition(condition);
        }
    }


    public void AddAction(Action action, bool isNew = false)
    {
        //If the action is new, add it to the list.
        if (isNew) actions.Add(action);

        VisualElement actionPrefab = m_ActionPrefabUxml.CloneTree();

        if (ActionsSV.childCount > 0)
        {
            actionPrefab.Q<VisualElement>("RemoveButton").style.display = DisplayStyle.Flex;
            var removeButton = actionPrefab.Q<UnityEngine.UIElements.Button>("Button");
            removeButton.clickable = new Clickable(evt => { RemoveAction(actionPrefab, action); });
        }

        var actionManager = new ActionDropdownsManager();
        //actionManagers.Add(actionManager);
        actionManager.SetUpDropdownMenus(actionPrefab, action);

        ActionsSV.Add(actionPrefab);
    }
    public void AddCondition(CustomCondition condition, bool isNew = false)
    {
        //If the condition is new, add it to the list.
        if (isNew) conditions.Add(condition);

        var conditionsSV = ConditionsContainer.Q<ScrollView>("ConditionsSV");
        VisualElement conditionPrefab = m_ConditionPrefabUxml.CloneTree();

        var removeButton = conditionPrefab.Q<UnityEngine.UIElements.Button>("Button");
        removeButton.clickable = new Clickable(evt => { RemoveCondition(conditionPrefab, condition); });

        if (conditionsSV.childCount > 0)
        {
            conditionPrefab.Q<VisualElement>("ToCheckC").Q<DropdownField>("AndOr").style.display = DisplayStyle.Flex;
        }

        var conditionManager = new ConditionDropdownsManager();
        //conditionManagers.Add(conditionManager);
        conditionManager.SetUpDropdownMenus(conditionPrefab, condition);
            
        conditionsSV.Add(conditionPrefab);

        //The section is visible iff there is at least one condition.
        SetConditionsSectionVisibility(DisplayStyle.Flex);
    }


    public void RemoveAction(VisualElement actionElement, Action action)
    {
        actions.Remove(action);
        ActionsSV.Remove(actionElement);
    }
    public void RemoveCondition(VisualElement conditionElement, CustomCondition condition)
    {
        var conditionsSV = ConditionsContainer.Q<ScrollView>("ConditionsSV");
        conditions.Remove(condition);
        conditionsSV.Remove(conditionElement);

        if(conditionsSV.childCount > 0)
        {
            var andOrDrop = conditionsSV.contentContainer.Q<VisualElement>("Condition").Q<VisualElement>("ToCheckC").Q<DropdownField>("AndOr");
            andOrDrop.value = "";
            andOrDrop.style.display = DisplayStyle.None;
        }
        
        //Manage section visibility
        //If there are no more conditions, the section is not longer visible.
        if(conditionsSV.childCount == 0)
            SetConditionsSectionVisibility(DisplayStyle.None);
    }

    private void SetConditionsSectionVisibility(DisplayStyle displayStyle)
    {
        conditionHeader.style.display = displayStyle;
        ConditionsContainer.style.display = displayStyle;
    }


    public void DiscardRule()
    {
        //Discard Event
        eventAction = new Action();
        eventManager.SetUpDropdownMenus(EventContainer, eventAction);

        //Discard Actions
        actions = new List<Action>() { new Action() };
        ActionsSV.Clear();
        AddAction(actions[0]);

        //Discard Condtions
        conditions = new List<CustomCondition>();
        var conditionSV = ConditionsContainer.Q<ScrollView>("ConditionsSV");
        conditionSV.Clear();
        SetConditionsSectionVisibility(DisplayStyle.None);
    }

    public Rule GetRule()
    {
        if(conditions.Count == 0)
            return new Rule(eventAction, actions);
        var condition = CustomCondition.ParseCustomToCondition(conditions);
        return new Rule(eventAction, condition, actions);
    }
}





public class ActionDropdownsManager
{
    #region Attributes
    //Action Dropdowns and TextField
    private DropdownField subjectDrop;
    private DropdownField verbDrop;
    private DropdownField objectVerbDrop;
    private Label prefixThe;
    private DropdownField prepDrop;
    private DropdownField objectDrop;
    private DropdownField valueDrop;
    private TextField textField;
    private IntegerField intField;
    private FloatField decimalField;
    private DropdownField inputDrop;

    private Dictionary<int, Dictionary<GameObject, string>> subjects = new();
    private Dictionary<int, VerbComposition> verbsItem = new();
    private Dictionary<string, List<ActionAttribute>> verbsString = new();
    //Dictionary for the subject selected with all its state variables and the type
    private Dictionary<string, (ECARules4AllType, Type)> stateVariables = new();

    //Selected
    private GameObject subjectSelected; //gameobject with the subject
    private GameObject previousSelectedSubject, previousSelectedObject = null;
    private string subjectSelectedType; //e.g. ECALight, Character....
    private string verbSelectedString; //string with the verb
    private string VerbSelectedType;
    private GameObject objectSelected;
    private string objSelectedType;

    public static Dictionary<string, Color> colorDict = new()
    {
        {"blue", Color.blue}, // 0xff1f77b4,
        {"green", Color.green}, // 0xffd62728
        {"red", Color.red}, // 0xff9467bd
        {"purple", Color.magenta}, // 0xff9467bd
        {"gray", Color.gray}, // 0xff7f7f7f
        {"grey", Color.grey}, // 0xff7f7f7f
        {"yellow", Color.yellow}, // 0xffbcbd22
        {"cyan", Color.cyan}, // 0xff17becf
        {"white", Color.white}, // 0xffffffff
    };
    public static Dictionary<string, string> colorDictHex = new()
    {
        {"blue", "#1f77b4ff"},
        {"orange", "#ff7f0eff"},
        {"green", "#d62728ff"},
        {"red", "#9467bdff"},
        {"purple", "#9467bdff"},
        {"brown", "#8c564bff"},
        {"pink", "#e377c2ff"},
        {"gray", "#7f7f7fff"},
        {"grey", "#7f7f7fff"},
        {"yellow", "#bcbd22ff"},
        {"cyan", "#bcbd22ff"},
        {"white", "#ffffffff"},
    };
    public static Dictionary<Color, string> reversedColorDict = new()
    {
       // { UIColors.blue, "blue" }, // 0xff1f77b4,
        {UIColors.orange, "orange"}, // 0xffff7f0e
        {UIColors.green, "green"}, // 0xffd62728
        {UIColors.red, "red"}, // 0xff9467bd
        {UIColors.purple, "purple"}, // 0xff9467bd
        {UIColors.brown, "brown"}, // 0xff8c564b
        {UIColors.pink, "pink"}, // 0xffe377c2
        {UIColors.gray, "gray"}, // 0xff7f7f7f
        {UIColors.grey, "grey"}, // 0xff7f7f7f
        {UIColors.yellow, "yellow"}, // 0xffbcbd22
        {UIColors.cyan, "cyan"}, // 0xff17becf
        {UIColors.white, "white"}, // 0xffffffff
    };
    #endregion

    //The values are saved in the action, but not permanently (user needs to press Save for that).
    public void SetUpDropdownMenus(VisualElement actionContainer, Action action)
    {
        var subjectPart = actionContainer.Q<VisualElement>("SubjectC");
        subjectDrop = subjectPart.Q<DropdownField>("Subject");

        var verbPart = actionContainer.Q<VisualElement>("VerbC");
        verbDrop = verbPart.Q<DropdownField>("Verb");
        objectVerbDrop = verbPart.Q<DropdownField>("ObjectVerb");

        var objectPart = actionContainer.Q<VisualElement>("ObjectC");
        prefixThe = objectPart.Q<Label>("PrefixThe");
        prepDrop = objectPart.Q<DropdownField>("Prep");
        objectDrop = objectPart.Q<DropdownField>("Object");
        valueDrop = objectPart.Q<DropdownField>("Value");
        textField = objectPart.Q<VisualElement>("InputField").Q<TextField>("TextInput");
        intField = objectPart.Q<VisualElement>("InputField").Q<IntegerField>("IntInput");
        decimalField = objectPart.Q<VisualElement>("InputField").Q<FloatField>("DecimalInput");
        inputDrop = objectPart.Q<VisualElement>("InputField").Q<DropdownField>("Dropdown");


        subjectDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedSubject(subjectDrop, action); });
        verbDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedVerb(verbDrop, action); });
        //object with the value e.g. changes "active" ...
        objectVerbDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedObjectValue(objectVerbDrop, action); });
        //object without the value e.g. looks at gameobject
        objectDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedObject(objectDrop, action); });

        prepDrop.RegisterValueChangedCallback(delegate { action.SetModifier(prepDrop.value); });
        valueDrop.RegisterValueChangedCallback(delegate { action.SetModifierValue(valueDrop.value); });
        textField.RegisterValueChangedCallback(delegate { action.SetModifierValue(textField.value); });
        intField.RegisterValueChangedCallback(delegate { action.SetModifierValue(intField.value); });
        decimalField.RegisterValueChangedCallback(delegate { action.SetModifierValue(decimalField.value); });
        inputDrop.RegisterValueChangedCallback(delegate { action.SetModifierValue(inputDrop.value); });

        //prepDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedPrep(prepDrop, action); });
        //valueDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedInput(valueDrop.value, action); });
        //textField.RegisterValueChangedCallback(delegate { DropdownValueChangedInput(textField.value, action); });
        //intField.RegisterValueChangedCallback(delegate { DropdownValueChangedInput(intField.value, action); });
        //decimalField.RegisterValueChangedCallback(delegate { DropdownValueChangedInput(decimalField.value, action); });
        //inputDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedInput(inputDrop.value, action); });

        SetUpSubject(subjectDrop, action);
    }


    void SetUpSubject(DropdownField subjectDrop, Action action)
    {
        subjects = RuleUtils.FindSubjects();
        List<string> entries = new List<string>();

        string actionSubjectValue = "<no-value>";
        GameObject actionSubject = action.GetSubject();

        subjectDrop.choices.Clear();

        entries.Add("<no-value>");

        for (int i = 0; i < subjects.Count; i++)
        {
            foreach (KeyValuePair<GameObject, string> entry in subjects[i])
            {
                string type = RuleUtils.FindInnerTypeNotBehaviour(entry.Key);
                type = RuleUtils.RemoveECAFromString(type);
                entries.Add(type + " " + entry.Key.name);
                if (actionSubject == entry.Key) 
                    actionSubjectValue = type + " " + entry.Key.name;
            }
        }
        entries.Sort();
        subjectDrop.choices = entries;
        subjectDrop.value = actionSubjectValue;

    }
    void DropdownValueChangedSubject(DropdownField subjectDropdown, Action action)
    {
        DisableNextComponent("subject", action);
        if (subjectDropdown.value == "<no-value>")
        {
            action.SetSubject(null);
            return;
        }

        verbDrop.style.display = DisplayStyle.Flex;
        verbDrop.choices.Clear();

        //When we go back from the rule list we assign the value to 0
        if (subjectDropdown.index == 0) return;

        //retrieve selected string and gameobject
        string selectedSubjectString = subjectDropdown.value;

        //I need to cut the string because in the dropdown we use "Type Name", the dictionary only contains the type
        string selectedCutString = Regex.Match(selectedSubjectString, "[^ ]* (.*)").Groups[1].Value;
        previousSelectedSubject = subjectSelected;

        subjectSelected = GameObject.Find(selectedCutString).gameObject;
        action.SetSubject(subjectSelected);

        //we have to find it from the dictionary, because some types are trimmed (see ECALight -> Light)
        foreach (var item in subjects)
        {
            foreach (var keyValuePair in item.Value)
            {
                if (keyValuePair.Key == subjectSelected)
                {
                    subjectSelectedType = keyValuePair.Value;
                }
            }
        }


        verbsItem = RuleUtils.FindActiveVerbs(subjectSelected, subjects, subjectSelectedType, true);
        RuleUtils.FindPassiveVerbs(subjectSelected, subjects, subjectSelectedType, ref verbsItem);

        verbsString.Clear();

        verbsString = RuleUtils.PopulateVerbsString(verbsItem);

        // Add options to verb dropdown
        List<string> entries = new List<string>();
        entries.Add("<no-value>");
        foreach (var s in verbsString)
        {
            entries.Add(s.Key);
        }
        entries.Sort();
        verbDrop.choices = entries;
        verbDrop.value = "<no-value>";
    }
    void DropdownValueChangedVerb(DropdownField verbDrop, Action action)
    {
        //retrieve selected string and gameobject
        verbSelectedString = verbDrop.value;
        int verbSelectedIndex = verbDrop.index;

        DisableNextComponent("verb", action);


        if (verbSelectedString == "<no-value>")
        {
            action.SetActionMethod(null);
            return;
        }
        action.SetActionMethod(verbSelectedString);

        //now, I need to know if the object would be a GameObject or a value 
        List<ActionAttribute> actionAttributes = verbsString[verbSelectedString];

        // Used to sort each dropdown's options
        List<string> entries = new List<string>();

        //we need to activate the object dropdown
        if (actionAttributes.Count == 1 || RuleUtils.SameAttributesList(actionAttributes))
        {
            ActionAttribute ac = actionAttributes[0];
            if (ac.ObjectType != null)
            {
                //Debug.Log(ac.ObjectType.Name);
                VerbSelectedType = ac.ObjectType.Name;

                switch (ac.ObjectType.Name)
                {
                    case "Object":
                    case "ECAObject":
                    case "GameObject":
                        prefixThe.style.display = DisplayStyle.Flex;
                        objectDrop.style.display = DisplayStyle.Flex;
                        objectDrop.choices.Clear();
                        entries.Add("<no-value>");

                        for (int i = 0; i < subjects.Count; i++)
                        {
                            foreach (KeyValuePair<GameObject, string> entry in subjects[i])
                            {
                                //TODO handle alias
                                if (entry.Key != subjectSelected)
                                {
                                    //type needs a refactor: it can't be a behaviour and if contains "ECA" should be parsed
                                    string type = RuleUtils.FindInnerTypeNotBehaviour(entry.Key);
                                    type = RuleUtils.RemoveECAFromString(type);
                                    // objDrop.options.Add(new Dropdown.OptionData(type + " " + entry.Key.name));
                                    entries.Add(type + " " + entry.Key.name);
                                }
                            }
                        }

                        break;
                    case "YesNo":
                        objectDrop.style.display = DisplayStyle.Flex;
                        objectDrop.choices.Clear();
                        entries.Add("<no-value>");
                        entries.Add("yes");
                        entries.Add("no");
                        break;
                    case "TrueFalse":
                        objectDrop.style.display = DisplayStyle.Flex;
                        objectDrop.choices.Clear();
                        entries.Add("<no-value>");
                        entries.Add("true");
                        entries.Add("false");
                        break;
                    case "OnOff":
                        objectDrop.style.display = DisplayStyle.Flex;
                        objectDrop.choices.Clear();
                        entries.Add("<no-value>");
                        entries.Add("on");
                        entries.Add("off");
                        break;
                    case "Single": //Float
                        ActivateInputField("decimal");
                        break;
                    case "String":
                        ActivateInputField("string");
                        break;
                    case "Rotation":
                        objectVerbDrop.style.display = DisplayStyle.Flex;
                        objectVerbDrop.choices.Clear();
                        objectVerbDrop.choices.Add("<no-value>");
                        objectVerbDrop.choices.Add("x");
                        objectVerbDrop.choices.Add("y");
                        objectVerbDrop.choices.Add("z");
                        objectVerbDrop.value = "<no-value>";
                        break;
                    case "Int32":
                        ActivateInputField("integer");
                        break;
                    default:
                        //it can be a typeof(EcaComponent), but first we need to retrieve the component
                        prefixThe.style.display = DisplayStyle.Flex;
                        string comp = ac.ObjectType.Name;
                        Component c = subjectSelected.GetComponent(comp);
                        objectDrop.style.display = DisplayStyle.Flex;
                        objectDrop.choices.Clear();
                        objectDrop.choices.Add("<no-value>");
                        //it's possible that the verb is passive (e.g. character eats food),
                        //in this case we don't find it in the selected subject, but in one of the subjects
                        if (c == null)
                        {
                            if (subjects.Count == 0) subjects = RuleUtils.FindSubjects();
                            RuleUtils.AddObjectPassiveVerbs(subjects, comp, objectDrop);

                        }
                        else //the verb is not passive, the object component can be found in all ecaobject in the scene
                        {
                            RuleUtils.AddObjectActiveVerbs(subjects, comp, objectDrop, subjectSelected);
                        }

                        break;
                }

                entries.Sort();
                objectDrop.choices = entries;
                objectDrop.value = "<no-value>";
            }
            //value e.g. increases intensity
            else if (ac.ValueType != null)
            {
                objectVerbDrop.style.display = DisplayStyle.Flex;
                objectVerbDrop.choices.Clear();
                objectVerbDrop.choices.Add("<no-value>");
                objectVerbDrop.choices.Add(ac.variableName);
            }
        }
        //in the else case, the sentence is composed only of two words (e.g. vehicle starts)
        //we don't need to activate anything

        //if actionAttributes.Count is >1 means that there are verbs like changes, that has
        //more attributes (active, visibility...)
        //we activate the object value drop
        else
        {
            VerbSelectedType = null;
            objectVerbDrop.choices.Clear();
            objectVerbDrop.choices.Add("<no-value>");
            foreach (var ac in actionAttributes)
            {
                if (ac.ValueType != null)
                {
                    objectVerbDrop.style.display = DisplayStyle.Flex;
                    // objValueDrop.options.Add(new Dropdown.OptionData(ac.variableName));
                    entries.Add(ac.variableName);
                }
            }
            entries.Sort();
            objectVerbDrop.choices = entries;
            objectVerbDrop.value = "<no-value>";
        }
    }
    void DropdownValueChangedObject(DropdownField objectDrop, Action action)
    {
        DisableNextComponent("object", action);
        //retrieve selected string and gameobject
        var objSelectedString = objectDrop.value;
        if (objSelectedString == "<no-value>")
        {
            action.SetObject(null);
            return;
        }

        string selectedCutString = Regex.Match(objSelectedString, "[^ ]* (.*)").Groups[1].Value;
        //The object selected is a GameObject
        if (GameObject.Find(selectedCutString) != null)
        {
            previousSelectedObject = objectSelected;
            objectSelected = GameObject.Find(selectedCutString);
            action.SetObject(objectSelected);
        }
        else objectSelected = null;
    }
    void DropdownValueChangedObjectValue(DropdownField objectVerbDrop, Action action)
    {
        DisableNextComponent("object", action);
        //retrieve selected string and gameobject
        var objSelectedString = objectVerbDrop.value;
        if (objSelectedString == "<no-value>") return;
        objectSelected = null;

        action.SetActionMethod($"{action.GetActionMethod()} {objSelectedString}");

        prepDrop.style.display = DisplayStyle.Flex;

        //retrieve action attributes
        verbsString = RuleUtils.PopulateVerbsString(verbsItem);
        List<ActionAttribute> actionAttributes = verbsString[verbSelectedString];
        valueDrop.choices.Clear();

        stateVariables = RuleUtils.FindStateVariables(subjectSelected);
        foreach (var ac in actionAttributes)
        {
            // Used to sort each dropdown's options
            List<string> entries = new List<string>();

            if (ac.ObjectType == typeof(Rotation))
            {
                ActivateInputField("decimal");
                prepDrop.style.display = DisplayStyle.None;
                objSelectedType = "Rotation";
                return;
            }

            if (ac.variableName == objSelectedString)
            {
                prepDrop.choices.Add(ac.ModifierString);
                prepDrop.value = ac.ModifierString;
                objSelectedType = ac.ValueType.Name;

                switch (ac.ValueType.Name)
                {
                    case "YesNo":
                        valueDrop.style.display = DisplayStyle.Flex;
                        entries.Add("<no-value>");
                        entries.Add("yes");
                        entries.Add("no");
                        break;
                    case "TrueFalse":
                        valueDrop.style.display = DisplayStyle.Flex;
                        entries.Add("<no-value>");
                        entries.Add("true");
                        entries.Add("false");
                        break;
                    case "OnOff":
                        valueDrop.style.display = DisplayStyle.Flex;
                        entries.Add("<no-value>");
                        entries.Add("on");
                        entries.Add("off");
                        break;
                    case "String":
                        if (objSelectedString == "mesh")
                        {
                            valueDrop.style.display = DisplayStyle.Flex;
                            entries.Add("<no-value>");
                            foreach (var mesh in UIManager.items)
                                entries.Add(mesh);
                        }
                        else ActivateInputField("alphanumeric");
                        break;

                    case "ECAColor":
                        valueDrop.style.display = DisplayStyle.Flex;
                        entries.Add("<no-value>");
                        // Add colors to dropdown
                        foreach (KeyValuePair<string, Color> kvp in colorDict)
                            entries.Add(kvp.Key);
                        break;

                    case "Single":
                    case "Double":
                        ActivateInputField("decimal");
                        break;

                    case "Int32":
                        ActivateInputField("Integer");
                        break;
                    //TODO optimize
                    case "POV":
                        valueDrop.style.display = DisplayStyle.Flex;
                        entries.Add("<no-value>");
                        entries.Add("First");
                        entries.Add("Third");
                        break;
                }
                entries.Sort();
                valueDrop.choices = entries;
                valueDrop.value = "<no-value>";
                return;
            }
        }

    }
    void DropdownValueChangedPrep(DropdownField prepDrop, Action action)
    {
        action.SetModifier(prepDrop.value);
    }
    void DropdownValueChangedInput(object inputDrop, Action action)
    {
        if ((inputDrop as string) == "<no-value>") action.SetModifierValue(null);
        action.SetModifierValue(inputDrop);
    }

    void ActivateInputField(string validationType)
    {
        switch (validationType)
        {
            case "decimal":
                decimalField.style.display = DisplayStyle.Flex;
                decimalField.Focus();
                decimalField.value = 0;
                break;
            case "integer":
                intField.style.display = DisplayStyle.Flex;
                intField.Focus();
                intField.value = 0;
                break;
            default:
                textField.style.display = DisplayStyle.Flex;
                textField.Focus();
                textField.value = "";
                break;
        }
    }
    void DisableNextComponent(string changedField, Action action)
    {
        switch (changedField)
        {
            // Change subject
            case "subject":
                verbDrop.style.display = DisplayStyle.None;
                objectVerbDrop.style.display = DisplayStyle.None;
                action.SetActionMethod(null);

                prefixThe.style.display = DisplayStyle.None;
                prepDrop.style.display = DisplayStyle.None;
                objectDrop.style.display = DisplayStyle.None;
                valueDrop.style.display = DisplayStyle.None;
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                inputDrop.style.display = DisplayStyle.None;
                action.SetObject(null);
                action.SetModifier(null);
                action.SetModifierValue(null);
                break;
            // Change verb
            case "verb":
                objectVerbDrop.style.display = DisplayStyle.None;

                prefixThe.style.display = DisplayStyle.None;
                prepDrop.style.display = DisplayStyle.None;
                objectDrop.style.display = DisplayStyle.None;
                valueDrop.style.display = DisplayStyle.None;
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                inputDrop.style.display = DisplayStyle.None;
                action.SetObject(null);
                action.SetModifier(null);
                action.SetModifierValue(null);
                break;
            // Change object
            case "object":
                valueDrop.style.display = DisplayStyle.None;
                valueDrop.choices.Clear();
                action.SetModifier(null);
                action.SetModifierValue(null);
                break;
        }
    }
    GameObject GetGameObjectFromName(string name)
    {
        for (int i = 0; i < subjects.Count; i++)
        {
            foreach (KeyValuePair<GameObject, string> entry in subjects[i])
            {
                string type = RuleUtils.FindInnerTypeNotBehaviour(entry.Key);
                type = RuleUtils.RemoveECAFromString(type);
                if (name == entry.Key.name)
                {
                    return entry.Key;
                }
            }
        }
        return null;
    }
}


public class ConditionDropdownsManager
{
    #region Attributes
    //Action Dropdowns and TextField
    private DropdownField toCheckDrop;
    private DropdownField propertyDrop;
    private DropdownField checkSymbolDrop;
    private DropdownField compareWithDrop;
    private DropdownField andOrDrop;

    //Input Fields reference
    private TextField textField;
    private IntegerField intField;
    private FloatField decimalField;

    //Dictionary foreach subject with the reference of the gameobject
    private Dictionary<int, Dictionary<GameObject, string>> toCheckDictionary = new Dictionary<int, Dictionary<GameObject, string>>();
    //Dictionary for the subject selected with all its state variables and the type
    private Dictionary<string, (ECARules4AllType, Type)> stateVariables = new Dictionary<string, (ECARules4AllType, Type)>();

    //Selected toCheck
    private GameObject toCheckSelected, previousToCheckSelected; //gameobject with the subject
    private string toCheckSelectedType;

    //Selected property
    private string propertySelected;

    //selected symbol
    private string selectedSymbol;

    private static List<string> booleanSymbols = new List<string>() { "is", "is not" };
    private static List<string> matemathicalSymbols = new List<string>() { "=", "!=", ">", "<", "<=", ">=" };

    private ECARules4AllType compareWithType;
    #endregion

    //The values are saved in the condition, but not permanently (user needs to press Save for that).
    public void SetUpDropdownMenus(VisualElement conditionContainer, CustomCondition cCondition)
    {
        var toCheckPart = conditionContainer.Q<VisualElement>("ToCheckC");
        toCheckDrop = toCheckPart.Q<DropdownField>("ToCheck");
        andOrDrop = toCheckPart.Q<DropdownField>("AndOr");
        andOrDrop.choices = new List<string>() { "And", "Or", "Not" };
        andOrDrop.SetValueWithoutNotify("And");

        var propertyPart = conditionContainer.Q<VisualElement>("PropertyC");
        propertyDrop = propertyPart.Q<DropdownField>("Property");
        checkSymbolDrop = propertyPart.Q<DropdownField>("CheckSymbol");

        var compareWithPart = conditionContainer.Q<VisualElement>("CompareWithC");
        compareWithDrop = compareWithPart.Q<DropdownField>("CompareWith");
        textField = compareWithPart.Q<TextField>("TextInput");
        intField = compareWithPart.Q<IntegerField>("IntInput");
        decimalField = compareWithPart.Q<FloatField>("DecimalInput");


        //Listener
        andOrDrop.RegisterValueChangedCallback(delegate { cCondition.op = CustomCondition.ParseStringToConditionType(andOrDrop.value); });
        toCheckDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedToCheck(toCheckDrop, cCondition); });
        
        propertyDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedProperty(propertyDrop, cCondition); });
        checkSymbolDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedCheckSymbol(checkSymbolDrop, cCondition); });

        compareWithDrop.RegisterValueChangedCallback(delegate { cCondition.condition.SetValueToCompare(GetGameObjectFromName(compareWithDrop.value)); });
        textField.RegisterValueChangedCallback(delegate { cCondition.condition.SetValueToCompare(textField.value); });
        intField.RegisterValueChangedCallback(delegate { cCondition.condition.SetValueToCompare(intField.value); });
        decimalField.RegisterValueChangedCallback(delegate { cCondition.condition.SetValueToCompare(decimalField.value); });


        //at start we must populate the first dropdown
        PopulateToCheckDropdown(cCondition);
    }


    private void PopulateToCheckDropdown(CustomCondition cCondition)
    {
        toCheckDrop.choices.Clear();
        toCheckDictionary = RuleUtils.FindSubjects();

        string conditionSubjectValue = "<no-value>";
        GameObject conditionSubject = cCondition.condition.GetSubject();

        // Used to sort each dropdown's options
        List<string> entries = new List<string>();
        entries.Add("<no-value>");

        for (int i = 0; i < toCheckDictionary.Count; i++)
        {
            foreach (KeyValuePair<GameObject, string> entry in toCheckDictionary[i])
            {
                string type = RuleUtils.FindInnerTypeNotBehaviour(entry.Key);
                type = RuleUtils.RemoveECAFromString(type);
                entries.Add(type + " " + entry.Key.name);
                if (conditionSubject == entry.Key)
                    conditionSubjectValue = type + " " + entry.Key.name;
            }
        }
        entries.Sort();
        toCheckDrop.choices = entries;
        toCheckDrop.value = conditionSubjectValue;
    }
    private void DropdownValueChangedToCheck(DropdownField toCheck, CustomCondition cCondition)
    {
        //if previous activated, hide next elements
        DisableNextComponent("toCheck");

        //retrieve selected string and gameobject
        string selectedSubjectString = toCheck.value;
        if (selectedSubjectString == "<no-value>") return;

        //activate next element
        propertyDrop.style.display = DisplayStyle.Flex;
        propertyDrop.choices.Clear();


        //I need to cut the string because in the dropdown we use "Type Name", the dictionary only contains the type
        string selectedCutString = Regex.Match(selectedSubjectString, "[^ ]* (.*)").Groups[1].Value;
        toCheckSelectedType = Regex.Match(selectedSubjectString, "^[^ ]+").Value;
        previousToCheckSelected = toCheckSelected;
        toCheckSelected = GameObject.Find(selectedCutString).gameObject;
        cCondition.condition.SetSubject(toCheckSelected);

        stateVariables = RuleUtils.FindStateVariables(toCheckSelected);

        // Used to sort each dropdown's options
        List<string> entries = new List<string>();
        entries.Add("");
        foreach (var var in stateVariables)
        {
            if (var.Key == "rotation")
            {
                entries.Add("rotation x");
                entries.Add("rotation y");
                entries.Add("rotation z");
            }
            else entries.Add(var.Key);
        }
        entries.Sort();
        propertyDrop.choices = entries;
        propertyDrop.SetValueWithoutNotify("<no-value>");
    }
    private void DropdownValueChangedProperty(DropdownField property, CustomCondition cCondition)
    {
        //if previous activated, hide next elements
        DisableNextComponent("property");

        //retrieve selected string and type
        propertySelected = property.value;
        if (propertySelected == "<no-value>") return;

        //activate next element
        checkSymbolDrop.style.display = DisplayStyle.Flex;
        checkSymbolDrop.choices.Clear();

        if (propertySelected.StartsWith("rotation ")) propertySelected = "rotation";
        cCondition.condition.SetProperty(propertySelected);

        //thanks to the dictionary, we can retrieve the type:
        ECARules4AllType type = stateVariables[propertySelected].Item1;


        // Used to sort each dropdown's options
        List<string> entries = new List<string>();
        entries.Add("<no-value>");
        switch (type)
        {
            case ECARules4AllType.Float:
            case ECARules4AllType.Integer:
                foreach (var symbol in matemathicalSymbols)
                {
                    entries.Add(symbol);
                }
                break;

            case ECARules4AllType.Boolean:
            case ECARules4AllType.Position:
            case ECARules4AllType.Rotation:
            case ECARules4AllType.Path:
            case ECARules4AllType.Color:
            case ECARules4AllType.Text:
            case ECARules4AllType.Identifier:
            case ECARules4AllType.Time:
                foreach (var symbol in booleanSymbols)
                {
                    entries.Add(symbol);
                }
                break;
        }
        entries.Sort();
        checkSymbolDrop.choices = entries;
        checkSymbolDrop.SetValueWithoutNotify("<no-value>");
    }
    private void DropdownValueChangedCheckSymbol(DropdownField checkSymbol, CustomCondition cCondition)
    {
        //retrieve selected string 
        selectedSymbol = checkSymbol.value;
        if (selectedSymbol == "<no-value>") return;
        cCondition.condition.SetSymbol(selectedSymbol);

        ECARules4AllType type = stateVariables[propertySelected].Item1;
        compareWithType = type;

        // Used to sort each dropdown's options
        List<string> entries = new List<string>();

        switch (type)
        {
            case ECARules4AllType.Color:
                compareWithDrop.choices.Clear();
                compareWithDrop.style.display = DisplayStyle.Flex;
                entries.Add("<no-value>");
                // Add colors to dropdown
                foreach (KeyValuePair<string, Color> kvp in DropdownHandler.colorDict)
                    entries.Add(kvp.Key);
                compareWithDrop.SetValueWithoutNotify("<no-value>");
                //if previous activated, hide the input fields
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                break;
            case ECARules4AllType.Position:
                //This uses the raycaster. TODO: take position of currently selected GameObject.
                compareWithDrop.choices.Clear();
                compareWithDrop.style.display = DisplayStyle.Flex;
                entries.Add("<no-value>");
                compareWithDrop.SetValueWithoutNotify("<no-value>");
                //if previous activated, hide the input fields
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                break;
            case ECARules4AllType.Boolean:
                compareWithDrop.choices.Clear();
                compareWithDrop.style.display = DisplayStyle.Flex;
                entries.Add("<no-value>");
                //TODO togliere questo schifo
                if (toCheckSelectedType == "ECALight" || toCheckSelectedType == "Light")
                {
                    entries.Add("on");
                    entries.Add("off");
                }
                else
                {
                    entries.Add("true");
                    entries.Add("false");
                }
                compareWithDrop.SetValueWithoutNotify("<no-value>");
                //if previous activated, hide the input fields
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                break;
            case ECARules4AllType.Float:
            case ECARules4AllType.Time:
            case ECARules4AllType.Rotation:
                ActivateInputField("decimal");
                break;
            case ECARules4AllType.Integer:
                ActivateInputField("integer");
                break;
            case ECARules4AllType.Text:
                ActivateInputField("alphanumeric");
                break;

            case ECARules4AllType.Identifier:
                //TODO alias
                if (propertySelected == "pov")
                {
                    compareWithDrop.choices.Clear();
                    compareWithDrop.style.display = DisplayStyle.Flex;
                    entries.Add("<no-value>");
                    entries.Add("First");
                    entries.Add("Third");
                    compareWithDrop.SetValueWithoutNotify("<no-value>");
                    //if previous activated, hide the input fields
                    textField.style.display = DisplayStyle.None;
                    intField.style.display = DisplayStyle.None;
                    decimalField.style.display = DisplayStyle.None;
                }

                break;
        }
        entries.Sort();
        compareWithDrop.choices = entries;
        compareWithDrop.SetValueWithoutNotify("<no-value>");
    }


    void DisableNextComponent(string changedField)
    {
        switch (changedField)
        {
            // ToCheck
            case "toCheck":
                propertyDrop.style.display = DisplayStyle.None;
                checkSymbolDrop.style.display = DisplayStyle.None;
                compareWithDrop.style.display = DisplayStyle.None;
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;

                break;
            // Property
            case "property":
                checkSymbolDrop.style.display = DisplayStyle.None;
                compareWithDrop.style.display = DisplayStyle.None;
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                break;
        }
    }
    void ActivateInputField(string validationType)
    {
        switch (validationType)
        {
            case "decimal":
                decimalField.style.display = DisplayStyle.Flex;
                decimalField.Focus();
                decimalField.value = 0;
                break;
            case "integer":
                intField.style.display = DisplayStyle.Flex;
                intField.Focus();
                intField.value = 0;
                break;
            default:
                textField.style.display = DisplayStyle.Flex;
                textField.Focus();
                textField.value = "";
                break;
        }
        compareWithDrop.style.display = DisplayStyle.None;
    }
    GameObject GetGameObjectFromName(string name)
    {
        for (int i = 0; i < toCheckDictionary.Count; i++)
        {
            foreach (KeyValuePair<GameObject, string> entry in toCheckDictionary[i])
            {
                string type = RuleUtils.FindInnerTypeNotBehaviour(entry.Key);
                type = RuleUtils.RemoveECAFromString(type);
                if (name == entry.Key.name)
                {
                    return entry.Key;
                }
            }
        }
        return null;
    }
}
