// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using static XliffTasks.Model.XlfNames;

namespace XliffTasks.Model
{
    internal static class XElementExtensions
    {
        public static string GetTargetValue(this XElement transUnitElement) => 
            transUnitElement.Element(Target)?.Value
            ?? transUnitElement.Element(Source).Value;

        public static void SetTargetValue(this XElement transUnitElement, string value)
        {
            XElement targetElement = transUnitElement.Element(Target);
            if (targetElement == null)
            {
                XElement sourceElement = transUnitElement.Element(Source);
                targetElement = new XElement(Target, new XAttribute("state", "new"));
                sourceElement.AddAfterSelf(targetElement);
            }

            targetElement.Value = value;
            if (targetElement.Attribute("state") == null)
            {
                targetElement.Add(new XAttribute("state", "new"));
            }
        }

        public static string GetTargetState(this XElement transUnitElement) =>
            transUnitElement.Element(Target)?.Attribute("state")?.Value
            ?? "new";

        public static void SetTargetState(this XElement transUnitElement, string value)
        {
            XElement targetElement = transUnitElement.Element(Target);
            if (targetElement == null)
            {
                XElement sourceElement = transUnitElement.Element(Source);
                targetElement = new XElement(Target);
                sourceElement.AddAfterSelf(targetElement);
            }

            XAttribute stateAttribute = targetElement.Attribute("state");
            if (stateAttribute == null)
            {
                stateAttribute = new XAttribute("state", value);
                targetElement.Add(stateAttribute);
            }
            else
            {
                stateAttribute.Value = value;
            }
        }

        public static string GetSourceValue(this XElement transUnitElement) =>
            transUnitElement.Element(Source).Value;

        public static void SetSourceValue(this XElement transUnitElement, string value) =>
            transUnitElement.Element(Source).Value = value;

        public static string GetNoteValue(this XElement transUnitElement) =>
            transUnitElement.Element(Note)?.Value;

        public static void SetNoteValue(this XElement transUnitElement, string value)
        {
            XElement noteElement = transUnitElement.Element(Note);
            if (noteElement == null)
            {
                XElement priorElement = transUnitElement.Element(Target) ?? transUnitElement.Element(Source);
                noteElement = new XElement(Note);
                priorElement.AddAfterSelf(noteElement);
            }

            noteElement.Value = value;
            noteElement.SelfCloseIfPossible();
        }

        public static string GetId(this XElement transUnitElement) =>
            transUnitElement.Attribute("id").Value;
    }
}
