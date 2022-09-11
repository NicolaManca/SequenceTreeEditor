using ECARules4All.RuleEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

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

    //Selected
    static GameObject subjectSelected; //gameobject with the subject
    static GameObject previousSelectedSubject, previousSelectedObject = null;
    static string subjectSelectedType; //e.g. ECALight, Character....
    static string verbSelectedString; //string with the verb
    static string VerbSelectedType;


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
        valueDrop.RegisterValueChangedCallback(delegate { DropdownValueChangedObjectValue(valueDrop); });


        SetUpSubject(subjectDrop);
        //SetUpVerb(verbDrop);
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
                Debug.Log(ac.ObjectType.Name);
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
                        valueDrop.style.display = DisplayStyle.Flex;
                        valueDrop.choices.Clear();
                        valueDrop.choices.Add("<no-value>");
                        valueDrop.choices.Add("x");
                        valueDrop.choices.Add("y");
                        valueDrop.choices.Add("z");
                        valueDrop.SetValueWithoutNotify("<no-value>");
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
                valueDrop.style.display = DisplayStyle.Flex;
                valueDrop.choices.Clear();
                valueDrop.choices.Add("<no-value>");
                valueDrop.choices.Add(ac.variableName);
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
            valueDrop.choices.Clear();
            valueDrop.choices.Add("<no-value>");
            foreach (var ac in actionAttributes)
            {
                if (ac.ValueType != null)
                {
                    valueDrop.style.display = DisplayStyle.Flex;
                    // objValueDrop.options.Add(new Dropdown.OptionData(ac.variableName));
                    entries.Add(ac.variableName);
                }
            }
            entries.Sort();
            valueDrop.choices = entries;
            valueDrop.SetValueWithoutNotify("<no-value>");
        }
    }

    static void DropdownValueChangedObjectValue(DropdownField valueDrop)
    {
        throw new NotImplementedException();
    }

    static void DropdownValueChangedObject(DropdownField objectDrop)
    {
        throw new NotImplementedException();
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
                inputDrop.style.display = DisplayStyle.None;
                break;
            // Change verb
            case "verb":
                prefixThe.style.display = DisplayStyle.None;
                prepDrop.style.display = DisplayStyle.None;
                objectDrop.style.display = DisplayStyle.None;
                valueDrop.style.display = DisplayStyle.None;
                textField.style.display = DisplayStyle.None;
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
