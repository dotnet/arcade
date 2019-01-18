// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using static XliffTasks.Model.XlfNames;

namespace XliffTasks.Model
{
    internal static class XElementExtensions
    {
        public static string GetTargetValueOrDefault(this XElement transUnitElement) => 
            transUnitElement.Element(Target)?.Value
            ?? transUnitElement.Element(Source).Value;

        public static void SetTargetValue(this XElement transUnitElement, string value)
        {
            XElement targetElement = transUnitElement.Element(Target);
            if (targetElement == null)
            {
                XElement sourceElement = transUnitElement.Element(Source);
                targetElement = new XElement(Target);
                sourceElement.AddAfterSelf(targetElement);
            }

            targetElement.Value = value;
        }

        public static string GetTargetStateOrDefault(this XElement transUnitElement) =>
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
    }
}
