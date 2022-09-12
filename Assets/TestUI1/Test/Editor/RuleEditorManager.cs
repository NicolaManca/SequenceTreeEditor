using ECARules4All;
using ECARules4All.RuleEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Examples.UIRule.Prefabs;

public class RuleEditorManager
{
    //Event Dropdowns and TextField
    static DropdownField subjectDrop;
    static DropdownField verbDrop;
    static DropdownField objectVerbDrop;
    static Label prefixThe;
    static DropdownField prepDrop;
    static DropdownField objectDrop;
    static DropdownField valueDrop;
    static TextField textField;
    static IntegerField intField;
    static FloatField decimalField;
    static DropdownField inputDrop;

    static Dictionary<int, Dictionary<GameObject, string>> subjects = new();
    static Dictionary<int, VerbComposition> verbsItem = new();
    static Dictionary<string, List<ActionAttribute>> verbsString = new();
    //Dictionary for the subject selected with all its state variables and the type
    static Dictionary<string, (ECARules4AllType, Type)> stateVariables = new();

    //Selected
    static GameObject subjectSelected; //gameobject with the subject
    static GameObject previousSelectedSubject, previousSelectedObject = null;
    static string subjectSelectedType; //e.g. ECALight, Character....
    static string verbSelectedString; //string with the verb
    static string VerbSelectedType;
    static GameObject objectSelected;
    static string objSelectedType;

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



    public static void SetUpEventDropdownMenus(VisualElement eventPart)
    {
        var subjectPart = eventPart.Q<VisualElement>("SubjectC");
        subjectDrop = subjectPart.Q<DropdownField>("Subject");

        var verbPart = eventPart.Q<VisualElement>("VerbC");
        verbDrop = verbPart.Q<DropdownField>("Verb");
        objectVerbDrop = verbPart.Q<DropdownField>("ObjectVerb");

        var objectPart = eventPart.Q<VisualElement>("ObjectC");
        prefixThe = objectPart.Q<Label>("PrefixThe");
        prepDrop = objectPart.Q<DropdownField>("Prep");
        objectDrop = objectPart.Q<DropdownField>("Object");
        valueDrop = objectPart.Q<DropdownField>("Value");
        textField = objectPart.Q<VisualElement>("InputField").Q<TextField>("TextInput");
        intField = objectPart.Q<VisualElement>("InputField").Q<IntegerField>("IntInput");
        decimalField = objectPart.Q<VisualElement>("InputField").Q<FloatField>("DecimalInput");
        inputDrop = objectPart.Q<VisualElement>("InputField").Q<DropdownField>("Dropdown");


        subjectDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedSubject(subjectDrop); });
        verbDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedVerb(verbDrop); });
        //object without the value e.g. looks at gameobject
        objectDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedObject(objectDrop); });
        //object with the value e.g. changes "active" ...
        objectVerbDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedObjectValue(objectVerbDrop); });


        SetUpSubject(subjectDrop);
    }



    static void SetUpSubject(DropdownField subjectDrop)
    {
        subjects = RuleUtils.FindSubjects();
        List<string> entries = new List<string>();

        subjectDrop.choices.Clear();

        entries.Add("<no-value>");

        for (int i = 0; i < subjects.Count; i++)
        {
            foreach (KeyValuePair<GameObject, string> entry in subjects[i])
            {
                string type = RuleUtils.FindInnerTypeNotBehaviour(entry.Key);
                type = RuleUtils.RemoveECAFromString(type);
                entries.Add(type + " " + entry.Key.name);
            }
        }
        entries.Sort();
        subjectDrop.choices = entries;

        subjectDrop.SetValueWithoutNotify("<no-value>");

    }

    static void DropdownValueChangedSubject(DropdownField subjectDropdown)
    {
        DisableNextComponent("subject");
        if (subjectDropdown.value == "<no-value>") return;

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

        verbDrop.SetValueWithoutNotify("<no-value>");
    }

    static void DropdownValueChangedVerb(DropdownField verbDrop)
    {
        //retrieve selected string and gameobject
        verbSelectedString = verbDrop.value;
        int verbSelectedIndex = verbDrop.index;

        DisableNextComponent("verb");

        if (verbSelectedString == "<no-value>") return;
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
                        objectVerbDrop.SetValueWithoutNotify("<no-value>");
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
                objectDrop.SetValueWithoutNotify("<no-value>");
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
            objectVerbDrop.SetValueWithoutNotify("<no-value>");
        }
    }
    
    static void DropdownValueChangedObject(DropdownField objectDrop)
    {
        //Debug.Log("Called ChangedObjectValue");
        DisableNextComponent("object");
        //retrieve selected string and gameobject
        var objSelectedString = objectDrop.value;


        string selectedCutString = Regex.Match(objSelectedString, "[^ ]* (.*)").Groups[1].Value;
        //The object selected is a GameObject
        if (GameObject.Find(selectedCutString) != null)
        {
            previousSelectedObject = objectSelected;
            objectSelected = GameObject.Find(selectedCutString);
        }
        else objectSelected = null;
    }

    static void DropdownValueChangedObjectValue(DropdownField objectVerbDrop)
    {
        DisableNextComponent("object");
        //retrieve selected string and gameobject
        var objSelectedString = objectVerbDrop.value;
        //Debug.Log($"Called ChangedObjectValue: {objSelectedString}");
        objectSelected = null;

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
                prepDrop.SetValueWithoutNotify(ac.ModifierString);
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
                valueDrop.SetValueWithoutNotify("<no-value>");
                return;
            }
        }

    }




    static void ActivateInputField(string validationType)
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

    static void DisableNextComponent(string changedField)
    {
        switch (changedField)
        {
            // Change subject
            case "subject":
                verbDrop.style.display = DisplayStyle.None;
                objectVerbDrop.style.display = DisplayStyle.None;

                prefixThe.style.display = DisplayStyle.None;
                prepDrop.style.display = DisplayStyle.None;
                objectDrop.style.display = DisplayStyle.None;
                valueDrop.style.display = DisplayStyle.None;
                textField.style.display = DisplayStyle.None;
                intField.style.display = DisplayStyle.None;
                decimalField.style.display = DisplayStyle.None;
                inputDrop.style.display = DisplayStyle.None;
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
                break;
            // Change object
            case "object":
                valueDrop.style.display = DisplayStyle.None;
                valueDrop.choices.Clear();
                break;
        }
    }
}
